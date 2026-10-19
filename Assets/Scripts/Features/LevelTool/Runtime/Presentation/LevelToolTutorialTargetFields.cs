#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Tutorial;
using UnityEngine.UIElements;
using static LevelTool.Presentation.LevelToolPanelFields;

namespace LevelTool.Presentation
{
    internal static class LevelToolTutorialTargetFields
    {
        public static void Draw(VisualElement card, LevelDefinition level, TutorialConditionDefinition condition, int index, Action<Action<TutorialConditionDefinition>> change, Func<List<BoardCoordinate>> selectedCoordinates, Action pick, bool initialAction)
        {

            if (condition.target == null)
            { Button(card, "tutorial-create-target-" + index, "조건 대상 지정", () => change(data => data.target = new TutorialTargetDefinition())); return; }
            var target = condition.target;
            var kinds = condition.kind == TutorialConditionKind.Generated ? new[] { TutorialTargetKind.Board, TutorialTargetKind.Area } : Enum.GetValues(typeof(TutorialTargetKind)).Cast<TutorialTargetKind>().ToArray();
            string[] names = { "보드 전체", "특정 개체", "종류", "선택 영역", "이전 생성 결과" };
            Choice(card, "tutorial-target-kind-" + index, "조건 대상", kinds.Select(value => value.ToString()).ToArray(), kinds.Select(value => names[(int)value]).ToArray(), target.kind.ToString(),
                value => change(data => data.target.kind = (TutorialTargetKind)Enum.Parse(typeof(TutorialTargetKind), value)));
            if (condition.kind < TutorialConditionKind.Generated) TutorialEnum(card, "tutorial-target-layer-" + index, "대상 층", target.layer, value => change(data => data.target.layer = value),
                value => value == TutorialTargetLayer.Content ? "블록 / 장애물" : value == TutorialTargetLayer.Cover ? "덮개" : "바닥");
            if (target.kind == TutorialTargetKind.Area || target.kind == TutorialTargetKind.Entity)
            {
                if (pick != null) Button(card, "tutorial-target-select-" + index, "보드에서 조건 대상 선택", pick);
                card.Add(new Label(target.kind == TutorialTargetKind.Entity ? "대상 위치: " + target.coordinate : "대상 영역: " + target.cells.Count + "칸"));
                Button(card, "tutorial-target-pick-" + index, "현재 선택 칸을 조건 대상으로", () => change(data =>
                {
                    var cells = selectedCoordinates();
                    if (cells.Count == 0 || data.target.kind == TutorialTargetKind.Entity && cells.Count != 1) throw new ArgumentException("개체는 한 칸, 영역은 한 칸 이상 선택하세요.");
                    if (data.target.kind == TutorialTargetKind.Entity) data.target.coordinate = cells[0]; else data.target.cells = cells;
                }));
            }
            if (target.kind == TutorialTargetKind.Generated) TutorialText(card, "tutorial-target-binding-" + index, "이전 생성 연결 이름", target.binding, value => change(data => data.target.binding = value));
            if (target.kind == TutorialTargetKind.Definition)
            {
                var definitions = level.CreateElementCatalog().Definitions.ToArray();
                Choice(card, "tutorial-target-definition-" + index, "대상 종류", new[] { "" }.Concat(definitions.Select(value => value.Id.Value)).ToArray(), new[] { "선택하세요" }.Concat(definitions.Select(value => value.DisplayName)).ToArray(), target.definitionId,
                    value => change(data => data.target.definitionId = value));
            }
            string warning = TutorialTargetCapabilities.ConditionError(TutorialTargetCapabilities.Resolve(level, target,
                initialAction), condition.kind);
            if (warning != null) card.Add(new Label(warning) { style = { whiteSpace = WhiteSpace.Normal } });
        }
    }
}
#endif
