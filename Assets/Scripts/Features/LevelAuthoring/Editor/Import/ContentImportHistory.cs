using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LevelAuthoring.Storage;

namespace LevelAuthoring.Editor
{
    internal sealed class ImportHistoryEntry
    {
        internal string Path;
        
        internal string[] Files, Directories;
        internal long Bytes;
        internal string Fingerprint;
    }

    internal sealed class ImportHistoryPlan
    {
        internal string Root, ProtectedCandidate, Fingerprint;
        internal readonly List<ImportHistoryEntry> Entries = new List<ImportHistoryEntry>();
        internal readonly List<string> Protected = new List<string>();
        internal long Bytes => Entries.Sum(value => value.Bytes);
        internal int FileCount => Entries.Sum(value => value.Files.Length);
    }

    // 소유한 경로만 열거하고 실제 삭제 직전에 원본 선택과 전체 목록을 다시 대조한다.
    internal static class ContentImportHistory
    {
        internal static ImportHistoryPlan Preview(string root, string protectedCandidate) =>
            WithLocks(root, () => Scan(root, protectedCandidate));

        internal static void Delete(ImportHistoryPlan plan, string protectedCandidate)
        {
            WithLocks(plan.Root, () =>
            {
                ImportHistoryPlan current = Scan(plan.Root, protectedCandidate);
                if (current.Fingerprint != plan.Fingerprint)
                    throw new InvalidOperationException("원본 선택 또는 정리 대상이 바뀌었습니다. 목록을 다시 확인하세요.");
                foreach (ImportHistoryEntry entry in current.Entries)
                {
                    foreach (string file in entry.Files)
                    {
                        CheckPath(plan.Root, file);
                        File.Delete(file);
                    }
                    foreach (string directory in entry.Directories.OrderByDescending(value => value.Length))
                    {
                        CheckPath(plan.Root, directory);
                        System.IO.Directory.Delete(directory, false);
                    }
                }
                return true;
            });
        }

        private static T WithLocks<T>(string root, Func<T> action)
        {
            root = System.IO.Path.GetFullPath(root);
            string configuration = System.IO.Path.Combine(root, AuthoringSourceSelection.DefaultConfiguration);
            string journal = System.IO.Path.Combine(root, "Library/AuthoringPacks/publication.json");
            CheckPath(root, configuration);
            CheckPath(root, journal);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(configuration));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(journal));
            CheckPath(root, configuration + ".import.lock");
            CheckPath(root, journal + ".lock");
            using FileStream import = new FileStream(configuration + ".import.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using FileStream publication = new FileStream(journal + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (File.Exists(journal))
                throw new InvalidOperationException("변환 복구 기록이 남아 있습니다. 3단계에서 복구·변환을 완료한 뒤 정리하세요.");
            return action();
        }

        private static ImportHistoryPlan Scan(string root, string protectedCandidate)
        {
            root = System.IO.Path.GetFullPath(root);
            string configuration = System.IO.Path.Combine(root, AuthoringSourceSelection.DefaultConfiguration);
            string selected = File.Exists(configuration) ? AuthoringSourceSelection.ReadFolder(configuration) : null;
            ImportHistoryPlan plan = new ImportHistoryPlan
            {
                Root = root,
                ProtectedCandidate = string.IsNullOrEmpty(protectedCandidate) ? null : System.IO.Path.GetFullPath(protectedCandidate)
            };
            if (selected != null) plan.Protected.Add("현재 원본: " + selected);
            if (plan.ProtectedCandidate != null) plan.Protected.Add("검토 중인 후보: " + plan.ProtectedCandidate);
            string candidates = System.IO.Path.Combine(root, "ContentData/Candidates");
            CheckPath(root, candidates);
            if (System.IO.Directory.Exists(candidates))
                foreach (string directory in System.IO.Directory.GetDirectories(candidates).Select(System.IO.Path.GetFullPath))
                {
                    string name = System.IO.Path.GetFileName(directory);
                    if (Contains(directory, selected) || Contains(selected, directory) || Contains(directory, plan.ProtectedCandidate) || Contains(plan.ProtectedCandidate, directory))
                    { continue; }
                    if (name != "main" && !Regex.IsMatch(name, @"\A(?:import-[0-9]{8}-[0-9]{6}-[a-f0-9]{32}|\.package-[a-f0-9]{32})\z"))
                    { plan.Protected.Add("관리 대상이 아닌 폴더: " + directory); continue; }
                    Add(plan, directory, true);
                }
            string settings = System.IO.Path.GetDirectoryName(configuration);
            foreach (string file in System.IO.Directory.GetFiles(settings, "AuthoringSource.json.*"))
            {
                string suffix = file.Substring(configuration.Length);
                if (suffix == ".previous" || Regex.IsMatch(suffix, @"\A\.(?:previous|pending)-[a-f0-9]{32}\z"))
                    Add(plan, file, false);
            }
            foreach (string relative in new[] { "Assets/Data/LevelPacks", "Assets/Data/ElementPacks", "Library/AuthoringPacks" })
            {
                string directory = System.IO.Path.Combine(root, relative);
                CheckPath(root, directory);
                if (!System.IO.Directory.Exists(directory)) continue;
                foreach (string file in System.IO.Directory.GetFiles(directory))
                    if (Regex.IsMatch(System.IO.Path.GetFileName(file),
                        @"\A(?:levels-[0-9]{6,}\.bytes|content-generation\.bytes|default-content\.bytes|publication\.json)\.(?:pending|replace)-[a-f0-9]{32}(?:\.meta)?\z"))
                        Add(plan, file, false);
            }
            plan.Protected.Add("게임 리소스·Addressables, 원본 ZIP, 로그, 잠금 파일은 보존합니다.");
            plan.Fingerprint = HashText((File.Exists(configuration) ? File.ReadAllText(configuration) : "") + "\n" +
                plan.ProtectedCandidate + "\n" + string.Join("\n", plan.Entries.OrderBy(value => value.Path, StringComparer.Ordinal)
                    .Select(value => value.Path + "|" + value.Fingerprint)));
            return plan;
        }

        private static void Add(ImportHistoryPlan plan, string path, bool directory)
        {
            path = System.IO.Path.GetFullPath(path);
            CheckPath(plan.Root, path);
            List<string> files = new List<string>(), directories = new List<string>();
            if (directory) Collect(plan.Root, path, files, directories);
            else files.Add(path);
            StringBuilder fingerprint = new StringBuilder();
            long bytes = 0;
            foreach (string file in files.OrderBy(value => value, StringComparer.Ordinal))
            {
                using FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
                using SHA256 hash = SHA256.Create();
                bytes += stream.Length;
                fingerprint.Append(file).Append('|').Append(Convert.ToBase64String(hash.ComputeHash(stream))).Append('\n');
            }
            foreach (string folder in directories.OrderBy(value => value, StringComparer.Ordinal)) fingerprint.Append(folder).Append('\n');
            plan.Entries.Add(new ImportHistoryEntry
            {
                Path = path, Files = files.ToArray(), Directories = directories.ToArray(),
                Bytes = bytes, Fingerprint = HashText(fingerprint.ToString())
            });
        }

        private static void Collect(string root, string directory, List<string> files, List<string> directories)
        {
            CheckPath(root, directory);
            directories.Add(directory);
            foreach (string entry in System.IO.Directory.GetFileSystemEntries(directory))
            {
                CheckPath(root, entry);
                if (System.IO.Directory.Exists(entry)) Collect(root, entry, files, directories);
                else files.Add(entry);
            }
        }

        private static bool Contains(string directory, string path) => directory != null && path != null &&
            (string.Equals(directory, path, StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith(directory + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        private static void CheckPath(string root, string path)
        {
            path = System.IO.Path.GetFullPath(path);
            if (!Contains(root, path) || string.Equals(root, path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("정리 경로가 프로젝트 밖을 가리킵니다: " + path);
            // 상위 폴더와 파일의 링크를 모두 거부해 연결된 외부 파일을 따라가지 않는다.
            new JsonContentStore(path);
        }

        private static string HashText(string text)
        {
            using SHA256 hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
    }
}
