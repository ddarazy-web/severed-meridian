using System;
using System.Collections.Generic;
using Simulation;

namespace Levels.Editor
{
    public static class LevelEditorInputIdentity
    {
        // 제작 중 오류가 있는 입력은 이력과 같다고 표시하지 않는다. 실행 검사는 기존 검증 경로가 맡는다.
        public static bool Matches(LevelDefinition level, string fingerprint)
        {
            if (level == null) return false;
            try { return LevelStateBuilder.Fingerprint(level) == fingerprint; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (KeyNotFoundException) { return false; }
        }
    }
}
