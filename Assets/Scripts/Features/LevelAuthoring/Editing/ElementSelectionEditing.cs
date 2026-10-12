#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Validation;
using Newtonsoft.Json.Linq;
using static LevelAuthoring.Editing.DocumentBoardRules;

namespace LevelAuthoring.Editing
{
    // 클립보드는 이 세션에만 속하며 문서와 분리된 사본을 보관한다.
    public sealed class ElementSelectionEditing
    {
        private readonly AuthoringEditSession session;
        private readonly Func<IReadOnlyDictionary<string, JObject>> definitionResolver;
        private JObject[] clipboard;
        private JObject[] clipboardConnections;
        private int anchorRow;
        private int anchorColumn;
        public bool HasClipboard => clipboard != null && clipboard.Length > 0;
        public string ClipboardLayer => HasClipboard ? (string)clipboard[0]["layer"] : null;

        public ElementSelectionEditing(AuthoringEditSession session, Func<IReadOnlyDictionary<string, JObject>> definitionResolver = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.definitionResolver = definitionResolver;
        }

        public string[] SelectedInstanceIds(string layer) => Selection(layer).Select(item => (string)item["instanceId"]).ToArray();

        public int[] SelectedCells(string layer)
        {
            IReadOnlyDictionary<string, JObject> definitions = Definitions();
            return Selection(layer).SelectMany(item =>
            {
                int row = (int)item["coordinate"]["row"], column = (int)item["coordinate"]["column"];
                int size = Size(Definition(definitions, (string)item["definitionId"]));
                return Enumerable.Range(row, size).SelectMany(r => Enumerable.Range(column, size).Select(c => r * 9 + c));
            }).Distinct().OrderBy(cell => cell).ToArray();
        }

        public void Copy(string layer)
        {
            JObject[] selected = Selection(layer);
            if (selected.Length == 0) throw new ContentFormatException("복사할 요소를 선택하세요.");
            HashSet<string> ids = new HashSet<string>(selected.Select(item => (string)item["instanceId"]), StringComparer.Ordinal);
            JObject[] connections = List(session.Get(session.SelectedLevelId).Data, "connections").Cast<JObject>()
                .Where(item => ids.Contains((string)item["generatorId"]) && ids.Contains((string)item["targetId"]))
                .Select(item => (JObject)item.DeepClone()).ToArray();
            clipboard = selected.Select(item => (JObject)item.DeepClone()).ToArray();
            clipboardConnections = connections;
            anchorRow = selected.Min(item => (int)item["coordinate"]["row"]);
            anchorColumn = selected.Min(item => (int)item["coordinate"]["column"]);
        }

        public void Cut(string layer)
        {
            // 삭제보다 먼저 복사하여 캡처가 실패하면 원본을 유지한다.
            Copy(layer);
            Delete(layer);
        }

        public void Delete(string layer)
        {
            JObject[] selected = Selection(layer);
            if (selected.Length == 0) return;
            IReadOnlyDictionary<string, JObject> definitions = Definitions();
            string levelId = session.SelectedLevelId;
            session.Apply("선택 요소 삭제", documents =>
            {
                foreach (JObject item in selected)
                    LevelDocumentEditing.Erase(documents[levelId].Data, definitions, layer, (int)item["coordinate"]["row"], (int)item["coordinate"]["column"]);
            });
        }

        public void Paste(int row, int column)
        {
            if (!HasClipboard) throw new ContentFormatException("복사한 요소가 없습니다.");
            IReadOnlyDictionary<string, JObject> definitions = Definitions();
            string levelId = session.SelectedLevelId;
            int rowOffset = row - anchorRow, columnOffset = column - anchorColumn;
            session.Apply("선택 요소 붙여넣기", documents =>
            {
                JObject level = documents[levelId].Data;
                string catalogId = (string)level["catalogId"];
                ContentDocument catalog = null;
                if (catalogId != null && (!documents.TryGetValue(catalogId, out catalog) || catalog.Kind != "catalog"))
                    throw new ContentFormatException("붙여넣을 레벨의 카탈로그 참조가 없습니다.");
                Dictionary<string, string> ids = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (JObject original in clipboard)
                {
                    JObject value = (JObject)original.DeepClone();
                    ContentDocumentValidator.ValidateElementPlacement(value);
                    int r = (int)value["coordinate"]["row"] + rowOffset, c = (int)value["coordinate"]["column"] + columnOffset;
                    string definitionId = (string)value["definitionId"], layer = (string)value["layer"];
                    int size = Size(Definition(definitions, definitionId));
                    if (catalog != null && definitionResolver == null)
                    {
                        ContentDocument definitionDocument = documents.Values.FirstOrDefault(document => document.Kind == "element" && (string)document.Data["definition"]["id"] == definitionId);
                        if (definitionDocument == null || !(catalog.Data["definitionIds"] is JArray members) || !members.Values<string>().Contains(definitionDocument.Id))
                            throw new ContentFormatException("붙여넣을 요소가 레벨 카탈로그에 없습니다.");
                    }
                    else if (catalogId == null && level["embeddedDefinitions"] is JArray embedded && embedded.Count > 0 && !embedded.Any(definition => (string)definition["id"] == definitionId))
                        throw new ContentFormatException("붙여넣을 요소가 레벨 내장 정의에 없습니다.");
                    // Place의 같은 정의 속성 수정 경로도 붙여넣기에서는 충돌로 처리한다.
                    if (List(level, "elements").Cast<JObject>().Any(item =>
                    {
                        if ((string)item["layer"] != layer) return false;
                        int otherRow = (int)item["coordinate"]["row"], otherColumn = (int)item["coordinate"]["column"];
                        int otherSize = Size(Definition(definitions, (string)item["definitionId"]));
                        return r < otherRow + otherSize && r + size > otherRow && c < otherColumn + otherSize && c + size > otherColumn;
                    }))
                        throw new ContentFormatException("다른 요소가 점유합니다.");
                    LevelDocumentEditing.Place(level, definitions, definitionId, r, c, false, (int)value["durability"], (int)value["requiredCharge"], (string)value["color"], (string)value["rocketDirection"]);
                    JObject placed = (JObject)level["elements"].Last();
                    if ((string)placed["layer"] != layer) throw new ContentFormatException("복사한 요소의 층과 정의가 다릅니다.");
                    if (!JToken.DeepEquals(placed["durability"], value["durability"]) || !JToken.DeepEquals(placed["requiredCharge"], value["requiredCharge"]) ||
                        !JToken.DeepEquals(placed["hasColor"], value["hasColor"])) throw new ContentFormatException("복사한 요소의 속성과 현재 정의가 다릅니다.");
                    string id = (string)placed["instanceId"];
                    ids.Add((string)original["instanceId"], id);
                    value["instanceId"] = id;
                    value["coordinate"] = new JObject { ["row"] = r, ["column"] = c };
                    placed.Replace(value);
                }
                // 두 끝점이 함께 복사된 연결만 새 본체로 연결하고 원본 연결은 보존한다.
                foreach (JObject connection in clipboardConnections)
                {
                    ConnectionDocumentEditing.Add(level, definitions, ids[(string)connection["generatorId"]], ids[(string)connection["targetId"]]);
                    JArray vertices = (JArray)connection["vertices"];
                    if (vertices.Count > 0)
                    {
                        int[] shifted = vertices.Select(vertex =>
                        {
                            int r = (int)vertex["row"] + rowOffset, c = (int)vertex["column"] + columnOffset;
                            if (r < 0 || r > 9 || c < 0 || c > 9) throw new ContentFormatException("전선 꼭짓점이 보드 밖입니다.");
                            return r * 10 + c;
                        }).ToArray();
                        ConnectionDocumentEditing.SetWire(level, definitions, List(level, "connections").Count - 1, shifted, shifted.Length == 1);
                    }
                }
                // 다른 문서와 미완성 튜토리얼은 그대로 편집할 수 있게 두고 새 본체의 참조만 검사한다.
                foreach (JObject original in clipboard)
                    if ((string)Definition(definitions, (string)original["definitionId"])["reaction"] == "GeneratorCharge" &&
                        !List(level, "connections").Any(connection => (string)connection["generatorId"] == ids[(string)original["instanceId"]]))
                        throw new ContentFormatException("복사한 발전기의 연결 대상이 없습니다.");
            });
        }

        private IReadOnlyDictionary<string, JObject> Definitions()
        {
            // 내장 기본 정의는 화면이 이미 사용하는 카탈로그 해석을 전달받아 JSON에 추가하지 않는다.
            if (definitionResolver != null) return definitionResolver();
            JObject level = session.Get(session.SelectedLevelId).Data;
            if ((string)level["catalogId"] == null && level["embeddedDefinitions"] is JArray embedded && embedded.Count > 0)
                return embedded.Cast<JObject>().ToDictionary(definition => (string)definition["id"], definition => definition, StringComparer.Ordinal);
            return session.Documents.Where(document => document.Kind == "element")
                .ToDictionary(document => (string)document.Data["definition"]["id"], document => (JObject)document.Data["definition"], StringComparer.Ordinal);
        }

        private JObject[] Selection(string layer)
        {
            if (layer != "Block" && layer != "Obstacle" && layer != "Cover" && layer != "Dust") throw new ContentFormatException("요소 편집 층을 선택하세요.");
            JObject level = session.Get(session.SelectedLevelId).Data;
            if ((int?)level["schemaVersion"] != 5) throw new ContentFormatException("요소 배치 형식의 레벨이 필요합니다.");
            IReadOnlyDictionary<string, JObject> definitions = Definitions();
            JArray items = List(level, "elements");
            int[] cells = session.SelectedCells;
            JObject[] selected = items.Cast<JObject>().Where(item => (string)item["layer"] == layer &&
                cells.Any(cell => Covers(item, Size(Definition(definitions, (string)item["definitionId"])), cell))).ToArray();
            foreach (JObject item in selected)
            {
                string id = (string)item["instanceId"];
                if (string.IsNullOrWhiteSpace(id) || items.Count(other => (string)other["instanceId"] == id) != 1)
                    throw new ContentFormatException("누락되거나 중복된 본체 ID를 먼저 수정하세요.");
            }
            if (cells.Any(cell => selected.Count(item => Covers(item, Size(Definition(definitions, (string)item["definitionId"])), cell)) > 1))
                throw new ContentFormatException("중복 점유 칸을 먼저 수정하세요.");
            return selected;
        }
    }
}
#endif
