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
        public event Action<string, int, string> LevelSelected;

        private void Awake() => retry.onClick.AddListener(Reload);
        private void OnEnable() => Reload();
        private void OnDisable()
        {
            loading?.Cancel();
            loading?.Dispose();
            loading = null;
        }
        private void OnDestroy() => retry.onClick.RemoveListener(Reload);

        private void Reload()
        {
            loading?.Cancel();
            loading?.Dispose();
            loading = new CancellationTokenSource();
            Load(loading.Token).Forget();
        }

        private async UniTaskVoid Load(CancellationToken token)
        {
            snapshot = null;
            list.gameObject.SetActive(false);
            retry.gameObject.SetActive(false);
            status.text = "레벨 목록을 불러오는 중…";
            try
            {
                LobbyIndexSnapshot result = await LobbyIndexLoader.LoadAsync(token);
                token.ThrowIfCancellationRequested();
                snapshot = result;
                status.text = result.Levels.Count == 0 ? "등록된 레벨이 없습니다." : "플레이할 레벨을 선택하세요.";
                list.gameObject.SetActive(result.Levels.Count > 0);
                list.SetItems(result.Levels, Select);
            }
            catch (OperationCanceledException) { }
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
            if (snapshot == null) return;
            if (LevelSelected == null) status.text = $"레벨 {level.Number} 선택 · 게임 연결은 다음 단계입니다.";
            LevelSelected?.Invoke(level.Id, level.Number, snapshot.Generation);
        }
    }
}
