$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture = Join-Path $project ('Temp/LogCleanupTest-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Tools/Build') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'Tools/Build/Clean-Logs.ps1') -Destination (Join-Path $fixture 'Tools/Build/Clean-Logs.ps1')
$cleaner = Join-Path $fixture 'Tools/Build/Clean-Logs.ps1'
function Put([string]$relative, [string]$value = 'fixture') {
    $path = Join-Path $fixture $relative
    New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    [IO.File]::WriteAllText($path, $value)
}
function Check([bool]$condition, [string]$label) { if (-not $condition) { throw $label }; Write-Host "PASS $label" }
Put 'Logs/old.cs'
Put 'Logs/old-tree/output.bin'
Put 'Logs/recent-tree/old.log'
Put 'Logs/ExternalBuild/completed/result.json' '{"exitCode":0}'
Put 'Logs/ExternalBuild/completed/Unity.log'
Put 'Logs/ExternalBuild/unfinished/request.json'
Put 'Logs/locked.log'
Get-ChildItem (Join-Path $fixture 'Logs') -Recurse -Force | ForEach-Object { $_.LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-10) }
Put 'Logs/recent.log'
Put 'Logs/recent-tree/new.log'
$preview = & $cleaner -Preview
Check (Test-Path (Join-Path $fixture 'Logs/old.cs')) 'preview preserves old script'
Check ($preview.Candidates -contains 'old.cs') 'preview reports old script'
$held = [IO.File]::Open((Join-Path $fixture 'Logs/locked.log'), 'Open', 'Read', 'None')
try { $result = & $cleaner } finally { $held.Dispose() }
Check (-not (Test-Path (Join-Path $fixture 'Logs/old.cs'))) 'old script deleted'
Check (-not (Test-Path (Join-Path $fixture 'Logs/old-tree'))) 'old output tree deleted'
Check (-not (Test-Path (Join-Path $fixture 'Logs/ExternalBuild/completed'))) 'completed old build deleted'
foreach ($name in @('recent.log','recent-tree/old.log','ExternalBuild/unfinished/request.json','locked.log')) {
    Check (Test-Path (Join-Path $fixture ('Logs/' + $name))) "preserved $name"
}
$gate = [IO.File]::Open((Join-Path $fixture 'Logs/ExternalBuild/operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try { $result = & $cleaner; Check ($result.Status -eq 'Busy') 'active external build blocks cleanup' } finally { $gate.Dispose() }
New-Item -ItemType Directory -Path (Join-Path $fixture 'Temp') -Force | Out-Null
$gate = [IO.File]::Open((Join-Path $fixture 'Temp/UnityLockfile'), 'OpenOrCreate', 'ReadWrite', 'None')
try { $result = & $cleaner; Check ($result.Status -eq 'Busy') 'open Unity blocks cleanup' } finally { $gate.Dispose() }
Put 'Library/MatchBuildTool/current.json' '{"phase":4}'
$result = & $cleaner
Check ($result.Status -eq 'Busy') 'unfinished build journal blocks cleanup'
Put 'Library/MatchBuildTool/current.json' '{"phase":6}'
Put 'outside/keep.txt'
$link = Join-Path $fixture 'Logs/linked'
New-Item -ItemType Junction -Path $link -Target (Join-Path $fixture 'outside') | Out-Null
(Get-Item -LiteralPath $link).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-10)
$result = & $cleaner
Check (Test-Path (Join-Path $fixture 'outside/keep.txt')) 'junction target outside Logs preserved'
Check (Test-Path -LiteralPath $link) 'junction not traversed or deleted'
Put 'Logs/custom-retention.log'
(Get-Item (Join-Path $fixture 'Logs/custom-retention.log')).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-3)
$result = & $cleaner -RetentionDays 2
Check (-not (Test-Path (Join-Path $fixture 'Logs/custom-retention.log'))) 'custom retention respected'
Put 'Library/MatchBuildTool/current.json' 'invalid-json'
$result = & $cleaner
Check ($result.Status -eq 'Error') 'unreadable journal prevents deletion'
Write-Host "Fixture: $fixture"
