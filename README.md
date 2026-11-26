# Race & BetterRNG

SNSRModLoader plugin for the September 2018 Subnautica speedrun build.

## Build

Run `./Build.ps1`. It only builds `dist/SubnauticaSpeedrunningMod.dll` and
regenerates `dist/mod-update.xml`. It never installs or starts the game.

The build needs the game's managed assemblies and SNSRModLoader SDK. The project
uses the neighboring loader build when available, otherwise the installed loader
in the game's Mods/Loader folder. Custom paths can be supplied with MSBuild's
`GameRoot` and `LoaderDirectory` properties when building the project directly.

Open `SubnauticaSpeedrunningMod.sln` in Cursor. Workspace settings select this
solution so the C# service loads the renamed project and its references. If Cursor
still reports the deleted SubnauticaPracticeMod project, run **Developer: Reload
Window** once. The game marks its required 2018 GameMode enum obsolete; CS0618 is
intentionally suppressed for this compatibility target.

## Release and automatic updates

1. Increase `PluginVersion` in `Plugin/Plugin.cs` for every new release.
2. Run `./Build.ps1` after your final source and seed changes.
3. Create the GitHub release with tag `v<PluginVersion>` in the repository named
   by `UpdateRepository`. Upload the exact `dist/SubnauticaSpeedrunningMod.dll`
   as the release asset, keeping that filename.
4. Publish `dist/mod-update.xml` on the repository's `main` branch after the asset
   is available. Do not rebuild or alter the DLL after generating its manifest.

The manifest automatically includes the plugin ID, version, required SDK version,
September2018 target, release asset URL and final SHA-256 hash. The loader checks
that these agree with the DLL. Only newer versions are offered. Keep PluginId
stable so settings and installed identity survive updates.

The mod is one DLL. All three seed lists are embedded; no seed folders, XML
profiles, language files, launcher, bundled saves, or auxiliary mod DLLs need to
be shipped. Unity/game assemblies and SNSRModLoader/Harmony are supplied by the
game and installed loader. Do not attach those dependencies to the mod release.
The race server remains a separate hosted service.

Settings and the persistent player identity use the loader's settings directory;
replacing the DLL does not overwrite them. The loader owns update checks,
prompts, verification and replacement; this mod has no separate updater.

## Checks

- `dotnet run --project tests/SeedPools/SeedPools.csproj`
- `dotnet run --project tests/Shutdown/Shutdown.csproj`
- `dotnet run --project tests/Release/Release.csproj`

The release check uses the neighboring loader's real manifest and DLL validators,
checks embedded seed contents, and rejects unexpected auxiliary dependencies.
The first two checks use lightweight game/transport doubles and start no game.
