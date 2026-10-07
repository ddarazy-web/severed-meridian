using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Board;
using Elements;
using GameScreen.Editor;
using Levels;
using Levels.Editor;
using MemoryPack;
using Simulation;
using UnityEditor;
using UnityEngine;
namespace Tutorial.Editor
{
    public static class LevelTutorialDataVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static readonly List<LevelDefinition> Owned = new List<LevelDefinition>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static void Reject(Action action, string message)
        { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Check(rejected, message); }
        public static LevelDefinition Fixture(int number = 1)
        {
            LevelDefinition level = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            level.hideFlags = HideFlags.HideAndDontSave;
            using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("levelNumber").intValue = number; data.ApplyModifiedPropertiesWithoutUndo(); }
            return level;
        }
        private static LevelDefinition Track(LevelDefinition level) { Owned.Add(level); return level; }
        public static void Populate(LevelDefinition level)
        {
            level.Tutorial.seed = 71019;
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "두 칸을 바꿔 주세요", highlights = new List<BoardCoordinate> { new BoardCoordinate(2, 2) } });
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Swap, instructions = "스와이프", hasFirst = true, first = new BoardCoordinate(2, 2), hasSecond = true, second = new BoardCoordinate(2, 3), results = new List<TutorialResultDefinition> { new TutorialResultDefinition { kind = TutorialResultKind.Generated, definitionId = "power.rocket", hasCoordinate = true, coordinate = new BoardCoordinate(2, 3) } } });
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.PowerSwap, instructions = "새 로켓 교환", actionDefinitionId = "power.rocket", hasFirst = true, first = new BoardCoordinate(2, 3), hasSecond = true, second = new BoardCoordinate(2, 4), results = new List<TutorialResultDefinition> { new TutorialResultDefinition { kind = TutorialResultKind.Activated, definitionId = "power.rocket" } } });
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, instructions = "무료 망치", item = BoardItem.Hammer, hasFirst = true, first = new BoardCoordinate(3, 3), results = new List<TutorialResultDefinition> { new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "supply.normal.fixed", count = 2 } } });
            level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = new BoardCoordinate(0, 2), mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop, items = new List<ElementSupplyItemDefinition> { new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", count = 7, color = RabbitColor.Type2 }, new ElementSupplyItemDefinition { definitionId = "power.rocket", count = 3, direction = RocketDirection.Vertical } } });
        }
        public static void Run()
        {
            Results.Clear(); int exit = 0;
            try
            {
                LevelDefinition plain = Track(Fixture()); ElementCatalog catalog = plain.CreateElementCatalog(); byte[] old = LevelPackCodec.Encode(new[] { plain }, catalog);
                Check(old.SequenceEqual(LevelPackCodec.EncodeWithTutorial(new[] { plain }, catalog)), "튜토리얼 없는 팩2 바이트 동일");
                string json = JsonUtility.ToJson(plain); int boundary = json.LastIndexOf(",\"elements\":", StringComparison.Ordinal);
                using (SHA256 hash = SHA256.Create()) Check(LevelStateBuilder.Fingerprint(plain) == BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json.Substring(0, boundary) + "}"))).Replace("-", ""), "기존 v4 지문 공식 보존");
                LevelDefinition v1 = Track(LevelPackCodec.ReadLevel(LevelPackCodec.Encode(new[] { plain }), 1)), v2 = Track(LevelPackCodec.ReadLevel(old, 1));
                Check(!v1.HasTutorial && !v2.HasTutorial, "구형 팩1/2 읽기");
                string diskPath = "Assets/Data/LevelPacks/levels-000001.bytes"; byte[] disk = File.ReadAllBytes(diskPath); LevelWithCatalog diskLevel = LevelPackCodec.ReadLevelWithCatalog(disk, 1); Track(diskLevel.Level);
                Check(disk.SequenceEqual(LevelPackCodec.EncodeWithTutorial(new[] { diskLevel.Level }, diskLevel.Catalog)), "이미 저장된 출시 팩2와 재인코딩 바이트 동일");
                Check(LevelStateBuilder.Fingerprint(v2) == BitConverter.ToString(SHA256.Create().ComputeHash(LevelPackCodec.Encode(new[] { v2 }, v2.CreateElementCatalog()))).Replace("-", ""), "튜토리얼 없는 v5 지문 공식 보존");
                LevelDefinition level = Track(Fixture()); Populate(level);
                Check(LevelDefinitionValidator.Validate(level).Count == 0, "정상 네 종류·미래 생성 파워 초기 배치 불요");
                string values = JsonUtility.ToJson(level.Tutorial); byte[] bytes = LevelPackCodec.Snapshot(level);
                Check(bytes[4] == 3, "튜토리얼 팩3 선택"); LevelDefinition restored = Track(LevelPackCodec.ReadLevel(bytes, 1));
                Check(JsonUtility.ToJson(restored.Tutorial) == values, "단계·강조·좌표·아이템·조건·공급·시드 전체 값 왕복");
                Check(JsonUtility.ToJson(Track(LevelPackCodec.Copy(level)).Tutorial) == values, "v4 Copy 보존");
                Check(JsonUtility.ToJson(Track(LevelPackCodec.Copy(restored)).Tutorial) == values, "v5 Copy 보존");
                LevelDefinition jsonCopy = Track(ScriptableObject.CreateInstance<LevelDefinition>()); JsonUtility.FromJsonOverwrite(LevelPackCodec.CaptureJson(restored), jsonCopy);
                Check(JsonUtility.ToJson(jsonCopy.Tutorial) == values && LevelDefinitionValidator.Validate(jsonCopy).Count == 0, "JSON 사본·카탈로그 보존");
                Check(JsonUtility.ToJson(Track(PuzzleEditorLaunchRequest.Capture(level, PuzzleEditorLevelSource.Asset, 123).CreateDefinition()).Tutorial) == values, "실제 Asset 실행 요청 보존");
                string fingerprint = LevelStateBuilder.Fingerprint(level); level.Tutorial.steps[0].instructions += " 수정";
                Check(LevelStateBuilder.Fingerprint(level) != fingerprint, "튜토리얼 변경을 새 입력으로 식별"); level.Tutorial.steps[0].instructions = "두 칸을 바꿔 주세요";
                Reject(() => LevelPackCodec.Encode(new[] { level }), "팩1 정보 손실 쓰기 거절"); Reject(() => LevelPackCodec.Encode(new[] { level }, catalog), "팩2 정보 손실 쓰기 거절");
                var compare = typeof(Elements.Editor.ElementPackVerification).GetMethod("State", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Check(compare.Invoke(null, new object[] { LevelStateBuilder.Build(level, 123).State }).Equals(compare.Invoke(null, new object[] { LevelStateBuilder.Build(restored, 123).State })), "Asset·팩3 전체 게임 상태·난수 동일");
                Check(LevelStateBuilder.Fingerprint(level) == LevelStateBuilder.Fingerprint(restored), "Asset·팩3 튜토리얼 지문 동일");
                Check(compare.Invoke(null, new object[] { LevelStateBuilder.Build(plain, 123).State }).Equals(compare.Invoke(null, new object[] { LevelStateBuilder.Build(level, 123).State })), "1단계 저장 추가가 기존 게임 결과를 바꾸지 않음");
                var boundaries = new List<LevelDefinition>();
                foreach (int number in new[] { 1, 50, 51, 100, 101 }) { LevelDefinition item = Track(Fixture(number)); Populate(item); boundaries.Add(item); Check(Track(LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(item), number)).LevelNumber == number, "50레벨 경계 왕복 " + number); }
                Check(LevelPackBuild.CreatePackBytes(boundaries).Count == 3 && LevelPackCodec.Address(50) == "Levels/levels-000001" && LevelPackCodec.Address(51) == "Levels/levels-000051", "기존 구간·주소 유지 · 디스크 출력 없음");
                LevelDefinition mixed = Track(Fixture(50)); byte[] mix = LevelPackCodec.EncodeWithTutorial(new[] { mixed, level }, catalog);
                Check(!Track(LevelPackCodec.ReadLevel(mix, 50)).HasTutorial && Track(LevelPackCodec.ReadLevel(mix, 1)).HasTutorial, "혼합 구간 메타데이터 대응");
                byte[] header = (byte[])bytes.Clone(); header[4] = 4; Reject(() => LevelPackCodec.ReadLevel(header, 1), "헤더 버전 거절"); Reject(() => LevelPackCodec.ReadLevel(bytes.Take(bytes.Length / 2).ToArray(), 1), "잘린 봉투 거절");
                PackedTutorialLevelPack envelope = LevelPackCodec.DecodeTutorial(mix); envelope.Tutorials = envelope.Tutorials.Take(1).ToArray(); Reject(() => LevelPackCodec.ReadLevel(Wrap(envelope), 1), "누락 메타데이터 거절");
                envelope = LevelPackCodec.DecodeTutorial(mix); envelope.Tutorials[1].LevelNumber = envelope.Tutorials[0].LevelNumber; Reject(() => LevelPackCodec.ReadLevel(Wrap(envelope), 1), "중복 메타데이터 거절");
                envelope = LevelPackCodec.DecodeTutorial(bytes); envelope.FirstLevel = 51; Reject(() => LevelPackCodec.ReadLevel(Wrap(envelope), 1), "봉투·내부 구간 불일치 거절");
                VerifyValidation(level); VerifyClosure(level, catalog);
                LevelDefinition memory = Track(Fixture(100001)); Populate(memory); string path = LevelPackBuild.FilePath(memory.LevelNumber);
                if (File.Exists(path) || File.Exists(path + ".meta")) throw new InvalidOperationException("시험 경로 이미 존재");
                try { File.WriteAllBytes(path, LevelPackCodec.Snapshot(memory)); Check(JsonUtility.ToJson(Track(PuzzleEditorLaunchRequest.Capture(memory, PuzzleEditorLevelSource.MemoryPack, 123).CreateDefinition()).Tutorial) == values, "실제 MemoryPack 실행 요청 보존"); }
                finally { File.Delete(path); }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally { foreach (LevelDefinition level in Owned) if (level != null) UnityEngine.Object.DestroyImmediate(level); Owned.Clear(); File.WriteAllLines("Logs/Tutorial/Stage01/data-results.txt", Results); }
            EditorApplication.Exit(exit);
        }
        private static byte[] Wrap(PackedTutorialLevelPack pack)
        { byte[] payload = MemoryPackSerializer.Serialize(pack), bytes = new byte[payload.Length + 8]; bytes[0] = 0x54; bytes[1] = 0x46; bytes[2] = 0x50; bytes[3] = 0x4b; bytes[4] = 3; Array.Copy(payload, 0, bytes, 8, payload.Length); return bytes; }
        private static void VerifyValidation(LevelDefinition original)
        {
            void Invalid(Action<LevelDefinition> change, string message)
            { LevelDefinition level = Track(LevelPackCodec.Copy(original)); change(level); Check(LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.InvalidTutorial && issue.PropertyPath.StartsWith("tutorial")), message); Reject(() => LevelPackCodec.Snapshot(level), message + " 저장 거절"); }
            Invalid(level => level.Tutorial.steps[1].first = new BoardCoordinate(9, 0), "범위 밖 대상"); Invalid(level => level.Tutorial.steps[1].second = new BoardCoordinate(4, 4), "비인접 교환");
            Invalid(level => level.Tutorial.steps[2].actionDefinitionId = "missing", "누락 파워 ID"); Invalid(level => level.Tutorial.steps[3].hasSecond = true, "망치 복수 대상"); Invalid(level => level.Tutorial.steps[3].item = (BoardItem)99, "아이템 종류 오류");
            Invalid(level => level.Tutorial.steps[1].first = new BoardCoordinate(0, 0), "초기 무작위 대상 불가"); Invalid(level => level.Tutorial.supply.sources[0].mode = SupplyMode.Random, "무작위 공급 방식"); Invalid(level => level.Tutorial.supply.sources[0].items[0].definitionId = "supply.normal.random", "무작위 공급 정의");
            Invalid(level => level.Tutorial.supply.sources[0].items[0].count = 0, "공급 수량 오류"); Invalid(level => level.Tutorial.supply.sources[0].items[0].color = (RabbitColor)99, "공급 색 오류"); Invalid(level => level.Tutorial.steps[1].results[0].definitionId = "missing", "조건 ID 누락"); Invalid(level => level.Tutorial.steps[1].results[0].count = 0, "조건 수량 오류");
            Invalid(level => { using SerializedObject input = new SerializedObject(level); input.FindProperty("moveCount").intValue = 1; input.ApplyModifiedPropertiesWithoutUndo(); }, "부족한 이동 수");
            Invalid(level => { using SerializedObject input = new SerializedObject(level); input.FindProperty("board").FindPropertyRelative("cells").GetArrayElementAtIndex(20).FindPropertyRelative("isActive").boolValue = false; input.ApplyModifiedPropertiesWithoutUndo(); }, "비활성 칸");
            Invalid(level => { using SerializedObject input = new SerializedObject(level); SerializedProperty walls = input.FindProperty("flow").FindPropertyRelative("walls"); walls.arraySize++; SerializedProperty wall = walls.GetArrayElementAtIndex(walls.arraySize - 1); wall.FindPropertyRelative("a").FindPropertyRelative("row").intValue = 2; wall.FindPropertyRelative("a").FindPropertyRelative("column").intValue = 2; wall.FindPropertyRelative("b").FindPropertyRelative("row").intValue = 2; wall.FindPropertyRelative("b").FindPropertyRelative("column").intValue = 3; input.ApplyModifiedPropertiesWithoutUndo(); }, "교환 사이 벽");
        }
        private static void VerifyClosure(LevelDefinition level, ElementCatalog catalog)
        {
            PackedElementDefinition body = PackedElementDefinition.FromDefinition(catalog.Get(new ElementId("obstacle.scrap"))); body.id = "tutorial.test.body";
            PackedElementDefinition supply = PackedElementDefinition.FromDefinition(catalog.Get(new ElementId("supply.scrap"))); supply.id = "tutorial.test.supply"; supply.supply.hasObstacle = false; supply.supply.obstacleDefinitionId = body.id;
            ElementCatalog custom = new ElementCatalog(catalog.Definitions.Concat(new[] { body.ToDefinition(), supply.ToDefinition() })); LevelDefinition test = Track(LevelPackCodec.Copy(level));
            test.Tutorial.supply.sources[0].items.Add(new ElementSupplyItemDefinition { definitionId = supply.id, count = 2, durability = 3 }); test.Tutorial.steps[3].results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = body.id, count = 2 });
            byte[] bytes = LevelPackCodec.EncodeWithTutorial(new[] { test }, custom); LevelWithCatalog read = LevelPackCodec.ReadLevelWithCatalog(bytes, 1); Track(read.Level);
            Check(read.Catalog.Get(new ElementId(supply.id)) != null && read.Catalog.Get(new ElementId(body.id)) != null, "미배치 공급·결과·생성 본체 정의 폐쇄");
            PackedTutorialLevelPack envelope = LevelPackCodec.DecodeTutorial(bytes); ElementLevelPack inner = LevelPackCodec.DecodeElements(envelope.ElementPack); inner.Definitions = inner.Definitions.Where(item => item.id != body.id).ToArray(); byte[] payload = MemoryPackSerializer.Serialize(inner);
            envelope.ElementPack = new byte[payload.Length + 8]; envelope.ElementPack[0] = 0x45; envelope.ElementPack[1] = 0x46; envelope.ElementPack[2] = 0x50; envelope.ElementPack[3] = 0x4b; envelope.ElementPack[4] = 2; Array.Copy(payload, 0, envelope.ElementPack, 8, payload.Length); Reject(() => LevelPackCodec.ReadLevel(Wrap(envelope), 1), "팩 내 튜토리얼 생성 본체 누락 거절");
        }
    }
}




