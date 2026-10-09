using AutoPlay;

namespace Levels.Editor
{
    // 기존 창의 저장 위치와 호출 계약만 유지하고 기록·복구 규칙은 실행용 저장소와 공유한다.
    internal sealed class BotBatchStore
    {
        internal const string DefaultRoot = "Library/Match/AutoPlay";
        private readonly AutoPlay.BotBatchStore store;
        internal BotBatchStore(string root = DefaultRoot) { store = new AutoPlay.BotBatchStore(root); }
        internal void Save(BotBatchRecord record, BotBatchGame game) => store.Save(record, game);
        internal BotBatchRecord LoadLatest(bool persistRecovery = true) => store.LoadLatest(persistRecovery);
        internal static bool SameVersions(BotBatchRecord record) => AutoPlay.BotBatchStore.SameVersions(record);
        internal static void WriteAtomic(string path, string contents) => AutoPlay.BotBatchStore.WriteAtomic(path, contents);
    }
}
