$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $root 'Network/QueueTiming.cs'))
$checks = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'QueueTimingChecks.cs'))
Add-Type -TypeDefinition ($source + $checks)
[QueueTimingChecks]::Run()
Write-Host 'PASS: local elapsed time, connect before countdown, skip countdown after slow connection, bounded timeout and fresh queue reset.'
