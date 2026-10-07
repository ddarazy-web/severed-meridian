using System.Collections.Generic;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleHudView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text moves;
        [SerializeField] private PuzzleMissionView missionPrefab;
        [SerializeField] private Transform missionRoot;
        private readonly List<PuzzleMissionView> views = new List<PuzzleMissionView>();
        private readonly PuzzleHudState compatibilityState = new PuzzleHudState();
        public System.Action<string> Describe;
        public void Configure(UnityEngine.UI.Text label, PuzzleMissionView prefab, Transform parent)
        { moves = label; missionPrefab = prefab; missionRoot = parent; }
        public void Refresh(PuzzleGameSession session)
            => Refresh(PuzzleHudPresenter.Capture(session, compatibilityState));
        public void Refresh(PuzzleHudState state)
        {
            moves.text = state.MovesText;
            int count = state.Missions.Count;
            while (views.Count < count) views.Add(Instantiate(missionPrefab, missionRoot));
            for (int i = 0; i < views.Count; i++)
            {
                views[i].gameObject.SetActive(i < count);
                if (i < count) views[i].Refresh(state.Missions[i], Describe);
                else views[i].Clear();
            }
        }
    }
}
