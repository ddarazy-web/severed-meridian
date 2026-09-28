using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public readonly struct PlacementSelection
    {
        public readonly PlacementLayer Layer;
        public readonly int Kind;
        public readonly BoardCoordinate Coordinate;
        public readonly string Id;
        public PlacementSelection(PlacementLayer layer, int kind, BoardCoordinate coordinate, string id = null)
        { Layer = layer; Kind = kind; Coordinate = coordinate; Id = id; }
    }

    public static class LevelCommonEditing
    {
        [Serializable] private struct Value { public string field; public int value; }
        [Serializable] private struct ClipboardValue { public string field; public string value; }
        [Serializable] private sealed class Settings
        {
            public string type;
            public int version;
            public string layer;
            public string kind;
            public List<ClipboardValue> values;
        }

        public static bool TrySelect(LevelDefinition level, PlacementLayer layer, BoardCoordinate cell, out PlacementSelection selection)
        {
            selection = default;
            if (!LevelBoardEditing.CanEdit(level) || !Enum.IsDefined(typeof(PlacementLayer), layer)) return false;
            int index = LevelPlacementRules.Find(level, layer, cell);
            if (index < 0) return false;
            int kind = layer switch
            {
                PlacementLayer.Block => (int)level.InitialBlocks[index].Kind,
                PlacementLayer.Obstacle => (int)level.Obstacles[index].Kind,
                PlacementLayer.Cover => (int)level.Covers[index].Kind,
                _ => 0
            };
            if ((layer == PlacementLayer.Block && !Enum.IsDefined(typeof(InitialBlockKind), kind)) ||
                (layer == PlacementLayer.Obstacle && !Enum.IsDefined(typeof(ObstacleKind), kind)) ||
                (layer == PlacementLayer.Cover && !Enum.IsDefined(typeof(CoverKind), kind))) return false;
            if (layer == PlacementLayer.Obstacle)
            {
                ObstaclePlacementDefinition body = level.Obstacles[index];
                if (LevelConnectionRules.Find(level, body.Id) != index ||
                    LevelPlacementRules.Footprint(body.Coordinate, LevelPlacementRules.Size(body.Kind)).Any(part => LevelPlacementRules.Find(level, layer, part) != index)) return false;
                selection = new PlacementSelection(layer, kind, body.Coordinate, body.Id);
            }
            else selection = new PlacementSelection(layer, kind, cell);
            return true;
        }

        public static int Resolve(LevelDefinition level, PlacementSelection selection)
        {
            return TrySelect(level, selection.Layer, selection.Coordinate, out PlacementSelection current) && current.Equals(selection)
                ? LevelPlacementRules.Find(level, selection.Layer, selection.Coordinate) : -1;
        }

        public static string[] Fields(PlacementSelection selection) => selection.Layer switch
        {
            PlacementLayer.Block => (InitialBlockKind)selection.Kind switch
            {
                InitialBlockKind.FixedNormal => new[] { "fixedColor" },
                InitialBlockKind.Rocket => new[] { "rocketDirection" },
                _ => Array.Empty<string>()
            },
            PlacementLayer.Obstacle => (ObstacleKind)selection.Kind switch
            {
                ObstacleKind.Generator => new[] { "requiredCharge" },
                ObstacleKind.ColorLock => new[] { "durability", "color" },
                _ => new[] { "durability" }
            },
            _ => new[] { "durability" }
        };

        public static string Name(PlacementSelection selection) => selection.Layer switch
        {
            PlacementLayer.Block => LevelPlacementRules.Name((InitialBlockKind)selection.Kind),
            PlacementLayer.Obstacle => LevelPlacementRules.Name((ObstacleKind)selection.Kind),
            PlacementLayer.Cover => (CoverKind)selection.Kind == CoverKind.Web ? "거미줄" : "곰팡이",
            _ => "먼지"
        };

        public static int Read(LevelDefinition level, PlacementSelection selection, string field)
        {
            int index = Resolve(level, selection);
            if (index < 0 || !Fields(selection).Contains(field)) throw new ArgumentException("선택 속성을 다시 확인하세요.");
            using SerializedObject data = new SerializedObject(level);
            return data.FindProperty(LevelObstacleEditing.ListPath(selection.Layer)).GetArrayElementAtIndex(index).FindPropertyRelative(field).intValue;
        }

        public static int Maximum(PlacementSelection selection, string field) => field == "requiredCharge" ? 5 :
            selection.Layer == PlacementLayer.Obstacle ? LevelPlacementRules.MaxDurability((ObstacleKind)selection.Kind) :
            selection.Layer == PlacementLayer.Cover && (CoverKind)selection.Kind == CoverKind.Mold ? 1 : 3;

        public static string Copy(LevelDefinition level, PlacementSelection selection)
        {
            if (Resolve(level, selection) < 0 || Fields(selection).Length == 0) return null;
            return JsonUtility.ToJson(new Settings { type = "MatchPlacementSettings", version = 1, layer = selection.Layer.ToString(), kind = selection.Kind.ToString(CultureInfo.InvariantCulture),
                values = Fields(selection).Select(field => new ClipboardValue { field = field, value = Read(level, selection, field).ToString(CultureInfo.InvariantCulture) }).ToList() });
        }

        public static string Set(LevelDefinition level, IReadOnlyList<PlacementSelection> selections, string field, int value, out int changed)
        {
            changed = 0;
            if (selections == null || selections.Count == 0) return "대상을 선택하세요.";
            PlacementSelection first = selections[0];
            if (selections.Any(item => item.Layer != first.Layer || item.Kind != first.Kind)) return "같은 종류만 공통 편집할 수 있습니다.";
            return Apply(level, selections, new[] { new Value { field = field, value = value } }, out changed);
        }

        public static string Paste(LevelDefinition level, IReadOnlyList<PlacementSelection> selections, string text, out int changed, out int excluded)
        {
            changed = excluded = 0;
            if (selections == null || selections.Count == 0) return "대상을 선택하세요.";
            Settings settings;
            try { settings = JsonUtility.FromJson<Settings>(text); }
            catch (ArgumentException) { return "설정 클립보드 형식이 아닙니다."; }
            if (settings == null || settings.type != "MatchPlacementSettings" || settings.version != 1 || settings.values == null)
                return "지원하지 않는 설정 클립보드입니다.";
            PlacementSelection[] targets = selections.Distinct().Where(item => item.Layer.ToString() == settings.layer && item.Kind.ToString(CultureInfo.InvariantCulture) == settings.kind).ToArray();
            excluded = selections.Distinct().Count() - targets.Length;
            if (targets.Length == 0) return $"같은 종류 대상이 없습니다. 제외 {excluded}개.";
            string[] fields = Fields(targets[0]);
            if (fields.Length == 0 || settings.values.Count != fields.Length ||
                !settings.values.Select(item => item.field).OrderBy(field => field).SequenceEqual(fields.OrderBy(field => field)))
                return "복사할 속성이 없거나 클립보드 속성이 누락/중복되었습니다.";
            // 문자열로 보관하여 누락된 수치가 0이 되거나 소수가 정수로 바뀌는 것을 막는다.
            List<Value> values = new List<Value>();
            foreach (ClipboardValue item in settings.values)
            {
                if (!int.TryParse(item.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ||
                    value.ToString(CultureInfo.InvariantCulture) != item.value) return "클립보드 수치는 명시적인 정수여야 합니다.";
                values.Add(new Value { field = item.field, value = value });
            }
            return Apply(level, targets, values, out changed);
        }

        private static string Apply(LevelDefinition level, IReadOnlyList<PlacementSelection> targets, IReadOnlyList<Value> values, out int changed)
        {
            changed = 0;
            if (!LevelBoardEditing.CanEdit(level)) return "현재 형식의 레벨을 선택하세요.";
            PlacementSelection[] unique = targets.Distinct().ToArray();
            foreach (PlacementSelection target in unique)
            {
                if (Resolve(level, target) < 0) return "선택 대상이 변경되었거나 중복됩니다. 전체 작업을 취소했습니다.";
                foreach (Value value in values)
                {
                    if (!Fields(target).Contains(value.field)) return "이 종류에 적용할 수 없는 속성입니다.";
                    if (value.field == "color" || value.field == "fixedColor")
                    {
                        if (!Enum.IsDefined(typeof(RabbitColor), value.value) || level.Colors?.Contains((RabbitColor)value.value) != true)
                            return "대상 레벨에서 사용하는 색을 지정하세요.";
                    }
                    else if (value.field == "rocketDirection")
                    {
                        if (!Enum.IsDefined(typeof(RocketDirection), value.value)) return "로켓 방향이 잘못되었습니다.";
                    }
                    else if (value.value < (value.field == "requiredCharge" ? 3 : 1) || value.value > Maximum(target, value.field))
                        return "내구도/충전량의 허용 범위를 벗어났습니다.";
                }
            }
            using SerializedObject data = new SerializedObject(level);
            foreach (PlacementSelection target in unique)
            {
                SerializedProperty item = data.FindProperty(LevelObstacleEditing.ListPath(target.Layer)).GetArrayElementAtIndex(Resolve(level, target));
                bool modified = false;
                foreach (Value value in values)
                {
                    SerializedProperty property = item.FindPropertyRelative(value.field);
                    if (property.intValue == value.value) continue;
                    property.intValue = value.value; modified = true;
                }
                if (modified) changed++;
            }
            if (changed > 0) LevelObstacleEditing.Commit(data, "선택 요소 공통 설정");
            return null;
        }
    }
}
