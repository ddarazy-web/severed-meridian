param([string]$Harness = (Join-Path $PSScriptRoot 'ProjectTests.ps1'))
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) ('Logs/TestHarness/SelfTests/' + [guid]::NewGuid().ToString('N'))
$passed = 0
function Check($condition, $message) {
    if (-not $condition) { throw $message }
    $script:passed++; Write-Output "PASS $message"
}
function Reject([scriptblock]$operation, [string]$message, [string]$pattern) {
    $rejected = $false
    try { & $operation | Out-Null } catch { $rejected = -not $pattern -or $_.Exception.Message -match $pattern }
    Check $rejected $message
}
New-Item -ItemType Directory -Path "$root/Assets", "$root/ProjectSettings", "$root/Tests/Editor/Features/Sample" -Force | Out-Null
[IO.File]::WriteAllText("$root/ProjectSettings/ProjectVersion.txt", 'm_EditorVersion: 6000.3.10f1')
[IO.File]::WriteAllText("$root/Tests/Editor/Features/Sample/Sample.cs", 'class Sample {}')
[IO.File]::WriteAllText("$root/Tests/Editor/Features/Sample/Sample.cs.meta", 'guid: 12345678901234567890123456789012')
try {
    & $Harness -Action Attach -ProjectPath $root | Out-Null
    $mounted = "$root/Assets/__ProjectTests/Editor/Features/Sample/Sample.cs"
    Check ((Get-Content $mounted -Raw) -eq 'class Sample {}') '실제 테스트 소스 연결'
    Check ((Get-FileHash "$mounted.meta").Hash -eq (Get-FileHash "$root/Tests/Editor/Features/Sample/Sample.cs.meta").Hash) '메타 바이트 보존'
    Reject { & $Harness -Action Attach -ProjectPath $root } '중복 연결 거절'
    [IO.File]::WriteAllText($mounted, 'class Modified {}')
    Reject { & $Harness -Action Detach -ProjectPath $root } '수정한 복사본 해제 거절'
    Check (Test-Path $mounted) '수정한 복사본 보존'
    Copy-Item -LiteralPath "$root/Tests/Editor/Features/Sample/Sample.cs" -Destination $mounted
    [IO.File]::WriteAllText("$root/Assets/__ProjectTests/foreign.txt", 'keep')
    Reject { & $Harness -Action Detach -ProjectPath $root } '알 수 없는 파일 해제 거절'
    Check (Test-Path "$root/Assets/__ProjectTests/foreign.txt") '알 수 없는 파일 보존'
    Remove-Item -LiteralPath "$root/Assets/__ProjectTests/foreign.txt"
    [IO.File]::WriteAllText("$root/Assets/__ProjectTests/Editor/Features/Sample.meta", "fileFormatVersion: 2`nguid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa`nfolderAsset: yes`nDefaultImporter:`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n")
    & $Harness -Action Detach -ProjectPath $root | Out-Null
    Check (-not (Test-Path "$root/Assets/__ProjectTests")) '소유한 연결 및 Unity 폴더 메타 해제'
    Check (Test-Path "$root/Tests/Editor/Features/Sample/Sample.cs") '외부 원본 보존'
    & $Harness -Action Detach -ProjectPath $root | Out-Null
    Reject { & $Harness -Action Run -ProjectPath $root -Method 'invalid;command' } '잘못된 실행 메서드 연결 전 거절'
    Check (-not (Test-Path "$root/Assets/__ProjectTests")) '입력 오류 시 연결하지 않음'
    New-Item -ItemType Directory -Path "$root/Temp" -Force | Out-Null
    $editorLock = [IO.File]::Open("$root/Temp/UnityLockfile", 'OpenOrCreate', 'ReadWrite', 'None')
    try { Reject { & $Harness -Action Attach -ProjectPath $root } '열린 프로젝트 Editor 연결 거절' }
    finally { $editorLock.Dispose() }
    # 실제 실패하는 외부 프로세스로 종료 코드와 finally 해제를 검사한다.
    $shellPath = (Get-Process -Id $PID).Path
    Reject { & $Harness -Action Run -ProjectPath $root -Method 'Sample.Run' -UnityPath $shellPath } '실제 비정상 종료 코드 전달' 'Unity 검사 실패\(exit [1-9][0-9]*\)'
    Check (-not (Test-Path "$root/Assets/__ProjectTests") -and -not (Test-Path "$root/Logs/TestHarness/connection.json")) '실행 실패 후 자동 해제'
    New-Item -ItemType Directory -Path "$root/Assets/__ProjectTests" | Out-Null
    [IO.File]::WriteAllText("$root/Assets/__ProjectTests/owned-by-user.txt", 'keep')
    Reject { & $Harness -Action Detach -ProjectPath $root } '미소유 폴더 해제 거절'
    Reject { & $Harness -Action Attach -ProjectPath $root } '미소유 폴더 덮어쓰기 거절'
    Check (Test-Path "$root/Assets/__ProjectTests/owned-by-user.txt") '사용자 폴더 보존'
    Write-Output "TOTAL PASS $passed"
} finally {
    # 검사 증거는 Logs에 남겨 실패 시 원본과 복사본을 확인할 수 있다.
    Write-Output "SELFTEST WORKSPACE $root"
}
