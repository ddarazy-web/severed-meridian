using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Editing;
using LevelAuthoring.Editor;
using LevelAuthoring.Runtime;
using Levels;
using Tutorial;
using UnityEditor;
using UnityEngine;

namespace LevelTool.Editor
{
    public static class LevelToolLegacyImport
    {
        public static string CreateDraftWorkspace(LevelDefinition source, TutorialFlowDefinition parameters, string destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var exported = LegacyContentExporter.Export(destination);
            var workspace = new AuthoringToolWorkspace(); workspace.Open(destination, "Cancel");
            var ids = exported.Snapshot.Project.Data["sourceIds"].ToDictionary(item => (string)item["sourceGuid"], item => (string)item["documentId"]);
            var resources = exported.Snapshot.Project.Data["resources"].ToDictionary(item => (string)item["path"], item => (string)item["id"]);
            string levelId = "level-" + Guid.NewGuid().ToString("N"), flowId = "tutorialFlow-" + Guid.NewGuid().ToString("N");
            var draft = UnityEngine.Object.Instantiate(source);
            TutorialFlowDefinition flow = parameters == null ? null : UnityEngine.Object.Instantiate(parameters);
            try
            {
                if (flow != null)
                {
                    flow.steps = TutorialAuthoringRules.CopySteps(source.Tutorial.steps);
                    draft.Tutorial.flow = flow;
                }
                string Reference(UnityEngine.Object value)
                {
                    if (value == null) return null;
                    if (flow != null && value == flow) return flowId;
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
                    if (ids.TryGetValue(guid, out string id)) return id;
                    throw new InvalidOperationException("임시 참조의 원본 JSON 작업 창에서 인계해 주세요: " + value.name);
                }
                string Resource(string path) => resources.TryGetValue(path, out string id) ? id : throw new InvalidOperationException("등록되지 않은 표현 리소스: " + path);
                var level = UnityAuthoringCodec.WriteDraft(draft, "level", levelId, Reference, Resource);
                level.Data["levelNumber"] = workspace.Session.Documents.Where(doc => doc.Kind == "level").Max(doc => (int)doc.Data["levelNumber"]) + 1;
                var shared = flow == null ? null : UnityAuthoringCodec.WriteDraft(flow, "tutorialFlow", flowId, Reference, Resource);
                // 불완전한 사본은 편집 이력에만 추가한다. 수정·검사 전에는 디스크에 공개 저장하지 않는다.
                workspace.Session.Apply("임시 튜토리얼 독립 사본 가져오기", docs =>
                {
                    if (shared != null) docs.Add(flowId, shared);
                    docs.Add(levelId, level);
                });
                workspace.Session.SelectLevel(levelId);
                return workspace.ExportState();
            }
            finally { UnityEngine.Object.DestroyImmediate(draft); if (flow != null) UnityEngine.Object.DestroyImmediate(flow); }
        }

        public static string CreateWorkspace(LevelDefinition source, string destination)
        {
            string path = AssetDatabase.GetAssetPath(source);
            if (source == null || string.IsNullOrEmpty(path)) throw new InvalidOperationException("저장된 구형 레벨 에셋을 선택하세요.");
            string guid = AssetDatabase.AssetPathToGUID(path);
            var exported = LegacyContentExporter.Export(destination);
            string id = (string)exported.Snapshot.Project.Data["sourceIds"].Single(item => (string)item["sourceGuid"] == guid)["documentId"];
            var workspace = new AuthoringToolWorkspace(); workspace.Open(destination, "Cancel"); workspace.Session.SelectLevel(id);
            return workspace.ExportState();
        }

        public static void Open(LevelDefinition source)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode를 종료한 뒤 구형 자료를 가져오세요.");
            if (source == null) { LevelToolLauncher.Launch(); return; }
            if (!EditorUtility.DisplayDialog("구형 레벨을 JSON 사본으로 가져오기",
                "현재 제작 에셋과 참조 자료를 별도 JSON 폴더로 복사하고 선택한 레벨을 엽니다. 원래 SO와 출시 팩은 변경하지 않습니다.\n이미 만든 JSON 작업은 레벨툴의 ‘폴더 열기’를 사용하세요.", "사본 만들기", "취소")) return;
            string folder = Path.GetFullPath("ContentData/Trials/game-authoring-stage-02/import-" + Guid.NewGuid().ToString("N"));
            LevelToolLauncher.LaunchWorkspace(CreateWorkspace(source, folder));
        }
    }
}
