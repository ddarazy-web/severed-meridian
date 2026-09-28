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
        private readonly HashSet<PlacementSelection> selectedPlacements = new HashSet<PlacementSelection>();

        private void SelectPlacements(BoardCoordinate coordinate, bool additive)
        {
            if (!additive) selectedPlacements.Clear();
            if (LevelCommonEditing.TrySelect(level, board.Layer, coordinate, out PlacementSelection target))
            {
                if (additive && selectedPlacements.Contains(target)) selectedPlacements.Remove(target);
                else selectedPlacements.Add(target);
                selected = selectedPlacements.Contains(target) ? target.Coordinate :
                    selectedPlacements.Count > 0 ? selectedPlacements.First().Coordinate : (BoardCoordinate?)null;
            }
            else
            {
                if (!additive) selected = coordinate;
                if (additive) operation.text = "이 층의 유일한 배치 요소만 추가 선택할 수 있습니다.";
            }
            inspectorPage = 0;
            Refresh();
        }

        private void RefreshPlacementSelection()
        {
            if (board.Brush != LevelBrush.Select) selectedPlacements.Clear();
            selectedPlacements.RemoveWhere(item => item.Layer != board.Layer || LevelCommonEditing.Resolve(level, item) < 0);
            board.PlacementSelection = selectedPlacements.SelectMany(item => LevelPlacementRules.Footprint(item.Coordinate,
                item.Layer == PlacementLayer.Obstacle ? LevelPlacementRules.Size((ObstacleKind)item.Kind) : 1)).Distinct().ToArray();
        }

        private bool BuildCommonProperties()
        {
            if (board.Brush != LevelBrush.Select || selectedPlacements.Count == 0) return false;
            PlacementSelection[] targets = selectedPlacements.OrderBy(item => item.Coordinate.Row).ThenBy(item => item.Coordinate.Column).ToArray();
            LevelDefinition owner = level;
            string snapshot = JsonUtility.ToJson(level);
            bool Current()
            {
                if (level == owner && JsonUtility.ToJson(level) == snapshot && selectedPlacements.SetEquals(targets)) return true;
                operation.text = "선택 또는 원본이 변경되었습니다. 다시 선택하세요.";
                Refresh(); return false;
            }
            if (targets.Length == 1)
            {
                if (LevelCommonEditing.Fields(targets[0]).Length == 0) selectedProperties.Add(new Label("복사할 가변 속성이 없습니다."));
                return false;
            }
            flowProperties.Clear();
            selectedProperties.Add(new Label($"{targets.Length}개 선택 · Ctrl/Cmd+클릭으로 추가/해제"));
            selectedProperties.Add(new Label("이동·삭제·연결 수정은 하나만 선택하세요."));
            if (targets.Any(item => item.Layer != targets[0].Layer || item.Kind != targets[0].Kind))
            {
                foreach (IGrouping<string, PlacementSelection> group in targets.GroupBy(LevelCommonEditing.Name))
                    selectedProperties.Add(new Label($"{group.Key} {group.Count()}개"));
                selectedProperties.Add(new Label("종류가 섞여 있습니다. 설정 붙여넣기는 같은 종류에만 적용합니다."));
                return true;
            }
            string[] fields = LevelCommonEditing.Fields(targets[0]);
            if (fields.Length == 0) selectedProperties.Add(new Label("공통으로 변경할 가변 속성이 없습니다."));
            foreach (string field in fields)
            {
                int[] values = targets.Select(item => LevelCommonEditing.Read(level, item, field)).Distinct().ToArray();
                bool mixed = values.Length > 1;
                string label = field switch { "fixedColor" => "색", "color" => "자물쇠 색", "rocketDirection" => "제거 방향", "requiredCharge" => "필요 충전량", _ => "내구도" };
                if (field == "fixedColor" || field == "color" || field == "rocketDirection")
                {
                    List<int> options = field == "rocketDirection" ? new List<int> { 0, 1 } :
                        (level.Colors ?? Array.Empty<RabbitColor>()).Where(color => Enum.IsDefined(typeof(RabbitColor), color)).Select(color => (int)color).Distinct().ToList();
                    List<string> choices = options.Select(value => field == "rocketDirection" ? (value == 0 ? "가로 한 줄" : "세로 한 줄") : "달토끼 " + (value + 1)).ToList();
                    bool placeholder = mixed || !options.Contains(values[0]);
                    if (placeholder) choices.Insert(0, mixed ? "— 혼합 —" : "잘못된 값 " + values[0]);
                    PopupField<string> input = new PopupField<string>(label, choices, placeholder ? 0 : options.IndexOf(values[0])) { name = "common-" + field };
                    input.RegisterValueChangedCallback(evt =>
                    {
                        int index = choices.IndexOf(evt.newValue) - (placeholder ? 1 : 0);
                        if (index >= 0 && index < options.Count) Change(field, options[index]);
                    });
                    selectedProperties.Add(input);
                }
                else
                {
                    IntegerField input = new IntegerField(label) { name = "common-" + field, value = values[0], showMixedValue = mixed, isDelayed = true };
                    input.RegisterValueChangedCallback(evt => Change(field, evt.newValue));
                    selectedProperties.Add(input);
                }
            }
            return true;

            void Change(string field, int value)
            {
                if (!Current()) return;
                board.CancelStroke();
                string error = LevelCommonEditing.Set(level, targets, field, value, out int changed);
                operation.text = error ?? $"공통 설정: 변경 {changed}개 · 나머지 속성 보존";
                Refresh();
            }
        }
    }
}

