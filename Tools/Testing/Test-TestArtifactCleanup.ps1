$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestArtifactCleanup.ps1')
$root = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) ('Logs/TestHarness/CleanupChecks/' + [guid]::NewGuid().ToString('N'))
$trial = 'ContentData/Trials/game-authoring-stage-03'
function Put($path, $text = '{}') {
    $path = Join-Path $root $path
    New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    [IO.File]::WriteAllText($path, $text)
}
function Check($value, $message) { if (!$value) { throw $message }; Write-Output "PASS $message" }
try {
    Put "$trial/session-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/draft.json"
    $before = Start-TestArtifactCleanup $root
    $new = "$trial/session-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
    Put "$new/draft.json"
    Put "$new/project.json"
    Put "$new/snapshots/ffffffffffffffffffffffffffffffff/0000.json"
    Put "$new/report.json"
    Put "$new/checks.txt"
    Put "$new/capture.png"
    Put 'ContentData/Candidates/main/draft.json'
    Put "$trial/my-work/draft.json"
    $log = 'Logs/LevelToolRecoveryPerformance/prepared-cccccccccccccccccccccccccccccccc'
    Put "$log/draft.json.previous"
    Put "$log/measurements.jsonl"
    $protected = "$trial/tool-dddddddddddddddddddddddddddddddd"
    Put "$protected/project.json"
    Put 'ProjectSettings/AuthoringSource.json' ('{"folder":"../' + $protected + '"}')
    $locked = "$trial/tool-eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
    Put "$locked/draft.json"
    $linked = "$trial/tool-ffffffffffffffffffffffffffffffff"
    Put "$linked/draft.json"
    Put 'outside/keep.json'
    $junction = Join-Path $root "$linked/link"
    New-Item -ItemType Junction -Path $junction -Target (Join-Path $root 'outside') | Out-Null
    $handle = [IO.File]::Open((Join-Path $root "$locked/draft.json"), 'Open', 'ReadWrite', 'None')
    try { $result = Complete-TestArtifactCleanup $before }
    finally { $handle.Dispose() }
    Check (!(Test-Path "$root/$new/draft.json") -and !(Test-Path "$root/$new/project.json")) '이번 실행 테스트 사본 제거'
    Check (Test-Path "$root/$trial/session-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/draft.json") '이전 사본 보존'
    foreach ($p in @("$new/report.json", "$new/checks.txt", "$new/capture.png", "$log/measurements.jsonl",
        'ContentData/Candidates/main/draft.json', "$trial/my-work/draft.json", "$protected/project.json", "$locked/draft.json")) {
        Check (Test-Path "$root/$p") "보호: $p"
    }
    Check (!(Test-Path "$root/$log/draft.json.previous")) '검사 복구 중복 제거'
    Check (!(Test-Path "$root/$new/snapshots")) '테스트 스냅샷과 빈 폴더 제거'
    Check ((Test-Path "$root/outside/keep.json") -and (Test-Path "$root/$linked/draft.json")) '링크 포함 사본과 외부 파일 보존'
    Check ($result.DeletedFiles -eq 4) '삭제 수 일치'
} finally {
    # 이 스크립트가 생성한 격리 검사 데이터만 제거하고 실행 결과는 콘솔에 남긴다.
    $allowed = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Logs/TestHarness/CleanupChecks')) + [IO.Path]::DirectorySeparatorChar
    if ($root.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path $root)) {
        if ($junction -and (Test-Path -LiteralPath $junction)) { [IO.Directory]::Delete($junction) }
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
