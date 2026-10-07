using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>서로 다른 ID와 공유 별칭, 상태 오류와 원본 독립성을 실제 조회로 검사한다.</summary>
    public static class ElementVisualVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static Type dtoType, catalogType, resolverType, stateType;
        internal static ElementVisualEffectDto[] FixtureEffects(string visualKey)
            => Array.Find(LegacyElementVisuals.Catalog.ToDto().definitions, definition => definition.key == visualKey).states[0].effectAnimations;
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static object Dto(string json) => JsonUtility.FromJson(json, dtoType);
        private static object Catalog(object dto) => catalogType.GetMethod("FromDto").Invoke(null, new[] { dto });
        private static object State(int durability = 1, int frame = 0, int color = 0, int size = 1)
            => Activator.CreateInstance(stateType, new object[] { color, durability, 0, 0, 0, frame, size });
        private static object Resolve(object resolver, string id, object state)
            => resolverType.GetMethod("Resolve").Invoke(resolver, new[] { (object)new ElementId(id), state });
        private static T Value<T>(object value, string property) => (T)value.GetType().GetProperty(property).GetValue(value);
        private static void Reject(Action action, string id)
        {
            Exception found = null;
            try { action(); } catch (Exception error) { found = error is TargetInvocationException ? error.InnerException : error; }
            Check(found != null && found.Message.Contains(id), "명시적 시각 오류 " + id);
        }
        private const string Fixture = "{\"definitions\":[" +
            "{\"key\":\"wood\",\"states\":[{\"durability\":1,\"path\":\"Obstacles/Crate/crate-durability-1-v1-256\",\"size\":0.96},{\"durability\":2,\"path\":\"Obstacles/Crate/crate-durability-2-v1-256\",\"size\":0.96}]} ," +
            "{\"key\":\"metal\",\"states\":[{\"durability\":1,\"path\":\"Obstacles/Scrap/scrap-durability-1-v1-256\",\"size\":1.1,\"pivotX\":0.4,\"pivotY\":0.6,\"order\":8,\"effects\":[\"Effects/GeneratorCharge/charge-pulse-01-v1-256\"]}]}]," +
            "\"bindings\":[{\"id\":\"fixture.wood\",\"visualKey\":\"wood\"},{\"id\":\"fixture.metal\",\"visualKey\":\"metal\"},{\"id\":\"fixture.shared\",\"visualKey\":\"wood\"}]}";

        public static void RunEffects()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0;
            try
            {
                string json = "{\"definitions\":[{\"key\":\"fixture.effects\",\"states\":[{\"durability\":1,\"path\":\"Obstacles/Crate/crate-durability-1-v1-256\",\"effectAnimations\":[{\"key\":\"hit\",\"frames\":[{\"path\":\"Effects/GeneratorCharge/charge-pulse-01-v1-256\",\"size\":1.3,\"order\":48},{\"path\":\"Effects/GeneratorCharge/charge-pulse-02-v1-256\",\"angle\":23}]}]}]}],\"bindings\":[{\"id\":\"fixture.effects.a\",\"visualKey\":\"fixture.effects\"},{\"id\":\"fixture.effects.shared\",\"visualKey\":\"fixture.effects\"}]}";
                ElementVisualCatalogDto dto = JsonUtility.FromJson<ElementVisualCatalogDto>(json);
                ElementVisualCatalog catalog = ElementVisualCatalog.FromDto(dto);
                ElementVisualResolver resolver = new ElementVisualResolver(catalog);
                MethodInfo effect = typeof(ElementVisualResolver).GetMethod("ResolveEffect");
                Check(effect != null, "공통 조회기가 ID/상태/효과 키로 등록 애니메이션을 선택");
                ElementVisualState state = new ElementVisualState(0, 1, 0, 0, 0, 0, 1);
                IReadOnlyList<ElementVisualFrame> Select(string id, string key) => (IReadOnlyList<ElementVisualFrame>)effect.Invoke(resolver,
                    new object[] { new ElementId(id), state, key });
                IReadOnlyList<ElementVisualFrame> frames = Select("fixture.effects.a", "hit");
                Check(frames.Count == 2 && frames[0].Path.EndsWith("01-v1-256") && frames[1].Path.EndsWith("02-v1-256") &&
                    frames[0].Size == 1.3f && frames[0].Order == 48 && frames[1].Angle == 23, "효과 순서/크기/정렬/회전은 등록 값");
                Check(ReferenceEquals(frames, Select("fixture.effects.shared", "hit")), "효과의 명시적 공유 별칭");
                JsonUtility.FromJsonOverwrite(json.Replace("charge-pulse-01", "changed"), dto);
                Check(Select("fixture.effects.a", "hit")[0].Path == frames[0].Path, "효과 제작 입력 변경 후 불변 값 유지");
                ElementVisualCatalogDto exported = catalog.ToDto();
                JsonUtility.FromJsonOverwrite(json.Replace("charge-pulse-01", "exported-change"), exported);
                Check(Select("fixture.effects.a", "hit")[0].Path.EndsWith("01-v1-256"), "효과 배포 배열 변경 후 불변 값 유지");
                IReadOnlyList<ElementVisualFrame> roundTrip = (IReadOnlyList<ElementVisualFrame>)effect.Invoke(new ElementVisualResolver(ElementVisualCatalog.FromDto(catalog.ToDto())),
                    new object[] { new ElementId("fixture.effects.a"), state, "hit" });
                Check(roundTrip.Count == 2 && roundTrip[1].Angle == 23, "효과 등록 배포 왕복 보존");
                Reject(() => Select("fixture.effects.a", "missing"), "fixture.effects.a");
                Reject(() => ElementVisualCatalog.FromDto(JsonUtility.FromJson<ElementVisualCatalogDto>(json.Replace("\"key\":\"hit\"", "\"key\":\"\""))), "fixture.effects.a");
                Reject(() => ElementVisualCatalog.FromDto(JsonUtility.FromJson<ElementVisualCatalogDto>(json.Replace("Effects/GeneratorCharge/charge-pulse-01-v1-256", ""))), "fixture.effects.a");
                bool rejected = false;
                try { ((ICollection<ElementVisualFrame>)frames).Clear(); } catch (NotSupportedException) { rejected = true; }
                Check(rejected && frames.Count == 2, "효과 프레임 목록 쓰기 거절");
                ElementVisualResolver legacy = new ElementVisualResolver(LegacyElementVisuals.Catalog);
                IReadOnlyList<ElementVisualFrame> damage = (IReadOnlyList<ElementVisualFrame>)effect.Invoke(legacy,
                    new object[] { new ElementId("obstacle.crate.wood"), state, "damage" });
                Check(damage.Count == 4 && damage[0].Path.Contains("WoodBreak") && damage[0].Order == 40,
                    "구형 본체 타격도 명시적 효과 등록 데이터로 조회");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            File.WriteAllLines("Logs/ElementFramework/Phase04/visual-effect-registration-results.txt", Results);
            EditorApplication.Exit(exit);
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0;
            try
            {
                Assembly assembly = typeof(ElementId).Assembly;
                dtoType = assembly.GetType("Elements.ElementVisualCatalogDto");
                catalogType = assembly.GetType("Elements.ElementVisualCatalog");
                resolverType = assembly.GetType("Elements.ElementVisualResolver");
                stateType = assembly.GetType("Elements.ElementVisualState");
                Check(dtoType != null && catalogType != null && resolverType != null && stateType != null,
                    "등록 시각 데이터에서 ID/상태 조회 가능");
                object dto = Dto(Fixture), catalog = Catalog(dto);
                object resolver = Activator.CreateInstance(resolverType, catalog);
                object wood = Resolve(resolver, "fixture.wood", State()), metal = Resolve(resolver, "fixture.metal", State());
                Check(Value<string>(wood, "Path") == "Obstacles/Crate/crate-durability-1-v1-256" &&
                    Value<string>(metal, "Path") == "Obstacles/Scrap/scrap-durability-1-v1-256", "같은 행동의 서로 다른 ID가 별도 그림 선택");
                Check(Value<string>(Resolve(resolver, "fixture.shared", State()), "Path") == Value<string>(wood, "Path"), "명시적 공유 별칭");
                Check(Value<string>(Resolve(resolver, "fixture.wood", State(2)), "Path") == "Obstacles/Crate/crate-durability-2-v1-256", "실제 내구도 상태 조회");
                Check(Math.Abs(Value<float>(metal, "Size") - 1.1f) < .00001f && Value<int>(metal, "Order") == 8 &&
                    Value<Vector2>(metal, "Pivot") == new Vector2(.4f, .6f), "크기 피벗 정렬은 등록 값");
                Check(Value<IReadOnlyList<string>>(metal, "Effects")[0] == "Effects/GeneratorCharge/charge-pulse-01-v1-256", "효과 키 조회");
                JsonUtility.FromJsonOverwrite(Fixture.Replace("crate-durability-1", "changed"), dto);
                Check(Value<string>(Resolve(resolver, "fixture.wood", State()), "Path") == "Obstacles/Crate/crate-durability-1-v1-256", "DTO 수정 후 불변 카탈로그 유지");
                Reject(() => Resolve(resolver, "fixture.missing", State()), "fixture.missing");
                Reject(() => Resolve(resolver, "fixture.wood", State(13)), "fixture.wood");
                Reject(() => Resolve(resolver, "fixture.wood", State(1, 4)), "fixture.wood");
                Reject(() => Catalog(Dto(Fixture.Replace("fixture.metal", "fixture.wood"))), "fixture.wood");
                Reject(() => Catalog(Dto(Fixture.Replace("\"visualKey\":\"metal\"", "\"visualKey\":\"missing\""))), "fixture.metal");
                Reject(() => Catalog(Dto(Fixture.Replace("\"path\":\"Obstacles/Scrap/scrap-durability-1-v1-256\"", "\"path\":\"\""))), "fixture.metal");
                string axes = "{\"definitions\":[{\"key\":\"charged\",\"states\":[{\"color\":2,\"durability\":3,\"charge\":2,\"requiredCharge\":4,\"direction\":1,\"frame\":2,\"logicalSize\":2,\"path\":\"Obstacles/Generator/generator-charge-2-of-4-v1-512\",\"sheetColumns\":2,\"sheetRows\":2,\"sheetFrame\":3,\"size\":2.16,\"offsetX\":0.1,\"offsetY\":0.2,\"angle\":15}]}],\"bindings\":[{\"id\":\"fixture.axes\",\"visualKey\":\"charged\"}]}";
                object axisResolver = Activator.CreateInstance(resolverType, Catalog(Dto(axes)));
                object axisState = Activator.CreateInstance(stateType, new object[] { 2, 3, 2, 4, 1, 2, 2 });
                object selected = Resolve(axisResolver, "fixture.axes", axisState);
                Check(Value<string>(selected, "Path") == "Obstacles/Generator/generator-charge-2-of-4-v1-512" &&
                    Value<int>(selected, "SheetFrame") == 3 && Value<int>(selected, "SheetColumns") == 2 &&
                    Value<int>(selected, "SheetRows") == 2 && Value<Vector2>(selected, "Offset") == new Vector2(.1f, .2f) &&
                    Value<float>(selected, "Angle") == 15, "색 충전 방향 프레임 실제 점유 크기와 시트 값 조회");
                for (int axis = 0; axis < 7; axis++)
                {
                    object[] changed = { 2, 3, 2, 4, 1, 2, 2 }; changed[axis] = (int)changed[axis] + 1;
                    Reject(() => Resolve(axisResolver, "fixture.axes", Activator.CreateInstance(stateType, changed)), "fixture.axes");
                }
                Reject(() => Catalog(Dto(axes.Replace("\"sheetFrame\":3", "\"sheetFrame\":4"))), "fixture.axes");
                Reject(() => Catalog(Dto(Fixture.Replace("\"durability\":2", "\"durability\":1"))), "fixture.wood");
                object exported = catalogType.GetMethod("ToDto").Invoke(catalog, null);
                JsonUtility.FromJsonOverwrite(Fixture.Replace("crate-durability-1", "export-mutation"), exported);
                Check(Value<string>(Resolve(resolver, "fixture.wood", State()), "Path") == "Obstacles/Crate/crate-durability-1-v1-256", "배포 배열 수정과 기존 카탈로그 독립");
                Type assetType = assembly.GetType("Elements.ElementVisualCatalogAsset");
                Check(assetType != null, "시각 제작 원본 존재");
                ScriptableObject source = ScriptableObject.CreateInstance(assetType);
                object authored;
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"catalog\":" + Fixture + "}", source);
                    authored = assetType.GetMethod("CreateCatalog").Invoke(source, null);
                    JsonUtility.FromJsonOverwrite("{\"catalog\":" + Fixture.Replace("crate-durability-1", "authoring-mutation") + "}", source);
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
                object authoredResolver = Activator.CreateInstance(resolverType, authored);
                Check(Value<string>(Resolve(authoredResolver, "fixture.wood", State()), "Path") == "Obstacles/Crate/crate-durability-1-v1-256", "제작 원본 변경 및 파기 후 카탈로그 독립");
                ICollection<string> effectList = Value<IReadOnlyList<string>>(metal, "Effects") as ICollection<string>;
                bool readonlyRejected = false;
                try { effectList.Add("changed"); } catch (NotSupportedException) { readonlyRejected = true; }
                Check(readonlyRejected && effectList.Count == 1, "효과 목록의 수정 거절");
                ElementVisualCatalogAsset overlay = ScriptableObject.CreateInstance<ElementVisualCatalogAsset>();
                ElementCatalogAsset authoring = ScriptableObject.CreateInstance<ElementCatalogAsset>();
                ElementVisualCatalog composed;
                try
                {
                    string generated = "{\"catalog\":{\"definitions\":[{\"key\":\"fixture.generator\",\"generates\":[\"power.drone\"],\"states\":[{\"path\":\"Obstacles/Crate/crate-durability-1-v1-256\"}]}],\"bindings\":[{\"id\":\"fixture.generator\",\"visualKey\":\"fixture.generator\"}]}}";
                    JsonUtility.FromJsonOverwrite(generated, overlay);
                    typeof(ElementCatalogAsset).GetField("visuals", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(authoring, overlay);
                    composed = authoring.CreateVisualCatalog();
                    Check(composed.Get(new ElementId("fixture.generator")).Generates[0].Value == "power.drone" &&
                        composed.Get(new ElementId("power.drone")).States.Count == 5, "제작 카탈로그 합성 후 기본 생성 참조 검증");
                    JsonUtility.FromJsonOverwrite("{\"catalog\":{\"definitions\":[],\"bindings\":[]}}", overlay);
                }
                finally { UnityEngine.Object.DestroyImmediate(authoring); UnityEngine.Object.DestroyImmediate(overlay); }
                Check(composed.Get(new ElementId("fixture.generator")).Generates[0].Value == "power.drone", "합성 카탈로그는 제작 원본 변경/파기와 독립");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            File.WriteAllLines("Logs/ElementFramework/Phase04/visual-results.txt", Results);
            EditorApplication.Exit(exit);
        }
    }
}
