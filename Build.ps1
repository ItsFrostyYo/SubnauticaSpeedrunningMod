# Build the single-DLL release and refresh its loader update manifest.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SubnauticaSpeedrunningMod.csproj'
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$source = Get-Content (Join-Path $PSScriptRoot 'Plugin/Plugin.cs') -Raw
function Read-Constant([string]$name) {
    $match = [regex]::Match($source, ('public const string ' + $name + ' = "([^"]+)";'))
    if (-not $match.Success) { throw "Missing Plugin.$name constant." }
    return $match.Groups[1].Value
}

$dll = Join-Path $PSScriptRoot 'dist/SubnauticaSpeedrunningMod.dll'
$id = Read-Constant 'PluginId'
$version = Read-Constant 'PluginVersion'
$repository = Read-Constant 'UpdateRepository'
[void][version]::Parse($version)
$loaderDirectory = dotnet msbuild $project -nologo -getProperty:LoaderDirectory
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the loader SDK.' }
$minimumLoader = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $loaderDirectory 'SNSRModLoader.dll')).Version.ToString()

[xml]$manifest = '<modUpdate schemaVersion="1"><target version="September2018" /></modUpdate>'
$manifest.modUpdate.SetAttribute('id', $id)
$manifest.modUpdate.SetAttribute('version', $version)
$manifest.modUpdate.SetAttribute('minimumLoaderVersion', $minimumLoader)
$manifest.modUpdate.SetAttribute('sha256', (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant())
$manifest.modUpdate.SetAttribute('url', "https://github.com/$repository/releases/download/v$version/SubnauticaSpeedrunningMod.dll")
$manifest.Save((Join-Path $PSScriptRoot 'dist/mod-update.xml'))
Write-Host "Built dist/SubnauticaSpeedrunningMod.dll and updated dist/mod-update.xml (v$version)."
