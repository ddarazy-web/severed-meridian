using System.Collections.Generic;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleHudView
    {
        [SerializeField] private UnityEngine.UI.Image collectionPrefab;
        private readonly List<UnityEngine.UI.Image> collections = new List<UnityEngine.UI.Image>();
        public int CollectionPoolCount => collections.Count;
        public void ConfigureFeedback(UnityEngine.UI.Image prefab) => collectionPrefab = prefab;

        public void Frame(PuzzleGameSession session)
            => Frame(PuzzleHudPresenter.Capture(session, compatibilityState));
        public void Frame(PuzzleHudState state)
        {
            moves.rectTransform.localScale = Vector3.one * (1 + .12f * Mathf.Sin(state.MovesPulse * Mathf.PI));
            foreach (UnityEngine.UI.Image image in collections) { image.gameObject.SetActive(false); image.sprite = null; }
            for (int i = 0; i < views.Count; i++) views[i].Animate(i < state.Missions.Count ? state.Missions[i].Pulse : 0);
            if (!state.IsReady || state.BoardCamera == null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || collectionPrefab == null) return;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform surface = canvas.transform as RectTransform;
            for (int flightIndex = 0; flightIndex < state.Flights.Count; flightIndex++)
            {
                PuzzleHudFlightState flight = state.Flights[flightIndex];
                while (collections.Count <= flight.Slot)
                {
                    UnityEngine.UI.Image image = Instantiate(collectionPrefab, surface, false);
                    image.name = "MissionCollection" + collections.Count; collections.Add(image);
                }
                UnityEngine.UI.Image icon = collections[flight.Slot];
                Vector2 origin = state.BoardCamera.WorldToScreenPoint(flight.Origin);
                Vector2 target = RectTransformUtility.WorldToScreenPoint(uiCamera, views[flight.MissionIndex].Icon.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, origin, uiCamera, out Vector2 start);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, target, uiCamera, out Vector2 end);
                float t = Mathf.SmoothStep(0, 1, flight.Progress);
                icon.rectTransform.anchoredPosition = Vector2.Lerp(start, end, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * Mathf.Min(80, Vector2.Distance(start, end) * .12f);
                icon.sprite = state.Missions[flight.MissionIndex].Sprite;
                icon.gameObject.SetActive(icon.sprite != null); icon.rectTransform.SetAsLastSibling();
            }
        }

        private void OnDisable()
        {
            if (moves != null) moves.rectTransform.localScale = Vector3.one;
            foreach (UnityEngine.UI.Image image in collections) if (image != null) { image.gameObject.SetActive(false); image.sprite = null; }
            foreach (PuzzleMissionView view in views) if (view != null) view.Animate(0);
        }
        private void OnDestroy()
        { foreach (UnityEngine.UI.Image image in collections) if (image != null) Destroy(image.gameObject); }
    }
}
