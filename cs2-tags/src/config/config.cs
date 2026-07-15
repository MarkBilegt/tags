using CounterStrikeSharp.API.Core;

namespace Tags;

public sealed class Config : BasePluginConfig
{
    public int CacheSeconds { get; set; } = 15;
    public string DatabaseConfigPath { get; set; } = "csgo/cfg/MatchZy/database.json";
    public string RankColor { get; set; } = "{Gold}";
    public string MessageColor { get; set; } = "{White}";
    public string DeadPrefix { get; set; } = "{Grey}*DEAD* ";
    public string SpectatorPrefix { get; set; } = "{LightPurple}(SPEC) ";
    public string TerroristPrefix { get; set; } = "{Gold}(T) ";
    public string CounterTerroristPrefix { get; set; } = "{LightBlue}(CT) ";
    public string OwnerColor { get; set; } = "{Red}";
    public string ManagerColor { get; set; } = "{Gold}";
    public string HeadAdminColor { get; set; } = "{Purple}";
    public string VipColor { get; set; } = "{Lime}";
    public string AdminColor { get; set; } = "{LightBlue}";
    public string[] ReloadCommands { get; set; } = ["css_raitotags_reload"];
    public string[] RankCommands { get; set; } = ["css_rank"];

    public void Validate()
    {
        CacheSeconds = Math.Clamp(CacheSeconds, 5, 300);
        DatabaseConfigPath = string.IsNullOrWhiteSpace(DatabaseConfigPath)
            ? "csgo/cfg/MatchZy/database.json"
            : DatabaseConfigPath.Trim();
    }
}
