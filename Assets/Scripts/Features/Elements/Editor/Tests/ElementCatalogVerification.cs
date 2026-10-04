using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Levels;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>정의·카탈로그 계약만 메모리에서 검사한다. 원본·씬·배포 팩은 쓰지 않는다.</summary>
    public static class ElementCatalogVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage14";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static Type definitionType, catalogType;
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static object Define(ElementId id, string name) => Activator.CreateInstance(definitionType, new object[] { id, name });
        private static Array Definitions(params object[] entries)
        {
            Array result = Array.CreateInstance(definitionType, entries.Length);
            for (int i = 0; i < entries.Length; i++) result.SetValue(entries[i], i);
            return result;
        }
        private static object Catalog(object entries) => Activator.CreateInstance(catalogType, new[] { entries });
        private static object Get(object catalog, ElementId id) => catalogType.GetMethod("Get").Invoke(catalog, new object[] { id });
        private static int Count(object catalog) => (int)catalogType.GetProperty("Count").GetValue(catalog);
        private static void Reject(Action operation, Type expected, string input, string required = null)
        {
            Exception found = null;
            try { operation(); }
            catch (Exception error) { found = error is TargetInvocationException ? error.InnerException : error; }
            Check(found != null && found.GetType() == expected && (required == null || found.Message.Contains(required)), "명시적 오류/ID " + input);
            Values.Add(JsonUtility.ToJson(new Entry { name = "reject", input = input, error = found.GetType().Name + ": " + found.Message }));
        }
        private static void Observe(string name, object catalog, ElementId id, string expected)
        {
            object result = Get(catalog, id);
            ElementId actualId = (ElementId)definitionType.GetProperty("Id").GetValue(result);
            string actualName = (string)definitionType.GetProperty("DisplayName").GetValue(result);
            Check(actualId == id && actualName == expected, name + " 정확한 ID/표시명 " + id.Value);
            Values.Add(JsonUtility.ToJson(new Entry { name = name, input = id.Value, output = actualId.Value, displayName = actualName, count = Count(catalog) }));
        }
        [Serializable] private sealed class Entry { public string name, input, output, displayName, error; public int count; }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); int exit = 0;
            try
            {
                definitionType = typeof(ElementId).Assembly.GetType("Elements.ElementDefinition");
                Check(definitionType != null, "불변 메모리 정의 계약 존재");
                catalogType = typeof(ElementId).Assembly.GetType("Elements.ElementCatalog");
                Check(catalogType != null, "읽기 전용 카탈로그 계약 존재");
                Check(definitionType.IsSealed && !typeof(UnityEngine.Object).IsAssignableFrom(definitionType) && definitionType.GetProperties().All(property => property.SetMethod == null) && definitionType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).All(field => field.IsInitOnly && (field.FieldType == typeof(string) || field.FieldType == typeof(ElementId) || field.FieldType == typeof(ElementPlacementProfile) || field.FieldType == typeof(ElementChargePlacementProfile))), "정의는 ID/표시명/프로필 불변 값만 보유");
                Check(typeof(ElementPlacementProfile).IsSealed && typeof(ElementPlacementProfile).GetProperties().All(property => property.SetMethod == null) && typeof(ElementPlacementProfile).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).All(field => field.IsInitOnly && field.FieldType == typeof(int)), "조합한 프로필도 불변 정수만 보유");
                Check(typeof(ElementChargePlacementProfile).IsSealed && typeof(ElementChargePlacementProfile).GetProperties().All(property => property.SetMethod == null) && typeof(ElementChargePlacementProfile).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).All(field => field.IsInitOnly && field.FieldType == typeof(int)), "충전 배치 프로필도 불변 정수만 보유");
                Check(catalogType.GetProperties().Select(property => property.Name).SequenceEqual(new[] { "Count" }) && catalogType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Where(method => !method.IsSpecialName).Select(method => method.Name).SequenceEqual(new[] { "Get" }), "Count/Get만 공개·등록 수정 API 없음");
                ObstacleKind[] kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance, ObstacleKind.Generator };
                string[] names = { "나무상자", "고철 뭉치", "고물 회수 캡슐", "색깔 자물쇠", "금속기둥 상자", "고장 난 발전기" };
                object[] entries = kinds.Select((kind, i) => Define(LegacyElementMap.Get(kind), names[i])).ToArray();
                Array input = Definitions(entries); object catalog = Catalog(input);
                Check(Count(catalog) == 6, "기존6종 Count6");
                string global = JsonUtility.ToJson(UnityEngine.Random.state);
                for (int i = 0; i < kinds.Length; i++) Observe("legacy-six", catalog, LegacyElementMap.Get(kinds[i]), names[i]);
                object old = Get(catalog, LegacyElementMap.Get(ObstacleKind.Crate));
                input.SetValue(Define(LegacyElementMap.Get(ObstacleKind.Crate), "교체된 표시명"), 0); input.SetValue(null, 1);
                Check(ReferenceEquals(old, Get(catalog, LegacyElementMap.Get(ObstacleKind.Crate))) && Count(catalog) == 6, "입력 배열 교체/null 이후 원래 불변 정의 유지");
                Observe("array-after-change", catalog, LegacyElementMap.Get(ObstacleKind.Crate), names[0]);
                Observe("array-after-null", catalog, LegacyElementMap.Get(ObstacleKind.Scrap), names[1]);
                IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(definitionType));
                foreach (object entry in entries) list.Add(entry);
                object fromList = Catalog(list); list.Clear(); list.Add(Define(new ElementId("element.late"), "나중 정의"));
                Check(list.Count == 1 && Count(fromList) == 6, "입력 목록 Clear/추가가 카탈로그 수에 영향 없음");
                Observe("list-after-clear", fromList, LegacyElementMap.Get(ObstacleKind.Crate), names[0]);
                Reject(() => Get(fromList, new ElementId("element.late")), typeof(KeyNotFoundException), "사후 추가 ID 조회", "element.late");
                object renamed = Catalog(Definitions(Define(LegacyElementMap.Get(ObstacleKind.Crate), "새 표시명"), Define(LegacyElementMap.Get(ObstacleKind.Scrap), "새 표시명")));
                Check(Count(renamed) == 2, "다른 ID의 동일 표시명 허용");
                Observe("name-independent", renamed, LegacyElementMap.Get(ObstacleKind.Crate), "새 표시명");
                Observe("same-name", renamed, LegacyElementMap.Get(ObstacleKind.Scrap), "새 표시명");
                string[] variants = { "element.case", "ELEMENT.CASE", " element.case " };
                object caseCatalog = Catalog(Definitions(variants.Select(value => Define(new ElementId(value), "이름:" + value)).ToArray()));
                Check(Count(caseCatalog) == 3, "Ordinal 대소문자/공백 구별 Count3");
                foreach (string variant in variants) Observe("ordinal", caseCatalog, new ElementId(variant), "이름:" + variant);
                object empty = Catalog(Definitions()); Check(Count(empty) == 0, "빈 카탈로그 Count0");
                Reject(() => Get(empty, new ElementId("element.missing")), typeof(KeyNotFoundException), "빈 카탈로그 미등록", "element.missing");
                Reject(() => Catalog(null), typeof(ArgumentNullException), "null 입력");
                Reject(() => Catalog(Definitions((object)null)), typeof(ArgumentException), "null 정의");
                Reject(() => Define(default, "무효"), typeof(ArgumentException), "정의 default ID");
                Reject(() => Get(catalog, default), typeof(ArgumentException), "Get default ID");
                foreach (string invalidName in new string[] { null, "", " ", "\t" }) Reject(() => Define(new ElementId("element.invalid-name"), invalidName), typeof(ArgumentException), "표시명=" + (invalidName ?? "<null>"), "element.invalid-name");
                Reject(() => Catalog(Definitions(Define(new ElementId("element.duplicate"), "첫 이름"), Define(new ElementId(new string("element.duplicate".ToCharArray())), "다른 이름"))), typeof(ArgumentException), "중복 ID", "element.duplicate");
                Reject(() => Get(catalog, new ElementId("element.absent")), typeof(KeyNotFoundException), "미등록 ID", "element.absent");
                Check(global == JsonUtility.ToJson(UnityEngine.Random.state), "메타데이터/조회/오류 검사는 전역 난수 무소비");
                object[] fiveHundred = Enumerable.Range(0, 500).Select(i => Define(new ElementId("element.fixture." + i.ToString("D3")), "정의" + i.ToString("D3"))).ToArray();
                object scale = Catalog(Definitions(fiveHundred)), reverse = Catalog(Definitions(fiveHundred.Reverse().ToArray()));
                Check(Count(scale) == 500 && Count(reverse) == 500, "정순/역순 Count500·중복0");
                for (int i = 0; i < 500; i++)
                {
                    ElementId id = new ElementId("element.fixture." + i.ToString("D3"));
                    Observe("scale-forward", scale, id, "정의" + i.ToString("D3"));
                    Observe("scale-reverse", reverse, id, "정의" + i.ToString("D3"));
                    Check(ReferenceEquals(Get(scale, id), Get(reverse, id)), "입력 순서 독립·동일 불변 정의 " + id.Value);
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally { File.WriteAllLines(Evidence + "/catalog-results.txt", Results); File.WriteAllLines(Evidence + "/catalog-values.jsonl", Values); }
            EditorApplication.Exit(exit);
        }
    }
}
