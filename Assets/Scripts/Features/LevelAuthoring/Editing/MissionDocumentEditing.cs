#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    public static class MissionDocumentEditing
    {
        private static readonly string[] Kinds = { "Color", "Crate", "Web", "Scrap", "Dust", "Safe", "ColorLock", "Appliance", "Mold", "Recovery", "Unselected" };
        public static void Set(JObject level, int index, string kind, string color, int count)
        {
            JArray list = DocumentBoardRules.List(level, "missions");
            if (index < 0 || index > list.Count || index == list.Count && list.Count >= 4) throw new ContentFormatException("미션은 최대 4개입니다. 유효한 위치를 선택하세요.");
            if (!Kinds.Contains(kind)) throw new ContentFormatException("지원하지 않는 미션입니다.");
            if (!new[] { "Type1", "Type2", "Type3", "Type4", "Type5" }.Contains(color)) throw new ContentFormatException("지원하지 않는 색입니다.");
            if (kind != "Mold" && count < 1) throw new ContentFormatException("목표 수량은 양수여야 합니다.");
            if (kind == "Color" && !DocumentBoardRules.List(level, "colors").Values<string>().Contains(color)) throw new ContentFormatException("레벨 사용 색을 선택하세요.");
            if (list.Where((item, i) => i != index).Any(item => (string)item["kind"] == kind && (kind != "Color" || (string)item["color"] == color))) throw new ContentFormatException("같은 대상의 미션이 이미 있습니다.");
            JObject value = new JObject { ["kind"] = kind, ["color"] = color, ["count"] = kind == "Mold" ? 0 : count };
            if (index == list.Count) list.Add(value); else list[index] = value;
        }
        public static void Remove(JObject level, int index)
        {
            JArray list = DocumentBoardRules.List(level, "missions");
            if (index < 0 || index >= list.Count) throw new ContentFormatException("미션을 선택하세요.");
            list.RemoveAt(index);
        }
    }
}
#endif
