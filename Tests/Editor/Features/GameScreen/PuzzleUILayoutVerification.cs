using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static class PuzzleUILayoutVerification
    {
        [MenuItem("Tools/Match/UI 배치 검증")]
        public static void Run()
        {
            var results = new List<string>(); int failures = 0;
            void Check(bool value, string name) { results.Add((value ? "PASS " : "FAIL ") + name); if (!value) failures++; }
            Rect wide = PuzzleScreenLayout.CalculateBoard(new Rect(0, 0, 1280, 720));
            Check(wide == new Rect(359, 72, 562, 562), "가로 목업 359/86/562 보드");
            Rect portrait = PuzzleScreenLayout.CalculateBoard(new Rect(0, 0, 450, 800));
            Check(portrait == new Rect(19, 153, 412, 412), "세로 목업 19/235/412 보드");
            foreach (Vector2 size in new[] { new Vector2(1280,720), new Vector2(450,800), new Vector2(450,975), new Vector2(600,800) })
                foreach (bool inset in new[] { false, true })
                {
                    Rect safe = inset ? new Rect(17, 24, size.x - 29, size.y - 65) : new Rect(0, 0, size.x, size.y);
                    Rect board = PuzzleScreenLayout.CalculateBoard(safe);
                    Check(safe.Contains(board.min) && board.xMax <= safe.xMax && board.yMax <= safe.yMax && board.width == board.height,
                        "안전 영역/정사각 보드 " + size + "/" + inset);
                    float scale = safe.height > safe.width ? safe.width / 450 : safe.height / 720;
                    Check(safe.width >= safe.height || board.yMin >= safe.yMin + 125 * scale, "하단 아이템 간격 " + size + "/" + inset);
                }
            Directory.CreateDirectory(PuzzleUIStateVerification.Output);
            File.WriteAllLines(PuzzleUIStateVerification.Output + "layout-results.txt", results);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
            if (failures > 0) throw new InvalidOperationException("배치 실패 " + failures);
        }
    }
}
