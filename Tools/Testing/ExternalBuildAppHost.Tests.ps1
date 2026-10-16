$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $project 'Tools/Build/BuildToolHost.ps1')
$fixture = Join-Path $project ('Temp/BuildAppHost-' + [guid]::NewGuid().ToString('N'))
function Put($path,$text) { $full = Join-Path $fixture $path; New-Item -ItemType Directory (Split-Path $full) -Force | Out-Null; [IO.File]::WriteAllText($full,$text) }
function Check($ok,$label) { if (-not $ok) { throw $label }; Write-Host "PASS $label" }
Put 'ProjectSettings/ProjectVersion.txt' 'm_EditorVersion: 6000.3.10f1'
Put 'Assets/Settings/BuildProfiles/Fixture.asset' 'fixture'
Put 'Assets/Settings/BuildProfiles/Fixture.asset.meta' 'guid: 0123456789abcdef0123456789abcdef'
Put 'Unity.exe' 'mock'
$script:launches = 0
function Start-Process {
    param($FilePath,$ArgumentList,$WorkingDirectory,[switch]$PassThru,$WindowStyle)
    $script:launches++
    $path = [regex]::Match($ArgumentList,'"-matchBuildRequest" "([^"]+)"').Groups[1].Value
    $r = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $response = @{version=2;requestId=$r.requestId;exitCode=0;data=@{operation=$r.operation;arguments=$ArgumentList}}
    [IO.File]::WriteAllText((Join-Path (Split-Path $path) 'result.json'),($response | ConvertTo-Json -Depth 15))
    $p = [pscustomobject]@{ExitCode=0}; $p | Add-Member ScriptMethod WaitForExit {return $true}; return $p
}
foreach ($operation in @('check','build')) {
    $request = [pscustomobject]@{version=2;requestId=[guid]::NewGuid().ToString('N');operation=$operation;profileGuid='0123456789abcdef0123456789abcdef';expectedRevision='revision'}
    $r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath (Join-Path $fixture 'Unity.exe')
    Check ($r.exitCode -eq 0 -and $r.requestId -eq $request.requestId) "v2 $operation"
    $progress = Get-Content (Join-Path $fixture ('Library/MatchBuildApp/' + $request.requestId + '/progress.json')) -Raw | ConvertFrom-Json
    Check ($progress.requestId -eq $request.requestId -and $progress.logPath) 'progress is correlated'
    if ($operation -eq 'build') { Check ($r.data.arguments -match '-activeBuildProfile') 'v2 build selects GUID profile' }
}
$request = [pscustomobject]@{version=2;requestId='..\escape';operation='settings'}
$r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath (Join-Path $fixture 'Unity.exe')
Check ($r.errorCode -eq 'InvalidRequest') 'unsafe request ID rejected'
Remove-Item Function:Start-Process
Write-Host "Fixture $fixture"
