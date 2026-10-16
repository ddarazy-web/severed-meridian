[CmdletBinding()]
param(
    [ValidateRange(1, 3650)][int]$RetentionDays = 7,
    [switch]$Preview
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$logsRoot = Join-Path $projectRoot 'Logs'
$prefix = $logsRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$gate = $null
$editorGate = $null
$result = [ordered]@{ Status = 'Complete'; RetentionDays = $RetentionDays; Preview = [bool]$Preview; DeletedEntries = 0; DeletedBytes = [long]0; Candidates = @(); Skipped = @(); Errors = @() }
try {
    if (-not (Test-Path -LiteralPath $logsRoot)) { [pscustomobject]$result; return }
    if ((Get-Item -LiteralPath $logsRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Logs must not be a link.' }
    $external = Join-Path $logsRoot 'ExternalBuild'
    if ((Test-Path -LiteralPath $external) -and ((Get-Item -LiteralPath $external).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'ExternalBuild must not be a link.' }
    New-Item -ItemType Directory -Path $external -Force | Out-Null
    try { $gate = [IO.File]::Open((Join-Path $external 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { $result.Status = 'Busy'; [pscustomobject]$result; return }
    $editorLock = Join-Path $projectRoot 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $editorLock) {
        try { $editorGate = [IO.File]::Open($editorLock, 'Open', 'ReadWrite', 'None') }
        catch { $result.Status = 'Busy'; [pscustomobject]$result; return }
    }
    $journal = Join-Path $projectRoot 'Library/MatchBuildTool/current.json'
    if (Test-Path -LiteralPath $journal) {
        $current = Get-Content -LiteralPath $journal -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -eq $current.phase -or $current.phase -notin @(6, 7, 8)) { $result.Status = 'Busy'; [pscustomobject]$result; return }
    }
    $cutoff = [DateTime]::UtcNow.AddDays(-$RetentionDays)
    $targets = @(Get-ChildItem -LiteralPath $logsRoot -Force | Where-Object { $_.FullName -ne $external }) +
        @(Get-ChildItem -LiteralPath $external -Force | Where-Object { $_.Name -ne 'operation.lock' })
    foreach ($target in $targets) {
        $path = [IO.Path]::GetFullPath($target.FullName)
        if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Outside Logs: $path" }
        $relative = $path.Substring($prefix.Length)
        if ($target.PSIsContainer -and $target.Parent.FullName -eq $external -and -not (Test-Path -LiteralPath (Join-Path $path 'result.json'))) {
            $result.Skipped += $relative; continue
        }
        $pending = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
        $pending.Push($target)
        $files = [Collections.Generic.List[string]]::new()
        $eligible = $true
        $bytes = [long]0
        while ($pending.Count -gt 0) {
            $item = $pending.Pop()
            if (-not $item.FullName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escaped Logs.' }
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $item.LastWriteTimeUtc -ge $cutoff -or $item.Extension -eq '.lock') { $eligible = $false; break }
            if ($item -is [IO.DirectoryInfo]) {
                foreach ($child in $item.EnumerateFileSystemInfos()) { $pending.Push($child) }
            } else { $files.Add($item.FullName); $bytes += $item.Length }
        }
        if (-not $eligible) { $result.Skipped += $relative; continue }
        try {
            foreach ($file in $files) {
                $probe = [IO.File]::Open($file, 'Open', 'Read', 'None'); $probe.Dispose()
            }
            $result.Candidates += $relative
            if (-not $Preview) {
                # 검사한 Logs 하위 경로만 삭제하며 링크가 포함된 트리는 제외한다.
                Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Stop
                $result.DeletedEntries++; $result.DeletedBytes += $bytes
            }
        } catch { $result.Errors += ($relative + ': ' + $_.Exception.Message) }
    }
} catch { $result.Status = 'Error'; $result.Errors += $_.Exception.Message }
finally {
    if ($editorGate) { $editorGate.Dispose() }
    if ($gate) { $gate.Dispose() }
}
if ($result.Errors.Count -gt 0 -and $result.Status -eq 'Complete') { $result.Status = 'Partial' }
[pscustomobject]$result
