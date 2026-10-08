using System;
using System.Collections.Generic;
using System.Linq;
using GameScreen;
using LevelAuthoring.Documents;
using Levels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LevelAuthoring.Runtime
{
    public static class JsonPuzzlePlayAdapter
    {
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            { "level", typeof(LevelDefinition) }, { "element", typeof(Elements.ElementDefinitionAsset) },
            { "catalog", typeof(Elements.ElementCatalogAsset) }, { "visual", typeof(Elements.ElementVisualCatalogAsset) },
            { "tutorialFlow", typeof(Tutorial.TutorialFlowDefinition) }
        };
        public static PuzzlePlayRequest CreateRequest(ContentSnapshot snapshot, string levelId, int seed)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var root = snapshot.Get(levelId);
            if (root.Kind != "level") throw new ContentFormatException("게임 입력은 level 문서여야 합니다.");
            var resources = snapshot.Project.Data["resources"].ToDictionary(item => (string)item["id"], item => (string)item["path"], StringComparer.Ordinal);
            var owned = new List<ScriptableObject>();
            var resolved = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
            var resolving = new HashSet<string>(StringComparer.Ordinal);
            ScriptableObject Resolve(string id)
            {
                if (resolved.TryGetValue(id, out var existing)) return existing;
                if (!resolving.Add(id)) throw new ContentFormatException("제작 문서 참조가 순환합니다: " + id);
                var document = snapshot.Get(id);
                if (!Types.TryGetValue(document.Kind, out var type)) throw new ContentFormatException("실행 입력에서 지원하지 않는 문서 종류: " + document.Kind);
                var value = UnityAuthoringCodec.Read(document, type, Resolve, key => resources[key], owned.Add);
                resolving.Remove(id); resolved.Add(id, value); return value;
            }
            try
            {
                var level = (LevelDefinition)Resolve(levelId);
                // 공유 흐름은 Capture 내부의 기존 해석기를 통해 실행 사본에서만 펼친다.
                return PuzzlePlayRequest.Capture(level, seed);
            }
            finally
            {
                for (int index = owned.Count - 1; index >= 0; index--)
                    if (owned[index] != null)
                    {
                        if (Application.isPlaying) Object.Destroy(owned[index]);
                        else Object.DestroyImmediate(owned[index]);
                    }
            }
        }
    }
}
