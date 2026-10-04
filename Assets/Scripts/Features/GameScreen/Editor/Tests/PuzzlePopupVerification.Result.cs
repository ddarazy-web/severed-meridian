using System;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using PopupUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static partial class PuzzlePopupVerification
    {
        private static async UniTask ResultChecks(PuzzlePopupBinding binding, PuzzleGameSession session)
        {
            LevelDefinition fixture = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", fixture);
                using (SerializedObject edit = new SerializedObject(fixture))
                {
                    SerializedProperty blocks = edit.FindProperty("initialBlocks"); blocks.arraySize = 81;
                    for (int i = 0; i < 81; i++)
                    {
                        SerializedProperty block = blocks.GetArrayElementAtIndex(i);
                        LevelFlowEditing.SetCoordinate(block.FindPropertyRelative("coordinate"), new BoardCoordinate(i / 9, i % 9));
                        block.FindPropertyRelative("kind").intValue = (int)(i == 40 ? InitialBlockKind.Rocket : InitialBlockKind.FixedNormal);
                        block.FindPropertyRelative("fixedColor").intValue = (i / 9 * 2 + i % 9) % 5;
                        block.FindPropertyRelative("rocketDirection").intValue = (int)RocketDirection.Horizontal;
                    }
                    edit.ApplyModifiedPropertiesWithoutUndo();
                }
                Check(LevelSupplyEditing.AddTopSources(fixture) == null, "소유한 결과 시험 레벨 상단 공급");
                typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(session, LevelPackCodec.Encode(new[] { fixture }));
                await session.RestartAsync(CancellationToken.None);
                await UniTask.WaitUntil(() => session.CanAcceptInput).Timeout(TimeSpan.FromSeconds(30));
                Check(session.TryActivate(new BoardCoordinate(4, 4)), "실제 파워 행동으로 결과 생성");
                await UniTask.WaitUntil(() => session.ResultReady || session.HasFailed).Timeout(TimeSpan.FromSeconds(60));
                Check(session.ResultReady && !session.HasFailed, "실제 연출 수집 종료 결과 준비");
                binding.Refresh(); PopupHandle result = binding.Service.Top.Value;
                PuzzleResultView view = binding.Service.GetView(result) as PuzzleResultView;
                Check(view != null && binding.Service.Count == 1, "실제 게임 결과 공통 서비스 단일 표시");
                await UniTask.Yield();
                ExecuteEvents.Execute(view.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                Check(binding.Service.Top == result && binding.Service.Count == 1, "실제 결과 Cancel 닫기0");
                PopupHandle pause = binding.Service.Open(PuzzlePopupBinding.PauseId, new PuzzlePauseState());
                typeof(PuzzlePopupBinding).GetMethod("BindPause", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(binding, new object[] { binding.Service.GetView(pause) });
                for (int i = 0; i < 10; i++) binding.Refresh();
                Check(binding.Service.Top == pause && binding.Service.Count == 2 && binding.Service.GetView(result) == view,
                    "pause 아래 결과 반복 Refresh 인스턴스 순서 보존");
                binding.Service.Close(pause);
                object state = session.State; string logical = session.LogicalSessionId;
                Button retry = (Button)typeof(PuzzleResultView).GetField("retry", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                // 캐시된 후보가 즉시 완료돼도 백그라운드에서는 확정하지 않아 실제 준비 상태를 관찰한다.
                session.SendMessage("OnApplicationPause", true);
                using (CancellationTokenSource cancel = new CancellationTokenSource())
                {
                    UniTask restart = session.RestartAsync(cancel.Token);
                    Check(binding.Service.GetView(result) == view && binding.Service.Top == result && !retry.interactable,
                        "결과 Retry 준비 중 기존 결과 인스턴스 유지 버튼 잠금: count=" + binding.Service.Count +
                        " same=" + (binding.Service.GetView(result) == view) + " top=" + (binding.Service.Top == result) +
                        " retry=" + retry.interactable + " restarting=" + session.IsRestarting + " message=" + session.Message);
                    cancel.Cancel(); await restart;
                }
                session.SendMessage("OnApplicationPause", false);
                Check(session.ResultReady && ReferenceEquals(session.State, state) && session.LogicalSessionId == logical &&
                    binding.Service.GetView(result) == view && retry.interactable,
                    "결과 Retry 취소 기존 결과 문맥 버튼 복귀");
            }
            finally { session.SendMessage("OnApplicationPause", false); UnityEngine.Object.Destroy(fixture); }
        }
    }
}
