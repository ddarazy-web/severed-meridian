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
    [switch]$PreserveLogs,
    [switch]$Help
)
$ErrorActionPreference = 'Stop'
if ($Help) {
    Write-Output 'Build.bat -ListProfiles'
    Write-Output 'Build.bat -Profile "Android Game" -Format apk -DebugMode off [-Check]'
    Write-Output 'Build.bat -Profile "Windows Level Editor" -Option "debug-mode=off"'
    Write-Output 'Options: -UseDefaults, -PreserveLogs, -UnityPath "C:\...\Unity.exe". See Tools/Build/README.md.'
    exit 0
}
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
    $projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $operation = if ($ListProfiles) { 'list' } elseif ($Check) { 'check' } else { 'build' }
    $request = [pscustomobject]@{ version=1; operation=$operation; profile=$Profile; options=$choices; useSavedSettings=(-not $UseDefaults) }
    . (Join-Path $PSScriptRoot 'BuildToolHost.ps1')
    $result = Invoke-BuildToolHost -ProjectPath $projectRoot -Request $request -UnityPath $UnityPath -PreserveLogs:$PreserveLogs
    Write-Output ($result | ConvertTo-Json -Depth 30)
    $exitCode = [int]$result.exitCode
} catch { [Console]::Error.WriteLine($_.Exception.Message); $exitCode = 2 }
exit $exitCode
