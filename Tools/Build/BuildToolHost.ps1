function Invoke-BuildToolHost {
    param([string]$ProjectPath, [object]$Request, [string]$UnityPath)
    $ErrorActionPreference = 'Stop'
    $gate = $null
    $runDirectory = $null
    try {
        $projectRoot = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
        $versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
        if (-not (Test-Path -LiteralPath $versionFile)) { throw 'InvalidProject: ProjectVersion.txt not found.' }
        if ($Request.version -notin @(1,2)) { throw 'InvalidRequest: unsupported version.' }
        $operation = [string]$Request.operation
        $allowed = if ($Request.version -eq 1) { @('list','check','build') } else { @('settings','save-settings','cleanup-preview','cleanup','check','build','signing-aliases','save-signing') }
        if ($operation -notin $allowed) { throw 'UnsupportedOperation: unknown operation.' }
        if ($Request.version -eq 2) {
            if ($Request.requestId -isnot [string] -or [string]::IsNullOrWhiteSpace($Request.requestId)) { throw 'InvalidRequest: requestId required.' }
            if ($Request.requestId -notmatch '^[a-zA-Z0-9_-]{1,80}$') { throw 'InvalidRequest: unsafe requestId.' }
            $fields = @('version','requestId','operation','profileGuid')
            if ($operation -eq 'save-settings') { $fields += @('expectedRevision','appVersion','options') }
            if ($operation -in @('check','build')) { $fields += 'expectedRevision' }
            if ($operation -in @('signing-aliases','save-signing')) { $fields += @('expectedRevision','keystorePath','alias','customKeystore','credentialsFile') }
            if ($operation -like 'cleanup*') { $fields = @('version','requestId','operation','retentionDays') }
            foreach ($property in $Request.PSObject.Properties.Name) { if ($property -notin $fields) { throw 'InvalidRequest: unsupported field.' } }
            if ($Request.PSObject.Properties.Name -contains 'appVersion') {
                if ($Request.appVersion -isnot [pscustomobject]) { throw 'InvalidRequest: appVersion must be object.' }
                foreach ($property in $Request.appVersion.PSObject.Properties) {
                    if ($property.Name -notin @('name','code') -or $property.Value -isnot [string]) { throw 'InvalidRequest: unsupported version field or type.' }
                }
            }
            if ($Request.PSObject.Properties.Name -contains 'options') {
                if ($Request.options -isnot [pscustomobject]) { throw 'InvalidRequest: options must be object.' }
                foreach ($property in $Request.options.PSObject.Properties) {
                    if ($property.Name -notmatch '^[a-zA-Z0-9_.-]+$' -or $property.Value -isnot [bool]) { throw 'InvalidRequest: options must contain boolean values.' }
                }
            }
        }
        if ($operation -like 'cleanup*') {
            $days = 7
            if ($Request.PSObject.Properties.Name -contains 'retentionDays') {
                if ($Request.retentionDays -isnot [int] -and $Request.retentionDays -isnot [long]) { throw 'InvalidRequest: retentionDays must be integer.' }
                $days = $Request.retentionDays
            }
            $cleanup = & (Join-Path $PSScriptRoot 'Clean-Logs.ps1') -ProjectPath $projectRoot -RetentionDays $days -Preview:($operation -eq 'cleanup-preview')
            $code = if ($cleanup.Status -eq 'Complete') { 0 } else { 2 }
            return [pscustomobject]@{ version=2; requestId=$Request.requestId; exitCode=$code; data=$cleanup; errorCode=$(if ($code) {$cleanup.Status} else {$null}); message=$(if ($code) {'Cleanup did not complete. Inspect data.Skipped/Errors.'} else {$null}) }
        }
        $version = (Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$').Matches.Groups[1].Value.Trim()
        if (-not $UnityPath) { $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe" }
        if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw 'UnityNotFound: Unity executable not found.' }
        $UnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
        if ($operation -eq 'build') {
            $cleanup = & (Join-Path $PSScriptRoot 'Clean-Logs.ps1') -ProjectPath $projectRoot
            Write-Host ("Log cleanup: {0}, removed {1} entries." -f $cleanup.Status, $cleanup.DeletedEntries)
        }
        $logsRoot = Join-Path $projectRoot 'Logs/ExternalBuild'
        New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
        try { $gate = [IO.File]::Open((Join-Path $logsRoot 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch { throw 'Busy: another external operation is running.' }
        $editorLock = Join-Path $projectRoot 'Temp/UnityLockfile'
        if (Test-Path -LiteralPath $editorLock) {
            try { $probe = [IO.File]::Open($editorLock, 'Open', 'ReadWrite', 'None'); $probe.Dispose() }
            catch { throw 'Busy: this project is open in Unity. Save and close it first.' }
        }
        $runDirectory = Join-Path $logsRoot ((Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
        New-Item -ItemType Directory -Path $runDirectory | Out-Null
        $requestPath = Join-Path $runDirectory 'request.json'
        $resultPath = Join-Path $runDirectory 'result.json'
        $logPath = Join-Path $runDirectory 'Unity.log'
        if ($Request.version -eq 2) {
            $bridgeDirectory = Join-Path $projectRoot ('Library/MatchBuildApp/' + $Request.requestId)
            New-Item -ItemType Directory -Path $bridgeDirectory -Force | Out-Null
            [IO.File]::WriteAllText((Join-Path $bridgeDirectory 'progress.json'), (@{requestId=$Request.requestId;status='Unity 준비 중 · 컴파일과 초기화를 기다립니다.';logPath=$logPath} | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
        }
        [IO.File]::WriteAllText($requestPath, ($Request | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
        Write-Host "Log: $logPath"
        $arguments = @('-batchmode','-projectPath',$projectRoot,'-logFile',$logPath,'-executeMethod','Products.Editor.BuildToolBatch.Run','-matchBuildRequest',$requestPath)
        if ($operation -eq 'build') {
            $profileAssets = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Settings/BuildProfiles') -Filter '*.asset' | Where-Object {
                if ($Request.version -eq 1) { $_.BaseName -ceq $Request.profile }
                else { $Request.profileGuid -match '^[0-9a-fA-F]{32}$' -and (Select-String -LiteralPath ($_.FullName + '.meta') -Pattern ('^guid: ' + $Request.profileGuid + '$') -Quiet) }
            })
            if ($profileAssets.Count -ne 1) { throw 'UnknownProfile: build profile not found.' }
            $arguments += @('-activeBuildProfile', ('Assets/Settings/BuildProfiles/' + $profileAssets[0].Name))
        }
        $quotedArguments = ($arguments | ForEach-Object {
            if ($_ -match '["\r\n]') { throw 'InvalidRequest: quotes/newlines are not supported in Unity paths.' }
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
                    if ([DateTime]::Parse($current.startedUtc).ToUniversalTime() -ge $startedUtc -and $current.status -and $current.status -ne $lastStatus) { Write-Host $current.status; $lastStatus = $current.status }
                } catch { }
            }
        }
        $process.WaitForExit()
        if (-not (Test-Path -LiteralPath $resultPath)) { throw "MissingResult: Unity exited ($($process.ExitCode)) without result. Inspect $logPath" }
        $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($result.version -ne $Request.version -or ($Request.version -eq 2 -and $result.requestId -ne $Request.requestId) -or $null -eq $result.exitCode) { throw 'InvalidResult: response does not match request.' }
        if ($process.ExitCode -ne 0 -and $result.exitCode -eq 0) { throw 'UnityFailed: process failed despite success response.' }
        Write-Host "Result: $resultPath"
        return $result
    } catch {
        $message = $_.Exception.Message
        $errorCode = ($message -split ':',2)[0]
        if ($errorCode -notin @('Busy','InvalidRequest','InvalidProject','UnityNotFound','UnknownProfile','UnsupportedOperation','MissingResult','InvalidResult','UnityFailed')) { $errorCode = 'HostError' }
        return [pscustomobject]@{ version=$Request.version; requestId=$Request.requestId; exitCode=2; data=$null; errorCode=$errorCode; message=$message }
    } finally { if ($gate) { $gate.Dispose() } }
}
