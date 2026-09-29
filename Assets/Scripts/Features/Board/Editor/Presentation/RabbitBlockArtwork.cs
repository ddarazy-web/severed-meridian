using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>편집 보드·도구 목록·플레이 테스트가 공유하는 일반 블록 그림 조회. 런타임 규칙에는 의존시키지 않는다.</summary>
    internal static class RabbitBlockArtwork
    {
        // 기존 색 번호 순서는 분홍→노랑→파랑→초록→보라다. 파일 이름 순서로 정렬하면
        // 저장된 레벨의 종류와 화면 색이 달라지므로 명시적인 대응을 유지한다.
        private static readonly string[] fileNames = { "pink", "yellow", "blue", "green", "purple" };
        private static readonly Texture2D[] textures = new Texture2D[5];

        /// <param name="color">레벨 데이터에 저장된 기존 달토끼 종류.</param>
        /// <returns>해당 종류의 투명 PNG. 잘못된 종류나 파일 누락은 null로 반환해 기존 문자 표시를 유지한다.</returns>
        internal static Texture2D Get(RabbitColor color)
        {
            int index = (int)color;
            if (index < 0 || index >= fileNames.Length) return null;
            // 포인터 이동 때마다 다시 그리는 보드에서 같은 에셋을 반복 조회하지 않는다.
            // Unity의 파기된 오브젝트도 null로 판정하므로 재임포트 뒤 다시 로드할 수 있다.
            if (textures[index] == null)
                textures[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Textures/Blocks/rabbit-" + fileNames[index] + "-v1-256.png");
            return textures[index];
        }
    }
}
