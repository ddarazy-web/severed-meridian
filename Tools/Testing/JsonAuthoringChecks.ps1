param([ValidateSet('Portable','Unity','All')][string]$Action = 'Portable')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repo
try {
    if ($Action -ne 'Portable') {
        foreach ($method in @(
            'LevelAuthoring.Editor.JsonAuthoringVerification.Run',
            'LevelAuthoring.Editor.JsonExportVerification.Run',
            'LevelAuthoring.Editor.JsonSharedFixtureVerification.Run',
            'LevelAuthoring.Editor.JsonRequestVerification.Run',
            'LevelAuthoring.Editor.JsonPlayVerification.Run')) {
            & (Join-Path $PSScriptRoot 'ProjectTests.ps1') -Action Run -Method $method
        }
    }
    if ($Action -ne 'Unity') {
        $jsonLibrary = Get-ChildItem (Join-Path $repo 'Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll') | Select-Object -First 1
        if (!$jsonLibrary) { throw 'Unity에서 공식 Newtonsoft JSON 패키지를 먼저 복원하세요.' }
        if (!(Test-Path 'Logs/GameAuthoringStage02/latest-fixtures.txt')) { throw '먼저 -Action Unity로 대표 시험 문서를 생성하세요.' }
        $linkRoot = Join-Path $repo ('ContentData/Trials/game-authoring-stage-02/link-check-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $linkRoot 'outside'),(Join-Path $linkRoot 'inside') -Force | Out-Null
        New-Item -ItemType Junction -Path (Join-Path $linkRoot 'inside/link') -Target (Join-Path $linkRoot 'outside') | Out-Null
        [IO.File]::WriteAllText((Join-Path $repo 'Logs/GameAuthoringStage02/link-root.txt'), (Join-Path $linkRoot 'inside'))
        & dotnet run --project (Join-Path $repo 'Tests/Portable/LevelAuthoring/LevelAuthoringChecks.csproj') "-p:JsonLibrary=$($jsonLibrary.FullName)"
        if ($LASTEXITCODE -ne 0) { throw "독립 JSON 검사 실패: $LASTEXITCODE" }
    }
} finally { Pop-Location }
