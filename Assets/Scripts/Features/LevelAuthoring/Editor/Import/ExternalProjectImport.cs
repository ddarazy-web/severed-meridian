using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editor
{
    public sealed class ExternalImportReport
    {
        public string Folder { get; internal set; }
        public string ProjectId { get; internal set; }
        public bool DifferentProject { get; internal set; }
        public IReadOnlyList<string> Changes { get; internal set; }
        public IReadOnlyList<string> Errors { get; internal set; }
        public int RemovedCount { get; internal set; }
        public bool CanAdopt => Errors.Count == 0;
        internal string CandidateHash, ConfigurationPath, PreviousConfiguration, PreviousFolder, PreviousHash;
    }

    // 외부 제작 원본은 SO 이관 보고서와 비교하지 않는다. 검사 후 선택 설정만 교체한다.
    public static class ExternalProjectImport
    {
        public static ExternalImportReport Import(Stream zip, string candidatesRoot, string configurationPath, CancellationToken token, Action<string, float> progress = null)
        {
            string folder = ProjectPackageStore.Import(zip, candidatesRoot,
                "import-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"), token);
            return Inspect(folder, configurationPath, token, progress);
        }

        public static ExternalImportReport Inspect(string folder, string configurationPath, CancellationToken token, Action<string, float> progress = null)
        {
            token.ThrowIfCancellationRequested();
            folder = Path.GetFullPath(folder); configurationPath = Path.GetFullPath(configurationPath);
            var candidate = new ContentSnapshotStore(folder).Read();
            string previous = File.Exists(configurationPath) ? File.ReadAllText(configurationPath) : null;
            string previousFolder = previous == null ? null : AuthoringSourceSelection.ReadFolder(configurationPath);
            var old = previousFolder == null ? null : new ContentSnapshotStore(previousFolder).Read();
            var report = new ExternalImportReport
            {
                Folder = folder, CandidateHash = candidate.Hash, ProjectId = candidate.Snapshot.Project.Id,
                ConfigurationPath = configurationPath, PreviousConfiguration = previous,
                PreviousFolder = previousFolder, PreviousHash = old?.Hash,
                DifferentProject = old != null && old.Snapshot.Project.Id != candidate.Snapshot.Project.Id,
                Errors = Validate(candidate.Snapshot, token, progress).AsReadOnly()
            };
            var changes = new List<string>();
            var existing = old?.Snapshot.Documents.ToDictionary(value => value.Id) ?? new Dictionary<string, ContentDocument>();
            foreach (var document in candidate.Snapshot.Documents.OrderBy(value => value.Kind).ThenBy(value => (int?)value.Data["levelNumber"] ?? 0).ThenBy(value => value.Id))
            {
                string status = !existing.TryGetValue(document.Id, out var before) ? "추가" : SameDocument(before, document) ? "유지" : "변경";
                string label = Label(document);
                if (before != null && before.Kind == "level" && document.Kind == "level" &&
                    (int)before.Data["levelNumber"] != (int)document.Data["levelNumber"])
                    label += " (이전 레벨 " + before.Data["levelNumber"] + ")";
                changes.Add(status + " · " + label);
                existing.Remove(document.Id);
            }
            foreach (var removed in existing.Values.OrderBy(value => value.Id)) changes.Add("제외 예정 · " + Label(removed));
            report.RemovedCount = existing.Count;
            report.Changes = changes.AsReadOnly();
            EnsureUnchanged(report);
            return report;
        }

        private static string Label(ContentDocument document) =>
            (document.Kind == "level" ? "레벨 " + document.Data["levelNumber"] : document.Kind) + " · " + document.Id;

        private static bool SameDocument(ContentDocument first, ContentDocument second)
        {
            if (first.Kind == "project") first.Data["documents"] = new JArray();
            if (second.Kind == "project") second.Data["documents"] = new JArray();
            return ContentJson.Write(first) == ContentJson.Write(second);
        }

        private static List<string> Validate(ContentSnapshot snapshot, CancellationToken token, Action<string, float> progress)
        {
            var errors = new List<string>();
            try { AuthoringSourceSelection.ValidateResources(snapshot, true); }
            catch (Exception error) { errors.Add("project.resources · " + error.Message); }
            if (errors.Count > 0) return errors;
            using (var graph = new AuthoringObjectGraph(snapshot.Documents))
            {
                var levels = snapshot.Documents.Where(value => value.Kind == "level").ToArray();
                int checkedLevels = 0;
                foreach (var document in levels)
                {
                    token.ThrowIfCancellationRequested();
                    progress?.Invoke(Label(document), checkedLevels++ / (float)(levels.Length + 1));
                    try
                    {
                        var level = (LevelDefinition)graph.Resolve(document.Id);
                        foreach (var issue in Tutorial.LevelTutorialReplayValidator.Validate(level))
                            errors.Add(Label(document) + " · " + issue);
                    }
                    catch (Exception error) { errors.Add(Label(document) + " · " + error.Message); }
                }
            }
            token.ThrowIfCancellationRequested();
            if (errors.Count == 0)
            {
                progress?.Invoke("MemoryPack 메모리 변환 검사", 0.95f);
                try { _ = JsonContentPackBuild.CreateBytes(snapshot); }
                catch (Exception error) { errors.Add("MemoryPack 변환 검사 · " + error.Message); }
            }
            return errors;
        }

        public static string Adopt(ExternalImportReport report, bool confirmReplacement, CancellationToken token, Action<string, float> progress = null)
        {
            if (report == null || !report.CanAdopt) throw new ContentFormatException("검사를 통과한 후보가 필요합니다.");
            if (!confirmReplacement) throw new ContentFormatException("프로젝트 전체 교체 및 제외 예정 항목을 확인하세요.");
            token.ThrowIfCancellationRequested();
            string path = report.ConfigurationPath;
            string directory = Path.GetDirectoryName(path);
            _ = new ContentSnapshotStore(directory);
            Directory.CreateDirectory(directory);
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new ContentFormatException("링크 설정 파일은 변경할 수 없습니다.");
            using var gate = new FileStream(path + ".import.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            EnsureUnchanged(report);
            // 검사 이후 Unity 표현 리소스가 제거된 경우도 다시 검증한다.
            var errors = Validate(new ContentSnapshotStore(report.Folder).Read().Snapshot, token, progress);
            if (errors.Count > 0) throw new ContentFormatException(string.Join("\n", errors));
            var data = new JObject { ["version"] = 1,
                ["folder"] = Path.GetRelativePath(directory, report.Folder).Replace('\\', '/'),
                ["projectId"] = report.ProjectId, ["adoptedHash"] = report.CandidateHash };
            string pending = path + ".pending-" + Guid.NewGuid().ToString("N");
            string backup = report.PreviousConfiguration == null ? null : path + ".previous-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(pending, data.ToString());
                token.ThrowIfCancellationRequested();
                EnsureUnchanged(report);
                if (report.PreviousConfiguration == null) File.Move(pending, path);
                else File.Replace(pending, path, backup);
                return backup;
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }

        private static void EnsureUnchanged(ExternalImportReport report)
        {
            if ((File.Exists(report.ConfigurationPath) ? File.ReadAllText(report.ConfigurationPath) : null) != report.PreviousConfiguration ||
                new ContentSnapshotStore(report.Folder).Read().Hash != report.CandidateHash ||
                (report.PreviousFolder != null && new ContentSnapshotStore(report.PreviousFolder).Read().Hash != report.PreviousHash))
                throw new ContentFormatException("후보·기존 원본 또는 선택 설정이 변경됐습니다. 다시 검사하세요.");
        }
    }
}
