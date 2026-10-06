using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using Elements.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private bool maintenanceExpanded;
        private void BuildMissionSettings()
        {
            if (!LevelSupplyEditing.CanEdit(level)) return;
            LevelDefinition owner = level; string snapshot = JsonUtility.ToJson(level);
            Foldout maintenance = new Foldout { text = "공급 유지 · 레벨 공통", value = maintenanceExpanded, name = "supply-maintenance" };
            maintenance.RegisterValueChangedCallback(evt => maintenanceExpanded = evt.newValue);
            properties.Add(maintenance);
            bool elements = level.SchemaVersion == LevelDefinition.CurrentSchemaVersion;
            string supplyPath = elements ? "elementSupply." : "supply.";
            AddNumber(maintenance, "고철 유지", supplyPath + "scrapTarget", elements ? level.ElementSupply.scrapTarget : level.Supply.ScrapTarget);
            AddNumber(maintenance, "추가 한도", supplyPath + "scrapLimit", elements ? level.ElementSupply.scrapLimit : level.Supply.ScrapLimit);
            AddNumber(maintenance, "고철 내구도", supplyPath + "scrapDurability", elements ? level.ElementSupply.scrapDurability : level.Supply.ScrapDurability);
            AddNumber(maintenance, "부품 유지", supplyPath + "recoveryTarget", elements ? level.ElementSupply.recoveryTarget : level.Supply.RecoveryTarget);
            if (elements)
            {
                AddDefinition("고철 공급 정의", "scrapDefinitionId", level.ElementSupply.scrapDefinitionId, ElementSupplyBehavior.Obstacle);
                AddDefinition("회수 공급 정의", "recoveryDefinitionId", level.ElementSupply.recoveryDefinitionId, ElementSupplyBehavior.Recovery);
            }
            maintenance.Add(new Label("한도는 생성구들이 공유합니다. 회수 추가량은 남은 미션 목표를 따릅니다."));
            Foldout missions = new Foldout { text = $"미션 · {level.Missions.Count}/4", value = true, name = "mission-settings" };
            properties.Add(missions);
            List<string> kinds = Enum.GetValues(typeof(MissionKind)).Cast<MissionKind>().Select(LevelMissionRules.Name).ToList();
            for (int i = 0; i < level.Missions.Count; i++)
            {
                int index = i; LevelMissionDefinition mission = level.Missions[i];
                VisualElement row = new VisualElement(); row.AddToClassList("mission-card"); missions.Add(row);
                if (!Enum.IsDefined(typeof(MissionKind), mission.Kind))
                    row.Add(new Label("잘못된 미션 종류입니다. 원본 Inspector에서 수정하세요."));
                else
                {
                    PopupField<string> kind = new PopupField<string>("목표 " + (i + 1), kinds, (int)mission.Kind) { name = "mission-kind-" + i };
                    kind.RegisterValueChangedCallback(evt => Edit(() =>
                    {
                        return LevelMissionEditing.Set(level, index, (MissionKind)kinds.IndexOf(evt.newValue), mission.Color, Math.Max(1, mission.Count));
                    })); row.Add(kind);
                    if (mission.Kind != MissionKind.Mold)
                    {
                        IntegerField count = new IntegerField("목표 수량") { value = mission.Count, isDelayed = true, name = "mission-count-" + i };
                        count.RegisterValueChangedCallback(evt => Edit(() => LevelMissionEditing.Set(level, index, mission.Kind, mission.Color, evt.newValue)));
                        row.Add(count);
                    }
                    else row.Add(new Label("전부 제거 · 확산에 따라 남은 수량 변동"));
                    if (mission.Kind == MissionKind.Color)
                    {
                        List<RabbitColor> colors = (level.Colors ?? Array.Empty<RabbitColor>()).Where(color => Enum.IsDefined(typeof(RabbitColor), color)).Distinct().ToList();
                        if (colors.Count > 0)
                        {
                            PopupField<string> color = new PopupField<string>("색", colors.Select(value => "달토끼 " + ((int)value + 1)).ToList(), Math.Max(0, colors.IndexOf(mission.Color))) { name = "mission-color-" + i };
                            color.RegisterValueChangedCallback(evt => Edit(() => LevelMissionEditing.Set(level, index, mission.Kind, colors[color.index], mission.Count))); row.Add(color);
                        }
                    }
                    row.Add(new Label(LevelMissionRules.Supply(level, mission).ToString()) { name = "mission-supply-" + i });
                }
                row.Add(SupplyButton("미션 삭제", "delete-mission-" + i, () =>
                {
                    using SerializedObject edit = new SerializedObject(level); edit.FindProperty("missions").DeleteArrayElementAtIndex(index);
                    LevelObstacleEditing.Commit(edit, "미션 삭제"); return null;
                }));
            }
            Button add = SupplyButton("+ 미션 추가", "add-mission", () => LevelMissionEditing.Add(level));
            add.SetEnabled(level.Missions.Count < 4); missions.Add(add);
            missions.Add(new Label("구조·수량만 검사합니다. 실제 플레이 가능성은 아직 판정하지 않습니다."));

            void Edit(Func<string> action) { if (SupplyViewCurrent(owner, snapshot)) SupplyAction(action); }
            void Set(string path, int value) => Edit(() =>
            {
                if (elements) return ElementSupplyEditing.SetMaintenanceProperty(level, path.Substring(supplyPath.Length), value);
                if (value < 0 || (path == "supply.scrapDurability" && (value < 1 || value > 5))) return "유지 수량·한도는 0 이상, 고철 내구도는 1~5입니다.";
                using SerializedObject edit = new SerializedObject(level); edit.FindProperty(path).intValue = value;
                LevelObstacleEditing.Commit(edit, "공급·미션 설정"); return null;
            });
            void AddNumber(VisualElement parent, string label, string path, int value, string name = null)
            {
                IntegerField field = new IntegerField(label) { value = value, isDelayed = true, name = name ?? path.Replace('.', '-') };
                field.RegisterValueChangedCallback(evt => Set(path, evt.newValue)); parent.Add(field);
            }
            void AddDefinition(string label, string field, string id, ElementSupplyBehavior behavior)
            {
                List<ElementDefinition> definitions = level.CreateElementCatalog().Definitions.Where(value => value.Supply?.Behavior == behavior).OrderBy(value => value.Id.Value).ToList();
                List<string> names = definitions.Select(value => value.DisplayName + " [" + value.Id.Value + "]").ToList();
                int current = definitions.FindIndex(value => value.Id.Value == id);
                if (current < 0) { names.Insert(0, "정의 오류 [" + id + "]"); }
                if (names.Count == 0) return;
                PopupField<string> input = new PopupField<string>(label, names, Math.Max(0, current)) { name = "maintenance-" + field };
                input.RegisterValueChangedCallback(evt =>
                {
                    int index = names.IndexOf(evt.newValue) - (current < 0 ? 1 : 0);
                    if (index >= 0) Edit(() => ElementSupplyEditing.SetMaintenanceDefinition(level, field, definitions[index].Id));
                }); maintenance.Add(input);
            }
        }
    }
}
