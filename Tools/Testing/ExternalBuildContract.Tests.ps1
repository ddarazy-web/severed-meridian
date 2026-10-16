$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture = Join-Path $project ('Temp/ExternalContract-한글 space-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Tools/Build') -Force | Out-Null
Copy-Item (Join-Path $project 'Tools/Build/*.ps1') (Join-Path $fixture 'Tools/Build')
function Put($path,$value) { $p = Join-Path $fixture $path; New-Item -ItemType Directory (Split-Path $p) -Force | Out-Null; [IO.File]::WriteAllText($p,$value) }
function Check($ok,$label) { if (-not $ok) { throw $label }; Write-Host "PASS $label" }
Put 'ProjectSettings/ProjectVersion.txt' 'm_EditorVersion: 6000.3.10f1'
Put 'Logs/old.log' '12345678'
(Get-Item (Join-Path $fixture 'Logs/old.log')).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-10)
function Request($operation) {
    Put 'request.json' (@{version=2;requestId='fixture-01';operation=$operation;retentionDays=7} | ConvertTo-Json)
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixture 'Tools/Build/Invoke-BuildTool.ps1') -ProjectPath $fixture -RequestPath (Join-Path $fixture 'request.json') | Out-Null
    Get-Content (Join-Path $fixture 'result.json') -Raw | ConvertFrom-Json
}
$r = Request 'cleanup-preview'
Check ($r.requestId -eq 'fixture-01' -and $r.exitCode -eq 0) 'correlated response'
Check ($r.data.CandidateBytes -eq 8 -and (Test-Path (Join-Path $fixture 'Logs/old.log'))) 'preview bytes without delete'
$lock = [IO.File]::Open((Join-Path $fixture 'Logs/ExternalBuild/operation.lock'),'OpenOrCreate','ReadWrite','None')
try { $r = Request 'cleanup'; Check ($r.errorCode -eq 'Busy' -and $r.exitCode -ne 0) 'rechecks lock after preview' } finally { $lock.Dispose() }
$r = Request 'cleanup'
Check ($r.data.DeletedBytes -eq 8 -and -not (Test-Path (Join-Path $fixture 'Logs/old.log'))) 'fixture cleanup'
$r = Request 'unknown'
Check ($r.exitCode -ne 0 -and $r.errorCode) 'unknown request rejected'
Write-Host "Fixture: $fixture"

# 실제 Unity 대신 프로세스 경계만 모의한다. 설정 계약은 별도 Unity 검사에서 검증한다.
. (Join-Path $project 'Tools/Build/BuildToolHost.ps1')
$script:launches = 0
$script:missingResult = $false
$script:wrongId = $false
$script:fakeExit = 0
function Start-Process {
    param($FilePath,$ArgumentList,$WorkingDirectory,[switch]$PassThru,$WindowStyle)
    $script:launches++
    Check ($WindowStyle -eq 'Hidden') 'hidden Unity process'
    $m = [regex]::Match($ArgumentList,'"-matchBuildRequest" "([^"]+)"')
    $inputPath = $m.Groups[1].Value
    $inputData = Get-Content -LiteralPath $inputPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $script:missingResult) {
        $id = if ($script:wrongId) { 'wrong' } else { $inputData.requestId }
        $response = @{version=$inputData.version; requestId=$id; exitCode=0; data=@{operation=$inputData.operation; options=$inputData.options; saved=$inputData.useSavedSettings; arguments=$ArgumentList}}
        [IO.File]::WriteAllText((Join-Path (Split-Path $inputPath) 'result.json'),($response | ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
    }
    $process = [pscustomobject]@{ ExitCode=$script:fakeExit }
    $process | Add-Member ScriptMethod WaitForExit { return $true }
    return $process
}
$fakeUnity = Join-Path $fixture 'fake Unity.exe'
Put 'fake Unity.exe' 'not executable; mocked'
foreach ($operation in @('list','check','build')) {
    Put 'Assets/Settings/BuildProfiles/Fixture.asset' 'fixture'
    $request = [pscustomobject]@{version=1;operation=$operation;profile='Fixture';options=@{'debug-mode'=$true};useSavedSettings=$false}
    $r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath $fakeUnity
    Check ($r.exitCode -eq 0 -and $r.data.operation -eq $operation -and $r.data.options.'debug-mode' -eq $true -and $r.data.saved -eq $false) "v1 $operation preserved"
    if ($operation -eq 'build') { Check ($r.data.arguments -match '-activeBuildProfile') 'build selects profile' }
    else { Check ($r.data.arguments -notmatch '-activeBuildProfile') 'query does not activate profile' }
}
$request = [pscustomobject]@{version=2;requestId='test-id';operation='settings'}
$script:missingResult = $true
$r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath $fakeUnity
Check ($r.errorCode -eq 'MissingResult' -and $r.exitCode -ne 0) 'missing result fails'
$script:missingResult = $false; $script:wrongId = $true
$r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath $fakeUnity
Check ($r.errorCode -eq 'InvalidResult') 'wrong request ID fails'
$script:wrongId = $false; $script:fakeExit = 1
$r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath $fakeUnity
Check ($r.errorCode -eq 'UnityFailed') 'process failure overrides result'
$script:fakeExit = 0
$before = $script:launches
$lock = [IO.File]::Open((Join-Path $fixture 'Logs/ExternalBuild/operation.lock'),'OpenOrCreate','ReadWrite','None')
try { $r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath $fakeUnity; Check ($r.errorCode -eq 'Busy') 'concurrent request blocked' } finally { $lock.Dispose() }
New-Item -ItemType Directory -Path (Join-Path $fixture 'Temp') -Force | Out-Null
$lock = [IO.File]::Open((Join-Path $fixture 'Temp/UnityLockfile'),'OpenOrCreate','ReadWrite','None')
try { $r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath $fakeUnity; Check ($r.errorCode -eq 'Busy') 'project editor blocks external operation' } finally { $lock.Dispose() }
$r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath (Join-Path $fixture 'missing.exe')
Check ($r.errorCode -eq 'UnityNotFound') 'missing Unity'
$request.version = 99
$r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath $fakeUnity
Check ($r.errorCode -eq 'InvalidRequest') 'unsupported version'
Check ($before -eq $script:launches) 'rejected requests launch no process'
$request = [pscustomobject]@{version=2;requestId='invalid-options';operation='save-settings';options=[pscustomobject]@{'debug-mode'='not-boolean'}}
$r = Invoke-BuildToolHost -ProjectPath $fixture -Request $request -UnityPath $fakeUnity
Check ($r.errorCode -eq 'InvalidRequest' -and $before -eq $script:launches) 'invalid option payload rejected before logging'
Remove-Item Function:Start-Process
Write-Host 'PASS external host contract'
