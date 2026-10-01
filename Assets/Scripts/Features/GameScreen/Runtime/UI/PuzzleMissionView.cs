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
        public void Configure(UnityEngine.UI.Image image, UnityEngine.UI.Text label, UnityEngine.UI.Button target)
        { icon = image; count = label; button = target; }
        public void Refresh(RuntimeMission mission, Sprite sprite, System.Action<string> describe)
        {
            icon.sprite = sprite; icon.enabled = sprite != null;
            count.text = mission.Progress + "/" + mission.Target;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => describe(Name(mission.Definition.Kind) + "\n" + mission.Progress + " / " + mission.Target + " 수집"));
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
