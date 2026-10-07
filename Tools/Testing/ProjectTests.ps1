param(
    [ValidateSet('Status', 'Attach', 'Detach', 'Run', 'Compile')][string]$Action = 'Status',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../..'),
    [string]$UnityPath,
    [string]$Method
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
$source = Join-Path $projectRoot 'Tests/Editor'
$target = Join-Path $projectRoot 'Assets/__ProjectTests'
$stateDirectory = Join-Path $projectRoot 'Logs/TestHarness'
$statePath = Join-Path $stateDirectory 'connection.json'
if (-not (Test-Path "$projectRoot/ProjectSettings/ProjectVersion.txt") -or -not (Test-Path "$projectRoot/Assets")) {
    throw "Unity 프로젝트가 아닙니다: $projectRoot"
}
if ($Action -eq 'Status') {
    if (Test-Path $statePath) { Write-Output "연결 기록: $statePath"; Get-Content $statePath }
    elseif ((Test-Path $target) -or (Test-Path "$target.meta")) { throw "소유 기록 없는 폴더입니다. 자동 삭제하지 않습니다: $target" }
    else { Write-Output '테스트 연결 해제 상태' }
    return
}
New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
# 같은 프로젝트의 도구 실행과 Unity Editor 사용을 서로 겹치지 않게 한다.
$gate = [IO.File]::Open((Join-Path $stateDirectory 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
function Assert-EditorClosed {
    $editorLock = Join-Path $projectRoot 'Temp/UnityLockfile'
    if (Test-Path $editorLock) {
        try { $probe = [IO.File]::Open($editorLock, 'Open', 'ReadWrite', 'None'); $probe.Dispose() }
        catch { throw '이 프로젝트의 Unity Editor를 저장 후 닫아야 연결 상태를 변경하거나 별도 검사할 수 있습니다.' }
    }
}
function Connect-Tests {
    if ((Test-Path $target) -or (Test-Path "$target.meta") -or (Test-Path $statePath)) { throw '이미 연결되었거나 소유 기록이 남아 있습니다. Status를 확인하고 Detach하세요.' }
    if (-not (Test-Path $source)) { throw "테스트 원본이 없습니다: $source" }
    $sourceEntries = @(Get-ChildItem -LiteralPath $source -Recurse -Force)
    if (@($sourceEntries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0 -or
        ((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw '테스트 원본의 링크는 복사하지 않습니다.' }
    $files = @($sourceEntries | Where-Object { -not $_.PSIsContainer } | ForEach-Object {
        [pscustomobject]@{ path = 'Editor/' + $_.FullName.Substring($source.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    if ($files.Count -eq 0) { throw '빈 테스트 원본은 연결하지 않습니다.' }
    $directories = @('Editor') + @($sourceEntries | Where-Object { $_.PSIsContainer } | ForEach-Object { 'Editor/' + $_.FullName.Substring($source.Length + 1).Replace('\', '/') })
    # 복사 도중 중단해도 생성한 경로와 파일의 소유 정보를 남긴다.
    $state = [pscustomobject]@{ version = 1; project = $projectRoot; target = $target; files = $files; directories = $directories }
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding utf8
    New-Item -ItemType Directory -Path "$target/Editor" -Force | Out-Null
    foreach ($directory in $directories) { New-Item -ItemType Directory -Path (Join-Path $target $directory) -Force | Out-Null }
    foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $source $file.path.Substring(7)) -Destination (Join-Path $target $file.path) }
    Write-Output "테스트 연결: $($files.Count)개 파일 → $target"
}
function Disconnect-Tests {
    if (-not (Test-Path $statePath)) {
        if ((Test-Path $target) -or (Test-Path "$target.meta")) { throw "미소유 경로는 삭제하지 않습니다: $target" }
        Write-Output '이미 연결 해제 상태'; return
    }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($state.version -ne 1 -or $state.project -ne $projectRoot -or $state.target -ne $target) { throw '현재 프로젝트의 연결 기록이 아닙니다.' }
    # 모든 최종 경로를 확인한 뒤에만 삭제한다. 수정·추가 파일은 보존한다.
    $expected = @{}
    foreach ($file in $state.files) {
        $full = [IO.Path]::GetFullPath((Join-Path $target $file.path))
        if (-not $full.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '연결 기록의 파일이 대상 폴더 밖을 가리킵니다.' }
        $expected[$full] = $file.sha256
    }
    $allowedDirectories = @{}
    foreach ($directory in $state.directories) {
        $full = [IO.Path]::GetFullPath((Join-Path $target $directory))
        if (-not $full.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '연결 기록의 폴더가 대상 폴더 밖을 가리킵니다.' }
        $allowedDirectories[$full] = $true
    }
    $allowedDirectories[$target] = $true
    $entries = @()
    if (Test-Path $target) {
        if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '링크 대상은 삭제하지 않습니다.' }
        $entries = @(Get-ChildItem -LiteralPath $target -Recurse -Force)
    }
    foreach ($entry in $entries) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '연결 영역의 링크는 삭제하지 않습니다.' }
        if ($entry.PSIsContainer) {
            if (-not $allowedDirectories.ContainsKey($entry.FullName)) { throw "추가 폴더를 보존합니다: $($entry.FullName)" }
        } elseif ($expected.ContainsKey($entry.FullName)) {
            if ((Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash -ne $expected[$entry.FullName]) { throw "수정한 복사본을 보존합니다. Tests 원본으로 반영하거나 복구 후 다시 해제하세요: $($entry.FullName)" }
        } elseif ($entry.Extension -ne '.meta' -or -not $allowedDirectories.ContainsKey($entry.FullName.Substring(0, $entry.FullName.Length - 5)) -or
            (Get-Content -LiteralPath $entry.FullName -Raw) -notmatch '(?m)^folderAsset: yes\s*$') { throw "알 수 없는 파일을 보존합니다: $($entry.FullName)" }
    }
    if ((Test-Path "$target.meta") -and (Get-Content -LiteralPath "$target.meta" -Raw) -notmatch '(?m)^folderAsset: yes\s*$') { throw '루트 메타가 폴더 메타가 아닙니다. 보존합니다.' }
    # 재귀 삭제 대신 검증한 파일과 빈 폴더를 깊은 순서로 제거한다.
    foreach ($entry in @($entries | Where-Object { -not $_.PSIsContainer })) { Remove-Item -LiteralPath $entry.FullName }
    foreach ($entry in @($entries | Where-Object { $_.PSIsContainer } | Sort-Object { $_.FullName.Length } -Descending)) { [IO.Directory]::Delete($entry.FullName) }
    if (Test-Path $target) { [IO.Directory]::Delete($target) }
    if (Test-Path "$target.meta") { Remove-Item -LiteralPath "$target.meta" }
    Remove-Item -LiteralPath $statePath
    Write-Output '테스트 연결 해제 완료'
}
try {
    Assert-EditorClosed
    if ($Action -eq 'Attach') { Connect-Tests; return }
    if ($Action -eq 'Detach') { Disconnect-Tests; return }
    if ($Action -eq 'Run' -and $Method -notmatch '^[A-Za-z_][A-Za-z0-9_.]*$') { throw 'Run에는 기존 public static 검사 메서드의 전체 이름을 지정하세요.' }
    if (-not $UnityPath) {
        $versionText = Get-Content "$projectRoot/ProjectSettings/ProjectVersion.txt" -Raw
        if ($versionText -notmatch '(?m)^m_EditorVersion:\s*(\S+)') { throw 'Unity 버전을 읽을 수 없습니다.' }
        $UnityPath = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$($Matches[1])/Editor/Unity.exe"
    }
    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw 'Unity 실행 파일을 찾을 수 없습니다. -UnityPath를 지정하세요.' }
    Connect-Tests
    try {
        $logPath = Join-Path $stateDirectory ((Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $Action + '.log')
        $unityArguments = @('-batchmode', '-projectPath', $projectRoot, '-logFile', $logPath)
        if ($Action -eq 'Compile') { $unityArguments += '-quit' }
        else { $unityArguments += @('-executeMethod', $Method) }
        Write-Output "Unity $Action 로그: $logPath"
        # -nographics는 실제 UI 검사를 막으므로 사용하지 않는다. Run의 종료는 기존 검사 메서드가 담당한다.
        $quotedArguments = @($unityArguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' })
        $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
        $unityProcess.WaitForExit()
        $unityExit = $unityProcess.ExitCode
        $unityProcess.Dispose()
        if ($unityExit -ne 0) { throw "Unity 검사 실패(exit $unityExit): $logPath" }
        Write-Output "Unity $Action 성공(exit 0)"
    } finally { Assert-EditorClosed; Disconnect-Tests }
} finally { $gate.Dispose() }
