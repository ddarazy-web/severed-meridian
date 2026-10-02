using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleResultView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text title, detail;
        [SerializeField] private UnityEngine.UI.Button retry;
        [SerializeField] private UnityEngine.UI.Button nextLevel;
        public void Configure(UnityEngine.UI.Text heading, UnityEngine.UI.Text body, UnityEngine.UI.Button button)
        { title = heading; detail = body; retry = button; }
        public void Bind(System.Action action)
            => Bind(action, null);
        public void ConfigureNextButton(UnityEngine.UI.Button button) => nextLevel = button;
        public void Bind(System.Action retryAction, System.Action nextAction)
        {
            retry.onClick.RemoveAllListeners(); retry.onClick.AddListener(() => retryAction());
            if (nextLevel == null) return;
            nextLevel.onClick.RemoveAllListeners();
            if (nextAction != null) nextLevel.onClick.AddListener(() => nextAction());
        }
        public void SetTransition(bool visible, bool canAdvance, bool changing, string body)
        {
            detail.text = body;
            retry.interactable = !changing;
            if (nextLevel == null) return;
            nextLevel.gameObject.SetActive(visible);
            nextLevel.interactable = canAdvance && !changing;
        }
        public void Show(string heading, string body)
        { title.text = heading; detail.text = body; gameObject.SetActive(true); }
    }
}
