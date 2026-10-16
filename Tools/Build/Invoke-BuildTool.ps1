[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectPath, [Parameter(Mandatory=$true)][string]$RequestPath, [string]$UnityPath)
$ErrorActionPreference = 'Stop'
try {
    $RequestPath = (Resolve-Path -LiteralPath $RequestPath).Path
    $request = Get-Content -LiteralPath $RequestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    . (Join-Path $PSScriptRoot 'BuildToolHost.ps1')
    $result = Invoke-BuildToolHost -ProjectPath $ProjectPath -Request $request -UnityPath $UnityPath
} catch { $result = [pscustomobject]@{ version=2; requestId=$null; exitCode=2; data=$null; errorCode='InvalidRequest'; message=$_.Exception.Message } }
$json = $result | ConvertTo-Json -Depth 30
[IO.File]::WriteAllText((Join-Path (Split-Path ([IO.Path]::GetFullPath($RequestPath))) 'result.json'), $json, [Text.UTF8Encoding]::new($false))
Write-Output $json
exit ([int]$result.exitCode)
