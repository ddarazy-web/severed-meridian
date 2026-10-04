using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using PopupUI;

namespace GameScreen.Editor
{
    public static partial class PuzzlePopupVerification
    {
        private static async UniTask RestartChecks(PuzzlePopupBinding binding, PuzzleGameSession session)
        {
            object state = session.State;
            string logical = session.LogicalSessionId;
            FieldInfo artworkField = typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo bytesField = typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.Instance | BindingFlags.NonPublic);
            object artwork = artworkField.GetValue(session);
            byte[] bytes = (byte[])bytesField.GetValue(session);
            PopupHandle pause = binding.OpenPause();
            using (CancellationTokenSource cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel(); await session.RestartAsync(cancelled.Token);
            }
            Check(session.IsReady && !session.HasFailed && ReferenceEquals(session.State, state) &&
                ReferenceEquals(artworkField.GetValue(session), artwork) && session.LogicalSessionId == logical,
                "재시작 사전 취소 기존 플레이 리소스 문맥 보존");
            Check(binding.Service.Top == pause && session.IsPaused && !session.IsRestarting,
                "재시작 취소 기존 팝업 정지 잠금 복구");
            try
            {
                bytesField.SetValue(session, new byte[] { 0 });
                await session.RestartAsync(CancellationToken.None);
                Check(session.IsReady && !session.HasFailed && ReferenceEquals(session.State, state) &&
                    ReferenceEquals(artworkField.GetValue(session), artwork) && session.LogicalSessionId == logical,
                    "재시작 데이터 실패 기존 플레이 리소스 문맥 보존");
                Check(binding.Service.Top == pause && session.IsPaused && session.Message.StartsWith("다시 시작 실패"),
                    "재시작 실패 기존 팝업 유지 실패 안내");
            }
            finally { bytesField.SetValue(session, bytes); }
            binding.Service.Close(pause);
            session.SetPaused(true);
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                UniTask restart = session.RestartAsync(cancel.Token);
                await UniTask.Yield(); cancel.Cancel(); await restart;
            }
            Check(session.IsReady && ReferenceEquals(session.State, state) && session.LogicalSessionId == logical && session.IsPaused,
                "재시작 준비 중 취소 외부 정지 기존 문맥 보존");
            using (CancellationTokenSource release = new CancellationTokenSource())
            {
                UniTask waiting = session.RestartAsync(release.Token);
                Check(session.IsRestarting && session.IsPaused, "외부 정지 중 Retry 후보 확정 대기");
                bool resumed = session.SetPaused(false);
                if (!resumed) release.Cancel();
                await waiting.Timeout(TimeSpan.FromSeconds(30));
                Check(resumed && !session.IsPaused && session.IsReady && session.LogicalSessionId != logical,
                    "재시작 준비 중 외부 소유자 정지 해제 후보 확정 가능");
            }
            pause = binding.OpenPause();
            await session.RestartAsync(CancellationToken.None).Timeout(TimeSpan.FromSeconds(60));
            Check(session.IsReady && !session.HasFailed && !ReferenceEquals(session.State, state) &&
                session.LogicalSessionId != logical && !session.IsRestarting,
                "재시작 성공만 새 플레이 논리 문맥 확정");
            Check(binding.Service.Count == 0 && !session.IsPaused && !ReferenceEquals(artworkField.GetValue(session), artwork),
                "재시작 자신의 팝업 정지로 대기하지 않고 이전 팝업 폐기");
            await UniTask.WaitUntil(() => session.CanAcceptInput).Timeout(TimeSpan.FromSeconds(20));
            Check(session.CanAcceptInput, "재시작 이후 실제 보드 입력 가능");
        }
    }
}
