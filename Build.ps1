# Build the release DLL and generate its update manifest from compiled metadata.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SubnauticaSpeedrunningMod.csproj'
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$dll = Join-Path $PSScriptRoot 'dist/SubnauticaSpeedrunningMod.dll'
$loaderDirectory = dotnet msbuild $project -nologo -getProperty:LoaderDirectory
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the loader SDK.' }
foreach ($assembly in @('Mono.Cecil.dll', '0Harmony.dll', 'SNSRModLoader.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $loaderDirectory $assembly))
}
$candidates = New-Object 'System.Collections.Generic.List[SNSRModLoader.PluginInfo]'
$warnings = New-Object 'System.Collections.Generic.List[string]'
[void][SNSRModLoader.Discovery]::Scan((Split-Path $dll), [SNSRModLoader.GameVersion]::September2018,
    [Action[string]]{ param($message) $warnings.Add($message) },
    [Action[SNSRModLoader.PluginInfo]]{ param($plugin) if ($plugin.Path -eq $dll) { $candidates.Add($plugin) } })
if ($candidates.Count -ne 1) { throw "Cannot read built plugin metadata: $($warnings -join '; ')" }
$info = $candidates[0]
$minimumLoader = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $loaderDirectory 'SNSRModLoader.dll')).Version.ToString(3)

[xml]$manifest = '<modUpdate schemaVersion="1" />'
$manifest.modUpdate.SetAttribute('id', $info.Id)
$manifest.modUpdate.SetAttribute('version', $info.Version)
$manifest.modUpdate.SetAttribute('minimumLoaderVersion', $minimumLoader)
$manifest.modUpdate.SetAttribute('sha256', $info.Sha256)
$manifest.modUpdate.SetAttribute('url', "https://github.com/$($info.UpdateRepository)/releases/download/v$($info.Version)/SubnauticaSpeedrunningMod.dll")
foreach ($game in $info.Targets) {
    $target = $manifest.CreateElement('target')
    $target.SetAttribute('version', $game.ToString())
    [void]$manifest.modUpdate.AppendChild($target)
}
$manifest.Save((Join-Path $PSScriptRoot 'dist/mod-update.xml'))
Write-Host "Built dist/SubnauticaSpeedrunningMod.dll and updated dist/mod-update.xml (v$($info.Version); $($info.Targets -join ', '))."
