using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Elements;
using Elements.Editor;
using LevelAuthoring.Storage;
using Levels;

namespace LevelAuthoring.Editor
{
    public sealed class ContentPackStatus
    {
        public string State { get; private set; }
        public string SourceHash { get; private set; }
        public string GeneratedHash { get; private set; }
        public DateTime? GeneratedAt { get; private set; }
        public int LevelCount { get; private set; }
        public string[] OutputPaths { get; private set; } = Array.Empty<string>();
        public string Error { get; private set; }

        public static ContentPackStatus Read(string sourceFolder, string outputRoot)
        {
            var result = new ContentPackStatus();
            try
            {
                outputRoot = Path.GetFullPath(outputRoot);
                if (File.Exists(Path.Combine(outputRoot, "Library/AuthoringPacks/publication.json")))
                {
                    result.State = "RecoveryRequired";
                    result.Error = "완료되지 않은 팩 복구 기록이 있습니다. 복구 후 다시 변환하세요.";
                    return result;
                }
                var snapshot = new ContentSnapshotStore(Path.GetFullPath(sourceFolder)).Read().Snapshot;
                result.SourceHash = JsonContentPackBuild.SourceHash(snapshot);
                result.LevelCount = snapshot.Documents.Count(document => document.Kind == "level");
                string manifestPath = Path.Combine(outputRoot, JsonContentPackBuild.GenerationPath);
                if (!File.Exists(manifestPath)) { result.State = "Missing"; return result; }
                var generation = ContentPackGenerationCodec.Decode(File.ReadAllBytes(manifestPath));
                result.GeneratedHash = generation.SourceHash;
                result.GeneratedAt = File.GetLastWriteTime(manifestPath);
                var paths = new List<string> { JsonContentPackBuild.GenerationPath };
                // 세대 정보의 문자열을 파일 경로로 그대로 사용하지 않는다.
                foreach (string address in generation.Addresses)
                {
                    string relative;
                    if (address == ElementContentPackCodec.Address) relative = ElementContentPackBuild.OutputPath;
                    else if (Regex.IsMatch(address, @"\ALevels/levels-[0-9]{6,}\z"))
                        relative = "Assets/Data/LevelPacks/" + address.Substring("Levels/".Length) + ".bytes";
                    else throw new InvalidDataException("지원하지 않는 팩 주소: " + address);
                    paths.Add(relative);
                    ContentPackGenerationCodec.Verify(generation, address, File.ReadAllBytes(Path.Combine(outputRoot, relative)));
                }
                result.OutputPaths = paths.ToArray();
                if (!generation.Addresses.Contains(ElementContentPackCodec.Address))
                    throw new InvalidDataException("요소 팩 주소가 없습니다.");
                if (result.SourceHash == result.GeneratedHash)
                {
                    string[] expected = snapshot.Documents.Where(document => document.Kind == "level")
                        .Select(document => LevelPackCodec.Address((int)document.Data["levelNumber"]))
                        .Append(ElementContentPackCodec.Address).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
                    if (!expected.SequenceEqual(generation.Addresses.OrderBy(value => value, StringComparer.Ordinal)))
                        throw new InvalidDataException("원본 레벨 구간과 생성된 팩 주소가 다릅니다.");
                }
                result.State = result.SourceHash == result.GeneratedHash ? "Current" : "Outdated";
            }
            catch (Exception error) { result.State = "Invalid"; result.Error = error.Message; }
            return result;
        }
    }
}
