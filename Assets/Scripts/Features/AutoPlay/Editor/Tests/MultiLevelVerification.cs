using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoPlay;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    [MemoryPackable]
    public partial class PackageRoundTripFixture
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    /// <summary>설치한 패키지와 다중 레벨 실행을 작은 표본으로 확인한다. 추천 전체 20,000판은 실행하지 않는다.</summary>
    public static class MultiLevelVerification
    {
        private static readonly List<string> results = new List<string>();
        private static readonly List<LevelDefinition> owned = new List<LevelDefinition>();
        private static MultiLevelTestSession session;
        private static MultiLevelTestStore store;
        private static double deadline;
        private static string original;
        public static void Start()
        {
            try
            {
                PackageRoundTripFixture value = new PackageRoundTripFixture { Name = "달토끼", Value = 7 };
                PackageRoundTripFixture restored = MemoryPackSerializer.Deserialize<PackageRoundTripFixture>(MemoryPackSerializer.Serialize(value));
                Check(restored.Name == value.Name && restored.Value == 7, "MemoryPack 생성기·직렬화 왕복");
                Check(ZString.Format("{0}:{1}", restored.Name, restored.Value) == "달토끼:7", "ZString 문자열 생성");
                Check(UniTask.FromResult(7).GetAwaiter().GetResult() == 7, "UniTask 완료 값");
                string run = File.ReadAllText("Logs/Stage31Workflow/full-run-path.txt").Trim();
                string id = File.ReadAllText(run + "/records/latest.txt").Trim();
                BotMoveBalanceRecord saved = JsonUtility.FromJson<BotMoveBalanceRecord>(File.ReadAllText(run + "/records/" + id + "/balance.json"));
                foreach (string name in new[] { "첫 레벨", "오류 레벨", "마지막 레벨" })
                {
                    LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>(); level.name = name; owned.Add(level);
                    JsonUtility.FromJsonOverwrite(saved.definitionJson, level);
                    JsonUtility.FromJsonOverwrite("{\"moveCount\":" + (name == "오류 레벨" ? 0 : 1) + "}", level);
                }
                original = JsonUtility.ToJson(owned[0]);
                store = new MultiLevelTestStore("Logs/MultiLevelVerification/run-" + Guid.NewGuid().ToString("N"));
                session = new MultiLevelTestSession(owned, MultiLevelTestMode.Repeat, 1, store);
                Check(session.Record.entries.Count == 3, "선택한 세 레벨을 순서대로 복사");
                session.SetPaused(true); session.Advance(); Check(session.Index == 0, "시작 전 일시정지 유지"); session.SetPaused(false);
                deadline = EditorApplication.timeSinceStartup + 120;
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("소규모 시험 시간 초과");
                for (int step = 0; step < 100 && session.CanContinue; step++) session.Advance();
                if (session.CanContinue) return;
                Check(session.Record.entries[0].status == MultiLevelTestStatus.Completed && session.Record.entries[1].status == MultiLevelTestStatus.Error &&
                    session.Record.entries[2].status == MultiLevelTestStatus.Completed, "오류 레벨을 분리하고 다음 레벨 완료");
                for (int index = 0; index < 3; index += 2)
                {
                    MultiLevelTestEntry entry = session.Record.entries[index];
                    BotAnalysisReader reader = new BotAnalysisReader(Path.Combine(store.LevelRoot(session.Record, index), entry.resultId));
                    while (!reader.IsDone) reader.Advance();
                    Check(reader.Error == null && reader.Games.Count == 2, "레벨별 두 전략 결과 분리 " + index);
                }
                Check(JsonUtility.ToJson(owned[0]) == original, "원본 레벨 불변");
                Check(store.Load().entries.Count(e => e.status == MultiLevelTestStatus.Completed) == 2, "재조회에서 완료 결과 보존");
                session.Dispose();
                session = new MultiLevelTestSession(new[] { owned[0], owned[2] }, MultiLevelTestMode.Balance, 100, store);
                session.Advance(); session.SetPaused(true); session.Stop();
                Check(session.Record.entries.All(e => e.status == MultiLevelTestStatus.Stopped), "추천 모드 중지는 대기 레벨도 중지");
                Check(store.Load().entries.All(e => e.status == MultiLevelTestStatus.Stopped), "추천 시험 중지 기록 재조회");
                results.Add("DATA 신규 플레이 4판 · 추천 모드 플레이 0판"); Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick; session?.Dispose();
            foreach (LevelDefinition level in owned) UnityEngine.Object.DestroyImmediate(level);
            if (error != null) results.Add("FAIL " + error);
            File.WriteAllLines("Logs/MultiLevelVerification/results.txt", results);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
