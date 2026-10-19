using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;
using LevelTool;

namespace LevelAuthoring.Editor
{
    public static class ProjectTransferWorkflowVerification
    {
#if UNITY_EDITOR_WIN
        public static void RunWindows() => WindowsExercise().Forget();
        public static void RunWindowsImport() => WindowsExercise(true).Forget();

        private static async UniTaskVoid WindowsExercise(bool importOnly = false)
        {
            string directory = Path.GetFullPath("Logs/LevelDataTransferStage01/native-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            using CancellationTokenSource timeout = new CancellationTokenSource(600000);
            using WindowsLevelToolFileTransfer transfer = new WindowsLevelToolFileTransfer();
            try
            {
                string source = Path.GetFullPath("ContentData/Candidates/main");
                StoredContentSnapshot before = new ContentSnapshotStore(source).Read();
                if (!importOnly)
                using (Stream output = await transfer.CreateProject("Windows전달검사.zip", timeout.Token))
                {
                    if (output == null) throw new Exception("Windows 저장 선택 취소");
                    File.WriteAllText(Path.Combine(directory, "destination.txt"), ((FileStream)output).Name);
                    ProjectPackageStore.Export(source, output, timeout.Token, before.Hash);
                    output.Flush();
                }
                using (Stream input = await transfer.OpenProject(timeout.Token))
                {
                    if (input == null) throw new Exception("Windows 열기 선택 취소");
                    string restored = ProjectPackageStore.Import(input, directory, "Windows 복원본", timeout.Token);
                    if (ContentSnapshotFingerprint.Compute(before.Snapshot) != ContentSnapshotFingerprint.Compute(new ContentSnapshotStore(restored).Read().Snapshot))
                        throw new Exception("Windows 실제 파일 왕복 해시 불일치");
                }
                if (new ContentSnapshotStore(source).Read().Hash != before.Hash) throw new Exception("원본 변경");
                File.WriteAllText(Path.Combine(directory, "result.txt"), importOnly
                    ? "PASS Windows native open, ZIP matches current source, original preserved / FAIL 0"
                    : "PASS Windows native save/open, ZIP roundtrip, original preserved / FAIL 0");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                await UniTask.SwitchToMainThread();
                File.WriteAllText(Path.Combine(directory, "result.txt"), "FAIL " + error);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
#endif

        public static void Run()
        {
            string directory = Path.GetFullPath("Logs/LevelDataTransferStage01/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            int passed = 0;
            void Check(bool value, string message)
            {
                if (!value) throw new Exception(message);
                passed++; File.AppendAllText(Path.Combine(directory, "result.txt"), "PASS " + message + "\n");
            }
            try
            {
                string originalFolder = Path.GetFullPath("ContentData/Candidates/main");
                StoredContentSnapshot original = new ContentSnapshotStore(originalFolder).Read();
                string source = Path.Combine(directory, "한글 작업 사본");
                StoredContentSnapshot saved = new ContentSnapshotStore(source).Publish(original.Snapshot, null);
                Check(saved.Snapshot.Documents.Count(value => value.Kind == "level") == 15, "현재 15레벨 복사본");
                File.WriteAllText(Path.Combine(source, "private-log.txt"), "fixture-only");
                File.WriteAllText(Path.Combine(source, "old-backup.json"), "fixture-only");
                File.WriteAllText(Path.Combine(source, "credential-fixture.txt"), "NOT_A_REAL_SECRET");
                string archivePath = Path.Combine(directory, "한글 백업.zip");
                using (FileStream output = new FileStream(archivePath, FileMode.CreateNew))
                    ProjectPackageStore.Export(source, output, CancellationToken.None);
                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    string[] expected = saved.Snapshot.Project.Data["documents"].Select(value => (string)value["path"]).Append("project.json").OrderBy(value => value).ToArray();
                    Check(archive.Entries.Select(entry => entry.FullName).OrderBy(value => value).SequenceEqual(expected), "현재 공개 JSON만 ZIP 포함");
                }
                string imported;
                using (FileStream input = File.OpenRead(archivePath))
                    imported = ProjectPackageStore.Import(input, Path.Combine(directory, "다른 작업 폴더"), "복원본", CancellationToken.None);
                ContentSnapshot restored = new ContentSnapshotStore(imported).Read().Snapshot;
                Check(ContentSnapshotFingerprint.Compute(saved.Snapshot) == ContentSnapshotFingerprint.Compute(restored), "왕복 정규화 콘텐츠 해시 일치");
                Check(saved.Snapshot.Project.Id == restored.Project.Id, "프로젝트 ID 일치");
                Check(saved.Snapshot.Documents.Select(value => value.Id).OrderBy(value => value).SequenceEqual(restored.Documents.Select(value => value.Id).OrderBy(value => value)), "문서 ID 및 참조 검증 통과");
                using (FileStream duplicate = File.OpenRead(archivePath))
                {
                    bool rejected = false;
                    try { ProjectPackageStore.Import(duplicate, Path.Combine(directory, "다른 작업 폴더"), "복원본", CancellationToken.None); }
                    catch (ContentFormatException) { rejected = true; }
                    Check(rejected, "기존 복원 폴더 덮어쓰기 거절");
                }
                using (MemoryStream cancelled = new MemoryStream())
                {
                    bool rejected = false;
                    try { ProjectPackageStore.Export(source, cancelled, new CancellationToken(true)); }
                    catch (OperationCanceledException) { rejected = true; }
                    Check(rejected && cancelled.Length == 0, "취소 시 출력 없음");
                }
                using (WriteFailureStream failed = new WriteFailureStream())
                {
                    bool rejected = false;
                    try { ProjectPackageStore.Export(source, failed, CancellationToken.None); }
                    catch (IOException) { rejected = true; }
                    Check(rejected, "출력 쓰기 실패 전파");
                }
                Check(new ContentSnapshotStore(source).Read().Hash == saved.Hash, "취소 및 쓰기 실패 후 내부 저장본 보존");
                // 이전 제작 형식에서도 새 패키지 필드 없이 그대로 왕복해야 한다.
                string legacy = Path.Combine(directory, "이전 형식 fixture");
                ContentDocument[] legacyDocuments = saved.Snapshot.Documents;
                foreach (ContentDocument level in legacyDocuments.Where(value => value.Kind == "level")) level.Data["schemaVersion"] = 4;
                ContentSnapshot legacySnapshot = new ContentSnapshotStore(legacy).Publish(new ContentSnapshot(legacyDocuments), null).Snapshot;
                string legacyZip = Path.Combine(directory, "기존 구조.zip");
                ZipFile.CreateFromDirectory(legacy, legacyZip);
                using (FileStream package = File.OpenRead(legacyZip))
                {
                    string legacyImport = ProjectPackageStore.Import(package, Path.Combine(directory, "다른 작업 폴더"), "기존 형식", CancellationToken.None);
                    Check(ContentSnapshotFingerprint.Compute(legacySnapshot) == ContentSnapshotFingerprint.Compute(new ContentSnapshotStore(legacyImport).Read().Snapshot), "기존 구조 ZIP fixture 호환 (새 Export 사용 안 함)");
                }
                Check(new ContentSnapshotStore(originalFolder).Read().Hash == original.Hash, "실제 제작 원본 변경 없음");
                File.AppendAllText(Path.Combine(directory, "result.txt"), $"PASS {passed} / FAIL 0\n");
                Debug.Log($"PASS ProjectTransferWorkflowVerification {passed}/FAIL 0: {directory}");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.AppendAllText(Path.Combine(directory, "result.txt"), "FAIL " + error + "\n");
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        private sealed class WriteFailureStream : MemoryStream
        {
            public override void Write(byte[] buffer, int offset, int count) => throw new IOException("fixture write failure");
        }
    }
}
