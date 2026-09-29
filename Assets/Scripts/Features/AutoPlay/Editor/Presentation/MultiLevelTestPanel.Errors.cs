using System;
using System.IO;
using System.Linq;
using System.Text;
using AutoPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    internal sealed partial class MultiLevelTestPanel
    {
        private Foldout errorSection;
        private TextField errorReport;
        private Button copyError, revealError;
        private string errorFilePath;
        private Label copyNotice;
        private BotAnalysisReader errorTrialReader;
        private string errorReadFailure;

        /// <summary>통계 문구가 갱신되어도 사라지지 않는 전달용 오류 영역을 만든다.</summary>
        /// <param name="details">결과 카드의 스크롤 영역. 긴 로그도 창 밖으로 밀리지 않는다.</param>
        private void CreateErrorDetails(ScrollView details)
        {
            errorSection = new Foldout { text = "오류 상세 · 전달용 로그", value = true, name = "multi-error-section" };
            errorSection.style.marginBottom = 10;
            errorSection.style.display = DisplayStyle.None;
            VisualElement tools = new VisualElement(); tools.style.flexDirection = FlexDirection.Row; tools.style.flexWrap = Wrap.Wrap;
            copyError = new Button(() => {
                EditorGUIUtility.systemCopyBuffer = errorReport.value;
                copyNotice.text = "복사했습니다. 대화창에 Ctrl+V로 붙여넣으세요.";
            }) { text = "오류 내용 복사", name = "multi-copy-error", tooltip = "오류 원문, 시험 ID, 시드와 파일 위치를 함께 복사합니다." };
            copyError.SetEnabled(false); tools.Add(copyError);
            revealError = new Button(() => {
                // 조회 후 파일이 삭제될 수도 있으므로 클릭 시에도 실제 파일의 존재를 확인한다.
                if (File.Exists(errorFilePath)) EditorUtility.RevealInFinder(errorFilePath);
                else copyNotice.text = "파일이 없어졌습니다. ‘결과 다시 읽기’를 눌러 확인하세요.";
            }) { text = "오류 파일 선택", name = "multi-reveal-error" };
            revealError.SetEnabled(false); tools.Add(revealError);
            copyNotice = new Label(); copyNotice.style.whiteSpace = WhiteSpace.Normal;
            errorReport = new TextField { name = "multi-error-report", multiline = true, isReadOnly = true };
            errorReport.style.whiteSpace = WhiteSpace.Normal;
            errorReport.style.minHeight = 100;
            errorSection.Add(tools); errorSection.Add(copyNotice); errorSection.Add(errorReport);
            details.Insert(0, errorSection);
        }

        /// <summary>다른 레벨의 로그를 잘못 전달하지 않도록 선택 변경 시 조회 상태까지 비운다.</summary>
        private void ResetErrorDetails()
        {
            errorTrialReader = null; errorReadFailure = null;
            errorFilePath = null; revealError.SetEnabled(false); revealError.tooltip = "";
            errorReport.SetValueWithoutNotify(""); copyNotice.text = ""; copyError.SetEnabled(false);
            errorSection.style.display = DisplayStyle.None;
        }

        /// <summary>선택한 결과의 원문을 보존하고, 읽기 검증이 끝난 판만 재현 정보로 덧붙인다.</summary>
        private void RefreshErrorDetails()
        {
            if (record == null || results.selectedIndex < 0) return;
            MultiLevelTestEntry entry = record.entries[results.selectedIndex];
            BotAnalysisReader reader = repeatReader ?? errorTrialReader;
            string readFailure = errorReadFailure ?? reader?.Error ?? balanceReader?.Error;
            bool hasError = entry.status == MultiLevelTestStatus.Error || readFailure != null ||
                reader?.Record?.status == BotBatchStatus.Error || balanceReader?.Record?.status == BotBatchStatus.Error;
            if (!hasError) return;
            // 개별 오류 판이 확인되기 전에는 오류 원문을 보관한 시험 요약을 선택한다.
            errorFilePath = Path.GetFullPath(Path.Combine(store.Root, record.id, "queue.json"));
            StringBuilder text = new StringBuilder();
            text.AppendLine("[Match 시험 오류]").AppendLine("레벨: " + entry.name)
                .AppendLine("시험 방식: " + (record.mode == MultiLevelTestMode.Repeat ? "반복 시험" : "이동 횟수 추천"))
                .AppendLine("시험 ID: " + record.id).AppendLine("시작 시각(UTC): " + record.startedUtc)
                .AppendLine("레벨 에셋 GUID: " + entry.assetGuid).AppendLine("결과 ID: " + entry.resultId)
                .AppendLine("Unity: " + Application.unityVersion).AppendLine("오류 원문: " + entry.message)
                .AppendLine("시험 기록: " + Path.GetFullPath(Path.Combine(store.Root, record.id, "queue.json")));
            if (!string.IsNullOrEmpty(selectedFolder)) text.AppendLine("결과 폴더: " + Path.GetFullPath(selectedFolder));
            if (balanceReader?.Record != null) text.AppendLine("추천 시험 원문: " + balanceReader.Record.message);
            if (readFailure != null) text.AppendLine("기록 조회 오류: " + readFailure);
            if (reader != null)
            {
                text.AppendLine("판 묶음 원문: " + reader.Record?.message);
                if (!reader.IsDone) text.AppendLine("판별 기록 확인 중 · 완료 후 시드가 추가됩니다.");
                else if (reader.Error == null)
                {
                    text.AppendLine("실행 버전: " + reader.Record.engineVersion)
                        .AppendLine("봇 버전: 기본 " + reader.Record.basicVersion + " / 계획 " + reader.Record.planningVersion);
                    foreach (BotGameSummary game in reader.Games.Where(g => g.Outcome == BotSessionStatus.Error))
                    {
                        text.AppendLine().AppendLine($"발생 판: {game.Ordinal + 1}번째 · {(game.Strategy == BotStrategyKind.Basic ? "기본" : "계획")} 전략")
                            .AppendLine("시드: " + game.Seed).AppendLine($"이동: 사용 {game.UsedMoves}회 / 남음 {game.RemainingMoves}회")
                            .AppendLine("판 오류: " + game.Message)
                            .AppendLine("원본 파일: " + Path.Combine(reader.DirectoryPath, game.Ordinal.ToString("D6") + ".json"));
                    }
                    BotGameSummary firstError = reader.Games.FirstOrDefault(g => g.Outcome == BotSessionStatus.Error);
                    if (firstError != null)
                    {
                        string gameFile = Path.Combine(reader.DirectoryPath, firstError.Ordinal.ToString("D6") + ".json");
                        if (File.Exists(gameFile)) errorFilePath = gameFile;
                    }
                }
            }
            // 과거 기록에 없던 예외 코드나 스택을 만들어 붙이지 않는다. 기록된 원문만 전달한다.
            bool newlyShown = errorSection.style.display.value == DisplayStyle.None;
            errorReport.SetValueWithoutNotify(text.ToString()); copyError.SetEnabled(true);
            revealError.SetEnabled(File.Exists(errorFilePath));
            revealError.tooltip = "탐색기에서 오류 파일을 선택합니다. 개별 판 기록이 없으면 시험 요약을 선택합니다.\n" + errorFilePath;
            errorSection.style.display = DisplayStyle.Flex;
            if (newlyShown) errorSection.value = true;
        }

        /// <summary>추천 시험에서 실패한 구간은 완료 목록에 없으므로 마지막 판 묶음을 별도로 읽는다.</summary>
        private void OpenErrorTrial()
        {
            if (record.mode != MultiLevelTestMode.Balance || record.entries[results.selectedIndex].status != MultiLevelTestStatus.Error) return;
            string trials = Path.Combine(selectedFolder, "trials");
            string pointer = Path.Combine(trials, "latest.txt");
            if (!File.Exists(pointer)) return;
            string id = File.ReadAllText(pointer).Trim();
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("오류 판 묶음 식별자가 올바르지 않습니다.");
            errorTrialReader = new BotAnalysisReader(Path.Combine(trials, id));
        }
    }
}
