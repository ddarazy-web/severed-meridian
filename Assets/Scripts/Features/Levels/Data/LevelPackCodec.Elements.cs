using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using MemoryPack;
using UnityEngine;

namespace Levels
{
    public static partial class LevelPackCodec
    {
        private static readonly byte[] ElementMagic = { 0x45, 0x46, 0x50, 0x4b };

        public static LevelDefinition ReadLevel(byte[] bytes, int number, ElementContentData content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            LevelDefinition level = ReadLevel(bytes, number);
            level.BindContentData(content);
            return level;
        }

        // 기존 1인자 Encode는 팩1의 명시적 쓰기 계약으로 보존한다.
        public static byte[] Snapshot(LevelDefinition level) => level.HasTutorial
            ? EncodeWithTutorial(new[] { level }, level.CreateElementCatalog())
            : level.SchemaVersion == LevelDefinition.LegacySchemaVersion && level.ElementCatalog == null
                ? Encode(new[] { level }) : Encode(new[] { level }, level.CreateElementCatalog());

        public static LevelDefinition Copy(LevelDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.SchemaVersion != LevelDefinition.LegacySchemaVersion) return ReadLevel(Snapshot(source), source.LevelNumber);
            LevelDefinition copy = ScriptableObject.CreateInstance<LevelDefinition>();
            copy.hideFlags = HideFlags.HideAndDontSave;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy); return copy;
        }

        public static string CaptureJson(LevelDefinition source)
        {
            if (source.SchemaVersion == LevelDefinition.LegacySchemaVersion) return JsonUtility.ToJson(source);
            LevelDefinition copy = Copy(source);
            try { return JsonUtility.ToJson(copy); }
            finally { if (Application.isPlaying) UnityEngine.Object.Destroy(copy); else UnityEngine.Object.DestroyImmediate(copy); }
        }

        public static byte[] Encode(IEnumerable<LevelDefinition> levels, ElementCatalog catalog)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            LevelDefinition[] source = levels.ToArray();
            if (source.Any(level => level.HasTutorial)) throw new InvalidOperationException("튜토리얼 레벨은 팩3으로 저장해야 합니다.");
            return EncodeElements(source, catalog, Array.Empty<string>());
        }

        private static byte[] EncodeElements(IEnumerable<LevelDefinition> levels, ElementCatalog catalog, IEnumerable<string> references)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            LevelDefinition[] source = levels.OrderBy(level => level.LevelNumber).ToArray();
            if (source.Length == 0) throw new ArgumentException("빈 레벨 팩입니다.");
            foreach (LevelDefinition level in source)
                if (level.SchemaVersion != LevelDefinition.LegacySchemaVersion && level.SchemaVersion != LevelDefinition.CurrentSchemaVersion)
                    throw new InvalidOperationException("팩2는 스키마4·5만 변환합니다.");
            // 검사 중 임시 흐름 투영이 원본 데이터와 참조를 공유하지 않도록 값을 복사한다.
            PackedElementLevel[] data = source.Select(level => MemoryPackSerializer.Deserialize<PackedElementLevel>(
                MemoryPackSerializer.Serialize(level.ToElementPacked()))).ToArray();
            ElementLevelPack pack = new ElementLevelPack
            {
                FirstLevel = FirstLevel(data[0].LevelNumber), Levels = data,
                Definitions = DefinitionClosure(data, catalog, true, references).Select(PackedElementDefinition.FromDefinition).ToArray()
            };
            ValidateElements(pack);
            byte[] payload = MemoryPackSerializer.Serialize(pack), bytes = new byte[payload.Length + 8];
            Array.Copy(ElementMagic, bytes, 4); bytes[4] = FormatVersion;
            Array.Copy(payload, 0, bytes, 8, payload.Length); return bytes;
        }

        public static ElementLevelPack DecodeElements(byte[] bytes)
        {
            if (!IsElementPack(bytes) || bytes.Length < 8 || bytes[4] != FormatVersion || bytes[5] != 0 || bytes[6] != 0 || bytes[7] != 0)
                throw new InvalidOperationException("지원하지 않는 요소 팩 헤더입니다.");
            ElementLevelPack pack = MemoryPackSerializer.Deserialize<ElementLevelPack>(bytes.AsSpan(8));
            ValidateElements(pack); return pack;
        }

        public static LevelWithCatalog ReadLevelWithCatalog(byte[] bytes, int number)
        {
            if (IsTutorialPack(bytes)) return ReadTutorialLevel(bytes, number);
            if (!IsElementPack(bytes)) return new LevelWithCatalog(ReadLegacyLevel(bytes, number), LegacyElementDefinitions.DefaultCatalog);
            ElementLevelPack pack = DecodeElements(bytes);
            if (pack.FirstLevel != FirstLevel(number)) throw new InvalidOperationException("요청한 레벨 구간과 파일이 다릅니다.");
            PackedElementLevel data = pack.Levels.SingleOrDefault(level => level.LevelNumber == number);
            if (data == null) throw new KeyNotFoundException($"MemoryPack에 레벨 {number}이 없습니다.");
            ElementCatalog catalog = new ElementCatalog(pack.Definitions.Select(value => value.ToDefinition()));
            return new LevelWithCatalog(LevelDefinition.FromElementPacked(data, catalog), catalog);
        }

        private static bool IsElementPack(byte[] bytes) => bytes != null && bytes.Length >= 4 &&
            Enumerable.Range(0, 4).All(index => bytes[index] == ElementMagic[index]);

        private static ElementDefinition[] DefinitionClosure(IEnumerable<PackedElementLevel> levels, ElementCatalog catalog, bool includeBuiltins = false, IEnumerable<string> references = null)
        {
            HashSet<ElementId> used = new HashSet<ElementId>(); Queue<ElementId> pending = new Queue<ElementId>();
            Dictionary<ElementId, ElementDefinition> resolved = new Dictionary<ElementId, ElementDefinition>();
            Dictionary<ElementId, ElementDefinition> builtins = new Dictionary<ElementId, ElementDefinition>();
            void Include(string id) { ElementId value = new ElementId(id); if (used.Add(value)) pending.Enqueue(value); }
            if (references != null) foreach (string id in references) Include(id);
            // 매칭·연쇄·아이템은 이 생성 정의를 사용한다. 장애물 종류 전체는 포함하지 않는다.
            foreach (SupplyKind kind in new[] { SupplyKind.RandomNormal, SupplyKind.Rocket, SupplyKind.Bomb, SupplyKind.Drone, SupplyKind.Magnet })
            {
                Include(LegacyElementMap.Get(kind).Value);
                builtins.Add(LegacyElementMap.Get(kind), LegacyElementDefinitions.GetSupply(kind));
            }
            foreach (PackedElementLevel level in levels)
            {
                if (level.Elements == null || level.Supply?.sources == null) throw new ArgumentException("ID 배치·공급 목록이 없습니다.");
                foreach (ElementPlacementDefinition placement in level.Elements) Include(placement?.definitionId);
                foreach (ElementSupplySourceDefinition source in level.Supply.sources)
                {
                    if (source == null || source.items == null) throw new ArgumentException("생성구·공급 항목 목록이 없습니다.");
                    foreach (ElementSupplyItemDefinition item in source.items) Include(item?.definitionId);
                    if (source.mode != SupplyMode.Fixed || source.exhaustion == SupplyExhaustion.Random) Include(source.randomDefinitionId);
                    if (source.mode == SupplyMode.MaintainScrap) Include(level.Supply.scrapDefinitionId);
                    if (source.mode == SupplyMode.MaintainRecovery) Include(level.Supply.recoveryDefinitionId);
                }
                if (level.Missions?.Any(mission => mission.Kind == MissionKind.Recovery) == true) Include(LegacyElementMap.Get(SupplyKind.Recovery).Value);
            }
            while (pending.Count > 0)
            {
                ElementId id = pending.Dequeue();
                ElementDefinition definition = catalog.TryGet(id, out ElementDefinition selected) ? selected :
                    includeBuiltins && builtins.TryGetValue(id, out ElementDefinition builtin) ? builtin : catalog.Get(id);
                resolved.Add(id, definition); PackedElementDefinition.ValidateDefinition(definition);
                ElementSupplyProfile supply = definition.Supply;
                if (supply == null) continue;
                if (supply.ObstacleDefinitionId.HasValue) Include(supply.ObstacleDefinitionId.Value.Value);
                else if (supply.Obstacle.HasValue) Include(LegacyElementMap.Get(supply.Obstacle.Value).Value);
                foreach (ElementId choice in supply.ChoiceDefinitionIds) Include(choice.Value);
                foreach (SupplyKind choice in supply.Choices) Include(LegacyElementMap.Get(choice).Value);
            }
            return used.OrderBy(id => id.Value, StringComparer.Ordinal).Select(id => resolved[id]).ToArray();
        }

        private static void ValidateElements(ElementLevelPack pack)
        {
            if (pack == null || pack.FormatVersion != FormatVersion) throw new InvalidOperationException("지원하지 않는 요소 팩 버전입니다.");
            if (pack.Levels == null || pack.Levels.Length == 0 || pack.Levels.Length > LevelsPerPack || pack.Definitions == null)
                throw new InvalidOperationException("요소 팩 수량·정의 표 오류입니다.");
            ElementCatalog catalog = new ElementCatalog(pack.Definitions.Select(value => value == null
                ? throw new ArgumentException("팩 정의가 null입니다.") : value.ToDefinition()));
            HashSet<int> numbers = new HashSet<int>();
            foreach (PackedElementLevel data in pack.Levels)
            {
                if (data == null || data.SchemaVersion != LevelDefinition.CurrentSchemaVersion ||
                    FirstLevel(data.LevelNumber) != pack.FirstLevel || !numbers.Add(data.LevelNumber))
                    throw new InvalidOperationException("요소 팩의 스키마·번호·중복 오류입니다.");
                LevelDefinition level = LevelDefinition.FromElementPacked(data, catalog);
                try
                {
                    List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(level);
                    if (issues.Count > 0) throw new ArgumentException($"레벨 {data.LevelNumber}: " + string.Join("; ", issues));
                }
                finally { if (Application.isPlaying) UnityEngine.Object.Destroy(level); else UnityEngine.Object.DestroyImmediate(level); }
            }
            // 실행 중 생성될 정의와 프로필 참조 누락도 배치되지 않았다는 이유로 허용하지 않는다.
            DefinitionClosure(pack.Levels, catalog);
        }
    }
}
