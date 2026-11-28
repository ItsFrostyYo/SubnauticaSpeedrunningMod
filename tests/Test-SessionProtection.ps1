$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $root 'Network/SessionProtection.cs'))
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SessionProtectionChecks.cs'))
Add-Type -TypeDefinition ($source + $harness) -ReferencedAssemblies System.dll,System.Security.dll
[AccountProtectionTest]::Run()
$assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $root 'dist/SubnauticaSpeedrunningMod.dll'))
if ($assembly.GetReferencedAssemblies() | Where-Object { $_.Name -eq 'System.Security' }) { throw 'Plugin still depends on System.Security.dll.' }
Write-Host 'PASS: DPAPI round-trip, legacy compatibility in both directions, tamper/wrong-entropy rejection, and no System.Security plugin dependency.'
