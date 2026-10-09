using System;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        // 부모 창이 닫혀 Unity 참조가 null이 되어도 SO 제작 기능을 허용하지 않는다.
        [SerializeField] private bool jsonFlowDraftMode;
        [SerializeField] private string jsonFlowDocumentId;
        [SerializeField] private string jsonFlowSourceLevelId;
        [SerializeField] private string jsonFlowParentFolder;
        internal bool IsJsonFlowDraft => jsonFlowDraftMode;
        internal bool TryPrepareJsonDraftTest(out string reason)
        {
            reason = null;
            if (!IsJsonFlowDraft) return true;
            if (jsonFlowOwner == null || !string.Equals(jsonFlowOwner.jsonFolder, jsonFlowParentFolder, StringComparison.OrdinalIgnoreCase))
            {
                reason = "JSON 원본 창이 닫혔거나 작업 폴더가 바뀌었습니다. 원래 JSON 작업 폴더에서 공통 원본 편집을 다시 여세요. 기본 카탈로그로 대체하여 시험하지 않습니다.";
                return false;
            }
            try
            {
                // 부모/자식 OnEnable 순서는 보장되지 않으므로 부모 표시 세션부터 복구한다.
                jsonFlowOwner.RestoreJsonWorkspace();
                if (!jsonFlowOwner.IsJsonMode || temporaryTutorialSample == null)
                    throw new InvalidOperationException("JSON 작업 세션 또는 편집 사본이 없습니다.");
                Tutorial.TutorialFlowDefinition current = jsonFlowOwner.jsonWorkspace.Resolve(jsonFlowDocumentId) as Tutorial.TutorialFlowDefinition;
                LevelDefinition source = jsonFlowOwner.jsonWorkspace.Resolve(jsonFlowSourceLevelId) as LevelDefinition;
                if (current == null || source == null) throw new InvalidOperationException("공통 구성 또는 원본 레벨이 제거되었습니다.");
                editingTutorialFlow = current;
                // 단계와 파라미터 초안은 유지하고 파괴된 부모 소유 카탈로그 참조만 갱신한다.
                if (temporaryTutorialSample.ElementCatalog != source.ElementCatalog)
                {
                    using SerializedObject edit = new SerializedObject(temporaryTutorialSample);
                    edit.FindProperty("elementCatalog").objectReferenceValue = source.ElementCatalog;
                    edit.ApplyModifiedPropertiesWithoutUndo();
                }
                return true;
            }
            catch (Exception error)
            {
                reason = "JSON 원본 연결을 복구하지 못했습니다. 공통 원본 편집을 다시 여세요: " + error.Message;
                return false;
            }
        }
        internal const string JsonFlowDraftRestriction = "JSON 공통 원본의 독립 편집 사본입니다. 현재 사본 편집·시험과 공통 원본 적용만 가능합니다. 폴더 전환, 카탈로그 연결, 샘플 저장, 맵 모양 관리는 부모 JSON 작업 창에서 하세요. 배포 팩은 이 창에서 읽거나 갱신하지 않습니다.";
        private bool BlockJsonDraftExternalAction()
        {
            if (!IsJsonFlowDraft) return false;
            if (operation != null) operation.text = JsonFlowDraftRestriction;
            return true;
        }
    }
}
