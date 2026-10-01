using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleResultView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text title, detail;
        [SerializeField] private UnityEngine.UI.Button retry;
        public void Configure(UnityEngine.UI.Text heading, UnityEngine.UI.Text body, UnityEngine.UI.Button button)
        { title = heading; detail = body; retry = button; }
        public void Bind(System.Action action)
        { retry.onClick.RemoveAllListeners(); retry.onClick.AddListener(() => action()); }
        public void Show(string heading, string body)
        { title.text = heading; detail.text = body; gameObject.SetActive(true); }
    }
}
