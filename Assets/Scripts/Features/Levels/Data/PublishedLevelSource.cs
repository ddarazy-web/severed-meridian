using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Elements;

namespace Levels
{
    // 한 게임 진입 흐름이 소유한다. 공통 데이터와 현재 50레벨 구간만 보관한다.
    public sealed class PublishedLevelSource : IDisposable
    {
        private readonly string expectedGeneration;
        private readonly Func<string, CancellationToken, UniTask<byte[]>> read;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private ContentPackGeneration generation;
        private ElementContentData content;
        private PublishedLevelPack current;
        private bool disposed;
        private int pending;
        public PublishedLevelLoadTiming LastLoadTiming { get; private set; }

        public PublishedLevelSource(string generation) : this(generation, ContentPackAssetLoader.LoadAsync) { }
        internal PublishedLevelSource(string generation, Func<string, CancellationToken, UniTask<byte[]>> read)
        {
            if (string.IsNullOrWhiteSpace(generation)) throw new ArgumentException("로비의 제작 세대가 필요합니다.", nameof(generation));
            expectedGeneration = generation;
            this.read = read ?? throw new ArgumentNullException(nameof(read));
        }

        // 반환한 메모리 레벨의 소유권은 호출자에게 전달한다.
        public async UniTask<LevelDefinition> LoadAsync(int number, CancellationToken token)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PublishedLevelSource));
            int interval = LevelPackCodec.FirstLevel(number);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            pending++;
            bool entered = false;
            try
            {
                await gate.WaitAsync(linked.Token);
                entered = true;
                linked.Token.ThrowIfCancellationRequested();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                double metadataMilliseconds = 0, readMilliseconds = 0, decodeMilliseconds = 0;
                bool reused = current != null && current.FirstLevel == interval;
                if (generation == null)
                {
                    ContentPackGeneration candidate = ContentPackGenerationCodec.Decode(await read(ContentPackGenerationCodec.Address, linked.Token));
                    linked.Token.ThrowIfCancellationRequested();
                    if (!string.Equals(candidate.SourceHash, expectedGeneration, StringComparison.Ordinal))
                        throw new InvalidOperationException("로비와 게임 데이터의 제작 세대가 다릅니다. 목록을 다시 불러와 주세요.");
                    ElementContentData definitions = await ElementContentPackLoader.LoadAsync(candidate, read, linked.Token);
                    linked.Token.ThrowIfCancellationRequested();
                    generation = candidate;
                    content = definitions;
                    metadataMilliseconds = timer.Elapsed.TotalMilliseconds;
                }
                if (current == null || current.FirstLevel != interval)
                {
                    string address = LevelPackCodec.Address(number);
                    timer.Restart();
                    byte[] bytes = await read(address, linked.Token);
                    linked.Token.ThrowIfCancellationRequested();
                    readMilliseconds = timer.Elapsed.TotalMilliseconds;
                    timer.Restart();
                    ContentPackGenerationCodec.Verify(generation, address, bytes);
                    PublishedLevelPack candidate = LevelPackCodec.DecodePublished(bytes);
                    if (candidate.FirstLevel != interval) throw new InvalidOperationException("요청한 레벨 구간과 파일이 다릅니다.");
                    current = candidate;
                    decodeMilliseconds = timer.Elapsed.TotalMilliseconds;
                }
                timer.Restart();
                LevelDefinition level = current.CreateLevel(number);
                level.BindContentData(content);
                LastLoadTiming = new PublishedLevelLoadTiming(metadataMilliseconds, readMilliseconds, decodeMilliseconds, timer.Elapsed.TotalMilliseconds, reused);
                return level;
            }
            finally
            {
                if (entered) gate.Release();
                pending--;
                if (disposed && pending == 0) gate.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            generation = null;
            content = null;
            current = null;
            if (pending == 0) gate.Dispose();
        }
    }

    // 메타데이터는 세대/공통 정의 읽기와 해석을 포함한다. 레벨 팩은 읽기와 검증/해석을 분리한다.
    public readonly struct PublishedLevelLoadTiming
    {
        public readonly double MetadataMilliseconds, PackReadMilliseconds, PackDecodeMilliseconds, SelectedLevelMilliseconds;
        public readonly bool ReusedPack;
        public PublishedLevelLoadTiming(double metadata, double read, double decode, double selected, bool reused)
        { MetadataMilliseconds = metadata; PackReadMilliseconds = read; PackDecodeMilliseconds = decode; SelectedLevelMilliseconds = selected; ReusedPack = reused; }
    }
}
