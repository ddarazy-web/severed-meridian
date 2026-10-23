#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;

namespace LevelTool.Presentation
{
    // 이미 화면에 준비된 레벨만 읽는다. 체크리스트를 위해 다른 레벨을 열지 않는다.
    public sealed class LevelToolCreationChecklist
    {
        public bool BoardConfigured { get; }
        public bool MissionsConfigured { get; }
        public bool MovesConfigured { get; }
        public bool PlayChecked { get; }
        public bool Saved { get; }
        public string MissionError { get; }
        public string NextAction => !BoardConfigured ? "보드 구성" : !MovesConfigured || !MissionsConfigured ? "이동 횟수·미션 설정" :
            !PlayChecked ? "플레이 확인" : !Saved ? "파일 저장" : "제작 항목 확인됨";

        public LevelToolCreationChecklist(JObject level, bool played, bool dirty)
        {
            BoardConfigured = level["board"]?["cells"]?.Any(cell => (bool?)cell["isActive"] == true) == true;
            MovesConfigured = (int?)level["moveCount"] > 0;
            PlayChecked = played; Saved = !dirty;
            JArray missions = level["missions"] as JArray;
            if (missions == null || missions.Count == 0 || missions.Count > 4)
            { MissionError = "미션을 1~4개 설정하세요."; return; }
            JObject candidate = new JObject { ["missions"] = new JArray(), ["colors"] = level["colors"]?.DeepClone() ?? new JArray() };
            try
            {
                foreach (JToken mission in missions)
                    MissionDocumentEditing.Set(candidate, ((JArray)candidate["missions"]).Count, (string)mission["kind"], (string)mission["color"], (int)mission["count"]);
                MissionsConfigured = missions.All(mission => (string)mission["kind"] != "Unselected");
                if (!MissionsConfigured) MissionError = "미션 대상을 선택하세요.";
            }
            catch (Exception error) { MissionError = error.Message; }
        }
    }
}
#endif
