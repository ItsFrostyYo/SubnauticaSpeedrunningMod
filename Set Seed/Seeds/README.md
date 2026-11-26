Filtered Ruleset 8 race seeds
============================

Survival.csv and Creative.csv are embedded into the client and copied into the
Render server Data/Seeds directory. Keep both copies identical after changing a
list, then rebuild both applications. Numeric seeds go one per line; a Seed
header is optional. Hardcore.csv is included for future use, but Hardcore is
explicitly disabled on both client and server even when this file has seeds.
There is no fallback to another pool or the legacy Creative range/teleport system.

The original Creative filtering bounds are X [-100, -30], Z [-430, -330], initial
Y 0. Keep seeds whose naturally generated lifepod falls inside those bounds.

The server always chooses the seed from the selected mode's list. Private rooms
use seedId Ruleset-8 and spawnProfile ruleset-8-seeded. Client spawn coordinates,
seed strings and multipliers cannot override the server's assignment.

Checks: dotnet run --project tests/SeedPools/SeedPools.csproj
