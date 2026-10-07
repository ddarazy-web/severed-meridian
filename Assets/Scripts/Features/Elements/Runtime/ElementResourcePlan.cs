using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;
using Simulation;

namespace Elements
{
    /// <summary>현재 배치와 공급/생성 관계에서 도달 가능한 표현만 준비한다. 실행 상태는 소비하지 않는다.</summary>
    public sealed class ElementResourcePlan
    {
        public ReadOnlyCollection<ElementId> Definitions { get; }
        public ReadOnlyCollection<string> Paths { get; }
        public ReadOnlyCollection<string> Addresses { get; }
        public ReadOnlyCollection<ElementVisualFrame> Frames { get; }
        private readonly Dictionary<string, ElementId[]> owners;
        private ElementResourcePlan(HashSet<ElementId> definitions, HashSet<string> paths, Dictionary<string, HashSet<ElementId>> owners, HashSet<ElementVisualFrame> frames)
        {
            this.owners = owners.ToDictionary(pair => pair.Key, pair => pair.Value.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            Definitions = Array.AsReadOnly(definitions.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray());
            Paths = Array.AsReadOnly(paths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
            Addresses = Array.AsReadOnly(Paths.Select(BoardSpriteAtlas.AddressFor).Distinct().OrderBy(address => address, StringComparer.Ordinal).ToArray());
            Frames = Array.AsReadOnly(frames.OrderBy(frame => frame.Path, StringComparer.Ordinal).ThenBy(frame => frame.SheetColumns)
                .ThenBy(frame => frame.SheetRows).ThenBy(frame => frame.SheetFrame).ToArray());
        }

        public void ValidateResources(Func<string, bool> isAvailable)
        {
            if (isAvailable == null) throw new ArgumentNullException(nameof(isAvailable));
            foreach (string path in Paths)
            {
                try
                {
                    if (!isAvailable(path)) throw new InvalidOperationException("Sprite가 없습니다: " + path);
                }
                catch (InvalidOperationException error)
                {
                    string ids = owners.TryGetValue(path, out ElementId[] selected)
                        ? string.Join(", ", selected.Select(id => id.Value)) : "보드/미션 장식";
                    throw new InvalidOperationException($"요소 ID [{ids}] 자원 준비 실패: {path}", error);
                }
            }
        }

        public static ElementResourcePlan Create(LevelRuntimeState state, ElementVisualCatalog visuals)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (visuals == null) throw new ArgumentNullException(nameof(visuals));
            HashSet<ElementId> visited = new HashSet<ElementId>();
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            HashSet<ElementVisualFrame> frames = new HashSet<ElementVisualFrame>();
            Dictionary<string, HashSet<ElementId>> owners = new Dictionary<string, HashSet<ElementId>>(StringComparer.Ordinal);
            Queue<ElementDefinition> pending = new Queue<ElementDefinition>();
            void Enqueue(ElementDefinition definition) { if (definition != null) pending.Enqueue(definition); }
            void Add(string path) { if (path != null) paths.Add(path); }
            void AddOwned(string path, ElementId id)
            {
                Add(path);
                if (!owners.TryGetValue(path, out HashSet<ElementId> selected)) owners.Add(path, selected = new HashSet<ElementId>());
                selected.Add(id);
            }

            foreach (RuntimeCell cell in state.Cells.Where(cell => cell.IsActive))
            {
                // 곰팡이에 가려진 내용물도 제거 후 다시 나타날 수 있다.
                Enqueue(cell.ContentElement ?? LegacyElementDefinitions.GetContent(cell.Content, state.ElementCatalog));
                if (cell.Cover.HasValue) Enqueue(cell.CoverElement ?? LegacyElementDefinitions.Get(cell.Cover.Value));
                if (cell.DustDurability > 0) Enqueue(cell.DustElement ?? LegacyElementDefinitions.GetDust());
            }
            foreach (RuntimeObstacle body in state.Obstacles) Enqueue(body.Element);
            foreach (RuntimeSource source in state.Supply.Sources)
            {
                if (source.Mode == SupplyMode.Fixed)
                {
                    if (source.ItemDefinitions != null) foreach (ElementDefinition definition in source.ItemDefinitions) Enqueue(definition);
                    else foreach (SupplyItem item in source.Items) Enqueue(LegacyElementDefinitions.GetSupply(item.Kind));
                }
                if (source.Mode != SupplyMode.Fixed || source.Exhaustion == SupplyExhaustion.Random)
                    Enqueue(source.RandomDefinition ?? LegacyElementDefinitions.GetSupply(SupplyKind.RandomNormal));
                if (source.Mode == SupplyMode.MaintainScrap)
                    Enqueue(state.Supply.ScrapDefinition ?? LegacyElementDefinitions.GetSupply(SupplyKind.Scrap));
                if (source.Mode == SupplyMode.MaintainRecovery)
                    Enqueue(state.Supply.RecoveryDefinition ?? LegacyElementDefinitions.GetSupply(SupplyKind.Recovery));
            }
            // 매칭·부스터·조합은 초기 파워 배치와 관계없이 이 기본 행동을 만들 수 있다.
            foreach (SupplyKind kind in new[] { SupplyKind.RandomNormal, SupplyKind.Rocket, SupplyKind.Bomb, SupplyKind.Drone, SupplyKind.Magnet })
                Enqueue(state.ElementCatalog.Get(LegacyElementMap.Get(kind)));
            while (pending.Count > 0)
            {
                ElementDefinition definition = pending.Dequeue();
                if (!visited.Add(definition.Id)) continue;
                ElementSupplyProfile supply = definition.Supply;
                if (supply?.Behavior == ElementSupplyBehavior.Obstacle)
                {
                    Enqueue(ElementSupplyBehaviorRegistry.ResolveBody(supply, state.ElementCatalog));
                    continue;
                }
                if (supply?.Behavior == ElementSupplyBehavior.RandomPower)
                {
                    foreach (ElementId id in supply.ChoiceDefinitionIds) Enqueue(state.ElementCatalog.Get(id));
                    foreach (SupplyKind kind in supply.Choices) Enqueue(state.ElementCatalog.Get(LegacyElementMap.Get(kind)));
                    continue;
                }
                ElementVisualDefinition visual = visuals.Get(definition.Id);
                foreach (ElementId id in visual.Generates) Enqueue(state.ElementCatalog.Get(id));
            }
            ElementVisualPreparation.Validate(state, visuals, visited, (definition, frame) =>
            {
                frames.Add(frame);
                AddOwned(frame.Path, definition.Id);
                foreach (string effect in frame.Effects) AddOwned(effect, definition.Id);
            });
            Add(GameScreen.PuzzleArtworkPaths.Floor);
            foreach (RuntimeMission mission in state.Missions) Add(GameScreen.PuzzleArtworkPaths.Mission(mission.Definition));
            if (state.Flow.Walls.Count > 0) Add(GameScreen.PuzzleArtworkPaths.Wall(false));
            if (state.Flow.Portals.Count > 0) Add(GameScreen.PuzzleArtworkPaths.Portal(0, false));
            if (state.Flow.Arrivals.Count > 0) Add(GameScreen.PuzzleArtworkPaths.Arrival);
            if (state.Connections.Count > 0) Add(GameScreen.PuzzleArtworkPaths.Wire(false));
            return new ElementResourcePlan(visited, paths, owners, frames);
        }
        public void ValidateFrames(Func<ElementVisualFrame, bool> isAvailable)
        {
            foreach (ElementVisualFrame frame in Frames)
            {
                try
                {
                    if (!isAvailable(frame)) throw new InvalidOperationException("프레임 Sprite가 없습니다.");
                }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is UnityEngine.UnityException)
                {
                    string ids = string.Join(", ", owners[frame.Path].Select(id => id.Value));
                    throw new InvalidOperationException($"요소 ID [{ids}] 시트 프레임 준비 실패: {frame.Path} / {frame.SheetFrame}", error);
                }
            }
        }
    }
}
