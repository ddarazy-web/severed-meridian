using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleMissionView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private UnityEngine.UI.Text count;
        [SerializeField] private UnityEngine.UI.Button button;
        public RectTransform Icon => icon.rectTransform;
        public void Configure(UnityEngine.UI.Image image, UnityEngine.UI.Text label, UnityEngine.UI.Button target)
        { icon = image; count = label; button = target; }
        public void Refresh(RuntimeMission mission, Sprite sprite, System.Action<string> describe, int? displayedProgress = null)
        {
            icon.sprite = sprite; icon.enabled = sprite != null;
            int progress = displayedProgress ?? mission.Progress;
            count.text = (progress >= mission.Target ? "✓ " : "") + progress + "/" + mission.Target;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => describe?.Invoke(Name(mission.Definition.Kind) + "\n" + progress + " / " + mission.Target + " 수집"));
        }
        public void Animate(float pulse)
        {
            icon.rectTransform.localScale = Vector3.one * (1 + .15f * Mathf.Sin(pulse * Mathf.PI));
            icon.color = Color.Lerp(Color.white, new Color32(255, 215, 100, 255), pulse);
        }
        public static string Name(MissionKind kind) => kind switch
        {
            MissionKind.Color => "달토끼", MissionKind.Crate => "나무 상자", MissionKind.Web => "거미줄",
            MissionKind.Scrap => "고철", MissionKind.Dust => "먼지", MissionKind.Safe => "고물회수캡슐",
            MissionKind.ColorLock => "색깔 자물쇠", MissionKind.Appliance => "금속 기둥", MissionKind.Mold => "곰팡이",
            MissionKind.Recovery => "회수 부품", _ => kind.ToString()
        };

    }
}
