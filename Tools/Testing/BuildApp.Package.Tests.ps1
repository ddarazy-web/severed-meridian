[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference='Stop'
$PackagePath=(Resolve-Path -LiteralPath $PackagePath).Path
$manifest=Get-Content -LiteralPath (Join-Path $PackagePath 'package-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest.files){if((Get-FileHash -LiteralPath (Join-Path $PackagePath $file.name)).Hash -ne $file.sha256){throw "Package hash mismatch: $($file.name)"}}
Write-Output 'PASS package manifest hashes'
if(Get-Process -Name 'MoonRabbit.BuildApp' -ErrorAction SilentlyContinue){throw 'Close the existing build app before package smoke verification.'}
$state=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MoonRabbit/BuildApp/workspace.json'
$existed=Test-Path -LiteralPath $state
$original=if($existed){[IO.File]::ReadAllBytes($state)}else{$null}
$app=$null
try{
    $app=Start-Process -FilePath (Join-Path $PackagePath 'MoonRabbit.BuildApp.exe') -WorkingDirectory $PackagePath -WindowStyle Hidden -PassThru
    if(!$app.WaitForInputIdle(20000)){throw 'Packaged app did not initialize its UI.'}
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    do{$app.Refresh();if($app.HasExited){throw 'Packaged app exited unexpectedly'};if($app.MainWindowHandle -ne 0){break};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $deadline)
    if($app.MainWindowHandle -eq 0){throw 'Packaged main window not found'}
    Write-Output ('PASS packaged EXE window: '+$app.MainWindowTitle)
    if(!$app.CloseMainWindow()){throw 'Cannot close packaged test window'}
    if(!$app.WaitForExit(20000)){throw 'Packaged app is still busy; do not terminate it automatically'}
    if($app.ExitCode -ne 0){throw "Packaged app exit code $($app.ExitCode)"}
    Write-Output 'PASS packaged EXE normal close'
}finally{
    # 검사 앱이 정상 종료된 경우에만 이 검사 전 사용자 창 설정으로 되돌린다.
    if(!$app -or $app.HasExited){
        if($existed){[IO.File]::WriteAllBytes($state,$original)}elseif(Test-Path -LiteralPath $state){Remove-Item -LiteralPath $state}
    }
}
