using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>세 저장소의 소유 파일만 한 개씩 삭제한다. 하위 실패가 있으면 요약을 남겨 재조회할 수 있게 한다.</summary>
    internal sealed class TestRecordCleanup : IDisposable
    {
        internal readonly string[] Roots;
        internal readonly int[] Counts = new int[3];
        internal readonly List<string> Errors = new List<string>();
        internal int Deleted { get; private set; }
        internal int Failed { get; private set; }
        internal int FilesDeleted { get; private set; }
        internal bool IsDone { get; private set; }
        internal bool Scanning { get; private set; } = true;
        private readonly List<(int kind, string path)> runs = new List<(int, string)>();
        private IEnumerator<bool> work;
        private bool started;

        /// <param name="roots">검사에서만 임시 저장소 세 개를 주입한다. 제품은 고정된 세 루트만 사용한다.</param>
        internal TestRecordCleanup(string[] roots = null)
        {
            Roots = roots ?? new[] { BotBatchStore.DefaultRoot, BotMoveBalanceStore.DefaultRoot, MultiLevelTestStore.DefaultRoot };
            if (Roots.Length != 3) throw new ArgumentException("세 종류의 저장소가 필요합니다.");
            Roots = Roots.Select(Path.GetFullPath).ToArray();
            work = Scan().GetEnumerator();
        }

        /// <summary>호출자가 시간 예산 안에서 반복 호출한다. 확인 전에는 목록을 읽기만 한다.</summary>
        internal void Advance()
        {
            if (IsDone || work == null) return;
            try
            {
                if (!work.MoveNext()) { work.Dispose(); work = null; if (Scanning) Scanning = false; else IsDone = true; }
            }
            catch (Exception error)
            {
                Errors.Add(error.Message); work?.Dispose(); work = null;
                if (Scanning) Scanning = false; else IsDone = true;
            }
        }

        /// <summary>확인 창에서 동의한 뒤에만 호출한다. 목록을 다시 만들거나 임의 경로를 받지 않는다.</summary>
        internal void Begin()
        {
            if (Scanning || started || IsDone) throw new InvalidOperationException("삭제를 시작할 수 없는 상태입니다.");
            started = true; work = DeleteRuns().GetEnumerator();
        }

        private IEnumerable<bool> Scan()
        {
            for (int kind = 0; kind < Roots.Length; kind++)
            {
                string root = Roots[kind];
                bool safe = true;
                try { TestRecordPaths.Check(root, root); }
                catch (Exception error) { Errors.Add(error.Message); safe = false; }
                if (!safe || !Directory.Exists(root)) continue;
                foreach (string folder in Directory.EnumerateDirectories(root))
                {
                    if (Guid.TryParseExact(Path.GetFileName(folder), "N", out _))
                    { runs.Add((kind, folder)); Counts[kind]++; }
                    else Errors.Add("알 수 없는 폴더 보존: " + folder);
                    yield return true;
                }
                foreach (string file in Directory.EnumerateFiles(root))
                {
                    if (Path.GetFileName(file) != "latest.txt" && Path.GetFileName(file) != "latest.txt.tmp")
                        Errors.Add("알 수 없는 파일 보존: " + file);
                    yield return true;
                }
            }
        }

        private IEnumerable<bool> DeleteRuns()
        {
            foreach (var run in runs)
            {
                foreach (bool step in DeleteFolder(Roots[run.kind], run.path, run.kind)) yield return step;
                if (Directory.Exists(run.path)) Failed++; else Deleted++;
                yield return true;
            }
            for (int kind = 0; kind < Roots.Length; kind++)
            {
                // 살아남은 정상 요약만 포인터 후보로 쓴다. 원시 판을 다시 분석하지 않는다.
                RecordHeader latest = null;
                foreach (var run in runs.Where(r => r.kind == kind))
                {
                    try
                    {
                        string header = Path.Combine(run.path, HeaderName(kind));
                        TestRecordPaths.Check(Roots[kind], header);
                        if (File.Exists(header))
                        {
                            RecordHeader value = JsonUtility.FromJson<RecordHeader>(File.ReadAllText(header));
                            if (value != null && value.id == Path.GetFileName(run.path) && (kind == 2 ? value.version == 1 : value.formatVersion == 1) &&
                                (latest == null || string.CompareOrdinal(value.startedUtc, latest.startedUtc) > 0 ||
                                value.startedUtc == latest.startedUtc && string.CompareOrdinal(value.id, latest.id) > 0)) latest = value;
                        }
                    }
                    catch (Exception error) { Errors.Add("최근 기록 후보 제외: " + error.Message); }
                    yield return true;
                }
                try
                {
                    string pointer = Path.Combine(Roots[kind], "latest.txt"); TestRecordPaths.Check(Roots[kind], pointer);
                    string temporary = pointer + ".tmp"; TestRecordPaths.Check(Roots[kind], temporary);
                    if (latest == null) { if (File.Exists(pointer)) File.Delete(pointer); }
                    else BotBatchStore.WriteAtomic(pointer, latest.id);
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch (Exception error) { Errors.Add("최근 기록 정리 실패: " + error.Message); }
                yield return true;
            }
        }

        /// <param name="root">삭제를 허용한 저장 루트.</param><param name="folder">방문할 소유 폴더.</param>
        /// <param name="kind">0 반복, 1 추천, 2 여러 레벨, 3 trials, 4 레벨 순번 폴더.</param>
        /// <returns>파일 하나 또는 폴더 하나를 처리할 때마다 제어권을 돌려준다.</returns>
        private IEnumerable<bool> DeleteFolder(string root, string folder, int kind)
        {
            IEnumerator<string> directories = null;
            try { TestRecordPaths.Check(root, folder); if (Directory.Exists(folder)) directories = Directory.EnumerateDirectories(folder).GetEnumerator(); }
            catch (Exception error) { Errors.Add(error.Message); }
            if (directories == null) yield break;
            int beforeErrors = Errors.Count;
            using (directories)
            {
                while (Next(directories, out string child))
                {
                    string name = Path.GetFileName(child);
                    int childKind = -1;
                    if (kind == 1 && name == "trials") childKind = 3;
                    else if (kind == 3 && Guid.TryParseExact(name, "N", out _)) childKind = 0;
                    else if (kind == 2 && Regex.IsMatch(name, @"^\d{4,}$")) childKind = 4;
                    else if (kind == 4 && Guid.TryParseExact(name, "N", out _))
                    {
                        if (File.Exists(Path.Combine(child, "balance.json"))) childKind = 1;
                        else if (File.Exists(Path.Combine(child, "batch.json"))) childKind = 0;
                    }
                    if (childKind < 0) Errors.Add("알 수 없는 하위 폴더 보존: " + child);
                    else foreach (bool step in DeleteFolder(root, child, childKind)) yield return step;
                    yield return true;
                }
            }
            IEnumerator<string> files = null;
            try { files = Directory.EnumerateFiles(folder).GetEnumerator(); }
            catch (Exception error) { Errors.Add(error.Message); }
            if (files == null) yield break;
            string header = kind < 3 ? Path.Combine(folder, HeaderName(kind)) : null;
            using (files)
            {
                while (Next(files, out string file))
                {
                    if (file == header) continue;
                    string name = Path.GetFileName(file);
                    bool owned = kind == 0 && Regex.IsMatch(name, @"^\d{6}\.json(?:\.tmp)?$") ||
                        kind < 3 && name == HeaderName(kind) + ".tmp" ||
                        (kind == 1 || kind == 4) && (name == "latest.txt" || name == "latest.txt.tmp");
                    if (!owned) Errors.Add("알 수 없는 파일 보존: " + file);
                    else DeleteFile(root, file);
                    yield return true;
                }
            }
            // 부분 실패한 실행은 요약을 유지해야 목록에서 사라지지 않고 다시 정리할 수 있다.
            if (Errors.Count == beforeErrors)
            {
                if (header != null) DeleteFile(root, header);
                if (Errors.Count == beforeErrors)
                    try { TestRecordPaths.Check(root, folder); Directory.Delete(folder, false); }
                    catch (Exception error) { Errors.Add(error.Message); }
            }
            yield return true;
        }

        private bool Next(IEnumerator<string> enumerator, out string value)
        {
            value = null;
            try { if (!enumerator.MoveNext()) return false; value = enumerator.Current; return true; }
            catch (Exception error) { Errors.Add(error.Message); return false; }
        }

        private void DeleteFile(string root, string file)
        {
            try { TestRecordPaths.Check(root, file); if (File.Exists(file)) { File.Delete(file); FilesDeleted++; } }
            catch (Exception error) { Errors.Add("삭제 실패: " + file + " · " + error.Message); }
        }

        private static string HeaderName(int kind) => kind == 0 ? "batch.json" : kind == 1 ? "balance.json" : "queue.json";
        [Serializable] private sealed class RecordHeader { public string id, startedUtc; public int version, formatVersion; }
        public void Dispose() { work?.Dispose(); work = null; IsDone = true; }
    }
}
