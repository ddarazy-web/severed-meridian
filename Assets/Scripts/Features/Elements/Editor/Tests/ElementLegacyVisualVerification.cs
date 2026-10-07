using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Levels;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>구형 그림의 명시적 등록과 신규 ID의 무추론 경계를 검사한다.</summary>
    public static class ElementLegacyVisualVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static ElementVisualState State(int durability = 0, int color = -1, int charge = 0, int required = 0, int direction = 0, int frame = 0, int size = 1)
            => new ElementVisualState(color, durability, charge, required, direction, frame, size);
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0;
            try
            {
                Type legacy = typeof(ElementId).Assembly.GetType("Elements.LegacyElementVisuals");
                Check(legacy != null, "구형 표현이 명시적 시각 카탈로그로 등록됨");
                ElementVisualCatalog catalog = (ElementVisualCatalog)legacy.GetProperty("Catalog").GetValue(null);
                ElementVisualResolver resolver = new ElementVisualResolver(catalog);
                string[] colors = { "pink", "yellow", "blue", "green", "purple" };
                for (int i = 0; i < 5; i++)
                {
                    ElementVisualFrame normal = resolver.Resolve(new ElementId("supply.normal.random"), State(color: i));
                    Check(normal.Path == "Blocks/rabbit-" + colors[i] + "-v1-256", "일반 블록 색 " + i);
                    Check(resolver.Resolve(new ElementId("supply.normal.fixed"), State(color: i)).Path == normal.Path, "고정 블록 명시적 공유 " + i);
                    for (int durability = 1; durability <= 9; durability++)
                        Check(resolver.Resolve(new ElementId("obstacle.metal-rod-box"), State(durability, i, size: 2)).Path ==
                            "Obstacles/MetalRodBox/metal-rod-box-" + colors[i] + "-durability-" + durability + "-v1-256", "금속기둥 색/실제 내구도 " + i + "/" + durability);
                }
                foreach (int required in new[] { 3, 4, 5 })
                    for (int charge = 0; charge <= required; charge++)
                        Check(resolver.Resolve(new ElementId("obstacle.generator"), State(charge: charge, required: required, size: 2)).Path ==
                            "Obstacles/Generator/generator-charge-" + charge + "-of-" + required + "-v1-512", "발전기 충전 " + charge + "/" + required);
                ElementVisualFrame horizontal = resolver.Resolve(new ElementId("power.rocket"), State(direction: (int)RocketDirection.Horizontal));
                Check(horizontal.Path == "PowerBlocks/cleaning-rocket-horizontal-v1" && horizontal.Size == 1.12f &&
                    horizontal.Offset == new Vector2(-28.5f / 256, 28f / 256), "가로 로켓 크기와 중심 보존");
                Check(resolver.Resolve(new ElementId("power.rocket"), State(direction: (int)RocketDirection.Vertical)).Path ==
                    "PowerBlocks/cleaning-rocket-vertical-v1", "세로 로켓");
                for (int frame = 1; frame <= 4; frame++)
                {
                    ElementVisualFrame rotor = resolver.Resolve(new ElementId("power.drone"), State(frame: frame));
                    Check(rotor.Path == "PowerBlocks/collection-drone-rotor-4frames-v1" && rotor.SheetFrame == frame - 1 &&
                        rotor.SheetColumns == 2 && rotor.SheetRows == 2, "드론 회전 프레임 " + frame);
                }
                bool missing = false;
                try { resolver.Resolve(new ElementId("obstacle.unregistered.wood"), State(1)); }
                catch (ArgumentException error) { missing = error.Message.Contains("obstacle.unregistered.wood"); }
                Check(missing, "동일 행동이라도 신규 ID를 구형 종류로 추론하지 않음");
                bool outOfRange = false;
                try { resolver.Resolve(new ElementId("obstacle.crate.wood"), State(13)); }
                catch (ArgumentException error) { outOfRange = error.Message.Contains("obstacle.crate.wood"); }
                Check(outOfRange, "내구도13을 기존 마지막 그림으로 자르지 않음");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            File.WriteAllLines("Logs/ElementFramework/Phase04/legacy-visual-results.txt", Results);
            EditorApplication.Exit(exit);
        }
    }
}
