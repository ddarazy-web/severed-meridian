# 테스트 코드의 GUID 전용 출력 경로만 등록한다. 실제 편집의 draft-* 경로와 고정 fixture는 제외한다.
function Start-TestArtifactCleanup([string]$ProjectPath) {
    $root = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
    $rules = @(
        @('ContentData/Trials/game-authoring-stage-03', '(session|tool|tool-ui|flow-draft|flow-play|play|recovery|samples|window)'),
        @('ContentData/Trials/android-level-tool-stage-03', 'window-selection'),
        @('Logs/GameAuthoringStage05', '(advanced|recovery|reload|trial-lifecycle)'),
        @('Logs/LevelToolRecoveryPerformance', '(fixed-ten|prepared|history|pending)'),
        @('Logs/LevelToolRefactorStage04', '(recovery-cost|workspace|fingerprint|data-flow|packed-play)'),
        @('Logs/LevelToolManualSave', '(performance|check|ux)'),
        @('Logs/LevelToolRefreshPerformance', '(extended|viewports)'),
        @('Logs/LevelToolUiStage01', '(viewport-recovery|input)')
    )
    $scopes = foreach ($rule in $rules) {
        $path = Join-Path $root $rule[0]
        # 상위 경로의 junction도 검사하여 프로젝트 밖을 열거하지 않는다.
        $cursor = $path
        $safe = $true
        while ($cursor -and $cursor.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
            if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { $safe = $false; break }
            $cursor = Split-Path $cursor
        }
        if (!$safe) { continue }
        $existing = @{}
        if (Test-Path -LiteralPath $path) {
            foreach ($directory in Get-ChildItem -LiteralPath $path -Directory -Force) { $existing[$directory.FullName] = $true }
        }
        [pscustomobject]@{ Path = $path; Pattern = '^' + $rule[1] + '-[a-f0-9]{32}(-(copy|fresh|future|journal|legacy-[123]|new|recovered))?$'; Existing = $existing }
    }
    [pscustomobject]@{ Root = $root; Scopes = @($scopes) }
}

function Complete-TestArtifactCleanup($Snapshot) {
    $result = [ordered]@{ DeletedFiles = 0; DeletedBytes = [long]0; Skipped = @() }
    $configuration = Join-Path $Snapshot.Root 'ProjectSettings/AuthoringSource.json'
    $selected = $null
    if (Test-Path -LiteralPath $configuration) {
        # 원본 선택을 해석할 수 없으면 삭제하지 않는다.
        $selection = Get-Content -LiteralPath $configuration -Raw | ConvertFrom-Json
        if (!$selection.folder) { throw '정식 원본 경로를 확인할 수 없어 테스트 사본을 보존합니다.' }
        $selected = [IO.Path]::GetFullPath((Join-Path (Split-Path $configuration) $selection.folder))
    }
    foreach ($scope in $Snapshot.Scopes) {
        if (!(Test-Path -LiteralPath $scope.Path)) { continue }
        $cursor = $scope.Path
        $safe = $true
        while ($cursor -and $cursor.StartsWith($Snapshot.Root, [StringComparison]::OrdinalIgnoreCase)) {
            if ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint) { $safe = $false; break }
            $cursor = Split-Path $cursor
        }
        if (!$safe) { $result.Skipped += $scope.Path; continue }
        foreach ($directory in Get-ChildItem -LiteralPath $scope.Path -Directory -Force) {
            $path = [IO.Path]::GetFullPath($directory.FullName)
            if ($scope.Existing.ContainsKey($path) -or $directory.Name -notmatch $scope.Pattern) { continue }
            if (!$path.StartsWith($scope.Path + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '테스트 경로가 허용 범위를 벗어났습니다.' }
            if ($selected -and ($selected -eq $path -or $selected.StartsWith($path + '\', [StringComparison]::OrdinalIgnoreCase) -or $path.StartsWith($selected + '\', [StringComparison]::OrdinalIgnoreCase))) { continue }
            # 링크가 하나라도 있으면 해당 사본 전체를 보존한다.
            if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { $result.Skipped += $path; continue }
            $entries = @(Get-ChildItem -LiteralPath $path -Recurse -Force)
            if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { $result.Skipped += $path; continue }
            # 결과 JSON·JSONL·텍스트·로그·이미지·ZIP은 유지한다. 저장소 문서와 복구 사본만 삭제한다.
            $files = @($entries | Where-Object {
                ! $_.PSIsContainer -and (
                    $_.Name -match '^(project|draft|part-draft)\.json(\.(previous|pending|replace)(-[a-f0-9]+)?)?$' -or
                    $_.FullName.Substring($path.Length).Replace('\', '/') -match '/snapshots/[a-f0-9]{32}/[0-9]{4,}\.json$'
                )
            })
            try {
                foreach ($file in $files) { $probe = [IO.File]::Open($file.FullName, 'Open', 'Read', 'None'); $probe.Dispose() }
                foreach ($file in $files) {
                    Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
                    $result.DeletedFiles++; $result.DeletedBytes += $file.Length
                }
                # 결과가 남아 있는 폴더는 유지한다. 재귀 삭제는 사용하지 않는다.
                foreach ($folder in @($entries | Where-Object PSIsContainer | Sort-Object { $_.FullName.Length } -Descending) + @($directory)) {
                    if (@(Get-ChildItem -LiteralPath $folder.FullName -Force).Count -eq 0) { [IO.Directory]::Delete($folder.FullName) }
                }
            } catch { $result.Skipped += $path }
        }
    }
    [pscustomobject]$result
}
