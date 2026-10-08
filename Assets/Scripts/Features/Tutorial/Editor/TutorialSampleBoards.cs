using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Levels;
using Simulation;
using UnityEngine;

namespace Tutorial.Editor
{
    public sealed class TutorialSampleBoard
    {
        public string Id { get; }
        public string Title { get; }
        public string ExpectedResult { get; }
        internal TutorialSampleBoard(string id, string title, string expected)
        { Id = id; Title = title; ExpectedResult = expected; }
        public LevelDefinition CreateBoard() => TutorialSampleBoards.Create(Id, Title);
    }

    /// <summary>출시 데이터와 무관한 미저장 시험 보드. 작성자가 조건·공급을 살펴보는 독립 사본이다.</summary>
    public static class TutorialSampleBoards
    {
        public static IReadOnlyList<TutorialSampleBoard> All { get; } = Array.AsReadOnly(new[]
        {
            new TutorialSampleBoard("description", "설명 확인", "다음을 누르면 종료. 이동과 보드 상태는 그대로입니다."),
            new TutorialSampleBoard("swap", "지정한 두 칸 교환", "지정 교환의 실제 성공1회 후 종료합니다. 매칭 개수 조건과 분리되어 있습니다."),
            new TutorialSampleBoard("match", "직접 3매칭", "지정 두 칸 교환으로 정확히3개 매칭1회 후 종료합니다."),
            new TutorialSampleBoard("area", "영역 안에서 교환", "선택 영역 안에서 조건에 기여하는 교환을 안내하고 성공1회면 종료합니다."),
            new TutorialSampleBoard("rocket-h", "가로 로켓 생성", "세로4매칭으로 가로 로켓1개를 생성합니다."),
            new TutorialSampleBoard("rocket-v", "세로 로켓 생성", "가로4매칭으로 세로 로켓1개를 생성합니다."),
            new TutorialSampleBoard("bomb", "폭탄 생성", "꺾인5매칭으로 폭탄1개를 생성합니다."),
            new TutorialSampleBoard("drone", "드론 생성", "2×2매칭으로 드론1개를 생성합니다."),
            new TutorialSampleBoard("magnet", "자석 생성", "가로5매칭으로 자석1개를 생성합니다."),
            new TutorialSampleBoard("activate", "로켓 직접 단독 발동", "지정 로켓을 일반 블록과 교환해 단독 발동1회를 셉니다."),
            new TutorialSampleBoard("combine", "로켓 + 폭탄 조합", "두 파워의 조합1회만 인정합니다. 단독 발동 조건과 별개입니다."),
            new TutorialSampleBoard("damage", "내구도 합계 줄이기", "단독 로켓으로 상자의 실제 내구도를1 줄이면 종료합니다."),
            new TutorialSampleBoard("each", "각 대상의 내구도 줄이기", "종류로 선택한 두 상자가 각각1씩 피해를 받아야 종료합니다."),
            new TutorialSampleBoard("remaining", "남은 내구도 정확히2", "내구도3인 상자가 정확히2가 되면 종료합니다."),
            new TutorialSampleBoard("remove", "로켓으로 제거", "내구도1인 상자1개를 단독 로켓으로 제거하면 종료합니다."),
            new TutorialSampleBoard("hammer", "망치 무료 체험", "상자에 망치1회 성공. 무료 횟수1을 사용하고 실제 재고는 유지합니다."),
            new TutorialSampleBoard("item-swap", "교환 아이템 체험", "지정 두 칸에 교환 아이템1회 성공. 이동은 소비하지 않습니다."),
            new TutorialSampleBoard("shuffle", "섞기 아이템 체험", "섞기 아이템 실제 성공1회 후 종료합니다."),
            new TutorialSampleBoard("mission", "미션 진행 늘리기", "직접 매칭으로 첫 색상 미션의 실제 진행이1 이상 늘면 종료합니다."),
            new TutorialSampleBoard("follow", "생성 로켓 이어서 조작", "로켓 생성→낙하→같은 개체를 이름으로 찾아 교환·발동합니다."),
            new TutorialSampleBoard("two", "고정 공급과 두 번 교환", "같은 단계에서 성공 교환2회와 상자 내구도 합계2 감소를 모두 요구합니다. 두 행동 사이에도 고정 공급을 연속 소비합니다.")
        });

        [Serializable]
        private sealed class Layout
        {
            public int schemaVersion = LevelDefinition.CurrentSchemaVersion;
            public int levelNumber = 100001;
            public int moveCount = 20;
            public List<RabbitColor> colors = new List<RabbitColor> { RabbitColor.Type1, RabbitColor.Type2, RabbitColor.Type3, RabbitColor.Type4 };
            public List<ElementPlacementDefinition> elements = new List<ElementPlacementDefinition>();
            public ElementLevelSupplyDefinition elementSupply = new ElementLevelSupplyDefinition();
        }

        internal static LevelDefinition Create(string id, string title)
        {
            if (!All.Any(sample => sample.Id == id)) throw new ArgumentException("등록되지 않은 시험 보드입니다.", nameof(id));
            var data = new Layout();
            BoardCoordinate At(int row, int column) => new BoardCoordinate(row, column);
            for (int row = 0; row < 9; row++) for (int column = 0; column < 9; column++)
                data.elements.Add(new ElementPlacementDefinition { coordinate = At(row, column), definitionId = "supply.normal.fixed",
                    layer = PlacementLayer.Block, hasColor = true, color = data.colors[(row * 2 + column) % data.colors.Count] });
            ElementPlacementDefinition Cell(BoardCoordinate at) => data.elements.Single(cell => cell.coordinate.Equals(at));
            void Place(BoardCoordinate at, string definition, int durability = 1)
            {
                ElementPlacementDefinition cell = Cell(at); cell.definitionId = definition; cell.durability = durability;
                cell.layer = definition.StartsWith("obstacle.", StringComparison.Ordinal) ? PlacementLayer.Obstacle : PlacementLayer.Block;
                cell.instanceId = cell.layer == PlacementLayer.Obstacle ? "sample-" + at.Row + "-" + at.Column : "";
                cell.hasColor = definition == "supply.normal.fixed"; cell.rocketDirection = RocketDirection.Horizontal;
            }
            // 샘플 완료 뒤에도 일반 게임의 유효 행동이 남도록 독립 파워를 둔다.
            Place(At(8, 8), "power.rocket");
            BoardCoordinate first = At(4, 2), second = At(4, 3);
            var shape = new[] { At(3, 3), At(4, 3), At(5, 3) };
            if (id == "rocket-h" || id == "follow") shape = Enumerable.Range(3, 4).Select(row => At(row, 3)).ToArray();
            if (id == "rocket-v") { shape = Enumerable.Range(2, 4).Select(column => At(4, column)).ToArray(); first = At(3, 3); }
            if (id == "magnet") { shape = Enumerable.Range(2, 5).Select(column => At(4, column)).ToArray(); first = At(3, 4); second = At(4, 4); }
            if (id == "bomb") { shape = new[] { At(3, 2), At(3, 3), At(3, 4), At(4, 4), At(5, 4) }; first = At(2, 4); second = At(3, 4); }
            if (id == "drone") { shape = new[] { At(3, 3), At(3, 4), At(4, 3), At(4, 4) }; first = At(4, 5); second = At(4, 4); }
            bool powerAction = new[] { "activate", "combine", "damage", "each", "remaining", "remove", "hammer", "two" }.Contains(id);
            if (!powerAction)
            {
                foreach (BoardCoordinate at in shape) Cell(at).color = data.colors[0];
                Cell(second).color = data.colors[1]; Cell(first).color = data.colors[0];
                if (id != "bomb") Cell(At(3, 2)).color = data.colors[2];
                else Cell(At(6, 4)).color = data.colors[1];
                if (id == "magnet") Cell(At(3, 6)).color = data.colors[1];
                if (id == "drone") Cell(At(3, 2)).color = data.colors[1];
            }
            else
            {
                first = At(4, 0); second = At(4, 1); Place(first, "power.rocket");
                if (id == "combine") Place(second, "power.bomb");
                if (id == "two") { Cell(first).rocketDirection = RocketDirection.Vertical; Place(At(4, 7), "power.rocket"); Cell(At(4, 7)).rocketDirection = RocketDirection.Vertical; }
            }
            string crate = LegacyElementDefinitions.Get(ObstacleKind.Crate).Id.Value;
            if (new[] { "damage", "each", "remaining", "remove", "hammer" }.Contains(id)) Place(At(4, 4), crate, id == "remove" ? 1 : 3);
            if (id == "each") Place(At(4, 6), crate, 3);
            if (id == "two") { Place(At(8, 1), crate, 3); Place(At(8, 8), crate, 3); }
            if (id == "shuffle")
            {
                data.colors.Add(RabbitColor.Type5);
                foreach (ElementPlacementDefinition cell in data.elements.Where(cell => cell.hasColor))
                    cell.color = data.colors[(cell.coordinate.Row * 2 + cell.coordinate.Column) % data.colors.Count];
            }
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data), level);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":99}]}", level);
                level.name = "시험 · " + title; level.hideFlags = HideFlags.DontSave;
                level.Tutorial.seed = 12345;
                for (int column = 0; column < 9; column++)
                {
                    level.ElementSupply.sources.Add(new ElementSupplySourceDefinition { coordinate = At(0, column), mode = SupplyMode.Random });
                    level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = At(0, column), mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                        items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = data.colors[(index * 2 + column) % data.colors.Count] }).ToList() });
                }
                TutorialStepDefinition step = TutorialSampleCatalog.CreateSwap(first, second, id == "match");
                level.Tutorial.steps.Add(step);
                if (id == "description") { step.kind = TutorialStepKind.Description; step.conditions.Clear(); step.hasFirst = step.hasSecond = false; step.instructions = "설명 샘플입니다. 다음을 눌러 주세요."; }
                if (id == "area" || id == "two")
                {
                    step.actionArea = id == "two" ? new List<BoardCoordinate> { first, second, At(4, 7), At(4, 8) } : new List<BoardCoordinate> { first, second, At(3, 3), At(5, 3) };
                    step.hasFirst = step.hasSecond = false; step.conditions[0].requiredCount = id == "two" ? 2 : 1;
                    if (id == "two")
                    {
                        step.kind = TutorialStepKind.PowerSwap; step.actionDefinitionId = "power.rocket";
                        step.conditions.Add(new TutorialConditionDefinition { kind = TutorialConditionKind.DurabilityDecrease,
                            target = new TutorialTargetDefinition { kind = TutorialTargetKind.Definition, definitionId = crate }, requiredCount = 2 });
                    }
                }
                if (new[] { "rocket-h", "rocket-v", "bomb", "drone", "magnet", "follow", "activate" }.Contains(id))
                {
                    string power = id == "bomb" || id == "drone" || id == "magnet" ? "power." + id : "power.rocket";
                    step.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = id == "activate" ? TutorialConditionKind.Activated : TutorialConditionKind.Generated,
                        powerDefinitionId = power, target = new TutorialTargetDefinition(), anyDirection = id != "rocket-h" && id != "rocket-v", rocketDirection = id == "rocket-v" ? RocketDirection.Vertical : RocketDirection.Horizontal } };
                }
                if (id == "combine") step.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.Combined, target = new TutorialTargetDefinition(), allowedOrigins = new List<EffectOrigin> { EffectOrigin.RocketBomb } } };
                if (new[] { "damage", "each", "remaining", "remove" }.Contains(id))
                    step.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = id == "remove" ? TutorialConditionKind.Removed : id == "remaining" ? TutorialConditionKind.RemainingDurability : TutorialConditionKind.DurabilityDecrease,
                        target = new TutorialTargetDefinition { kind = id == "each" ? TutorialTargetKind.Definition : TutorialTargetKind.Entity, coordinate = At(4, 4), definitionId = crate },
                        aggregation = id == "each" ? TutorialDamageAggregation.Each : TutorialDamageAggregation.Total, requiredCount = id == "remaining" ? 2 : 1, allowedOrigins = new List<EffectOrigin> { EffectOrigin.Rocket } } };
                if (id == "hammer" || id == "item-swap" || id == "shuffle")
                {
                    step.kind = TutorialStepKind.Item; step.item = id == "hammer" ? BoardItem.Hammer : id == "shuffle" ? BoardItem.Shuffle : BoardItem.Swap;
                    step.hasFirst = id != "shuffle"; step.hasSecond = id == "item-swap";
                    if (id == "hammer") step.first = At(4, 4);
                    step.conditions = TutorialSampleCatalog.ItemSample(step.item).CreateConditions();
                    step.instructions = "표시된 아이템을 사용해 주세요.";
                }
                if (id == "mission")
                {
                    var condition = new TutorialConditionDefinition { kind = TutorialConditionKind.MissionProgress };
                    condition.SelectMission(0, level.Missions[0]); step.conditions = new List<TutorialConditionDefinition> { condition };
                }
                if (id == "follow")
                {
                    step.conditions[0].bindGeneratedAs = "만든 로켓";
                    TutorialStepDefinition next = TutorialSampleCatalog.CreateSwap(second, At(6, 4), false);
                    next.hasFirst = false; next.firstBinding = "만든 로켓";
                    next.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.Activated, powerDefinitionId = "power.rocket",
                        target = new TutorialTargetDefinition { kind = TutorialTargetKind.Generated, binding = "만든 로켓" } } };
                    level.Tutorial.steps.Add(next);
                }
                return level;
            }
            catch { UnityEngine.Object.DestroyImmediate(level); throw; }
        }
    }
}
