#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private AuthoringEditSession displaySession;
        private ContentProjectDirectory displayDirectory;
        private readonly Dictionary<string, long> displayVersions = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentDocument> displayDocuments = new Dictionary<string, ContentDocument>(StringComparer.Ordinal);
        private string displayedLevelId, propertiesKey;
        private JObject displayedLevel;
        private long definitionRevision, boardRevision, propertiesRevision;
        private bool forceProperties;
        private int displayDocumentReadCount, definitionBuildCount;
        private long displayContentVersion = -1;
        private long displayMetadataVersion = -1;

        // 내용 대신 불변 문서의 버전만 비교한다. 공유 사본과 선택 레벨도 미리보기의 의존성이다.
        private string DisplayContentKey() => Session == null ? "" :
            Session.SelectedLevelId + "|" + Session.ContentVersion + "|" +
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
            bool reset = !ReferenceEquals(displaySession, Session) || Session == null && !ReferenceEquals(displayDirectory, Workspace.Directory);
            if (reset)
            {
                displaySession = Session;
                displayDirectory = Workspace.Directory;
                displayVersions.Clear(); displayDocuments.Clear();
                displayContentVersion = -1;
                displayMetadataVersion = -1;
                validatedDocuments = null; validationComparisonKey = null;
                levelIndex = new LevelBrowserIndex(); browserRevision = -1;
                displayedLevelId = null; displayedLevel = null;
                paletteKey = boardKey = propertiesKey = null;
            }
            if (Session == null)
            {
                if (reset && Workspace.Directory != null)
                {
                    ContentDocument project = Workspace.Directory.Project; displayDocuments[project.Id] = project;
                    foreach (ProjectDocumentEntry entry in Workspace.Directory.Entries)
                        if (entry.Info == null) levelIndex.Upsert(entry.Id, 0, entry.Id + " · " + System.IO.Path.GetFileName(entry.RelativePath));
                        else if (entry.Info.Kind == "level") levelIndex.Upsert(entry.Id, entry.Info.LevelNumber, entry.Info.DisplayName);
                }
                return;
            }
            bool metadataChanged = displayMetadataVersion != Session.MetadataVersion;
            long metadataVersion = Session.MetadataVersion;
            bool definitionsChanged = reset, documentsChanged = displayContentVersion != Session.ContentVersion || metadataChanged;
            if (documentsChanged)
            {
                string[] ids = Session.DocumentIds;
                foreach (string removed in displayVersions.Keys.Except(ids).ToArray())
                {
                    definitionsChanged |= levelIndex.Get(removed) == null;
                    levelIndex.Remove(removed);
                    displayDocuments.Remove(removed); displayVersions.Remove(removed);
                }
                foreach (string id in ids)
                {
                    long version = Session.GetDocumentVersion(id);
                    bool versionChanged = !displayVersions.TryGetValue(id, out long previous) || previous != version;
                    if (!versionChanged && !metadataChanged) continue;
                    ContentDocumentInfo info = Session.GetDocumentInfo(id);
                    if (info.Kind == "level") levelIndex.Upsert(id, info.LevelNumber, info.DisplayName);
                    else if (info.Kind == "unknown") levelIndex.Upsert(id, 0, id + " · " + info.DisplayName);
                    else { levelIndex.Remove(id); definitionsChanged = true; }
                    if (info.Kind == "project" || id == Session.SelectedLevelId)
                    {
                        if (versionChanged || !displayDocuments.ContainsKey(id))
                        { displayDocuments[id] = Session.Get(id); displayDocumentReadCount++; }
                    }
                    else displayDocuments.Remove(id);
                    displayVersions[id] = version;
                }
            }
            // 이전 선택의 본문은 화면 캐시에 남기지 않는다. Undo는 세션의 불변 버전이 보존한다.
            foreach (string stale in displayDocuments.Where(pair => pair.Value.Kind == "level" && pair.Key != Session.SelectedLevelId).Select(pair => pair.Key).ToArray())
                displayDocuments.Remove(stale);
            if (!displayDocuments.ContainsKey(Session.SelectedLevelId))
            {
                displayDocuments[Session.SelectedLevelId] = Session.Get(Session.SelectedLevelId); displayDocumentReadCount++;
            }
            JObject level = displayDocuments[Session.SelectedLevelId].Data;
            levelIndex.Upsert(Session.SelectedLevelId, (int)level["levelNumber"], (string)level["displayName"]);
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
            displayContentVersion = Session.ContentVersion;
            displayMetadataVersion = metadataVersion;
        }
    }
}
#endif
