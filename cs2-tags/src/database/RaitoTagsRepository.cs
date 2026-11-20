using System.Text.Json;
using MySqlConnector;

namespace Tags;

internal sealed record RaitoIdentity(
    int Xp,
    int Kills,
    int Deaths,
    string RankName,
    int RankId,
    string? NextRankName,
    int? NextRankXp,
    string AdminRole,
    string? ClanTag)
{
    public static readonly RaitoIdentity Default = new(0, 0, 0, "Silver 1", 1, "Silver 2", 150, "USER", null);
}

internal sealed class RaitoTagsRepository
{
    private readonly string connectionString;

    public RaitoTagsRepository(string databaseConfigPath)
    {
        DatabaseConfig config = JsonSerializer.Deserialize<DatabaseConfig>(
            File.ReadAllText(databaseConfigPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The shared database configuration is empty.");

        if (!string.Equals(config.DatabaseType, "MySQL", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("REIGN Tags requires DatabaseType=MySQL.");
        }

        if (string.IsNullOrWhiteSpace(config.MySqlHost)
            || string.IsNullOrWhiteSpace(config.MySqlDatabase)
            || string.IsNullOrWhiteSpace(config.MySqlUsername)
            || config.MySqlPort is null)
        {
            throw new InvalidOperationException("The shared MySQL configuration is missing host, database, username, or port.");
        }

        connectionString = new MySqlConnectionStringBuilder
        {
            Server = config.MySqlHost,
            Port = (uint)config.MySqlPort.Value,
            Database = config.MySqlDatabase,
            UserID = config.MySqlUsername,
            Password = config.MySqlPassword ?? string.Empty,
            Pooling = true,
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 3,
            AllowUserVariables = false
        }.ConnectionString;

        using var connection = OpenConnection();
        using var command = new MySqlCommand("SELECT 1", connection);
        command.ExecuteScalar();
    }

    public RaitoIdentity GetIdentity(ulong steamId64)
    {
        using var connection = OpenConnection();
        using var command = new MySqlCommand("""
            SELECT
                COALESCE(progress.`xp`, 0) AS `xp`,
                COALESCE(progress.`kills`, 0) AS `kills`,
                COALESCE(progress.`deaths`, 0) AS `deaths`,
                COALESCE(currentRank.`name`, 'Silver 1') AS `rankName`,
                COALESCE(currentRank.`id`, 1) AS `rankId`,
                nextRank.`name` AS `nextRankName`,
                nextRank.`minXp` AS `nextRankXp`,
                CASE WHEN user.`roleExpiresAt` IS NOT NULL AND user.`roleExpiresAt` <= UTC_TIMESTAMP(3) THEN 'USER' ELSE COALESCE(user.`role`, 'USER') END AS `adminRole`,
                clan.`tag` AS `clanTag`
            FROM (SELECT @steamId64 AS `steamId64`) identity
            LEFT JOIN `PlayerProgress` progress ON progress.`steamId64` = identity.`steamId64`
            LEFT JOIN `User` user ON user.`steamId64` = identity.`steamId64`
            LEFT JOIN `ClanMember` clanMember ON clanMember.`userId` = user.`id`
            LEFT JOIN `Clan` clan ON clan.`id` = clanMember.`clanId`
            LEFT JOIN `RankDefinition` currentRank ON currentRank.`minXp` = (
                SELECT MAX(candidate.`minXp`)
                FROM `RankDefinition` candidate
                WHERE candidate.`minXp` <= COALESCE(progress.`xp`, 0)
            )
            LEFT JOIN `RankDefinition` nextRank ON nextRank.`minXp` = (
                SELECT MIN(candidate.`minXp`)
                FROM `RankDefinition` candidate
                WHERE candidate.`minXp` > COALESCE(progress.`xp`, 0)
            )
            LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("@steamId64", steamId64.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return RaitoIdentity.Default;

        return new RaitoIdentity(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? RaitoIdentity.Default.RankName : reader.GetString(3),
            reader.IsDBNull(4) ? RaitoIdentity.Default.RankId : Math.Clamp(reader.GetInt32(4), 1, 18),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            reader.IsDBNull(7) ? "USER" : reader.GetString(7).ToUpperInvariant(),
            reader.IsDBNull(8) ? null : reader.GetString(8).ToUpperInvariant());
    }

    private MySqlConnection OpenConnection()
    {
        var connection = new MySqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private sealed class DatabaseConfig
    {
        public string? DatabaseType { get; init; }
        public string? MySqlHost { get; init; }
        public string? MySqlDatabase { get; init; }
        public string? MySqlUsername { get; init; }
        public string? MySqlPassword { get; init; }
        public int? MySqlPort { get; init; }
    }
}
