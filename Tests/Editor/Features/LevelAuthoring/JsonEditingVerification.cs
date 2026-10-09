using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using Levels;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class JsonEditingVerification
    {
        public static void Run()
        {
            var owned = new List<ScriptableObject>();
            try
            {
                var source = ScriptableObject.CreateInstance<LevelDefinition>(); owned.Add(source);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":-1}", source);
                bool strictRejected = false;
                try { UnityAuthoringCodec.Write(source, "level", "level-draft", obj => "unused", path => path); }
                catch (ContentFormatException) { strictRejected = true; }
                if (!strictRejected) throw new Exception("엄격 내보내기가 잘못된 이동 수를 허용함");
                // API가 없을 때도 컴파일을 유지하며 명시적인 미구현 실패를 보고한다.
                var write = typeof(UnityAuthoringCodec).GetMethod("WriteDraft");
                var read = typeof(UnityAuthoringCodec).GetMethod("ReadDraft");
                if (write == null || read == null) throw new Exception("편집 전용 codec API 미구현");
                var document = (ContentDocument)write.Invoke(null, new object[] { source, "level", "level-draft", (Func<UnityEngine.Object,string>)(obj => "unused"), (Func<string,string>)(path => path) });
                if ((int)document.Data["moveCount"] != -1) throw new Exception("미완성 수치 소실");
                var restored = (LevelDefinition)read.Invoke(null, new object[] { document, typeof(LevelDefinition), (Func<string,ScriptableObject>)(id => null), (Func<string,string>)(id => id), (Action<ScriptableObject>)owned.Add });
                if (EditorJsonUtility.ToJson(restored) != EditorJsonUtility.ToJson(source)) throw new Exception("미완성 문서 표시 왕복 불일치");
                bool saveRejected = false;
                try { ContentJson.Write(document); } catch (ContentFormatException) { saveRejected = true; }
                if (!saveRejected) throw new Exception("미완성 문서의 공개 저장이 허용됨");
                Directory.CreateDirectory("Logs/GameAuthoringStage03");
                File.WriteAllText("Logs/GameAuthoringStage03/draft-codec-results.txt", "PASS strict export rejects invalid draft\nPASS draft preserves invalid fields\nPASS draft display roundtrip\nPASS public save rejects draft\n");
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}