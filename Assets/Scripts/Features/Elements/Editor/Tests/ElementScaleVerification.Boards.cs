using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elements.Editor
{
    public static partial class ElementScaleVerification
    {
        // 제작·실행 검사와 같은 입력의 세 화면을 확인한다. 화면 검사 중 규칙을 실행하지 않는다.
        private static async UniTask VerifyBoards(LevelDefinition level, ElementVisualCatalog visuals, LevelRuntimeState state)
        {
            using PuzzleArtwork artwork = new PuzzleArtwork(visuals);
            await artwork.PrepareAsync(state, CancellationToken.None);
            foreach (string path in new[] { "Blocks/", "PowerBlocks/", "Obstacles/MetalRodBox/", "Obstacles/Web/" })
                await LevelBoardArtwork.Warmup(path);
            LevelBoardView edit = new LevelBoardView(); edit.Display(level, null);
            GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"));
            Owned.Add(root);
            PuzzleWorldBoard world = root.GetComponent<PuzzleWorldBoard>(); world.Draw(state, artwork);
            MethodInfo bind = typeof(RuntimeBoardArtwork).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method => method.Name == "Bind" && method.GetParameters().Length == 4);
            BoardCoordinate[] positions = { new BoardCoordinate(3, 3), new BoardCoordinate(4, 7), new BoardCoordinate(4, 0) };
            ElementVisualFrame[] frames = { artwork.Visuals.Obstacle(state.Obstacles[0]),
                artwork.Visuals.Cover(state.CellAt(positions[1])), artwork.Visuals.Content(state.CellAt(positions[2])) };
            string original = JsonUtility.ToJson(level); byte[] packed = LevelPackCodec.Snapshot(level);
            int draws = state.Random.DrawCount;
            for (int i = 0; i < positions.Length; i++)
            {
                Label test = new Label(); bind.Invoke(null, new object[] { test, state.CellAt(positions[i]), state, visuals });
                VisualElement editor = i == 0 ? edit.LargeBodies.Single() : edit.ArtworkAt(positions[i], i == 1 ? "board-cover-art" : "board-content-art");
                Sprite editorSprite = editor.style.backgroundImage.value.sprite;
                Sprite testSprite = test.Q(i == 1 ? "runtime-cover" : "runtime-content").style.backgroundImage.value.sprite;
                Sprite expected = artwork.GetVisual(frames[i]);
                SpriteRenderer renderer = i == 2 ? world.OccupantAt(positions[i]) : world.GetComponentsInChildren<SpriteRenderer>()
                    .Single(image => image.sprite == expected);
                Check(editorSprite != null && testSprite != null && renderer.sprite != null &&
                    editorSprite.name == expected.name && testSprite.name == expected.name && renderer.sprite == expected,
                    "세 보드 실제 제작 ID/공유 이미지 " + level.Elements.Single(item => item.coordinate.Equals(positions[i]) &&
                        item.layer == (i == 0 ? PlacementLayer.Obstacle : i == 1 ? PlacementLayer.Cover : PlacementLayer.Block)).definitionId);
                Check(renderer.sortingOrder == frames[i].Order && renderer.color == Color.white,
                    "월드 등록 정렬/기본 색 " + i);
            }
            Check(Mathf.Approximately(edit.LargeBodies.Single().style.width.value.value, LevelBoardView.CellSize * frames[0].Size),
                "2×2 제작 본체의 편집 표시 크기");
            int[] capacity = world.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray();
            for (int repeat = 0; repeat < 5; repeat++) { edit.Display(level, null); world.Draw(state, artwork); }
            Check(world.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).SequenceEqual(capacity),
                "제작3종 반복 표시의 준비 용량 안 추가 생성0");
            Check(JsonUtility.ToJson(level) == original && LevelPackCodec.Snapshot(level).SequenceEqual(packed) && state.Random.DrawCount == draws,
                "세 보드 표시 후 원본/룰 팩/규칙 난수 무변경");
        }
    }
}
