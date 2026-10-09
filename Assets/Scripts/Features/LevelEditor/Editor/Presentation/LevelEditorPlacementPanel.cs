using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private static readonly List<string> LayerNames = new List<string> { "블록", "장애물/장치", "덮개", "먼지" };

        private void BuildPlacementProperties(BoardCoordinate coordinate)
        {
            PlacementLayer layer = board.Layer;
            LevelDefinition owner = level;
            string selectionSource = JsonUtility.ToJson(level);
            int index = LevelPlacementRules.Find(level, layer, coordinate);
            if (index < 0)
            {
                selectedProperties.Add(new Label(index == -2 ? "중복 배치: 기존 Inspector 원본 목록에서 수정하세요." : "이 층에 요소가 없습니다."));
                return;
            }
            if (layer == PlacementLayer.Obstacle && LevelPlacementRules.Footprint(level.Obstacles[index].Coordinate,
                LevelPlacementRules.Size(level.Obstacles[index].Kind)).Any(cell => LevelPlacementRules.Find(level, layer, cell) == -2))
            {
                selectedProperties.Add(new Label("본체의 일부 영역에 중복 배치가 있습니다. 기존 Inspector에서 수정하세요."));
                return;
            }
            string path = LevelObstacleEditing.ListPath(layer) + $".Array.data[{index}]";
            SerializedProperty item = data.FindProperty(path);
            if (layer == PlacementLayer.Block)
            {
                InitialBlockDefinition block = level.InitialBlocks[index];
                selectedProperties.Add(new Label(LevelPlacementRules.Name(block.Kind)));
                if (block.Kind == InitialBlockKind.Rocket)
                    AddChoiceProperty(item, "rocketDirection", "제거 방향", new List<string> { "가로 한 줄 ↔", "세로 한 줄 ↕" }, new[] { 0, 1 });
            }
            else if (layer == PlacementLayer.Obstacle)
            {
                ObstaclePlacementDefinition obstacle = level.Obstacles[index];
                selectedProperties.Add(new Label(LevelPlacementRules.Name(obstacle.Kind) + " / 기준 " + obstacle.Coordinate));
                if (obstacle.Kind == ObstacleKind.Generator)
                {
                    AddNumberProperty(item, "requiredCharge", "필요 충전량", 3, 5);
                    selectedProperties.Add(new HelpBox("아래 바닥 흐름·연결에서 대상과 전선을 편집합니다. 실제 충전·작동은 아직 실행하지 않습니다.", HelpBoxMessageType.Info));
                }
                else if (LevelPlacementRules.MaxDurability(obstacle.Kind) > 0)
                    AddNumberProperty(item, "durability", "내구도", 1, LevelPlacementRules.MaxDurability(obstacle.Kind));
                if (obstacle.Kind == ObstacleKind.ColorLock)
                {
                    List<int> colors = level.Colors?.Where(value => Enum.IsDefined(typeof(RabbitColor), value)).Select(value => (int)value).Distinct().ToList() ?? new List<int>();
                    AddChoiceProperty(item, "color", "자물쇠 색", colors.Select(value => "달토끼 " + (value + 1)).ToList(), colors);
                }
                if (LevelPlacementRules.Size(obstacle.Kind) == 2)
                    selectedProperties.Add(new Button(() =>
                    {
                        if (level != owner || JsonUtility.ToJson(level) != selectionSource) { Refresh(); return; }
                        board.BeginMove(index);
                        operation.text = "목적지 왼쪽 위 칸을 클릭하세요. Esc 또는 포커스 상실로 취소합니다.";
                    }) { text = "2×2 본체 이동", name = "move-obstacle" });
            }
            else if (layer == PlacementLayer.Cover)
            {
                CoverPlacementDefinition cover = level.Covers[index];
                selectedProperties.Add(new Label(cover.Kind == CoverKind.Web ? "거미줄" : cover.Kind == CoverKind.Mold ? "우주 곰팡이" : "잘못된 덮개"));
                if (Enum.IsDefined(typeof(CoverKind), cover.Kind))
                    AddNumberProperty(item, "durability", "내구도", 1, cover.Kind == CoverKind.Web ? 3 : 1);
                int block = LevelPlacementRules.Find(level, PlacementLayer.Block, coordinate);
                selectedProperties.Add(new Label(block >= 0 ? "내부: " + LevelPlacementRules.Name(level.InitialBlocks[block].Kind) : "내부 블록 없음/중복"));
            }
            else AddNumberProperty(item, "durability", "먼지 내구도", 1, 3);
            selectedProperties.Add(new Button(() =>
            {
                board.CancelStroke();
                if (level != owner || JsonUtility.ToJson(level) != selectionSource) { Refresh(); return; }
                EditLevel("선택 요소 삭제", () => operation.text = LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = layer, Erase = true }, new[] { coordinate }).ToString());
                Refresh();
            }) { text = "선택한 층의 요소 삭제", name = "delete-placement" });
        }

        private void AddNumberProperty(SerializedProperty item, string field, string label, int min, int max)
        {
            IntegerField input = new IntegerField(label + $" ({min}~{max})") { name = "selected-" + field, isDelayed = true };
            input.SetValueWithoutNotify(item.FindPropertyRelative(field).intValue);
            string path = item.propertyPath + "." + field;
            string snapshot = JsonUtility.ToJson(level);
            input.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue < min || evt.newValue > max)
                {
                    input.SetValueWithoutNotify(evt.previousValue);
                    operation.text = $"{label}: {min}~{max}만 입력할 수 있습니다. 기존 값은 보존합니다.";
                    return;
                }
                SetPlacementValue(path, evt.newValue, snapshot);
            });
            selectedProperties.Add(input);
        }

        private void AddChoiceProperty(SerializedProperty item, string field, string label, List<string> labels, IEnumerable<int> values)
        {
            List<int> options = values.ToList();
            int raw = item.FindPropertyRelative(field).intValue;
            int selectedIndex = options.IndexOf(raw);
            if (selectedIndex < 0) { selectedIndex = labels.Count; labels.Add("잘못된 값 " + raw); }
            PopupField<string> input = new PopupField<string>(label, labels, selectedIndex) { name = "selected-" + field };
            string path = item.propertyPath + "." + field;
            string snapshot = JsonUtility.ToJson(level);
            input.RegisterValueChangedCallback(evt =>
            {
                int choice = labels.IndexOf(evt.newValue);
                if (choice >= 0 && choice < options.Count) SetPlacementValue(path, options[choice], snapshot);
            });
            selectedProperties.Add(input);
        }

        private void SetPlacementValue(string path, int value, string snapshot)
        {
            board.CancelStroke();
            if (JsonUtility.ToJson(level) != snapshot) { Refresh(); return; }
            EditLevel("선택 요소 속성 변경", () =>
            {
                using SerializedObject edit = new SerializedObject(level);
                SerializedProperty field = edit.FindProperty(path);
                if (field == null || field.intValue == value) return;
                field.intValue = value;
                LevelObstacleEditing.Commit(edit, "선택 요소 속성 변경");
            });
            Refresh();
        }
    }
}
