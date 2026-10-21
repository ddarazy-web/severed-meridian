#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Threading;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;

namespace LevelAuthoring.Editing
{
    public sealed partial class AuthoringToolWorkspace
    {
        private ContentProjectDirectory directory;
        public ContentProjectDirectory Directory => Session?.DirectoryBaseline ?? directory;

        // 레벨툴의 열기는 목록만 준비한다. 편집 세션은 편집 버튼을 누른 뒤 만든다.
        public bool OpenListing(string folder, string dirtyChoice, bool backup = false)
        {
            if (dirtyChoice != "Save" && dirtyChoice != "Discard" && dirtyChoice != "Cancel")
                throw new ArgumentException("저장, 버리기, 취소 중 하나를 선택하세요.", nameof(dirtyChoice));
            if (Session?.IsDirty == true && dirtyChoice == "Cancel") return false;
            string path = Path.GetFullPath(folder);
            var repository = new ContentSnapshotStore(path);
            ContentProjectDirectory candidate = backup ? repository.ReadBackupDirectory() : repository.ReadDirectory();
            try
            {
                if (Session?.IsDirty == true && dirtyChoice == "Save")
                {
                    Save();
                    if (string.Equals(path, Folder, StringComparison.OrdinalIgnoreCase))
                    { candidate.Dispose(); candidate = backup ? repository.ReadBackupDirectory() : repository.ReadDirectory(); }
                }
                directory = candidate; Session = null; Folder = path; IsRecovery = backup;
                return true;
            }
            catch { candidate.Dispose(); throw; }
        }

        public bool ReloadListing(string dirtyChoice)
        {
            if (Folder == null) throw new ContentFormatException("먼저 작업 폴더를 여세요.");
            return OpenListing(Folder, dirtyChoice, IsRecovery);
        }

        public void RebuildListing(CancellationToken cancellationToken, Action<int, int> progress = null)
        {
            if (Directory == null || IsRecovery) throw new ContentFormatException("현재 프로젝트의 파일 목록을 연 뒤 실행하세요.");
            using var candidate = new ContentSnapshotStore(Folder).RebuildDirectoryIndex(cancellationToken, progress);
            cancellationToken.ThrowIfCancellationRequested();
            if (Session != null) Session.ApplyListing(candidate);
            else Directory.ApplyListing(candidate);
        }

        public void PrepareLevel(string id)
        {
            if (Session == null && Directory == null) throw new ContentFormatException("먼저 작업 폴더를 여세요.");
            ContentDocument document = Session != null ? Session.Get(id) : Directory.ReadDocument(id);
            if (document.Kind != "level") throw new ContentFormatException("이 파일은 레벨이 아닙니다: " + id);
            int version = document.Info.LevelSchemaVersion;
            if (version != 4 && version != 5) throw new ContentFormatException("레벨툴에서 지원하지 않는 레벨 형식입니다: " + id);
        }

        public void OpenLevel(string id)
        {
            PrepareLevel(id);
            if (Session == null) Session = new AuthoringEditSession(Directory, id);
            else Session.SelectLevel(id);
        }
    }
}
#endif
