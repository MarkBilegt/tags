# REIGN Tags

REIGN Tags is the in-game presentation layer for the REIGN CS2 platform. It is based on the original [cs2-tags](https://github.com/CounterStrike2-Plugins-Archive/cs2-tags) plugin by schwarper and retains its public capability API.

The REIGN fork replaces static SteamID, group, and permission tag rules with identities from the shared MySQL database:

- Chat: `[TAG] Rank player name [Admin Role]`
- Scoreboard clan field: `[TAG] Rank`
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
| Байлдагч | 0 |
| Ахлах  | 150 |
| Дэд түрүүч | 400 |
| Түрүүч | 750 |
| Ахлах түрүүч | 1,200 |
| Дэд ахлагч | 1,800 |
| Ахлагч | 2,500 |
| Ахлах ахлагч | 3,300 |
| Сургагч ахлагч | 4,200 |
| Тэргүүн ахлагч | 5,200 |
| Дэслэгч | 6,400 |
| Ахлах дэслэгч | 7,800 |
| Ахмад | 9,400 |
| Хошууч | 11,200 |
| Дэд хурандаа | 13,200 |
| Хурандаа | 15,400 |
| Бригадын генерал | 17,800 |
| Хошууч генерал | 20,500 |
| Дэслэгч генерал | 24,500 |
| Генерал | 30,000 |

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
