using System.Text;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    // 4단계 목업 UI 전까지 사용하는 읽기 전용 플레이 확인 표시다.
    public sealed class PuzzlePlayDebugView : MonoBehaviour
    {
        [SerializeField] private PuzzleGameSession session;
        [SerializeField] private PuzzleBoardInput input;
        private string summary;
        private GUIStyle style;
        public void Configure(PuzzleGameSession gameSession, PuzzleBoardInput boardInput)
        {
            if (isActiveAndEnabled && session != null) session.Changed -= Refresh;
            session = gameSession; input = boardInput;
            if (isActiveAndEnabled && session != null) session.Changed += Refresh;
            Refresh();
        }
        private void OnEnable() { if (session != null) session.Changed += Refresh; Refresh(); }
        private void OnDisable() { if (session != null) session.Changed -= Refresh; }
        private void Refresh()
        {
            if (session == null) return;
            StringBuilder text = new StringBuilder(session.Message);
            if (session.State != null)
            {
                text.Append("\n남은 이동: ").Append(session.State.MovesRemaining);
                foreach (RuntimeMission mission in session.State.Missions)
                    text.Append("  |  ").Append(mission.Definition.Kind).Append(' ').Append(mission.Progress).Append('/').Append(mission.Target);
                if (session.Outcome != null)
                    text.Append("\n").Append(session.Outcome.Kind).Append(session.Phase == BoardActionPhase.Stopped ? " · 종료" : " · 라스트팡 진행 중");
            }
            summary = text.ToString();
        }
        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            GUI.Box(new Rect(8, 8, Mathf.Min(Screen.width - 16, 800), 112), GUIContent.none);
            string selected = input != null && input.Selected.HasValue ? "\n선택: " + input.Selected.Value : "";
            GUI.Label(new Rect(16, 12, Mathf.Min(Screen.width - 32, 784), 104), summary + selected, style);
        }
    }
}
