using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;

namespace Tags;

internal static partial class TagText
{
    [GeneratedRegex(@"\{.*?\}|[\p{Cc}\p{Cf}]", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeChatContent();

    public static string Sanitize(string value) => UnsafeChatContent().Replace(value, string.Empty).Trim();

    public static string Colors(string value, CsTeam team)
    {
        string teamColor = TeamColor(team).ToString();
        return value.ReplaceColorTags().Replace("{TeamColor}", teamColor, StringComparison.OrdinalIgnoreCase);
    }

    public static char TeamColor(CsTeam team) => team switch
    {
        CsTeam.CounterTerrorist => ChatColors.LightBlue,
        CsTeam.Terrorist => ChatColors.Gold,
        CsTeam.Spectator => ChatColors.LightPurple,
        _ => ChatColors.White
    };

    public static void SetScoreboardRank(CCSPlayerController player, string rank)
    {
        if (!player.IsValid || player.IsBot || player.IsHLTV || player.Clan == rank) return;

        player.Clan = rank;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
        new EventNextlevelChanged(false).FireEventToClient(player);
    }

    // CompetitiveRankType 12 = Competitive (Silver 1 = 1 ... The Global Elite = 18); 0 clears the icon.
    public static bool SetCompetitiveRank(CCSPlayerController player, int rankId)
    {
        if (!player.IsValid || player.IsBot || player.IsHLTV) return false;

        int rank = Math.Clamp(rankId, 0, 18);
        int type = rank == 0 ? 0 : 12;
        // Legacy ranks show "N wins needed" instead of the badge until the player has 10 wins.
        int wins = rank == 0 ? 0 : 10;
        if (player.CompetitiveRanking == rank && player.CompetitiveRankType == type && player.CompetitiveWins == wins) return false;

        player.CompetitiveRanking = rank;
        player.CompetitiveRankType = (sbyte)type;
        player.CompetitiveWins = wins;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRanking");
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRankType");
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveWins");
        return true;
    }

    // Equivalent of the end-of-match "reveal ranks" message: makes the scoreboard show every player's rank badge.
    public static void RevealRanks(CCSPlayerController? onlyFor = null)
    {
        var message = UserMessage.FromId((int)ECstrike15UserMessages.CS_UM_ServerRankRevealAll);
        if (onlyFor is null) message.Recipients.AddAllPlayers();
        else message.Recipients.Add(onlyFor);
        message.Send();
    }

    public static void SetPlayerName(CCSPlayerController player, string name)
    {
        if (!player.IsValid || player.IsBot || player.IsHLTV || player.PlayerName == name) return;

        player.PlayerName = name;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iszPlayerName");
    }
}
