$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$gameJson = 'C:/Program Files (x86)/Steam/steamapps/common/Subnautica2018/Subnautica_Data/Managed/LitJson.dll'
[void][Reflection.Assembly]::LoadFrom($gameJson)
$source = [IO.File]::ReadAllText((Join-Path $root 'Network/AccountProtocol.cs'))
$harness = 'public static class ProtocolCheck { public static string Body(string key, string value) { return ItsFrosti.SpeedrunningMod.AccountProtocol.StringRequest(key, value); } }'
Add-Type -TypeDefinition ($source + $harness) -ReferencedAssemblies $gameJson
foreach ($field in @('deviceSecret','refreshToken')) {
    foreach ($value in @('simple-value', 'value-with-"quotes"-and-\slashes', '')) {
        $body = [ProtocolCheck]::Body($field,$value)
        $parsed = [LitJson.JsonMapper]::ToObject($body)
        if ([string]$parsed[$field] -ne $value) { throw 'Auth payload did not round-trip through game LitJson.' }
    }
}
Write-Host 'PASS: device polling and refresh JSON round-trip through the exact game LitJson library, including escaped values.'
