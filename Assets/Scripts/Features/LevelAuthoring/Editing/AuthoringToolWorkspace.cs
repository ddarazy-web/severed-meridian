#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    // 화면이나 Unity 수명주기와 무관하게 폴더 전환과 저장의 소유권을 관리한다.
    // UI는 파일 작업 중 편집 입력을 잠그고 이 작업을 작업 스레드에서 호출한다.
    public sealed class AuthoringToolWorkspace
    {
        public AuthoringEditSession Session { get; private set; }
        public string Folder { get; private set; }
        public bool IsRecovery { get; private set; }

        public string ExportState()
        {
            if (Session == null) return null;
            return new JObject
            {
                ["version"] = 1, ["folder"] = Folder, ["recovery"] = IsRecovery,
                ["session"] = JObject.Parse(Session.ExportState())
            }.ToString(Formatting.None);
        }

        public void RestoreState(string text)
        {
            try
            {
                JObject data = JObject.Parse(text);
                if ((int?)data["version"] != 1 || data["folder"]?.Type != JTokenType.String ||
                    !Path.IsPathRooted((string)data["folder"]) || data["recovery"]?.Type != JTokenType.Boolean ||
                    !(data["session"] is JObject state))
                    throw new ContentFormatException("레벨툴 복구본 형식이 잘못됐습니다.");
                AuthoringEditSession restored = AuthoringEditSession.Restore(state.ToString(Formatting.None));
                // 작성 중 문서 오류는 허용하되 도구에서 표시할 수 없는 버전은 거절한다.
                foreach (ContentDocument level in restored.Documents.Where(doc => doc.Kind == "level"))
                    if ((int?)level.Data["schemaVersion"] != 4 && (int?)level.Data["schemaVersion"] != 5)
                        throw new ContentFormatException("레벨툴에서 지원하지 않는 복구 레벨 형식입니다.");
                string folder = Path.GetFullPath((string)data["folder"]);
                Session = restored; Folder = folder; IsRecovery = (bool)data["recovery"];
            }
            catch (JsonException error) { throw new ContentFormatException("레벨툴 복구본이 손상됐습니다.", error); }
        }

        public bool Open(string folder, string dirtyChoice) => OpenCore(folder, dirtyChoice, false);
        public bool OpenBackup(string folder, string dirtyChoice) => OpenCore(folder, dirtyChoice, true);

        public bool Reload(string dirtyChoice)
        {
            if (Session == null) throw new ContentFormatException("먼저 작업 폴더를 여세요.");
            return OpenCore(Folder, dirtyChoice, IsRecovery);
        }

        private bool OpenCore(string folder, string dirtyChoice, bool backup)
        {
            if (dirtyChoice != "Save" && dirtyChoice != "Discard" && dirtyChoice != "Cancel")
                throw new ArgumentException("저장, 버리기, 취소 중 하나를 선택하세요.", nameof(dirtyChoice));
            if (Session != null && Session.IsDirty && dirtyChoice == "Cancel") return false;
            string path = Path.GetFullPath(folder);
            ContentSnapshotStore repository = new ContentSnapshotStore(path);
            // 읽기/버전 검사가 실패하면 버리기를 선택했더라도 현재 초안을 유지한다.
            StoredContentSnapshot stored = backup ? repository.ReadBackup() : repository.Read();
            AuthoringEditSession next = CreateSession(stored, Session?.SelectedLevelId);
            if (Session != null && Session.IsDirty && dirtyChoice == "Save")
            {
                Save();
                // 같은 폴더 재열기는 방금 저장한 내용을 읽어야 한다.
                if (string.Equals(path, Folder, StringComparison.OrdinalIgnoreCase))
                    next = CreateSession(backup ? repository.ReadBackup() : repository.Read(), Session.SelectedLevelId);
            }
            Session = next;
            Folder = path;
            IsRecovery = backup;
            return true;
        }

        public void Save()
        {
            if (Session == null) throw new ContentFormatException("먼저 작업 폴더를 여세요.");
            if (IsRecovery) throw new ContentFormatException("복구본은 사본 저장으로 다른 폴더에 저장하세요. 원본은 보호됩니다.");
            Session.Save(new ContentSnapshotStore(Folder));
        }

        public void SaveCopy(string folder)
        {
            if (Session == null) throw new ContentFormatException("먼저 작업 폴더를 여세요.");
            string path = Path.GetFullPath(folder);
            Session.SaveAs(new ContentSnapshotStore(path));
            // 공개까지 성공한 이후에만 저장 대상과 복구 상태를 바꾼다.
            Folder = path;
            IsRecovery = false;
        }

        public bool Create(string folder, ContentSnapshot template, string dirtyChoice = "Cancel")
        {
            if (dirtyChoice != "Save" && dirtyChoice != "Discard" && dirtyChoice != "Cancel")
                throw new ArgumentException("저장, 버리기, 취소 중 하나를 선택하세요.", nameof(dirtyChoice));
            if (Session?.IsDirty == true && dirtyChoice == "Cancel") return false;
            string path = Path.GetFullPath(folder);
            // 레벨이 없는 템플릿을 먼저 거절해 사용 불가능한 폴더가 남지 않게 한다.
            string id = template.Documents.FirstOrDefault(doc => doc.Kind == "level")?.Id;
            if (id == null) throw new ContentFormatException("프로젝트에 레벨이 필요합니다.");
            ValidateToolVersions(template);
            if (File.Exists(Path.Combine(path, "project.json"))) throw new ContentFormatException("이미 프로젝트가 있는 폴더입니다. 새 폴더를 선택하세요.");
            if (Session?.IsDirty == true && dirtyChoice == "Save") Save();
            StoredContentSnapshot published = new ContentSnapshotStore(path).Publish(template, null);
            Session = new AuthoringEditSession(published, id);
            Folder = path;
            IsRecovery = false;
            return true;
        }

        private static AuthoringEditSession CreateSession(StoredContentSnapshot stored, string preferredId)
        {
            ValidateToolVersions(stored.Snapshot);
            ContentDocument[] levels = stored.Snapshot.Documents.Where(doc => doc.Kind == "level")
                .OrderBy(doc => (int)doc.Data["levelNumber"]).ToArray();
            if (levels.Length == 0) throw new ContentFormatException("프로젝트에 편집할 레벨이 없습니다.");
            string id = levels.Any(doc => doc.Id == preferredId) ? preferredId : levels[0].Id;
            return new AuthoringEditSession(stored, id);
        }

        private static void ValidateToolVersions(ContentSnapshot snapshot)
        {
            foreach (ContentDocument level in snapshot.Documents.Where(doc => doc.Kind == "level"))
                if ((int)level.Data["schemaVersion"] != 4 && (int)level.Data["schemaVersion"] != 5)
                    throw new ContentFormatException("현재 레벨툴은 레벨 형식 4·5를 지원합니다. 기존 편집기에서 변환 후 여세요: " + level.Id);
        }
    }
}
#endif
