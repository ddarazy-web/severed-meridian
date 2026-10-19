using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using Levels;
using Simulation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class ExternalProjectImportVerification
    {
        public static void RunReviewRegression()
        {
            string root = Path.GetFullPath("Logs/LevelDataTransferStage02/review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(root);
            int failed = 0;
            void Check(bool value, string message)
            {
                if (!value) failed++;
                File.AppendAllText(Path.Combine(root, "result.txt"), (value ? "PASS " : "FAIL ") + message + "\n");
            }
            try
            {
                var snapshot = new ContentSnapshotStore(Path.GetFullPath("ContentData/Candidates/main")).Read().Snapshot;
                var documents = snapshot.Documents;
                var levels = documents.Where(value => value.Kind == "level").ToArray();
                levels[1].Data["levelNumber"] = levels[0].Data["levelNumber"];
                string diagnostic = "";
                try { _ = new ContentSnapshot(documents); }
                catch (ContentFormatException error) { diagnostic = error.Message; }
                Check(diagnostic.Contains(levels[1].Id) && diagnostic.Contains("levelNumber") && diagnostic.Contains("레벨 " + levels[0].Data["levelNumber"]), "중복 레벨 오류에 ID·번호·필드 표시");
                documents = snapshot.Documents;
                var missingReference = documents.First(value => value.Kind == "level");
                missingReference.Data["catalogId"] = "catalog-import-missing";
                diagnostic = "";
                try { _ = new ContentSnapshot(documents); }
                catch (ContentFormatException error) { diagnostic = error.Message; }
                Check(diagnostic.Contains(missingReference.Id) && diagnostic.Contains("catalogId") && diagnostic.Contains("레벨 " + missingReference.Data["levelNumber"]), "누락 참조 오류에 ID·번호·필드 표시");
                documents = snapshot.Documents;
                documents.Single(value => value.Kind == "project").Data["resources"][0]["path"] = "Assets/Scripts/Features/LevelAuthoring/Editor/Import/AuthoringSourceSelection.cs";
                string folder = Path.Combine(root, "existing-nonvisual");
                new ContentSnapshotStore(folder).Publish(new ContentSnapshot(documents), null);
                var report = ExternalProjectImport.Inspect(folder, Path.Combine(root, "source.json"), CancellationToken.None);
                Check(!report.CanAdopt && report.Errors.Any(value => value.Contains("AuthoringSourceSelection.cs")), "존재하는 비시각 에셋도 런타임 아틀라스로 해석 불가 시 거절");
                EditorApplication.Exit(failed == 0 ? 0 : 1);
            }
            catch (Exception error) { File.AppendAllText(Path.Combine(root, "result.txt"), "FAIL " + error); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void RunUI()
        {
            string root = Path.GetFullPath("Logs/LevelDataTransferStage02/ui-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(root);
            ContentImportWindow window = null;
            void Finish(Exception error)
            {
                File.WriteAllText(Path.Combine(root, "result.txt"), error == null ? "PASS EditorWindow panel, review gating, adoption, pending resources / FAIL 0" : "FAIL " + error);
                if (window != null) window.Close();
                if (error != null) Debug.LogException(error);
                EditorApplication.Exit(error == null ? 0 : 1);
            }
            try
            {
                string zip = Path.Combine(root, "test.zip");
                using (var output = File.Create(zip)) ProjectPackageStore.Export(Path.GetFullPath("ContentData/Candidates/main"), output, CancellationToken.None);
                window = ScriptableObject.CreateInstance<ContentImportWindow>();
                window.ConfigurationPath = Path.Combine(root, "settings", "source.json");
                window.CandidatesRoot = Path.Combine(root, "candidates");
                window.Show();
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var view = window.rootVisualElement;
                        if (view.panel == null) throw new Exception("EditorWindow 패널 미연결");
                        window.LoadZip(zip);
                        var button = view.Q<Button>("adopt-candidate");
                        if (button.enabledSelf) throw new Exception("확인 전 반영 버튼 활성화");
                        view.Q<Toggle>("confirm-replacement").value = true;
                        if (!button.enabledSelf || !view.Q<Label>("import-status").text.Contains("검사 통과")) throw new Exception("검사 결과 및 확인 게이트 오류");
                        typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .Invoke(button.clickable, new object[] { null });
                        EditorApplication.delayCall += () =>
                        {
                            try
                            {
                                if (!File.Exists(window.ConfigurationPath) || !view.Q<Label>("import-status").text.Contains("리소스 갱신 필요"))
                                    throw new Exception("실제 반영 버튼 실행/팩 미변환 안내 실패");
                                if (button.enabledSelf) throw new Exception("중복 반영 버튼 잠금 실패");
                                _ = new ContentSnapshotStore(AuthoringSourceSelection.ReadFolder(window.ConfigurationPath)).Read();
                                Finish(null);
                            }
                            catch (Exception error) { Finish(error); }
                        };
                    }
                    catch (Exception error) { Finish(error); }
                };
            }
            catch (Exception error) { Finish(error); }
        }

        public static void Run()
        {
            string root = Path.GetFullPath("Logs/LevelDataTransferStage02/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(root);
            int passed = 0;
            void Check(bool condition, string message)
            {
                if (!condition) throw new Exception(message);
                passed++; File.AppendAllText(Path.Combine(root, "result.txt"), "PASS " + message + "\n");
            }
            void Reject(Action action, string message)
            {
                bool rejected = false;
                try { action(); } catch (ContentFormatException) { rejected = true; }
                Check(rejected, message);
            }
            try
            {
                string realSelection = File.ReadAllText(AuthoringSourceSelection.DefaultConfiguration);
                var packs = Directory.GetFiles("Assets/Data", "*.bytes", SearchOption.AllDirectories)
                    .ToDictionary(path => path, path => File.ReadAllBytes(path));
                var original = new ContentSnapshotStore(Path.GetFullPath("ContentData/Candidates/main")).Read();
                string selection = Path.Combine(root, "settings", "source.json");
                string oldFolder = Path.Combine(root, "old");
                var old = new ContentSnapshotStore(oldFolder).Publish(original.Snapshot, null);
                Directory.CreateDirectory(Path.GetDirectoryName(selection));
                File.WriteAllText(selection, new JObject { ["version"] = 1, ["folder"] = "../old", ["projectId"] = old.Snapshot.Project.Id, ["adoptedHash"] = old.Hash }.ToString());
                string previous = File.ReadAllText(selection);
                using var archive = new MemoryStream();
                ProjectPackageStore.Export(oldFolder, archive, CancellationToken.None);
                archive.Position = 0;
                var report = ExternalProjectImport.Import(archive, Path.Combine(root, "candidates"), selection, CancellationToken.None);
                Check(report.CanAdopt, "현재 15레벨·튜토리얼·요소·표현·팩 검증: " + string.Join(" / ", report.Errors));
                Check(report.Changes.All(value => value.StartsWith("유지")), "동일 내용은 유지로 비교");
                Check(File.ReadAllText(selection) == previous, "후보 검사만으로 설정 변경 없음");
                Reject(() => ExternalProjectImport.Adopt(report, false, CancellationToken.None), "명시적 교체 확인 필요");
                string candidateText = File.ReadAllText(Path.Combine(report.Folder, "project.json"));
                File.AppendAllText(Path.Combine(report.Folder, "project.json"), " ");
                Reject(() => ExternalProjectImport.Adopt(report, true, CancellationToken.None), "검사 후 후보 변경 거절");
                File.WriteAllText(Path.Combine(report.Folder, "project.json"), candidateText);
                string oldText = File.ReadAllText(Path.Combine(oldFolder, "project.json"));
                File.AppendAllText(Path.Combine(oldFolder, "project.json"), " ");
                Reject(() => ExternalProjectImport.Adopt(report, true, CancellationToken.None), "검사 후 기존 원본 변경 거절");
                File.WriteAllText(Path.Combine(oldFolder, "project.json"), oldText);
                File.AppendAllText(selection, " ");
                Reject(() => ExternalProjectImport.Adopt(report, true, CancellationToken.None), "검사 후 선택 설정 변경 거절");
                File.WriteAllText(selection, previous);
                string backup = ExternalProjectImport.Adopt(report, true, CancellationToken.None);
                Check(File.ReadAllText(backup) == previous, "이전 선택 복구 사본 보존");
                Check(AuthoringSourceSelection.ReadFolder(selection) == report.Folder, "기존 원본 읽기 계약으로 채택 후보 조회");
                var adopted = new ContentSnapshotStore(AuthoringSourceSelection.ReadFolder(selection)).Read().Snapshot;
                using (var graph = new AuthoringObjectGraph(adopted.Documents))
                {
                    var playLevel = (LevelDefinition)graph.Resolve(adopted.Documents.First(value => value.Kind == "level").Id);
                    Check(StartingBoardBuilder.Build(playLevel, playLevel.HasTutorial ? playLevel.Tutorial.seed : 1).Status == StartingBoardStatus.Success,
                        "채택 JSON을 Unity 게임 시험 객체로 읽어 시작 보드 생성");
                }
                Check(new ContentSnapshotStore(oldFolder).Read().Snapshot.Project.Id == old.Snapshot.Project.Id, "이전 원본 폴더 유지");
                bool cancelled = false;
                archive.Position = 0;
                try { ExternalProjectImport.Import(archive, Path.Combine(root, "cancelled"), selection, new CancellationToken(true)); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && !Directory.Exists(Path.Combine(root, "cancelled")), "취소 시 후보 및 설정 변경 없음");
                bool corrupt = false;
                using (var broken = new MemoryStream(new byte[] { 1, 2, 3 }))
                {
                    try { ExternalProjectImport.Import(broken, Path.Combine(root, "broken"), selection, CancellationToken.None); }
                    catch (InvalidDataException) { corrupt = true; }
                    catch (ContentFormatException) { corrupt = true; }
                }
                Check(corrupt, "손상 ZIP 거절");
                using (var unsupported = new MemoryStream())
                {
                    using (var zip = new ZipArchive(unsupported, ZipArchiveMode.Create, true))
                    using (var writer = new StreamWriter(zip.CreateEntry("project.json").Open()))
                        writer.Write("{\"kind\":\"project\",\"schemaVersion\":999,\"id\":\"project-test\",\"data\":{}}");
                    unsupported.Position = 0;
                    Reject(() => ExternalProjectImport.Import(unsupported, Path.Combine(root, "unsupported"), selection, CancellationToken.None), "미지원 스키마 ZIP 거절");
                }
                bool validationCancelled = false;
                string beforeCancel = File.ReadAllText(selection);
                try { ExternalProjectImport.Inspect(report.Folder, selection, CancellationToken.None, (_, __) => throw new OperationCanceledException()); }
                catch (OperationCanceledException) { validationCancelled = true; }
                Check(validationCancelled && File.ReadAllText(selection) == beforeCancel, "검사 도중 취소 시 선택 설정 보존");
                var documents = original.Snapshot.Documents;
                var level = documents.First(value => value.Kind == "level");
                level.Data["levelNumber"] = (int)documents.Last(value => value.Kind == "level").Data["levelNumber"];
                string invalid = Path.Combine(root, "duplicate");
                var duplicateStored = new ContentSnapshotStore(invalid).Publish(original.Snapshot, null);
                string duplicatePath = Path.Combine(invalid, (string)duplicateStored.Snapshot.Project.Data["documents"].Single(value => (string)value["id"] == level.Id)["path"]);
                File.WriteAllText(duplicatePath, ContentJson.Write(level));
                Reject(() => ExternalProjectImport.Inspect(invalid, selection, CancellationToken.None), "중복 번호 후보 거절");
                documents = original.Snapshot.Documents;
                documents.Single(value => value.Kind == "project").Data["resources"][0]["path"] = "Assets/Missing-import-fixture.png";
                invalid = Path.Combine(root, "missing-resource");
                new ContentSnapshotStore(invalid).Publish(new ContentSnapshot(documents), null);
                var missing = ExternalProjectImport.Inspect(invalid, selection, CancellationToken.None);
                Check(!missing.CanAdopt && missing.Errors.Any(value => value.Contains("Missing-import-fixture")), "누락 표현 리소스 거절");
                Reject(() => ExternalProjectImport.Adopt(missing, true, CancellationToken.None), "실패 후보 채택 금지");
                documents = original.Snapshot.Documents;
                var removed = documents.Last(value => value.Kind == "level");
                var project = documents.Single(value => value.Kind == "project");
                project.Data["sourceIds"] = new JArray(project.Data["sourceIds"].Where(value => (string)value["documentId"] != removed.Id));
                var newProject = new ContentDocument("project", "project-import-test-other", project.Data);
                documents = documents.Where(value => value.Id != removed.Id && value.Kind != "project").Append(newProject).ToArray();
                level = documents.First(value => value.Kind == "level");
                level.Data["levelNumber"] = 51;
                var added = new ContentDocument("level", "level-import-test-added", level.Data);
                added.Data["levelNumber"] = 100;
                string changedFolder = Path.Combine(root, "changed");
                new ContentSnapshotStore(changedFolder).Publish(new ContentSnapshot(documents.Append(added)), null);
                var changed = ExternalProjectImport.Inspect(changedFolder, selection, CancellationToken.None);
                Check(changed.CanAdopt && changed.DifferentProject && changed.RemovedCount > 0, "다른 프로젝트 및 제외 예정 식별");
                Check(changed.Changes.Any(value => value.StartsWith("변경") && value.Contains("레벨 51")) &&
                    changed.Changes.Any(value => value.StartsWith("추가") && value.Contains("레벨 100")) &&
                    changed.Changes.Any(value => value.StartsWith("제외 예정") && value.Contains(removed.Id)), "문서 ID와 레벨 번호 기준 추가·변경·제외 비교");
                string selected = File.ReadAllText(selection);
                bool locked = false;
                using (var fileLock = new FileStream(selection + ".import.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    try { ExternalProjectImport.Adopt(changed, true, CancellationToken.None); }
                    catch (IOException) { locked = true; }
                }
                Check(locked && File.ReadAllText(selection) == selected, "채택 충돌 실패 시 기존 설정 보존");
                Check(new ContentSnapshotStore(Path.GetFullPath("ContentData/Candidates/main")).Read().Hash == original.Hash, "실제 제작 데이터 보존");
                Check(File.ReadAllText(AuthoringSourceSelection.DefaultConfiguration) == realSelection, "실제 원본 선택 보존");
                Check(packs.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "실제 MemoryPack 바이트 보존");
                File.AppendAllText(Path.Combine(root, "result.txt"), $"PASS {passed} / FAIL 0\n");
                Debug.Log("PASS ExternalProjectImport: " + passed + " / FAIL 0: " + root);
                EditorApplication.Exit(0);
            }
            catch (Exception error) { File.AppendAllText(Path.Combine(root, "result.txt"), "FAIL " + error); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
