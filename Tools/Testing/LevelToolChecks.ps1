param([ValidateSet('Portable','Unity','All')][string]$Action = 'All')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repo
try {
    if ($Action -ne 'Portable') {
        foreach ($method in @(
            'LevelAuthoring.Editor.LevelToolSceneVerification.Run',
            'LevelAuthoring.Editor.LevelToolLaunchVerification.Run',
            'LevelAuthoring.Editor.LevelToolLaunchVerification.RunDefault',
            'LevelAuthoring.Editor.LevelToolLaunchVerification.RunIncoming',
            'LevelAuthoring.Editor.LevelToolGatewayVerification.Run',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.RunHandoff',
            'LevelAuthoring.Editor.LevelToolPlayVerification.Run',
            'LevelAuthoring.Editor.TutorialAuthoringRuntimeVerification.Run',
            'LevelAuthoring.Editor.TutorialDraftEditingVerification.Run',
            'LevelAuthoring.Editor.SharedTutorialDraftVerification.Run',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.Run',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.RunTutorial',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.RunSharedTutorial',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.RunTutorialSamples',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.RunBot',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.RunMulti',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.RunVisual',
            'LevelAuthoring.Editor.LevelToolAdvancedVerification.RunRecords',
            'LevelAuthoring.Editor.BalanceJsonVerification.Run',
            'Levels.Editor.BotBatchVerification.Core',
            'Levels.Editor.BotAnalysisVerification.Core',
            'Levels.Editor.MultiLevelVerification.Start',
            'Levels.Editor.BotMoveBalanceVerification.RangesOnly',
            'Levels.Editor.TestRecordManagementVerification.Run',
            'LevelAuthoring.Editor.LevelToolReloadVerification.Run',
            'LevelAuthoring.Editor.LevelToolReloadVerification.RunShared',
            'LevelAuthoring.Editor.LevelToolTrialLifecycleVerification.Run',
            'Tutorial.Editor.TutorialComposerConditionsVerification.Run',
            'Levels.Editor.PlacementReplacementVerification.Run')) {
            & (Join-Path $PSScriptRoot 'ProjectTests.ps1') -Action Run -Method $method
        }
    }
    if ($Action -ne 'Unity') { & (Join-Path $PSScriptRoot 'JsonAuthoringChecks.ps1') -Action Portable }
} finally { Pop-Location }
