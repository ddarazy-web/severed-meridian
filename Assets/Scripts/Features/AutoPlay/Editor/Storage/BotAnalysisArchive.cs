using System;
using System.Collections.Generic;
using System.IO;
using AutoPlay;
using UnityEngine;

namespace Levels.Editor
{
    internal sealed class BotHistoryEntry
    {
        internal string DirectoryPath;
        internal BotBatchRecord Record;
        internal string Error;
    }

    /// <summary>이력 목록은 요약만 읽고, 상세 판 검사는 항목 선택 시 로더에 맡긴다.</summary>
    internal static class BotAnalysisCatalog
    {
        /// <param name="root">기존 로컬 기록 루트.</param><returns>한 항목씩 갱신 사이에 읽을 수 있는 목록.</returns>
        internal static IEnumerable<BotHistoryEntry> Scan(string root = BotBatchStore.DefaultRoot)
        {
            if (!Directory.Exists(root)) yield break;
            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                BotHistoryEntry entry = new BotHistoryEntry { DirectoryPath = Path.GetFullPath(directory) };
                try
                {
                    string path = Path.Combine(directory, "batch.json");
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("연결된 폴더/파일입니다.");
                    entry.Record = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(path));
                    if (entry.Record == null || entry.Record.formatVersion != 1 || !Guid.TryParseExact(entry.Record.id, "N", out _) ||
                        entry.Record.id != Path.GetFileName(directory)) throw new InvalidDataException("지원하지 않거나 손상된 시험 요약입니다.");
                }
                catch (Exception error) { entry.Error = error.Message; }
                yield return entry;
            }
        }
    }

    /// <summary>
    /// 검증된 한 시점의 기록을 새 폴더에 보관한다. 같은 부모의 임시 폴더를 완성한 뒤 이름을 바꿔
    /// 불완전한 기록을 완료된 보관본처럼 표시하지 않는다. 기존 폴더는 교체하거나 삭제하지 않는다.
    /// </summary>
    internal sealed class BotAnalysisExport : IDisposable
    {
        private readonly BotAnalysisReader source;
        private readonly string temporary;
        private int next;
        private bool disposed;
        internal string Destination { get; }
        internal bool IsDone { get; private set; }
        internal string Error { get; private set; }
        internal int Copied => next;

        /// <param name="source">끝까지 검증한 읽기 전용 사본.</param><param name="destination">아직 존재하지 않는 새 보관 폴더.</param>
        internal BotAnalysisExport(BotAnalysisReader source, string destination)
        {
            if (source == null || !source.IsDone || source.Error != null) throw new InvalidOperationException("정상적으로 읽은 시험을 먼저 선택하세요.");
            this.source = source;
            Destination = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string origin = source.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Destination.Equals(origin, StringComparison.OrdinalIgnoreCase) || Destination.StartsWith(origin + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("원본 기록 폴더 밖의 새 위치를 선택하세요.");
            if (Directory.Exists(Destination) || File.Exists(Destination)) throw new IOException("기존 파일이나 폴더를 덮어쓰지 않습니다. 새 이름을 선택하세요.");
            string parent = Path.GetDirectoryName(Destination);
            if (!Directory.Exists(parent) || (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("실제로 존재하는 보관 폴더를 선택하세요.");
            temporary = Destination + ".pending-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(temporary);
        }

        /// <summary>판 파일 하나씩 처리한다. 메타데이터는 모든 판이 저장된 후 확정한다.</summary>
        internal void Advance()
        {
            if (IsDone || disposed) return;
            try
            {
                if (next < source.Games.Count)
                {
                    BotBatchGame game = source.ReadGame(next);
                    File.WriteAllText(Path.Combine(temporary, next.ToString("D6") + ".json"), JsonUtility.ToJson(game));
                    next++; return;
                }
                File.WriteAllText(Path.Combine(temporary, "batch.json"), JsonUtility.ToJson(source.Record, true));
                // Directory.Move는 이미 존재하는 대상에 덮어쓰지 않는다. 확인 이후 생긴 동명 폴더도 보존한다.
                Directory.Move(temporary, Destination); IsDone = true;
            }
            catch (Exception error) { Error = error.Message; IsDone = true; }
        }

        /// <summary>이번 객체가 새로 만든 임시 폴더만 정리한다. 실패한 정리는 위치와 함께 사용자에게 알린다.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!Directory.Exists(temporary)) return;
            try
            {
                if ((File.GetAttributes(temporary) & FileAttributes.ReparsePoint) != 0) throw new IOException("임시 폴더가 외부 연결로 바뀌었습니다.");
                Directory.Delete(temporary, true);
            }
            catch (Exception error) { Error = (Error == null ? "" : Error + "\n") + "임시 기록 정리 실패: " + temporary + " · " + error.Message; }
        }
    }
}
