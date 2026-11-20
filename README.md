# REIGN Tags

REIGN Tags is the in-game presentation layer for the REIGN CS2 platform. It is based on the original [cs2-tags](https://github.com/CounterStrike2-Plugins-Archive/cs2-tags) plugin by schwarper and retains its public capability API.

The REIGN fork replaces static SteamID, group, and permission tag rules with identities from the shared MySQL database:

- Chat: `[TAG] player name [Admin Role]` (the rank is never shown in chat)
- Scoreboard: competitive rank icon (Silver 1 to The Global Elite) and the clan tag in the clan field
- Admin roles: `OWNER`, `MANAGER`, `HEAD ADMIN`, `VIP`, and `ADMIN`
- Emergency fallback: CounterStrikeSharp root admins display as `OWNER`
- Cache: asynchronous refresh with a configurable 15-second default

Players without a clan keep the shorter `Rank player name [Admin Role]` format.
Clan membership and tag changes refresh from the shared database cache within
the configured cache interval.

## Ranking rules

XP is recorded only during a real live MatchZy match. Warmup, knife rounds, practice, dry runs, test matches, bots, HLTV, and coaches are excluded.

| Event | XP |
| --- | ---: |
| Enemy kill | +5 |
| Death | -5 |
| Round win | +2 |
| Round loss | -2 |
| Assist | +1 |
| Round MVP | +3 |

XP cannot fall below zero.

| Rank | Minimum XP |
| --- | ---: |
| Silver 1 | 0 |
| Silver 2 | 150 |
| Silver 3 | 400 |
| Silver 4 | 750 |
| Silver Elite | 1,200 |
| Silver Elite Master | 1,800 |
| Gold Nova 1 | 2,500 |
| Gold Nova 2 | 3,300 |
| Gold Nova 3 | 4,300 |
| Gold Nova Master | 5,500 |
| Master Guardian 1 | 7,000 |
| Master Guardian 2 | 8,800 |
| Master Guardian Elite | 11,000 |
| Distinguished Master Guardian | 13,500 |
| Legendary Eagle | 16,500 |
| Legendary Eagle Master | 20,000 |
| Supreme Master First Class | 24,500 |
| The Global Elite | 30,000 |

The rank is shown on the scoreboard as the standard competitive rank icon.

## Requirements

- CounterStrikeSharp `1.0.371` or newer
- .NET 10 server runtime
- MySQL schema managed by `cs2-website/prisma/migrations`
- Shared database configuration at `csgo/cfg/MatchZy/database.json`

## Commands

- `!rank` or `css_rank`: show player name, rank, lifetime ranked kills, deaths, K.D, XP, and next promotion
- `css_raitotags_reload`: reload configuration and cached identities; requires `@css/root`

## Build

```powershell
dotnet build .\cs2-tags.sln
dotnet publish .\cs2-tags\cs2-tags.csproj -c Release
```

Deploy the contents of `BuildOutput/plugins/RaitoTags` to the matching CounterStrikeSharp plugin folder. The generated example configuration belongs in `addons/counterstrikesharp/configs/plugins/RaitoTags`.

## Credits

- [schwarper/cs2-tags archive](https://github.com/CounterStrike2-Plugins-Archive/cs2-tags)
- [HexTags](https://github.com/Hexer10/HexTags)
- daffyyyy and Yarukon for contributions to the upstream project

