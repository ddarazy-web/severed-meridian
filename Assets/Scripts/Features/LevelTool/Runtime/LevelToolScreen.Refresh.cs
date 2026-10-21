#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private AuthoringEditSession displaySession;
        private readonly Dictionary<string, long> displayVersions = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentDocument> displayDocuments = new Dictionary<string, ContentDocument>(StringComparer.Ordinal);
        private string displayedLevelId, propertiesKey;
        private JObject displayedLevel;
        private long definitionRevision, boardRevision, propertiesRevision;
        private bool forceProperties;
        private int displayDocumentReadCount, definitionBuildCount;

        // 내용 대신 불변 문서의 버전만 비교한다. 공유 사본과 선택 레벨도 미리보기의 의존성이다.
        private string DisplayContentKey() => Session == null ? "" :
            Session.SelectedLevelId + "|" + string.Join(",", Session.DocumentIds.OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => Session.GetDocumentVersion(id))) + "|" +
            (sharedTutorialDraft == null ? "" : sharedTutorialDraft.FlowId + ":" +
                sharedTutorialDraft.Session.GetDocumentVersion(sharedTutorialDraft.FlowId));

        // 내부 상태만 바꾸는 튜토리얼/시험 도구는 문서 변경 없이도 자체 패널을 다시 구성해야 한다.
        private void RefreshProperties()
        {
            forceProperties = true;
            Refresh();
        }

        private void PrepareDisplay()
        {
            bool reset = !ReferenceEquals(displaySession, Session);
            if (reset)
            {
                displaySession = Session;
                displayVersions.Clear(); displayDocuments.Clear();
                levelIndex = new LevelBrowserIndex(); browserRevision = -1;
                displayedLevelId = null; displayedLevel = null;
                paletteKey = boardKey = propertiesKey = null;
            }
            if (Session == null) return;
            bool definitionsChanged = reset, documentsChanged = reset;
            string[] ids = Session.DocumentIds;
            foreach (string removed in displayDocuments.Keys.Except(ids).ToArray())
            {
                definitionsChanged |= displayDocuments[removed].Kind != "level";
                levelIndex.Remove(removed);
                displayDocuments.Remove(removed); displayVersions.Remove(removed); documentsChanged = true;
            }
            foreach (string id in ids)
            {
                long version = Session.GetDocumentVersion(id);
                if (displayVersions.TryGetValue(id, out long previous) && previous == version) continue;
                ContentDocument document = Session.Get(id);
                if (document.Kind == "level") levelIndex.Upsert(id, (int)document.Data["levelNumber"], (string)document.Data["displayName"]);
                displayDocumentReadCount++;
                definitionsChanged |= document.Kind != "level";
                displayDocuments[id] = document; displayVersions[id] = version; documentsChanged = true;
            }
            JObject level = displayDocuments[Session.SelectedLevelId].Data;
            bool selected = displayedLevelId != Session.SelectedLevelId;
            definitionsChanged |= selected || !JToken.DeepEquals(displayedLevel?["catalogId"], level["catalogId"]) ||
                !JToken.DeepEquals(displayedLevel?["embeddedDefinitions"], level["embeddedDefinitions"]);
            if (definitionsChanged) { definitions = LevelToolDocuments.Definitions(Session); definitionRevision++; definitionBuildCount++; }
            // 이름·번호는 셀 표현에 영향을 주지 않는다. 다른 필드는 표시 의존성을 보수적으로 유지한다.
            bool boardChanged = selected || definitionsChanged || displayedLevel == null;
            if (!boardChanged && !ReferenceEquals(displayedLevel, level))
                boardChanged = level.Properties().Any(p => p.Name != "displayName" && p.Name != "levelNumber" &&
                    !JToken.DeepEquals(p.Value, displayedLevel[p.Name])) ||
                    displayedLevel.Properties().Any(p => p.Name != "displayName" && p.Name != "levelNumber" && level.Property(p.Name) == null);
            if (boardChanged) boardRevision++;
            if (documentsChanged || selected) propertiesRevision++;
            displayedLevelId = Session.SelectedLevelId; displayedLevel = level;
        }
    }
}
#endif
