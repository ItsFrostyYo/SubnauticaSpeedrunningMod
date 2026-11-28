$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $root 'UI/FreecamSession.cs'))
$stubs = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'FreecamSessionChecks.cs'))
Add-Type -TypeDefinition ($source + $stubs)
[FreecamSessionChecks]::Run()
Write-Host 'PASS: native camera activation/max speed, repeated result handling, pause ownership, frozen/slow-speed exit cleanup, rocket exclusion and freecam ESC handling.'
