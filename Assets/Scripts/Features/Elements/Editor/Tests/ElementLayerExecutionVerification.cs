using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>신규 층 정의의 피해량·미션·번식과 작업 사본의 독립성을 검사한다.</summary>
    public static class ElementLayerExecutionVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static TurnEffectContext Context() => (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);
        private static object Invoke(string name, string method, params object[] arguments) => typeof(LevelRuntimeState).Assembly
            .GetType("Simulation." + name).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
        private static ElementDefinition Layer(string id, ElementLayerBehavior behavior, MissionKind mission, int damage, ElementTurnProfile turn = null) =>
            new ElementDefinition(new ElementId(id), "확장 층", new ElementPlacementProfile(1, 3), null, null, null, null, null, null,
                new ElementLayerProfile(behavior, mission, damage), turn);

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                ElementDefinition web = Layer("cover.fixture.web", ElementLayerBehavior.CoverDurability, MissionKind.Web, 2);
                ElementDefinition dust = Layer("floor.fixture.dust", ElementLayerBehavior.NormalConsumption, MissionKind.Dust, 2);
                ElementDefinition mold = Layer("cover.fixture.mold", ElementLayerBehavior.CoverRemoval, MissionKind.Mold, 1,
                    new ElementTurnProfile(ElementTurnBehavior.AdjacentCoverSpread, 3));
                ElementCatalog catalog = new ElementCatalog(new[] { web, dust, mold });
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"initialBlocks\":[],\"obstacles\":[],\"covers\":[],\"dust\":[],\"elements\":[" +
                    "{\"definitionId\":\"cover.fixture.web\",\"layer\":2,\"coordinate\":{\"row\":2,\"column\":2},\"durability\":3}," +
                    "{\"definitionId\":\"floor.fixture.dust\",\"layer\":3,\"coordinate\":{\"row\":2,\"column\":4},\"durability\":3}," +
                    "{\"definitionId\":\"cover.fixture.mold\",\"layer\":2,\"coordinate\":{\"row\":5,\"column\":5},\"durability\":3}]," +
                    "\"missions\":[{\"kind\":2,\"count\":1},{\"kind\":4,\"count\":1},{\"kind\":8,\"count\":1}]}", level);
                string original = JsonUtility.ToJson(level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 857, catalog);
                Check(built.IsBuilt, "신규 거미줄/먼지/곰팡이 정의 구성 " + string.Join(";", built.Issues));
                LevelRuntimeState state = built.State;
                RuntimeCell webCell = state.CellAt(new BoardCoordinate(2, 2)), dustCell = state.CellAt(new BoardCoordinate(2, 4));
                Check(DamageReaction.Evaluate(state, webCell.Coordinate, DamageCause.Power, webCell.Coordinate, Context()).Amount == 2,
                    "신규 거미줄 정의의 실제 피해량2 조회");
                Invoke("WebRules", "Apply", state, webCell, Context());
                Check(webCell.CoverDurability == 1 && webCell.Content == RuntimeContent.Normal, "신규 거미줄 피해2·내용물 보존");
                Invoke("WebRules", "Apply", state, webCell, Context());
                Check(!webCell.Cover.HasValue && state.Missions[0].Progress == 1, "신규 거미줄 제거 미션 완료");
                Check(MissionProgressRules.Query(state, dustCell.Coordinate, Context()).Any(item => item.MissionIndex == 1 && item.Damage == 2),
                    "신규 먼지 정책의 미션 기여 피해2");
                Invoke("DustRules", "ConsumeNormal", state, dustCell, Context());
                Check(dustCell.DustDurability == 1 && state.Missions[1].Progress == 0, "신규 먼지 실제 피해2");
                MoldSpreadRecord spread = (MoldSpreadRecord)Invoke("MoldRules", "FinishTurn", state, Context());
                Check(spread.Reason == MoldSpreadReason.Spread && state.CellAt(spread.Target.Value).CoverDurability == 3,
                    "신규 곰팡이 번식 초기 내구도3 적용");
                PropertyInfo selected = typeof(RuntimeCell).GetProperty("CoverElement", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(ReferenceEquals(selected.GetValue(state.CellAt(spread.Target.Value)), mold), "번식한 곰팡이의 정의 ID 유지");
                LevelRuntimeState copy = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { state }, null);
                RuntimeCell copied = copy.CellAt(spread.Target.Value);
                Invoke("MoldRules", "Remove", copy, copied, Context());
                Check(!copied.Cover.HasValue && state.CellAt(spread.Target.Value).Cover.HasValue &&
                    copy.Missions[2].Progress == 1 && state.Missions[2].Progress == 0, "선택 정의를 공유해도 사본 층/미션 상태 독립");
                Check(JsonUtility.ToJson(level) == original, "층·번식·사본 실행은 제작 원본 불변");
                ElementDefinition alternative = Layer("cover.fixture.dust-mission", ElementLayerBehavior.CoverDurability, MissionKind.Dust, 2);
                ElementCatalog alternativeCatalog = new ElementCatalog(new[] { alternative });
                JsonUtility.FromJsonOverwrite("{\"elements\":[{\"definitionId\":\"cover.fixture.dust-mission\",\"layer\":2," +
                    "\"coordinate\":{\"row\":2,\"column\":2},\"durability\":3}],\"missions\":[{\"kind\":4,\"count\":1}]}", level);
                built = LevelStateBuilder.Build(level, 857, alternativeCatalog);
                Check(built.IsBuilt, "덮개 표시와 독립된 신규 먼지 미션 공급량으로 구성 " + string.Join(";", built.Issues));
                webCell = built.State.CellAt(new BoardCoordinate(2, 2));
                Invoke("WebRules", "Apply", built.State, webCell, Context());
                Invoke("WebRules", "Apply", built.State, webCell, Context());
                Check(built.State.Missions[0].Progress == 1, "같은 덮개 행동으로 선택 정의의 먼지 미션 완료");
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":2,\"count\":1}]}", level);
                Check(!LevelStateBuilder.Build(level, 857, alternativeCatalog).IsBuilt,
                    "표시가 거미줄이어도 실제 공급하지 않는 거미줄 미션 거절");
                ElementDefinition moldMission = Layer("cover.fixture.mold-mission", ElementLayerBehavior.CoverDurability, MissionKind.Mold, 2);
                JsonUtility.FromJsonOverwrite("{\"elements\":[{\"definitionId\":\"cover.fixture.mold-mission\",\"layer\":2," +
                    "\"coordinate\":{\"row\":2,\"column\":2},\"durability\":3}],\"missions\":[{\"kind\":8,\"count\":0}]}", level);
                built = LevelStateBuilder.Build(level, 857, new ElementCatalog(new[] { moldMission }));
                Check(built.IsBuilt && built.State.Missions[0].Target == 1 && built.State.Missions[0].Remaining == 1,
                    "동적 곰팡이 목표도 실제 선택 층 정의로 초기화");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines("Logs/ElementFramework/Phase03/layer-execution-results.txt", Results);
            }
            EditorApplication.Exit(exit);
        }
    }
}
