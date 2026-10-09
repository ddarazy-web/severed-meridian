using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelToolSceneVerification
    {
        public static void Run()
        {
            try
            {
                Type screen = typeof(LevelAuthoring.Runtime.JsonPuzzlePlayAdapter).Assembly.GetType("LevelTool.LevelToolScreen");
                if (screen == null) throw new Exception("실행용 레벨툴 화면이 없습니다.");
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/LevelTool.unity") == null)
                    throw new Exception("실행용 레벨툴 씬이 없습니다.");
                var template = LevelTool.LevelToolDocuments.NewProject();
                if (template == null) throw new Exception("새 프로젝트 기본 문서 생성 실패");
                var fixtures = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read().Snapshot;
                ContentDocument old = fixtures.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["schemaVersion"] == 4);
                JObject preview = LevelTool.LevelToolDocuments.UpgradePreview(old.Data);
                if ((int)old.Data["schemaVersion"] != 4 || (int)preview["schemaVersion"] != 5 || !preview["elements"].Any())
                    throw new Exception("구형 미리보기 변환 실패 또는 원본 변경");
                foreach (var field in old.Data.Properties())
                    if (!new[] { "schemaVersion", "elements", "elementSupply" }.Contains(field.Name) && !JToken.DeepEquals(field.Value, preview[field.Name]))
                        throw new Exception("구형 변환이 범위 밖 필드를 변경: " + field.Name);
                new ContentSnapshot(fixtures.Documents.Select(doc => doc.Id == old.Id ? new ContentDocument("level", old.Id, preview) : doc));
                Debug.Log("PASS legacy preview preserves advanced fields and validates");
                var stored = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read();
                var session = new Editing.AuthoringEditSession(stored, old.Id);
                string catalogId = (string)session.Get(old.Id).Data["catalogId"];
                string definitionId = session.Get(catalogId).Data["definitionIds"].Values<string>().First();
                JObject authored = (JObject)session.Get(definitionId).Data["definition"];
                session.Apply("부분 카탈로그", docs => docs[catalogId].Data["definitionIds"] = new JArray(definitionId));
                var resolved = LevelTool.LevelToolDocuments.Definitions(session);
                if (resolved.Count <= 1 || !JToken.DeepEquals(resolved[(string)authored["id"]], authored))
                    throw new Exception("부분 카탈로그 기본 정의 보충 또는 제작 정의 우선순위 실패");
                JObject custom = (JObject)authored.DeepClone();
                custom["id"] = "test.custom";
                session.Apply("내장 정의", docs =>
                {
                    docs[old.Id].Data["catalogId"] = null;
                    docs[old.Id].Data["embeddedDefinitions"] = new JArray(custom);
                });
                resolved = LevelTool.LevelToolDocuments.Definitions(session);
                if (resolved.Count != 1 || !resolved.ContainsKey("test.custom") || !JToken.DeepEquals(resolved["test.custom"], custom))
                    throw new Exception("내장 정의의 배타적 우선순위 실패");
                Debug.Log("PASS partial catalog fallback and embedded definitions");
                Debug.Log("PASS level tool scene and runtime screen");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
