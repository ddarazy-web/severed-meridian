using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>저장된 오류 사본만 읽는 UI 검사. 게임을 실행하지 않고 현재 사용자 창이나 기록을 변경하지 않는다.</summary>
    internal static class MultiLevelErrorReportVerification
    {
        private const string Evidence = "Logs/MultiLevelErrorReportVerification";

        // 배치 실행은 Unity의 -quit 옵션이 종료를 담당한다. 사용자 Editor를 종료하지 않는다.
        public static void RunBatch() => Run("batch");

        private sealed class ErrorVerificationWindow : EditorWindow { }

        [InitializeOnLoadMethod]
        private static void RunRequestedCheck()
        {
            if (!File.Exists(Evidence + "/request.txt")) return;
            string label = File.ReadAllText(Evidence + "/request.txt").Trim();
            File.Delete(Evidence + "/request.txt");
            EditorApplication.delayCall += () => Run(label);
        }

        private static void Run(string label)
        {
            List<string> checks = new List<string>();
            MultiLevelTestPanel panel = null;
            ErrorVerificationWindow window = null;
            string oldClipboard = EditorGUIUtility.systemCopyBuffer;
            try
            {
                panel = new MultiLevelTestPanel(() => false, _ => { }, new MultiLevelTestStore(Evidence + "/records"));
                panel.SetVisible(true);
                window = ScriptableObject.CreateInstance<ErrorVerificationWindow>();
                window.titleContent = new GUIContent("Match 오류 검사");
                window.ShowUtility(); window.rootVisualElement.Add(panel.Root);
                ListView results = panel.Root.Q<ListView>("multi-results");
                results.SetSelection(0);
                MethodInfo tick = typeof(MultiLevelTestPanel).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < 40; i++) tick.Invoke(panel, null);
                TextField report = panel.Root.Q<TextField>("multi-error-report");
                Check(report != null && report.isReadOnly, "오류 원문을 읽기 전용으로 표시", checks);
                Check(report?.value.Contains("자동 연쇄 400회") == true, "통계 조회 이후에도 원문 유지", checks);
                Check(report?.value.Contains("879610835") == true && report.value.Contains("9번째"), "실패 판 시드와 판 번호", checks);
                Check(report?.value.Contains("c5405c1ab0f34d3791ba1d8f872a3b7e") == true && report.value.Contains("000008.json"), "시험 ID와 원본 파일 경로", checks);
                Check(panel.Root.Q<Button>("multi-copy-error")?.enabledSelf == true, "전달용 복사 버튼 활성", checks);
                Button reveal = panel.Root.Q<Button>("multi-reveal-error");
                Check(reveal?.enabledSelf == true && reveal.tooltip.EndsWith("000008.json"), "오류 파일 선택이 실패한 판의 원본을 가리킴", checks);
                Button copy = panel.Root.Q<Button>("multi-copy-error");
                if (copy != null)
                    using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = copy; copy.SendEvent(evt); }
                Check(report != null && EditorGUIUtility.systemCopyBuffer == report.value, "복사 버튼으로 오류 전문을 클립보드에 전달", checks);
                // 실행 객체 없이 현재 기록 이름을 갱신해 위 목록과 아래 결과의 ID 일치를 검사한다.
                typeof(MultiLevelTestPanel).GetMethod("ShowCurrentHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                Check(panel.Root.Q<PopupField<string>>("multi-history").value.Contains("c5405c1a"), "상단 시험 이름과 현재 결과 일치", checks);
                results.ClearSelection();
                Check(panel.Root.Q<Button>("multi-copy-error")?.enabledSelf == false, "선택 해제 시 이전 오류 복사 방지", checks);
                Check(reveal?.enabledSelf == false && reveal.tooltip == "", "선택 해제 시 이전 오류 파일 선택 방지", checks);
                // 원본 JSON은 고치지 않고 메모리에 읽힌 항목만 바꿔 정상 결과와 조회 실패 전환을 검사한다.
                MultiLevelTestRecord record = (MultiLevelTestRecord)typeof(MultiLevelTestPanel).GetField("record", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                record.entries[0].status = MultiLevelTestStatus.Completed; record.entries[0].message = "정상 종료"; record.entries[0].resultId = null;
                results.SetSelection(0);
                Check(report?.value == "" && panel.Root.Q<Button>("multi-copy-error")?.enabledSelf == false, "정상 결과에 이전 오류를 표시하지 않음", checks);
                record.entries[0].resultId = "00000000000000000000000000000000";
                typeof(MultiLevelTestPanel).GetMethod("ReadSelected", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                Check(report?.value.Contains("기록 조회 오류:") == true, "원본 파일 조회 실패도 복사 가능한 오류로 표시", checks);
                Check(reveal?.enabledSelf == true && reveal.tooltip.EndsWith("queue.json"), "개별 판 기록이 없으면 시험 요약 파일 선택", checks);
                panel.ClearStoredResults();
                Check(panel.Root.Q<TextField>("multi-error-report")?.value == "", "기록 화면 초기화 시 이전 오류 제거", checks);
            }
            catch (Exception error) { checks.Add("FAIL " + error); }
            finally
            {
                panel?.Dispose();
                if (window != null) window.Close();
                EditorGUIUtility.systemCopyBuffer = oldClipboard;
                File.WriteAllLines(Evidence + "/" + label + "-results.txt", checks);
                Debug.Log("오류 로그 UI 검사\n" + string.Join("\n", checks));
            }
        }

        private static void Check(bool passed, string label, List<string> results) => results.Add((passed ? "PASS " : "FAIL ") + label);
    }
}
