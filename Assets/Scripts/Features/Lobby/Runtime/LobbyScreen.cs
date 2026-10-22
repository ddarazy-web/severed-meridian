using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Lobby
{
    public sealed class LobbyScreen : MonoBehaviour
    {
        [SerializeField] private LobbyLevelList list;
        [SerializeField] private Text status;
        [SerializeField] private Button retry;
        private CancellationTokenSource loading;
        private LobbyIndexSnapshot snapshot;
        private bool transitioning;
        private Text actionLabel;
        public LobbyIndexSnapshot Snapshot => snapshot;
        public double LastReadyMilliseconds { get; private set; }
        public double LastRestoreMilliseconds { get; private set; }
        public double LastRestoreReadDrainMilliseconds { get; private set; } = -1;
        private long restoreCancellationTimestamp;
        public float ScrollOffset => list.ScrollOffset;

        public void RestoreSelection(LobbyIndexSnapshot previous, float offset)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            LastRestoreReadDrainMilliseconds = -1;
            restoreCancellationTimestamp = loading == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            loading?.Cancel();
            loading?.Dispose();
            loading = null;
            snapshot = previous;
            list.gameObject.SetActive(true);
            list.SetItems(previous.Levels, Select);
            Canvas.ForceUpdateCanvases();
            list.RestoreScroll(offset);
            ShowSelection();
            LastRestoreMilliseconds = timer.Elapsed.TotalMilliseconds;
        }
        public event Action<string, int, string> LevelSelected;
        public event Action CancelRequested;

        private void Awake()
        {
            actionLabel = retry.GetComponentInChildren<Text>(true);
            retry.onClick.AddListener(InvokeAction);
        }
        private void OnEnable() => Reload();
        private void OnDisable()
        {
            loading?.Cancel();
            loading?.Dispose();
            loading = null;
        }
        private void OnDestroy() => retry.onClick.RemoveListener(InvokeAction);

        private void InvokeAction()
        {
            if (transitioning) CancelRequested?.Invoke();
            else Reload();
        }

        public void ShowTransition(string message, bool canCancel)
        {
            transitioning = true;
            list.gameObject.SetActive(false);
            status.text = message;
            actionLabel.text = "취소";
            retry.gameObject.SetActive(canCancel);
        }

        public void ShowSelection(string message = null, bool canRefresh = false)
        {
            transitioning = false;
            status.text = message ?? "플레이할 레벨을 선택하세요.";
            list.gameObject.SetActive(snapshot != null && snapshot.Levels.Count > 0);
            actionLabel.text = "다시 불러오기";
            retry.gameObject.SetActive(canRefresh);
        }

        private void Reload()
        {
            if (transitioning) return;
            loading?.Cancel();
            loading?.Dispose();
            loading = new CancellationTokenSource();
            Load(loading.Token).Forget();
        }

        private async UniTaskVoid Load(CancellationToken token)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            snapshot = null;
            list.gameObject.SetActive(false);
            retry.gameObject.SetActive(false);
            actionLabel.text = "다시 불러오기";
            status.text = "레벨 목록을 불러오는 중…";
            try
            {
                LobbyIndexSnapshot result = await LobbyIndexLoader.LoadAsync(token);
                token.ThrowIfCancellationRequested();
                snapshot = result;
                status.text = result.Levels.Count == 0 ? "등록된 레벨이 없습니다." : "플레이할 레벨을 선택하세요.";
                list.gameObject.SetActive(result.Levels.Count > 0);
                list.SetItems(result.Levels, Select);
                LastReadyMilliseconds = timer.Elapsed.TotalMilliseconds;
            }
            catch (OperationCanceledException)
            {
                if (restoreCancellationTimestamp != 0)
                    LastRestoreReadDrainMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - restoreCancellationTimestamp) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            }
            catch (Exception error)
            {
                if (token.IsCancellationRequested) return;
                Debug.LogException(error, this);
                status.text = "목록을 읽지 못했습니다.\n데이터 갱신 후 다시 시도해 주세요.";
                retry.gameObject.SetActive(true);
            }
        }

        private void Select(LobbyLevel level)
        {
            if (snapshot == null || transitioning) return;
            if (LevelSelected == null) status.text = $"레벨 {level.Number} 선택 · 게임 연결은 다음 단계입니다.";
            LevelSelected?.Invoke(level.Id, level.Number, snapshot.Generation);
        }
    }
}
