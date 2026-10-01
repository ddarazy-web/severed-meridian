using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzlePauseView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button resume, retry;
        public void Configure(UnityEngine.UI.Button resumeButton, UnityEngine.UI.Button retryButton)
        { resume = resumeButton; retry = retryButton; }
        public void Bind(System.Action resumeAction, System.Action retryAction)
        {
            resume.onClick.RemoveAllListeners(); retry.onClick.RemoveAllListeners();
            resume.onClick.AddListener(() => resumeAction()); retry.onClick.AddListener(() => retryAction());
        }
    }
}
