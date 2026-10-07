using System.IO;
using System.Linq;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleSettlementAnimationVerification
    {
        private static void VerifySupplyIsolation(PuzzleWorldBoard board)
        {
            SpriteRenderer[] images = board.GetComponentsInChildren<SpriteRenderer>();
            bool[] enabled = images.Select(image => image.enabled).ToArray();
            SpriteRenderer[] supplies = images.Where(image => image.name == "Supply-playback")
                .OrderBy(image => image.transform.parent.GetComponentInChildren<SpriteMask>().transform.position.y).ToArray();
            Check(supplies.Length == 2, "인접 두 생성구 동시 공급 fixture");
            SpriteRenderer lower = supplies[0];
            Sprite original = lower.sprite;
            Vector3 scale = lower.transform.localScale;
            Vector3 position = lower.transform.position;
            Color color = lower.color;
            Sprite probe = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            Camera camera = new GameObject("Mask probe", typeof(Camera)).GetComponent<Camera>();
            RenderTexture target = RenderTexture.GetTemporary(256, 256, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
            try
            {
                foreach (SpriteRenderer image in images) image.enabled = image == lower;
                // 두 마스크를 유지한 채 아래 공급 그림만 렌더하여 이웃 칸 누출을 측정한다.
                lower.sprite = probe; lower.transform.localScale = Vector3.one * .8f; lower.color = Color.green;
                // 대기열의 현재 이동 위치와 무관하게 두 공급구 경계에 검사 그림을 걸친다.
                lower.transform.position = board.transform.TransformPoint(new Vector3(0, -.5f, 0));
                camera.transform.position = board.transform.TransformPoint(new Vector3(0, -.5f, -10));
                camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
                File.WriteAllBytes(Output + "adjacent-supply-mask.png", pixels.EncodeToPNG());
                Check(pixels.GetPixel(128, 96).g > .5f, "공급 대상 칸 내부 픽셀 표시");
                Check(pixels.GetPixel(128, 160).g < .05f, "이웃 생성구 마스크에 공급 픽셀 누출 없음");
            }
            finally
            {
                lower.sprite = original; lower.transform.localScale = scale; lower.transform.position = position; lower.color = color;
                for (int i = 0; i < images.Length; i++) images[i].enabled = enabled[i];
                camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(camera.gameObject); Object.Destroy(pixels); Object.Destroy(probe);
            }
        }
    }
}
