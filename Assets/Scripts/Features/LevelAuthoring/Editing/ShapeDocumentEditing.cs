#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    public static class ShapeDocumentEditing
    {
        public static void SetActive(JObject level, IEnumerable<int> selection, bool active)
        {
            int[] cells = selection.Distinct().ToArray();
            if (!(level["board"]?["cells"] is JArray board) || board.Count != 81 || cells.Any(cell => cell < 0 || cell >= 81))
                throw new ContentFormatException("9×9 보드 안의 칸을 선택하세요.");
            // 기존 편집기와 같이 내용은 삭제하지 않는다. 비활성 칸의 남은 요소는 검사로 안내한다.
            foreach (int cell in cells) board[cell]["isActive"] = active;
        }
        public static string Register(AuthoringEditSession session, string levelId, string name, IEnumerable<string> history)
        {
            ContentDocument level = session.Get(levelId);
            if (level.Kind != "level") throw new ContentFormatException("레벨을 선택하세요.");
            if (string.IsNullOrWhiteSpace(name)) throw new ContentFormatException("모양 이름을 입력하세요.");
            JArray cells = Mask(level.Data);
            if (!cells.Any(cell => (bool)cell)) throw new ContentFormatException("활성 칸이 있는 모양만 등록할 수 있습니다.");
            if (session.Documents.Any(doc => doc.Kind == "shape" && JToken.DeepEquals(doc.Data["cells"], cells)))
                throw new ContentFormatException("같은 모양이 이미 등록되어 있습니다.");
            string id = "shape-" + Guid.NewGuid().ToString("N");
            var data = new JObject { ["displayName"] = name.Trim(), ["cells"] = cells,
                ["sourceName"] = level.Data["displayName"]?.DeepClone(), ["sourceLevelNumber"] = level.Data["levelNumber"]?.DeepClone(),
                ["obstacleHistory"] = new JArray(history ?? Array.Empty<string>()) };
            session.Apply("맵 모양 등록", docs => docs.Add(id, new ContentDocument("shape", id, data)));
            return id;
        }

        public static string[] Usage(AuthoringEditSession session, string shapeId)
        {
            ContentDocument shape = session.Get(shapeId);
            if (shape.Kind != "shape") throw new ContentFormatException("맵 모양을 선택하세요.");
            return session.Documents.Where(doc => doc.Kind == "level" && JToken.DeepEquals(Mask(doc.Data), shape.Data["cells"]))
                .Select(doc => doc.Id).ToArray();
        }

        public static void DeleteUnused(AuthoringEditSession session, string shapeId)
        {
            if (Usage(session, shapeId).Length != 0) throw new ContentFormatException("현재 작업 폴더의 레벨에서 사용 중인 모양입니다.");
            session.Apply("미사용 맵 모양 삭제", docs => docs.Remove(shapeId));
        }

        public static void ApplyToNewLevel(JObject level, JObject shape)
        {
            var cells = shape["cells"] as JArray;
            if (cells == null || cells.Count != 81 || !cells.Any(cell => (bool)cell))
                throw new ContentFormatException("9×9 활성 칸 모양이 필요합니다.");
            var board = level["board"]?["cells"] as JArray;
            if (board == null || board.Count != 81) throw new ContentFormatException("9×9 새 레벨이 필요합니다.");
            var sources = new JArray();
            for (int i = 0; i < 81; i++)
            {
                board[i]["isActive"] = (bool)cells[i];
                if (!(bool)cells[i] || (i >= 9 && (bool)cells[i - 9])) continue;
                sources.Add(new JObject { ["coordinate"] = new JObject { ["row"] = i / 9, ["column"] = i % 9 },
                    ["mode"] = "Random", ["exhaustion"] = "Stop", ["items"] = new JArray(), ["randomDefinitionId"] = "supply.normal.random" });
            }
            level["elementSupply"]["sources"] = sources;
        }

        private static JArray Mask(JObject level)
        {
            var cells = level["board"]?["cells"] as JArray;
            if (cells == null || cells.Count != 81) throw new ContentFormatException("9×9 레벨 모양이 필요합니다.");
            return new JArray(cells.Select(cell => (bool)cell["isActive"]));
        }
    }
}
#endif
