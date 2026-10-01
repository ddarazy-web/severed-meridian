using System.Collections.Generic;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleHudView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text moves;
        [SerializeField] private PuzzleMissionView missionPrefab;
        [SerializeField] private Transform missionRoot;
        private readonly List<PuzzleMissionView> views = new List<PuzzleMissionView>();
        public System.Action<string> Describe;
        public void Configure(UnityEngine.UI.Text label, PuzzleMissionView prefab, Transform parent)
        { moves = label; missionPrefab = prefab; missionRoot = parent; }
        public void Refresh(PuzzleGameSession session)
        {
            if (session.IsPresenting) return;
            moves.text = session.State == null ? "—" : session.State.MovesRemaining.ToString();
            int count = session.IsReady && !session.IsRestarting ? session.State.Missions.Count : 0;
            while (views.Count < count) views.Add(Instantiate(missionPrefab, missionRoot));
            for (int i = 0; i < views.Count; i++)
            {
                views[i].gameObject.SetActive(i < count);
                if (i < count) views[i].Refresh(session.State.Missions[i], session.MissionSprite(i), Describe);
            }
        }
    }
}
