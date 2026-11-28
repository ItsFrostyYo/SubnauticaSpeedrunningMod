$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$timing = [IO.File]::ReadAllText((Join-Path $root 'Network/QueueTiming.cs'))
$flow = [IO.File]::ReadAllText((Join-Path $root 'Network/Matchmaking.cs'))
# Substitute only the engine clock boundary; execute the real queue state machine.
$flow = [regex]::Replace($flow, '(?m)^using [^\r\n]+;\r?\n', '')
$flow = $flow.Replace('Clock.Elapsed.TotalSeconds', 'QueueFlowChecks.Now')
$stubs = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'QueueFlowChecks.cs'))
$imports = 'using System.Diagnostics; using System.Threading; using UnityEngine; using UnityEngine.SceneManagement; using ItsFrosti.SpeedrunningMod.Ui; using ItsFrosti.SpeedrunningMod.Seeds.Ruleset8;'
$compiler = New-Object System.CodeDom.Compiler.CompilerParameters -Property @{ CompilerOptions = '/nowarn:0414' }
[void]$compiler.ReferencedAssemblies.Add('System.dll')
Add-Type -TypeDefinition ($imports + $timing + $flow + $stubs) -CompilerParameters $compiler
[QueueFlowChecks]::Run()
Write-Host 'PASS: queue state machine connects before countdown/unload, skips slow countdown, recovers missing assignment and exposes terminal errors.'
