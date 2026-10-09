using System;
using System.Collections.Generic;
using AutoPlay;
using UnityEditor;

namespace Levels.Editor
{
    // 기존 에셋 화면은 식별자 조회만 수행하고 공통 실행기를 사용한다.
    internal sealed class MultiLevelTestSession : IDisposable
    {
        private readonly AutoPlay.MultiLevelTestSession session;
        internal MultiLevelTestRecord Record => session.Record;
        internal bool CanContinue => session.CanContinue;
        internal bool Paused => session.Paused;
        internal string Error => session.Error;
        internal int Index => session.Index;
        internal MultiLevelTestSession(IEnumerable<LevelDefinition> levels, MultiLevelTestMode mode, int samples, MultiLevelTestStore store)
        { session = new AutoPlay.MultiLevelTestSession(levels, mode, samples, store, level => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level))); }
        internal void Advance() => session.Advance();
        internal void SetPaused(bool paused) => session.SetPaused(paused);
        internal void Stop(bool interrupted = false) => session.Stop(interrupted);
        public void Dispose() => session.Dispose();
    }
}