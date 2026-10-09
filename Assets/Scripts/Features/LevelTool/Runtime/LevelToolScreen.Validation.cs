#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using LevelAuthoring.Validation;
using Levels;
using Newtonsoft.Json.Linq;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private readonly List<(string Document, LevelValidationIssue Issue)> validationIssues = new List<(string, LevelValidationIssue)>();
        private ContentDocument[] validatedDocuments;
        private string validatedLevel, validationMode;

        private ContentDocument[] ValidationDocuments() => sharedTutorialDraft == null ? Session.Documents :
            Session.Documents.Select(doc => doc.Id == sharedTutorialDraft.FlowId ? sharedTutorialDraft.Session.Get(doc.Id) : doc).ToArray();

        private bool ValidationIsCurrent()
        {
            if (validatedDocuments == null || Session == null || validatedLevel != Session.SelectedLevelId) return false;
            var current = ValidationDocuments().ToDictionary(doc => doc.Id);
            return current.Count == validatedDocuments.Length && validatedDocuments.All(doc =>
                current.TryGetValue(doc.Id, out var other) && other.Kind == doc.Kind && JToken.DeepEquals(doc.Data, other.Data));
        }

        private void ValidateLevel(bool replay)
        {
            if (busy || Session == null) return;
            validationIssues.Clear(); validatedLevel = Session.SelectedLevelId;
            validatedDocuments = ValidationDocuments();
            validationMode = replay ? "튜토리얼 논리 재생" : "구조·참조·레벨 검사";
            foreach (var document in validatedDocuments)
            {
                try { ContentDocumentValidator.Validate(document); }
                catch (Exception error) { AddValidationError(document.Id, error.Message); }
            }
            if (validationIssues.Count == 0)
            {
                try
                {
                    // 공유 사본도 실제 게임 시험과 동일한 원본 충돌·참조 검사에 통과해야 한다.
                    var snapshot = sharedTutorialDraft == null ? new ContentSnapshot(validatedDocuments) : sharedTutorialDraft.Preview(Session);
                    using var graph = new AuthoringObjectGraph(snapshot.Documents);
                    var level = (LevelDefinition)graph.Resolve(validatedLevel);
                    var issues = replay ? LevelTutorialReplayValidator.Validate(level) : LevelDefinitionValidator.Validate(level);
                    validationIssues.AddRange(issues.Select(issue => (validatedLevel, issue)));
                }
                catch (Exception error) { AddValidationError(validatedLevel, error.Message); }
            }
            DrawValidationResults();
            Show(validationMode + " · 오류 " + validationIssues.Count + "개. 정식 진행 기록과 편집 원본은 변경하지 않았습니다.");
            validationPanel.ScrollTo(root.Q("validation-results"));
        }

        private void AddValidationError(string document, string message) => validationIssues.Add((document,
            new LevelValidationIssue(LevelValidationCode.InvalidLevelNumber, message, "")));

        private void DrawValidationResults()
        {
            validationPanel.Clear();
            if (validatedDocuments == null) return;
            bool current = ValidationIsCurrent();
            var results = new Foldout { name = "validation-results", text = "검사 결과", value = true };
            validationPanel.Add(results);
            results.Add(new Label(validationMode + " · 오류 " + validationIssues.Count + (current ? "개" : "개 · 내용이 달라졌습니다. 다시 검사하세요."))
                { name = "validation-summary", style = { whiteSpace = WhiteSpace.Normal } });
            results.Add(new Label("논리 재생은 지정 행동과 생성 블록을 사본에서 실행합니다. 화면 연출·터치 느낌은 게임 시험에서 확인하세요.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            for (int i = 0; i < validationIssues.Count; i++)
            {
                var entry = validationIssues[i];
                var button = Button(results, "validation-issue-" + i, entry.Issue.Message, () => FocusValidationIssue(entry.Document, entry.Issue));
                button.tooltip = "문서: " + entry.Document + "\n" + entry.Issue.PropertyPath;
                button.style.whiteSpace = WhiteSpace.Normal;
                button.SetEnabled(current);
            }
        }

        private void FocusValidationIssue(string documentId, LevelValidationIssue issue)
        {
            if (!ValidationIsCurrent()) { Show("내용이 변경되었습니다. 먼저 다시 검사하세요."); return; }
            var document = Session.Get(documentId);
            if (document.Kind != "level") { Show("문서 " + documentId + " · " + issue.Message); return; }
            CancelPendingInput(); brush = null;
            if (Session.SelectedLevelId != documentId) Session.SelectLevel(documentId);
            string path = issue.PropertyPath ?? "";
            boardFocus = false;
            levelSettingsVisible = path == "moveCount" || path.StartsWith("colors") || path.StartsWith("missions") ||
                path == "displayName" || path == "levelNumber";
            inspectorPage = path.StartsWith("tutorial.supply") ? "튜토리얼 공급" : path.StartsWith("tutorial") ? "튜토리얼" :
                path.StartsWith("elementSupply") || path.StartsWith("supply") ? "공급" : path.StartsWith("flow") ? "흐름" :
                path.StartsWith("connections") ? "연결" : "기본";
            if (sharedTutorialDraft != null) inspectorPage = "튜토리얼";
            Match step = Regex.Match(path, @"steps\.Array\.data\[(\d+)\]");
            if (step.Success) tutorialStep = int.Parse(step.Groups[1].Value);
            if (issue.Coordinate.HasValue)
            {
                var cell = issue.Coordinate.Value;
                if (cell.Row >= 0 && cell.Row < 9 && cell.Column >= 0 && cell.Column < 9)
                    Session.SelectCells(new[] { cell.Row * 9 + cell.Column });
            }
            Refresh();
            properties.scrollOffset = UnityEngine.Vector2.zero;
            if (Session.SelectedCells.Length > 0) boardScroll.ScrollTo(board.Q("cell-" + Session.SelectedCells[0]));
            Show("문서 " + documentId + " · " + path + " · " + issue.Message);
        }
    }
}
#endif
