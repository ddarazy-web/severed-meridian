#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Globalization;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    // 화면이나 Unity 수명주기와 무관하게 폴더 전환과 저장의 소유권을 관리한다.
    // UI는 파일 작업 중 편집 입력을 잠그고 이 작업을 작업 스레드에서 호출한다.
    public sealed partial class AuthoringToolWorkspace
    {
        public AuthoringEditSession Session { get; private set; }
        public string Folder { get; private set; }
        public bool IsRecovery { get; private set; }

        public string ExportState()
        {
            if (Session == null) return null;
            using (StringWriter text = new StringWriter(CultureInfo.InvariantCulture))
            { WriteState(text); return text.ToString(); }
        }
        public void WriteState(TextWriter output)
        {
            if (Session == null) throw new ContentFormatException("기록할 편집 세션이 없습니다.");
            using var writer = new JsonTextWriter(output) { Formatting = Formatting.None, CloseOutput = false };
            writer.WriteStartObject();
            writer.WritePropertyName("version"); writer.WriteValue(1);
            writer.WritePropertyName("folder"); writer.WriteValue(Folder);
            writer.WritePropertyName("recovery"); writer.WriteValue(IsRecovery);
            writer.WritePropertyName("session"); Session.WriteState(writer);
            writer.WriteEndObject();
        }
        public void RestoreState(string text)
        {
            using var input = new StringReader(text);
            RestoreState(input);
        }
        public void RestoreState(TextReader input)
        {
            using var reader = new JsonTextReader(input) { CloseInput = false, DateParseHandling = DateParseHandling.None, MaxDepth = 96 };
            try
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    throw new ContentFormatException("레벨툴 복구본 형식이 잘못됐습니다.");
                JToken version = null, folderValue = null, recovery = null;
                AuthoringEditSession restored = null;
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) throw new ContentFormatException("레벨툴 복구 필드가 잘못됐습니다.");
                    string key = (string)reader.Value;
                    if (!reader.Read()) throw new ContentFormatException("레벨툴 복구본이 중간에 끝났습니다.");
                    switch (key)
                    {
                        case "version": version = JToken.ReadFrom(reader); break;
                        case "folder": folderValue = JToken.ReadFrom(reader); break;
                        case "recovery": recovery = JToken.ReadFrom(reader); break;
                        case "session": restored = AuthoringEditSession.ReadState(reader); break;
                        default: reader.Skip(); break;
                    }
                }
                if (reader.TokenType != JsonToken.EndObject || reader.Read() || (int?)version != 1 || folderValue?.Type != JTokenType.String ||
                    !Path.IsPathRooted((string)folderValue) || recovery?.Type != JTokenType.Boolean || restored == null)
                    throw new ContentFormatException("레벨툴 복구본 형식이 잘못됐습니다.");
                // 작성 중 문서 오류는 허용하되 도구에서 표시할 수 없는 버전은 거절한다.
                foreach (ContentDocumentInfo level in restored.DocumentIdsOfKind("level").Select(restored.GetDocumentInfo))
                    if (level.LevelSchemaVersion != 4 && level.LevelSchemaVersion != 5)
                        throw new ContentFormatException("레벨툴에서 지원하지 않는 복구 레벨 형식입니다.");
                string folder = Path.GetFullPath((string)folderValue);
                if (restored.DirectoryBaseline != null && !string.Equals(restored.DirectoryBaseline.Root.TrimEnd(Path.DirectorySeparatorChar),
                    folder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    throw new ContentFormatException("복구 원본 폴더와 작업 폴더가 다릅니다.");
                if (restored.DirectoryBaseline != null && restored.DirectoryBaseline.IsBackup != (bool)recovery)
                    throw new ContentFormatException("복구 원본 보호 상태가 일치하지 않습니다.");
                Session = restored; Folder = folder; IsRecovery = (bool)recovery; directory = null;
            }
            catch (JsonException error) { throw new ContentFormatException("레벨툴 복구본이 손상됐습니다.", error); }
        }

        // 검증을 마친 임시 작업공간의 세션을 이동한다. 대상 객체와 패널의 참조는 유지한다.
        internal void TakeRestoredState(AuthoringToolWorkspace restored)
        {
            if (restored == null || restored.Session == null || ReferenceEquals(restored, this))
                throw new InvalidOperationException("이동할 복구 작업공간이 없습니다.");
            Session = restored.Session; Folder = restored.Folder; IsRecovery = restored.IsRecovery; directory = restored.directory;
            restored.Session = null; restored.Folder = null; restored.IsRecovery = false; restored.directory = null;
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
            directory = null;
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
            directory = null;
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
            // 프로젝트 생성과 첫 레벨 추가는 별도 동작이다.
            string id = template.Documents.FirstOrDefault(doc => doc.Kind == "level")?.Id;
            ValidateToolVersions(template);
            if (File.Exists(Path.Combine(path, "project.json"))) throw new ContentFormatException("이미 프로젝트가 있는 폴더입니다. 새 폴더를 선택하세요.");
            if (Session?.IsDirty == true && dirtyChoice == "Save") Save();
            StoredContentSnapshot published = new ContentSnapshotStore(path).Publish(template, null);
            Session = new AuthoringEditSession(published, id);
            directory = null;
            Folder = path;
            IsRecovery = false;
            return true;
        }

        private static AuthoringEditSession CreateSession(StoredContentSnapshot stored, string preferredId)
        {
            ValidateToolVersions(stored.Snapshot);
            ContentDocumentInfo[] levels = stored.Snapshot.DocumentIds.Select(stored.Snapshot.GetDocumentInfo).Where(doc => doc.Kind == "level")
                .OrderBy(doc => doc.LevelNumber).ToArray();
            string id = levels.Any(doc => doc.Id == preferredId) ? preferredId : levels.FirstOrDefault()?.Id;
            return new AuthoringEditSession(stored, id);
        }

        private static void ValidateToolVersions(ContentSnapshot snapshot)
        {
            foreach (ContentDocumentInfo level in snapshot.DocumentIds.Select(snapshot.GetDocumentInfo).Where(doc => doc.Kind == "level"))
                if (level.LevelSchemaVersion != 4 && level.LevelSchemaVersion != 5)
                    throw new ContentFormatException("현재 레벨툴은 레벨 형식 4·5를 지원합니다. 기존 편집기에서 변환 후 여세요: " + level.Id);
        }
    }
}
#endif
