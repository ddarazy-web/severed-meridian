[CmdletBinding()]
param(
    [string]$Profile,
    [ValidateSet('apk', 'aab')][string]$Format,
    [ValidateSet('on', 'off')][string]$DebugMode,
    [string[]]$Option = @(),
    [switch]$UseDefaults,
    [switch]$ListProfiles,
    [switch]$Check,
    [string]$UnityPath,
    [switch]$Help
)
$ErrorActionPreference = 'Stop'
if ($Help) {
    Write-Output 'Build.bat -ListProfiles'
    Write-Output 'Build.bat -Profile "Android Game" -Format apk -DebugMode off [-Check]'
    Write-Output 'Build.bat -Profile "Windows Level Editor" -Option "debug-mode=off"'
    Write-Output 'Options: -UseDefaults, -UnityPath "C:\...\Unity.exe". See Tools/Build/README.md.'
    exit 0
}
$gate = $null
$exitCode = 2
try {
    if ($ListProfiles -and ($Profile -or $Check -or $Format -or $DebugMode -or $Option.Count -gt 0 -or $UseDefaults)) {
        throw '-ListProfiles cannot be combined with build arguments.'
    }
    if (-not $ListProfiles -and [string]::IsNullOrWhiteSpace($Profile)) { throw '-Profile is required. Use -ListProfiles or -Help.' }
    $choices = @{}
    foreach ($item in $Option) {
        foreach ($entry in $item.Split(',')) {
            if ($entry -notmatch '^([a-zA-Z0-9_.-]+)=(on|off|true|false)$') { throw "Invalid option: $entry. Use id=on or id=off." }
            $key = $Matches[1]
            if ($choices.ContainsKey($key)) { throw "Duplicate option: $key" }
            $choices[$key] = $Matches[2] -in @('on', 'true')
        }
    }
    if ($DebugMode) {
        if ($choices.ContainsKey('debug-mode')) { throw 'Specify either -DebugMode or -Option debug-mode, not both.' }
        $choices['debug-mode'] = $DebugMode -eq 'on'
    }
    if ($Format) {
        if ($choices.ContainsKey('android-app-bundle')) { throw 'Specify either -Format or -Option android-app-bundle, not both.' }
        $choices['android-app-bundle'] = $Format -eq 'aab'
    }
    $projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
    $versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
    $version = (Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$').Matches.Groups[1].Value.Trim()
    if (-not $UnityPath) { $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe" }
    $UnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
    if (-not $ListProfiles -and -not $Check) {
        $cleanup = & (Join-Path $PSScriptRoot 'Clean-Logs.ps1')
        Write-Output ("Log cleanup: {0}, removed {1} entries ({2:N1} MB)." -f $cleanup.Status, $cleanup.DeletedEntries, ($cleanup.DeletedBytes / 1MB))
        foreach ($failure in $cleanup.Errors) { Write-Warning $failure }
    }
    $logsRoot = Join-Path $projectRoot 'Logs/ExternalBuild'
    New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
    try { $gate = [IO.File]::Open((Join-Path $logsRoot 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { throw 'Another external build operation is already running for this project.' }
    $editorLock = Join-Path $projectRoot 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $editorLock) {
        try { $probe = [IO.File]::Open($editorLock, 'Open', 'ReadWrite', 'None'); $probe.Dispose() }
        catch { throw 'This project is open in Unity. Save and close it before external build.' }
    }
    $runDirectory = Join-Path $logsRoot ((Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $runDirectory | Out-Null
    $requestPath = Join-Path $runDirectory 'request.json'
    $resultPath = Join-Path $runDirectory 'result.json'
    $logPath = Join-Path $runDirectory 'Unity.log'
    $operation = if ($ListProfiles) { 'list' } elseif ($Check) { 'check' } else { 'build' }
    $request = @{ version = 1; operation = $operation; profile = $Profile; options = $choices; useSavedSettings = -not $UseDefaults }
    [IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    Write-Output "Unity version: $version"
    Write-Output "Log: $logPath"
    # 플랫폼 전환과 도메인 재로드 동안 유지하고, 진입점에서 결과를 기록한 뒤 종료한다.
    $arguments = @('-batchmode', '-projectPath', $projectRoot, '-logFile', $logPath,
        '-executeMethod', 'Products.Editor.BuildToolBatch.Run', '-matchBuildRequest', $requestPath)
    if ($operation -eq 'build') {
        $profileAssets = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Settings/BuildProfiles') -Filter '*.asset' |
            Where-Object { $_.BaseName -ceq $Profile })
        if ($profileAssets.Count -ne 1) { throw "Build profile asset not found: $Profile. Use -ListProfiles." }
        $arguments += @('-activeBuildProfile', ('Assets/Settings/BuildProfiles/' + $profileAssets[0].Name))
    }
    $quotedArguments = ($arguments | ForEach-Object {
        if ($_ -match '["\r\n]') { throw 'Quotes/newlines are not supported in Unity paths.' }
        '"' + $_ + '"'
    }) -join ' '
    $startedUtc = [DateTime]::UtcNow
    $process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
    $lastStatus = ''
    while (-not $process.WaitForExit(5000)) {
        $currentPath = Join-Path $projectRoot 'Library/MatchBuildTool/current.json'
        if ($operation -eq 'build' -and (Test-Path -LiteralPath $currentPath)) {
            try {
                $current = Get-Content -LiteralPath $currentPath -Raw -Encoding UTF8 | ConvertFrom-Json
                if ([DateTime]::Parse($current.startedUtc).ToUniversalTime() -ge $startedUtc -and $current.status -and $current.status -ne $lastStatus) {
                    Write-Output $current.status; $lastStatus = $current.status
                }
            } catch { } # 원자적 파일 교체 중에는 잠시 이력을 읽지 못할 수 있다.
        }
    }
    $process.WaitForExit()
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Unity exited ($($process.ExitCode)) without a result. Inspect $logPath" }
    $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Output ($result | ConvertTo-Json -Depth 12)
    Write-Output "Result: $resultPath"
    $exitCode = [int]$result.exitCode
    if ($process.ExitCode -ne 0 -and $exitCode -eq 0) { $exitCode = 2 }
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    $exitCode = 2
} finally {
    if ($gate) { $gate.Dispose() }
}
exit $exitCode
