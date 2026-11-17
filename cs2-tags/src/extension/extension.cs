using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
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

    public static void SetPlayerName(CCSPlayerController player, string name)
    {
        if (!player.IsValid || player.IsBot || player.IsHLTV || player.PlayerName == name) return;

        player.PlayerName = name;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iszPlayerName");
    }
}
