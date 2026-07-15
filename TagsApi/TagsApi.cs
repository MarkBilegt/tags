using CounterStrikeSharp.API.Core;

namespace TagsApi;

public abstract class Tags
{
    public class Tag
    {
        public string? Role { get; set; }
        public string RankName { get; set; } = string.Empty;
        public string AdminRole { get; set; } = string.Empty;
        public int Xp { get; set; }
        public string? NextRankName { get; set; }
        public int? NextRankXp { get; set; }
        public string? ScoreTag { get; set; }
        public string? ChatTag { get; set; }
        public string? ChatSuffix { get; set; }
        public string? ChatColor { get; set; }
        public string? NameColor { get; set; }
        public bool Visibility { get; set; } = true;
        public bool ChatSound { get; set; } = true;

        public Tag Clone()
        {
            return new Tag
            {
                Role = Role,
                RankName = RankName,
                AdminRole = AdminRole,
                Xp = Xp,
                NextRankName = NextRankName,
                NextRankXp = NextRankXp,
                ScoreTag = ScoreTag,
                ChatTag = ChatTag,
                ChatSuffix = ChatSuffix,
                ChatColor = ChatColor,
                NameColor = NameColor,
                Visibility = Visibility,
                ChatSound = ChatSound
            };
        }
    }

    public class MessageProcess
    {
        public required CCSPlayerController Player { get; set; }
        public required Tag Tag { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool ChatSound { get; set; } = true;
        public bool TeamMessage { get; set; } = false;
    }

    [Flags]
    public enum TagType
    {
        None = 0,
        ScoreTag = 1 << 0,
        ChatTag = 1 << 1,
        NameColor = 1 << 2,
        ChatColor = 1 << 3,
        ChatSuffix = 1 << 4
    }

    public enum TagPrePost
    {
        Pre,
        Post
    }
}
