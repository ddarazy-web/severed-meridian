using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Simulation;

namespace Elements
{
    /// <summary>신형 공급 값을 실행용 수량/커서와 불변 선택 정의로 나눈다.</summary>
    internal sealed class ElementLevelSupplyLayout
    {
        internal LevelSupplyDefinition Value { get; }
        internal Dictionary<BoardCoordinate, ElementDefinition[]> Items { get; } = new Dictionary<BoardCoordinate, ElementDefinition[]>();
        internal Dictionary<BoardCoordinate, ElementDefinition> Random { get; } = new Dictionary<BoardCoordinate, ElementDefinition>();
        internal ElementDefinition Scrap { get; }
        internal ElementDefinition Recovery { get; }
        private readonly ElementCatalog catalog;

        internal ElementLevelSupplyLayout(ElementLevelSupplyDefinition input, ElementCatalog catalog, List<LevelValidationIssue> issues)
        {
            this.catalog = catalog;
            List<SupplySourceDefinition> sources = new List<SupplySourceDefinition>();
            if (input?.sources == null)
            {
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, "신형 공급 목록이 없습니다.", "elementSupply"));
                Value = new LevelSupplyDefinition(); return;
            }
            for (int i = 0; i < input.sources.Count; i++)
            {
                ElementSupplySourceDefinition source = input.sources[i]; string path = $"elementSupply.sources.Array.data[{i}]";
                try
                {
                    if (source == null || source.items == null) throw new ArgumentException("생성구/고정 공급 목록이 없습니다.");
                    List<SupplyItem> values = new List<SupplyItem>(); List<ElementDefinition> definitions = new List<ElementDefinition>();
                    foreach (ElementSupplyItemDefinition item in source.items)
                    {
                        if (item == null) throw new ArgumentException("고정 공급 항목이 null입니다.");
                        ElementDefinition definition = Require(item.definitionId);
                        definitions.Add(definition);
                        if (definition.RequireSupply().Behavior == ElementSupplyBehavior.Obstacle) Body(definition);
                        values.Add(new SupplyItem(Kind(definition), item.count, item.color, item.direction, item.durability));
                    }
                    if (source.mode != SupplyMode.Fixed || source.exhaustion == SupplyExhaustion.Random)
                    {
                        ElementDefinition random = Require(source.randomDefinitionId);
                        if (random.RequireSupply().Behavior != ElementSupplyBehavior.RandomNormal)
                            throw new ArgumentException($"생성구 '{source.randomDefinitionId}'의 기본 공급은 무작위 일반 블록이어야 합니다.");
                        Random.Add(source.coordinate, random);
                    }
                    Items.Add(source.coordinate, definitions.ToArray());
                    sources.Add(new SupplySourceDefinition(source.coordinate, source.mode, source.exhaustion, values));
                }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
                { issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, error.Message, path, source?.coordinate)); }
            }
            try
            {
                if (input.sources.Any(source => source?.mode == SupplyMode.MaintainScrap))
                {
                    Scrap = Require(input.scrapDefinitionId);
                    if (Scrap.RequireSupply().Behavior != ElementSupplyBehavior.Obstacle || Body(Scrap).RequireRemovalMissionProfile().Kind != MissionKind.Scrap)
                        throw new ArgumentException($"유지 공급 '{input.scrapDefinitionId}'는 고철 제거 본체를 생성해야 합니다.");
                }
                if (input.sources.Any(source => source?.mode == SupplyMode.MaintainRecovery))
                {
                    Recovery = Require(input.recoveryDefinitionId);
                    if (Recovery.RequireSupply().Behavior != ElementSupplyBehavior.Recovery)
                        throw new ArgumentException($"유지 공급 '{input.recoveryDefinitionId}'는 회수 부품을 생성해야 합니다.");
                }
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
            { issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, error.Message, "elementSupply")); }
            Value = new LevelSupplyDefinition(sources, input.scrapTarget, input.scrapLimit, input.scrapDurability, input.recoveryTarget);
        }

        private ElementDefinition Require(string id)
        {
            try
            {
                ElementDefinition result = catalog.Get(new ElementId(id));
                PackedElementDefinition.ValidateDefinition(result);
                ElementSupplyProfile profile = result.RequireSupply();
                foreach (SupplyKind kind in profile.Choices)
                {
                    ElementDefinition choice = catalog.Get(LegacyElementMap.Get(kind));
                    if (choice.RequireSupply().Behavior != ElementSupplyBehavior.Power)
                        throw new ArgumentException($"선택 정의 ID '{choice.Id.Value}'가 파워 생성이 아닙니다.");
                }
                return result;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
            { throw new ArgumentException($"공급 정의 ID '{id ?? "<null>"}': {error.Message}", nameof(id), error); }
        }
        internal ElementDefinition Body(ElementDefinition supply)
        {
            ElementSupplyProfile profile = supply.RequireSupply();
            ElementDefinition result = ElementSupplyBehaviorRegistry.ResolveBody(profile, catalog);
            PackedElementDefinition.ValidateDefinition(result);
            if (result.ReactionBehavior != ElementReactionBehavior.Durability || result.RequirePlacement().Size != 1)
                throw new ArgumentException($"공급 본체 '{result.Id.Value}'는 한 칸 내구도 본체여야 합니다.");
            return result;
        }
        internal int MaxDurability(int source, int item)
        {
            ElementDefinition definition = Items[Value.Sources[source].Coordinate][item];
            return definition.RequireSupply().Behavior == ElementSupplyBehavior.Obstacle ? Body(definition).RequirePlacement().MaxDurability : 5;
        }
        internal int ScrapMaximum => Scrap == null ? 5 : Body(Scrap).RequirePlacement().MaxDurability;
        internal string ItemError(LevelDefinition level, int source, int item)
        {
            SupplyItem value = Value.Sources[source].Items[item]; ElementDefinition definition = Items[Value.Sources[source].Coordinate][item];
            string error = LevelSupplyRules.ItemError(level, value, () => MaxDurability(source, item));
            if (error == "고철 내구도는 1~5입니다.")
                error = $"공급 본체 내구도는 1~{MaxDurability(source, item)}입니다.";
            if (error == null && definition.RequireSupply().Behavior == ElementSupplyBehavior.Obstacle &&
                Body(definition).ColorMatchPolicy?.RequiresMatchingColor == true &&
                (!Enum.IsDefined(typeof(RabbitColor), value.Color) || level.Colors?.Contains(value.Color) != true))
                error = "공급 본체의 지정색은 레벨 사용 색이어야 합니다.";
            return error == null ? null : $"공급 정의 ID '{definition.Id.Value}': {error}";
        }
        internal MissionKind? Mission(ElementDefinition supply) => ElementSupplyBehaviorRegistry.Mission(supply, catalog);
        internal bool HasFixed(SupplySourceDefinition source, SupplyKind kind) => source.Mode == SupplyMode.Fixed &&
            Items.TryGetValue(source.Coordinate, out ElementDefinition[] values) && values.Any(definition =>
                Mission(definition) == (kind == SupplyKind.Scrap ? MissionKind.Scrap : MissionKind.Recovery));
        internal long Fixed(MissionKind mission) => Value.Sources.Where(source => source.Mode == SupplyMode.Fixed)
            .Sum(source => source.Items.Select((item, index) => Mission(Items[source.Coordinate][index]) == mission ? (long)item.Count : 0).Sum());

        private static SupplyKind Kind(ElementDefinition definition)
        {
            ElementSupplyProfile profile = definition.RequireSupply();
            return profile.Behavior switch
            {
                ElementSupplyBehavior.RandomNormal => SupplyKind.RandomNormal,
                ElementSupplyBehavior.FixedNormal => SupplyKind.FixedNormal,
                ElementSupplyBehavior.Recovery => SupplyKind.Recovery,
                ElementSupplyBehavior.Obstacle => SupplyKind.Scrap,
                ElementSupplyBehavior.RandomPower => SupplyKind.RandomPower,
                ElementSupplyBehavior.Power => profile.Content switch
                {
                    RuntimeContent.Rocket => SupplyKind.Rocket, RuntimeContent.Bomb => SupplyKind.Bomb,
                    RuntimeContent.Drone => SupplyKind.Drone, RuntimeContent.Magnet => SupplyKind.Magnet,
                    _ => throw new ArgumentException($"파워 공급 '{definition.Id.Value}'의 생성 설정이 잘못됐습니다.")
                },
                _ => throw new ArgumentException($"공급 '{definition.Id.Value}'의 행동이 잘못됐습니다.")
            };
        }
    }
}
