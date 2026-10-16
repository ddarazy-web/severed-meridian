[CmdletBinding()]
param([string]$OutputPath)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputPath){$OutputPath=Join-Path $projectRoot ('Build/BuildApp/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
if(Test-Path -LiteralPath $OutputPath){throw 'Output already exists. Choose a new folder; existing files are not overwritten.'}
# Unity 제품 빌드 없이 WPF 앱만 로컬 폴더로 게시한다.
& dotnet publish (Join-Path $PSScriptRoot 'BuildApp.csproj') --configuration Release --no-restore --self-contained false --output $OutputPath
if($LASTEXITCODE -ne 0){throw "External app publish failed: $LASTEXITCODE"}
$required=@('MoonRabbit.BuildApp.exe','MoonRabbit.BuildApp.dll','MoonRabbit.BuildApp.runtimeconfig.json','README.md')
foreach($file in $required){if(!(Test-Path -LiteralPath (Join-Path $OutputPath $file))){throw "Missing published file: $file"}}
$manifest=@{createdUtc=[DateTime]::UtcNow.ToString('O');framework='net8.0-windows';selfContained=$false;files=@(Get-ChildItem -LiteralPath $OutputPath -File | ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})}
[IO.File]::WriteAllText((Join-Path $OutputPath 'package-manifest.json'),($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
Write-Output "Local app package: $OutputPath"
Write-Output 'Requires supported .NET 8 Windows Desktop Runtime. No Unity product build or public upload performed.'
