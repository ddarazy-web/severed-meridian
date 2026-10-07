using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Tutorial;
using UnityEditor;
using UnityEngine;

public static class TutorialFocusVerification
{
    public static void Run()
    {
        string path = "Assets/Prefabs/UI/Puzzle/__TutorialFocusVerification.prefab";
        List<string> results = new List<string>();
        GameObject owned = null;
        bool created = false;
        int exit = 0;
        try
        {
            if (File.Exists(path)) throw new InvalidOperationException("소유하지 않은 검사 프리팹");
            GameObject prefab = GameScreen.Editor.PuzzleTutorialAssets.GenerateOverlay(path);
            created = true;
            owned = UnityEngine.Object.Instantiate(prefab);
            TutorialOverlayView view = owned.GetComponent<TutorialOverlayView>();
            view.SetCell(0, Vector2.zero, new Vector2(80, 80), true);
            UnityEngine.UI.Image cell = view.Content.Find("Cell0").GetComponent<UnityEngine.UI.Image>();
            if (cell.color.a != 0 || cell.GetComponent<UnityEngine.UI.Outline>().enabled)
                throw new InvalidOperationException("포커스 칸에 노란 덮개/테두리가 남음");
            results.Add("PASS 지정 칸의 덮개/노란 테두리 제거");
            Type shadeType = typeof(TutorialOverlayView).Assembly.GetType("Tutorial.TutorialFocusGraphic");
            UnityEngine.UI.Graphic shade = view.Content.GetComponent(shadeType) as UnityEngine.UI.Graphic;
            if (shade == null || shade.raycastTarget) throw new InvalidOperationException("화면 전체 어둠 막/입력 통과 누락");
            MethodInfo bounds = typeof(TutorialOverlayView).GetMethod("SetFocusBounds");
            MethodInfo item = typeof(TutorialOverlayView).GetMethod("SetItemFocus");
            MethodInfo populate = shadeType.GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (Vector2 size in new[] { new Vector2(1280, 720), new Vector2(720, 1280) })
            {
                Rect screen = new Rect(-size / 2, size);
                bounds.Invoke(view, new object[] { screen });
                for (int index = 0; index < 81; index++) view.SetCell(index, Vector2.zero, new Vector2(80, 80), false);
                view.SetCell(0, Vector2.zero, new Vector2(80, 80), true);
                view.SetCell(1, new Vector2(80, 0), new Vector2(80, 80), true);
                view.SetCell(2, new Vector2(20, 20), new Vector2(80, 80), true);
                item.Invoke(view, new object[] { (Rect?)new Rect(-size.x / 2 - 20, -100, 80, 60) });
                using (UnityEngine.UI.VertexHelper mesh = new UnityEngine.UI.VertexHelper())
                {
                    populate.Invoke(shade, new object[] { mesh });
                    List<UnityEngine.UIVertex> vertices = new List<UnityEngine.UIVertex>(); mesh.GetUIVertexStream(vertices);
                    foreach (Vector2 point in new[] { Vector2.zero, new Vector2(80, 0), new Vector2(20, 55), new Vector2(screen.xMin + 10, -80) })
                        if (Covered(vertices, point)) throw new InvalidOperationException("인접/겹침/화면 끝 포커스 내부가 어둡게 덮임");
                    foreach (Vector2 point in new[] { new Vector2(screen.xMin + 3, screen.yMin + 3), new Vector2(screen.xMax - 3, screen.yMax - 3), new Vector2(0, screen.yMax - 10), new Vector2(0, -120) })
                        if (!Covered(vertices, point)) throw new InvalidOperationException("화면 여백/HUD/비지정 부분이 어둡지 않음");
                    foreach (UnityEngine.UIVertex vertex in vertices)
                        if (vertex.color.a == 0 || vertex.color.a == 255 || vertex.color.r != 0 || vertex.color.g != 0 || vertex.color.b != 0)
                            throw new InvalidOperationException("검정 반투명 막 색상 오류");
                }
                results.Add("PASS 전체 화면 어둠/지정 칸·아이템 투명/겹친 구멍/화면 끝 " + size);
                for (int index = 0; index < 81; index++) view.SetCell(index, Vector2.zero, new Vector2(80, 80), false);
                item.Invoke(view, new object[] { null });
                using (UnityEngine.UI.VertexHelper mesh = new UnityEngine.UI.VertexHelper())
                {
                    populate.Invoke(shade, new object[] { mesh });
                    List<UnityEngine.UIVertex> vertices = new List<UnityEngine.UIVertex>(); mesh.GetUIVertexStream(vertices);
                    if (!Covered(vertices, Vector2.zero)) throw new InvalidOperationException("이전 단계 투명 구멍이 남음");
                }
            }
            results.Add("PASS 단계 변경 시 이전 투명 구멍 제거");
            foreach (UnityEngine.UI.Graphic graphic in owned.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                if (graphic.raycastTarget != (graphic.gameObject == view.Next.gameObject)) throw new InvalidOperationException("장식 입력 차단 " + graphic.name);
            results.Add("PASS 어둠 막·장식이 보드 입력을 가로채지 않음");
            view.Display(null, false, false);
            if (view.Content.gameObject.activeSelf) throw new InvalidOperationException("안내 종료 뒤 어둠 막이 남음");
            results.Add("PASS 안내 종료/숨김 시 전체 막 제거");
            File.WriteAllLines("Logs/Tutorial/FocusShade/results.txt", results);
        }
        catch (Exception error)
        {
            results.Add("FAIL " + error); File.WriteAllLines("Logs/Tutorial/FocusShade/results.txt", results);
            Debug.LogException(error); exit = 1;
        }
        finally
        {
            if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
            if (created) AssetDatabase.DeleteAsset(path);
        }
        EditorApplication.Exit(exit);
    }

    private static bool Covered(List<UnityEngine.UIVertex> vertices, Vector2 point)
    {
        for (int index = 0; index < vertices.Count; index += 3)
        {
            Vector2 a = vertices[index].position, b = vertices[index + 1].position, c = vertices[index + 2].position;
            float first = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
            float second = (c.x - b.x) * (point.y - b.y) - (c.y - b.y) * (point.x - b.x);
            float third = (a.x - c.x) * (point.y - c.y) - (a.y - c.y) * (point.x - c.x);
            if ((first >= 0 && second >= 0 && third >= 0) || (first <= 0 && second <= 0 && third <= 0)) return true;
        }
        return false;
    }
}
