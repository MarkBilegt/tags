using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using TagsApi;
using static TagsApi.Tags;

namespace Tags;

[MinimumApiVersion(371)]
public sealed class Tags : BasePlugin, IPluginConfig<Config>
{
    private sealed record CachedTag(Tag Tag, int Kills, int Deaths, DateTime ExpiresAtUtc);

    public override string ModuleName => "REIGN Tags";
    public override string ModuleVersion => "2.1.1";
    public override string ModuleAuthor => "REIGN, based on cs2-tags by schwarper";
    public override string ModuleDescription => "Database-backed REIGN clan, rank, and admin tags.";

    public static Tags Instance { get; private set; } = null!;
    public static TagsAPI Api { get; } = new();

    private readonly Dictionary<ulong, CachedTag> playerTags = [];
    private readonly ConcurrentDictionary<ulong, byte> refreshesInFlight = new();
    private RaitoTagsRepository? repository;
    private bool unloading;

    public Config Config { get; set; } = new();

    public override void Load(bool hotReload)
    {
        Instance = this;
        unloading = false;
        Capabilities.RegisterPluginCapability(ITagApi.Capability, () => Api);

        foreach (string command in Config.ReloadCommands.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddCommand(command, "Reload REIGN tags", CommandTagsReload);
        }

        foreach (string command in Config.RankCommands.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddCommand(command, "Show your REIGN rank", CommandRank);
        }

        HookUserMessage((int)EBaseUserMessages.UM_SayText2, OnMessage, HookMode.Pre);
        AddCommandListener("css_admins_reload", OnAdminsReloaded, HookMode.Post);
        InitializeRepository();

        if (hotReload)
        {
            ReloadTags();
        }
    }

    public override void Unload(bool hotReload)
    {
        unloading = true;
        UnhookUserMessage((int)EBaseUserMessages.UM_SayText2, OnMessage, HookMode.Pre);
        RemoveCommandListener("css_admins_reload", OnAdminsReloaded, HookMode.Post);
        playerTags.Clear();
        refreshesInFlight.Clear();
        repository = null;
    }

    public void OnConfigParsed(Config config)
    {
        config.Validate();
        Config = config;
    }

    private void InitializeRepository()
    {
        string configuredPath = Config.DatabaseConfigPath.Replace('/', Path.DirectorySeparatorChar);
        string databaseConfigPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(Server.GameDirectory, configuredPath);

        try
        {
            repository = new RaitoTagsRepository(databaseConfigPath);
            Logger.LogInformation("REIGN Tags connected to the shared MySQL database.");
        }
        catch (Exception ex)
        {
            repository = null;
            Logger.LogError(ex, "REIGN Tags could not open the shared database; rank zero and bootstrap permissions will be used.");
        }
    }

    private HookResult OnAdminsReloaded(CCSPlayerController? player, CommandInfo info)
    {
        ReloadTags();
        return HookResult.Continue;
    }

    [RequiresPermissions("@css/root")]
    public void CommandTagsReload(CCSPlayerController? player, CommandInfo info)
    {
        if (player is not null && !AdminManager.PlayerHasPermissions(new SteamID(player.SteamID), "@css/root"))
        {
            info.ReplyToCommand("[REIGN] You do not have permission to reload tags.");
            return;
        }

        try
        {
            Config.Reload();
            Config.Validate();
            InitializeRepository();
            ReloadTags();
            info.ReplyToCommand("[REIGN] Tags reloaded.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "REIGN Tags reload failed.");
            info.ReplyToCommand("[REIGN] Tag reload failed. Check the server console.");
        }
    }

    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void CommandRank(CCSPlayerController? player, CommandInfo info)
    {
        if (!IsHumanPlayer(player)) return;

        Tag tag = GetOrCreateTag(player!);
        string progress = tag.NextRankXp is int nextXp && !string.IsNullOrWhiteSpace(tag.NextRankName)
            ? $"{tag.Xp:N0}/{nextXp:N0} XP to {tag.NextRankName}"
            : $"{tag.Xp:N0} XP - maximum rank";

        CachedTag cached = playerTags[player!.SteamID];
        int kills = Math.Max(0, cached.Kills);
        int deaths = Math.Max(0, cached.Deaths);
        double killDeathRatio = deaths == 0 ? kills : (double)kills / deaths;
        string playerName = TagText.Sanitize(player.PlayerName);

        info.ReplyToCommand($"[{ChatColors.Green}REIGN{ChatColors.White}] Name: {ChatColors.Green}{playerName}{ChatColors.White} | Rank: {ChatColors.Green}{tag.RankName}{ChatColors.White}");
        info.ReplyToCommand($"[{ChatColors.Green}REIGN{ChatColors.White}] Kills: {ChatColors.Green}{kills}{ChatColors.White} | Deaths: {ChatColors.Green}{deaths}{ChatColors.White} | K.D: {ChatColors.Green}{killDeathRatio:0.00}{ChatColors.White} | {progress}");
        QueueIdentityRefresh(player!, true);
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (IsHumanPlayer(@event.Userid)) QueueIdentityRefresh(@event.Userid!, true);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid is { } player && player.SteamID > 0)
        {
            playerTags.Remove(player.SteamID);
            refreshesInFlight.TryRemove(player.SteamID, out _);
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (!IsHumanPlayer(@event.Userid)) return HookResult.Continue;

        Tag tag = GetOrCreateTag(@event.Userid!);
        ApplyScoreboardRank(@event.Userid!, tag);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        foreach (CCSPlayerController player in Utilities.GetPlayers().Where(IsHumanPlayer))
        {
            Tag tag = GetOrCreateTag(player);
            ApplyScoreboardRank(player, tag);
        }

        return HookResult.Continue;
    }

    public HookResult OnMessage(UserMessage message)
    {
        CCSPlayerController? player = Utilities.GetPlayerFromIndex(message.ReadInt("entityindex"));
        if (!IsHumanPlayer(player)) return HookResult.Continue;

        Tag storedTag = GetOrCreateTag(player!);
        Tag tag = storedTag.Visibility ? storedTag.Clone() : BuildTag(RaitoIdentity.Default, "USER");
        string chatMessage = TagText.Sanitize(message.ReadString("param2"));
        if (string.IsNullOrWhiteSpace(chatMessage)) return HookResult.Handled;

        string messageName = message.ReadString("messagename");
        bool teamMessage = !messageName.Contains("All", StringComparison.OrdinalIgnoreCase);
        var messageProcess = new MessageProcess
        {
            Player = player!,
            Tag = tag,
            PlayerName = TagText.Sanitize(player!.PlayerName),
            Message = chatMessage,
            ChatSound = message.ReadBool("chat"),
            TeamMessage = teamMessage
        };

        HookResult hookResult = Api.MessageProcessPre(messageProcess);
        if (hookResult >= HookResult.Handled) return hookResult;

        CsTeam team = (CsTeam)player.TeamNum;
        string context = BuildChatContext(player, teamMessage);
        string rankTag = TagText.Colors(messageProcess.Tag.ChatTag ?? string.Empty, team);
        string nameColor = TagText.Colors(messageProcess.Tag.NameColor ?? "{TeamColor}", team);
        string adminTag = TagText.Colors(messageProcess.Tag.ChatSuffix ?? string.Empty, team);
        string messageColor = TagText.Colors(messageProcess.Tag.ChatColor ?? Config.MessageColor, team);

        messageProcess.PlayerName = $"{context}{rankTag}{nameColor}{messageProcess.PlayerName}{adminTag}";
        messageProcess.Message = $"{messageColor}{messageProcess.Message}";

        hookResult = Api.MessageProcess(messageProcess);
        if (hookResult >= HookResult.Handled) return hookResult;

        message.SetString("messagename", $"{messageProcess.PlayerName}{ChatColors.White}: {messageProcess.Message}");
        message.SetBool("chat", messageProcess.Tag.ChatSound);
        Api.MessageProcessPost(messageProcess);
        return HookResult.Changed;
    }

    internal Tag GetOrCreateTag(CCSPlayerController player)
    {
        if (playerTags.TryGetValue(player.SteamID, out CachedTag? cached))
        {
            if (cached.ExpiresAtUtc <= DateTime.UtcNow) QueueIdentityRefresh(player, false);
            return cached.Tag;
        }

        string bootstrapRole = ResolveBootstrapRole(player, "USER");
        Tag fallback = BuildTag(RaitoIdentity.Default, bootstrapRole);
        playerTags[player.SteamID] = new CachedTag(fallback, 0, 0, DateTime.UtcNow.AddSeconds(Config.CacheSeconds));
        QueueIdentityRefresh(player, false);
        return fallback;
    }

    internal void ReloadTags()
    {
        foreach (CCSPlayerController player in Utilities.GetPlayers().Where(IsHumanPlayer))
        {
            QueueIdentityRefresh(player, true);
        }
    }

    private void QueueIdentityRefresh(CCSPlayerController player, bool force)
    {
        if (unloading || repository is null || player.SteamID == 0) return;
        if (!force
            && playerTags.TryGetValue(player.SteamID, out CachedTag? cached)
            && cached.ExpiresAtUtc > DateTime.UtcNow)
        {
            return;
        }

        ulong steamId = player.SteamID;
        int playerSlot = player.Slot;
        RaitoTagsRepository currentRepository = repository;
        if (!refreshesInFlight.TryAdd(steamId, 0)) return;

        _ = Task.Run(() => currentRepository.GetIdentity(steamId)).ContinueWith(task =>
        {
            refreshesInFlight.TryRemove(steamId, out _);
            if (unloading) return;

            if (!task.IsCompletedSuccessfully)
            {
                Logger.LogWarning(task.Exception?.GetBaseException(), "Could not refresh REIGN tags for SteamID {SteamId}.", steamId);
                return;
            }

            RaitoIdentity identity = task.Result;
            Server.NextFrame(() =>
            {
                if (unloading) return;
                CCSPlayerController? currentPlayer = Utilities.GetPlayerFromSlot(playerSlot);
                if (!IsHumanPlayer(currentPlayer) || currentPlayer!.SteamID != steamId) return;

                string adminRole = ResolveBootstrapRole(currentPlayer, identity.AdminRole);
                Tag tag = BuildTag(identity, adminRole);
                playerTags[steamId] = new CachedTag(
                    tag,
                    Math.Max(0, identity.Kills),
                    Math.Max(0, identity.Deaths),
                    DateTime.UtcNow.AddSeconds(Config.CacheSeconds));
                ApplyScoreboardRank(currentPlayer, tag);
                Api.TagsUpdatedPost(currentPlayer, tag);
            });
        }, TaskScheduler.Default);
    }

    private Tag BuildTag(RaitoIdentity identity, string adminRole)
    {
        string normalizedRole = adminRole.Trim().ToUpperInvariant();
        return new Tag
        {
            Role = normalizedRole,
            RankName = identity.RankName,
            AdminRole = normalizedRole,
            Xp = Math.Max(0, identity.Xp),
            NextRankName = identity.NextRankName,
            NextRankXp = identity.NextRankXp,
            ScoreTag = BuildScoreTag(identity.ClanTag, identity.RankName),
            ChatTag = BuildChatTag(identity.ClanTag, identity.RankName),
            ChatSuffix = BuildAdminTag(normalizedRole),
            NameColor = "{TeamColor}",
            ChatColor = Config.MessageColor,
            Visibility = true,
            ChatSound = true
        };
    }

    private string BuildChatTag(string? clanTag, string rankName)
    {
        string normalizedClanTag = NormalizeClanTag(clanTag);
        string clanPrefix = string.IsNullOrEmpty(normalizedClanTag)
            ? string.Empty
            : $"{Config.ClanColor}[{normalizedClanTag}] ";
        return $"{clanPrefix}{Config.RankColor}{rankName} ";
    }

    private static string BuildScoreTag(string? clanTag, string rankName)
    {
        string normalizedClanTag = NormalizeClanTag(clanTag);
        return string.IsNullOrEmpty(normalizedClanTag)
            ? rankName
            : $"[{normalizedClanTag}] {rankName}";
    }

    private static string NormalizeClanTag(string? clanTag)
    {
        if (string.IsNullOrWhiteSpace(clanTag)) return string.Empty;
        return new string(
            clanTag
                .Trim()
                .ToUpperInvariant()
                .Where(char.IsAsciiLetterOrDigit)
                .Take(6)
                .ToArray());
    }

    private string BuildAdminTag(string role) => role switch
    {
        "OWNER" => $" {Config.OwnerColor}[OWNER]",
        "MANAGER" => $" {Config.ManagerColor}[MANAGER]",
        "HEAD_ADMIN" => $" {Config.HeadAdminColor}[HEAD ADMIN]",
        "VIP" => $" {Config.VipColor}[VIP]",
        "ADMIN" => $" {Config.AdminColor}[ADMIN]",
        _ => string.Empty
    };

    private string BuildChatContext(CCSPlayerController player, bool teamMessage)
    {
        string context = player.PawnIsAlive ? string.Empty : Config.DeadPrefix;
        if (!teamMessage) return TagText.Colors(context, (CsTeam)player.TeamNum);

        context += (CsTeam)player.TeamNum switch
        {
            CsTeam.CounterTerrorist => Config.CounterTerroristPrefix,
            CsTeam.Terrorist => Config.TerroristPrefix,
            CsTeam.Spectator => Config.SpectatorPrefix,
            _ => string.Empty
        };
        return TagText.Colors(context, (CsTeam)player.TeamNum);
    }

    private static string ResolveBootstrapRole(CCSPlayerController player, string databaseRole)
    {
        string normalizedRole = string.IsNullOrWhiteSpace(databaseRole) ? "USER" : databaseRole.Trim().ToUpperInvariant();
        if (normalizedRole != "USER") return normalizedRole;

        return AdminManager.PlayerHasPermissions(new SteamID(player.SteamID), "@css/root") ? "OWNER" : "USER";
    }

    private static bool IsHumanPlayer(CCSPlayerController? player) =>
        player is { IsValid: true, IsBot: false, IsHLTV: false } && player.SteamID > 0;

    private static void ApplyScoreboardRank(CCSPlayerController player, Tag tag)
    {
        TagText.SetScoreboardRank(player, tag.Visibility ? tag.ScoreTag ?? tag.RankName : string.Empty);
    }

    internal void AddAttribute(CCSPlayerController player, TagType types, TagPrePost position, string value)
    {
        Tag tag = GetOrCreateTag(player);
        Api.TagsUpdatedPre(player, tag);
        if ((types & TagType.ScoreTag) != 0) tag.ScoreTag = Merge(position, tag.ScoreTag, value);
        if ((types & TagType.ChatTag) != 0) tag.ChatTag = Merge(position, tag.ChatTag, value);
        if ((types & TagType.ChatSuffix) != 0) tag.ChatSuffix = Merge(position, tag.ChatSuffix, value);
        if ((types & TagType.NameColor) != 0) tag.NameColor = Merge(position, tag.NameColor, value);
        if ((types & TagType.ChatColor) != 0) tag.ChatColor = Merge(position, tag.ChatColor, value);
        ApplyScoreboardRank(player, tag);
        Api.TagsUpdatedPost(player, tag);
    }

    internal void SetAttribute(CCSPlayerController player, TagType types, string value)
    {
        Tag tag = GetOrCreateTag(player);
        Api.TagsUpdatedPre(player, tag);
        if ((types & TagType.ScoreTag) != 0) tag.ScoreTag = value;
        if ((types & TagType.ChatTag) != 0) tag.ChatTag = value;
        if ((types & TagType.ChatSuffix) != 0) tag.ChatSuffix = value;
        if ((types & TagType.NameColor) != 0) tag.NameColor = value;
        if ((types & TagType.ChatColor) != 0) tag.ChatColor = value;
        ApplyScoreboardRank(player, tag);
        Api.TagsUpdatedPost(player, tag);
    }

    internal string? GetAttribute(CCSPlayerController player, TagType type)
    {
        Tag tag = GetOrCreateTag(player);
        return type switch
        {
            TagType.ScoreTag => tag.ScoreTag,
            TagType.ChatTag => tag.ChatTag,
            TagType.ChatSuffix => tag.ChatSuffix,
            TagType.NameColor => tag.NameColor,
            TagType.ChatColor => tag.ChatColor,
            _ => null
        };
    }

    internal void ResetAttributes(CCSPlayerController player) => QueueIdentityRefresh(player, true);

    internal bool GetVisibility(CCSPlayerController player) => GetOrCreateTag(player).Visibility;

    internal void SetVisibility(CCSPlayerController player, bool value)
    {
        Tag tag = GetOrCreateTag(player);
        tag.Visibility = value;
        ApplyScoreboardRank(player, tag);
    }

    internal bool GetChatSound(CCSPlayerController player) => GetOrCreateTag(player).ChatSound;

    internal void SetChatSound(CCSPlayerController player, bool value) => GetOrCreateTag(player).ChatSound = value;

    private static string Merge(TagPrePost position, string? currentValue, string newValue) => position switch
    {
        TagPrePost.Pre => newValue + currentValue,
        TagPrePost.Post => currentValue + newValue,
        _ => newValue
    };
}
