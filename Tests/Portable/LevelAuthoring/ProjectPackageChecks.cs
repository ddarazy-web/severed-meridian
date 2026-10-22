using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    private static void ProjectPackageChecks()
    {
        Action<string, Stream, CancellationToken> export = (folder, stream, token) => ProjectPackageStore.Export(folder, stream, token);
        Func<Stream, string, string, CancellationToken, string> import = ProjectPackageStore.Import;
        string fixture = File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt");
        string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-04/packages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string saved = Path.Combine(root, "saved");
            var store = new ContentSnapshotStore(saved);
            var original = store.Publish(new ContentSnapshotStore(fixture).Read().Snapshot, null);
            store.Publish(original.Snapshot, original.Hash);
            File.WriteAllText(Path.Combine(saved, "draft.json"), "private");
            string before = store.Read().Hash;
            byte[] bytes;
            using (var output = new MemoryStream())
            {
                export(saved, output, CancellationToken.None);
                Check(output.CanWrite, "package export leaves caller stream open");
                bytes = output.ToArray();
            }
            string[] published = new[] { "project.json" }.Concat(((JArray)store.Read().Snapshot.Project.Data["documents"]).Select(entry => (string)entry["path"])).ToArray();
            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                Check(zip.Entries.Select(entry => entry.FullName).OrderBy(path => path).SequenceEqual(published.OrderBy(path => path)),
                    "package exports only public JSON and excludes backups generations drafts");
            string imports = Path.Combine(root, "imports");
            using (var input = new PackageShortStream(new MemoryStream(bytes)))
            {
                string folder = import(input, imports, "한글 프로젝트", CancellationToken.None);
                Check(input.CanRead && Path.GetDirectoryName(folder) == imports, "package nonseek short reads and caller ownership");
                ContentSnapshot copied = new ContentSnapshotStore(folder).Read().Snapshot;
                ContentSnapshot originalSaved = store.Read().Snapshot;
                Check(copied.Documents.OrderBy(doc => doc.Id).Select(ContentJson.Write).SequenceEqual(originalSaved.Documents.OrderBy(doc => doc.Id).Select(ContentJson.Write)),
                    "package preserves Unicode document meaning IDs and references");
                foreach (ContentDocument level in originalSaved.Documents.Where(doc => doc.Kind == "level"))
                    Check(AuthoringTrialSource.Encode(copied, level.Id) == AuthoringTrialSource.Encode(originalSaved, level.Id),
                        "package preserves deterministic trial source input " + level.Id);
            }
            var workspace = new AuthoringToolWorkspace();
            workspace.Open(saved, "Cancel");
            string id = workspace.Session.SelectedLevelId;
            workspace.Session.SelectCells(new[] { 40 });
            workspace.Session.Apply("전송 실패 보호", docs => docs[id].Data["moveCount"] = (int)docs[id].Data["moveCount"] + 1);
            string state = workspace.ExportState();
            using (var failedInput = new PackageShortStream(new MemoryStream(bytes), true))
            {
                bool readFailed = false;
                try { import(failedInput, imports, "read-error", CancellationToken.None); }
                catch (IOException) { readFailed = true; }
                Check(readFailed && failedInput.CanRead, "package mid-read IO failure keeps caller stream open");
            }
            foreach (string path in new[] { "../escape.json", "/absolute.json", "C:/drive.json", "\\\\host\\share.json", "folder\\file.json", "./file.json", "folder//file.json", "folder/../file.json", "extra.json", "PROJECT.JSON" })
                PackageFailure(() => import(new MemoryStream(PackageMutate(bytes, zip => zip.CreateEntry(path))), imports, "bad", CancellationToken.None), "package rejects " + path);
            PackageFailure(() => import(new MemoryStream(PackageMutate(bytes, zip => zip.CreateEntry("project.json"))), imports, "bad", CancellationToken.None), "package duplicate entry");
            PackageFailure(() => import(new MemoryStream(PackageMutate(bytes, zip => zip.Entries.First(entry => entry.FullName != "project.json").Delete())), imports, "bad", CancellationToken.None), "package missing public member");
            PackageFailure(() => import(new MemoryStream(PackageMutate(bytes, zip => zip.GetEntry("project.json").ExternalAttributes = unchecked((int)0xA1FF0000))), imports, "bad", CancellationToken.None), "package symlink entry");
            PackageFailure(() => import(new MemoryStream(PackageMutate(bytes, zip =>
            {
                var entry = zip.GetEntry("project.json");
                string text; using (var reader = new StreamReader(entry.Open())) text = reader.ReadToEnd();
                JObject project = JObject.Parse(text); project["data"]["defaultCatalogId"] = "missing-reference";
                entry.Delete(); using (var writer = new StreamWriter(zip.CreateEntry("project.json").Open())) writer.Write(project.ToString());
            })), imports, "bad", CancellationToken.None), "package rejects invalid full references");
            PackageFailure(() => import(new MemoryStream(new byte[] { 1, 2, 3 }), imports, "bad", CancellationToken.None), "package corrupt ZIP");
            PackageFailure(() => import(new MemoryStream(bytes.Take(bytes.Length / 2).ToArray()), imports, "bad", CancellationToken.None), "package truncated ZIP");
            PackageFailure(() => import(new MemoryStream(bytes), imports, "한글 프로젝트", CancellationToken.None), "package existing name collision");
            foreach (string name in new[] { "../escape", "a/b", "CON", "name.", " name" })
                PackageFailure(() => import(new MemoryStream(bytes), imports, name, CancellationToken.None), "package invalid name " + name);
            var cancelled = new CancellationToken(true);
            PackageFailure(() => import(new MemoryStream(bytes), imports, "cancel", cancelled), "package import cancelled");
            using (var cancellation = new CancellationTokenSource())
                PackageFailure(() => import(new PackageGeneratedStream(129L * 1024 * 1024, cancellation), imports, "cancel", cancellation.Token), "package cancellation during source read");
            PackageLimit(() => import(new PackageGeneratedStream(129L * 1024 * 1024, null), imports, "bad", CancellationToken.None), "128 MiB", "package actual compressed input limit");
            PackageFailure(() => export(saved, new MemoryStream(), cancelled), "package export cancelled");
            PackageFailure(() => export(saved, new PackageFailingStream(), CancellationToken.None), "package output write failure");
            string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "file");
            PackageFailure(() => import(new MemoryStream(bytes), blocked, "bad", CancellationToken.None), "package disk failure");
            string oversizedPath = published.First(path => path != "project.json");
            byte[] oversized = PackageMutate(bytes, zip =>
            {
                var entry = zip.GetEntry(oversizedPath); entry.Delete();
                using (Stream stream = zip.CreateEntry(oversizedPath).Open())
                {
                    byte[] block = new byte[1024 * 1024];
                    for (int index = 0; index < 17; index++) stream.Write(block, 0, block.Length);
                }
            });
            PackageLimit(() => import(new PackageShortStream(new MemoryStream(oversized)), imports, "bad", CancellationToken.None), "16 MiB", "package inflated JSON limit");
            byte[] lyingSize = (byte[])oversized.Clone();
            int oversizedNameBytes = System.Text.Encoding.UTF8.GetByteCount(oversizedPath);
            for (int offset = 0; offset + 46 <= lyingSize.Length; offset++)
                if (BitConverter.ToUInt32(lyingSize, offset) == 0x02014B50 &&
                    BitConverter.ToUInt16(lyingSize, offset + 28) == oversizedNameBytes &&
                    offset + 46 + oversizedNameBytes <= lyingSize.Length &&
                    System.Text.Encoding.UTF8.GetString(lyingSize, offset + 46, oversizedNameBytes) == oversizedPath)
                {
                    Array.Clear(lyingSize, offset + 24, 4);
                    lyingSize[offset + 24] = 1;
                }
            PackageFailure(() => import(new MemoryStream(lyingSize), imports, "bad", CancellationToken.None), "package rejects forged declared size without registration");
            byte[] aggregate = PackageMutate(bytes, zip =>
            {
                byte[] block = new byte[1024 * 1024];
                for (int index = 0; index < 33; index++)
                    using (Stream stream = zip.CreateEntry("aggregate-" + index + ".json").Open())
                        for (int megabyte = 0; megabyte < 16; megabyte++) stream.Write(block, 0, block.Length);
            });
            PackageLimit(() => import(new MemoryStream(aggregate), imports, "bad", CancellationToken.None), "512 MiB", "package aggregate expansion limit");
            byte[] tooMany = PackageMutate(bytes, zip => { for (int index = 0; index < 10000; index++) zip.CreateEntry("excess-" + index + ".json"); });
            PackageLimit(() => import(new MemoryStream(tooMany), imports, "bad", CancellationToken.None), "10,000", "package entry count limit");
            Check(Directory.GetDirectories(imports).Select(Path.GetFileName).SequenceEqual(new[] { "한글 프로젝트" }) && !Directory.GetFiles(imports).Any(),
                "package failures clean only temporary artifacts and preserve project list");
            Check(store.Read().Hash == before && workspace.ExportState() == state, "package failures preserve saved JSON dirty selection undo redo");
        }
        finally { Directory.Delete(root, true); }
    }

    private static byte[] PackageMutate(byte[] bytes, Action<ZipArchive> change)
    {
        using (var stream = new MemoryStream())
        {
            stream.Write(bytes, 0, bytes.Length); stream.Position = 0;
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true)) change(zip);
            return stream.ToArray();
        }
    }
    private static void PackageFailure(Action action, string name)
    {
        try { action(); }
        catch (Exception error) when (error is ContentFormatException || error is IOException || error is OperationCanceledException || error is UnauthorizedAccessException)
        { Check(true, name); return; }
        throw new Exception("FAIL accepted " + name);
    }
    private static void PackageLimit(Action action, string limit, string name)
    {
        try { action(); }
        catch (ContentFormatException error) when (error.Message.Contains(limit)) { Check(true, name); return; }
        throw new Exception("FAIL missing limit " + name);
    }
    private sealed class PackageShortStream : Stream
    {
        private readonly Stream inner;
        private readonly bool failAfterFirstRead;
        private bool readStarted;
        public PackageShortStream(Stream inner, bool failAfterFirstRead = false)
        { this.inner = inner; this.failAfterFirstRead = failAfterFirstRead; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (failAfterFirstRead && readStarted) throw new IOException("Injected source read failure");
            readStarted = true;
            return inner.Read(buffer, offset, Math.Min(count, failAfterFirstRead ? 32 : 4093));
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
    private sealed class PackageFailingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new IOException("Injected flush failure");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Injected write failure");
    }
    private sealed class PackageGeneratedStream : Stream
    {
        private long remaining;
        private readonly CancellationTokenSource cancellation;
        public PackageGeneratedStream(long length, CancellationTokenSource cancellation) { remaining = length; this.cancellation = cancellation; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = (int)Math.Min(remaining, count);
            Array.Clear(buffer, offset, read); remaining -= read;
            cancellation?.Cancel(); return read;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
