using System;
using UnityEditor;

namespace Products.Editor
{
    [InitializeOnLoad]
    public static class BuildToolController
    {
        private static BuildToolRunner runner;
        public static string Session
        {
            get
            {
                string value = SessionState.GetString("Match.BuildTool.Session", "");
                if (value.Length == 0) { value = Guid.NewGuid().ToString("N"); SessionState.SetString("Match.BuildTool.Session", value); }
                return value;
            }
        }
        public static BuildToolRunner Runner => runner ??= new BuildToolRunner(new UnityProfileBuildBackend(), new BuildToolRunStore(BuildToolRunStore.DefaultRoot), Session);
        static BuildToolController() { EditorApplication.update += Update; }
        private static void Update()
        {
            // 창 조회만으로 새 실행을 만들지 않는다. 저장된 명시적 요청만 진행한다.
            if (runner == null && !System.IO.File.Exists(System.IO.Path.Combine(BuildToolRunStore.DefaultRoot, "current.json"))) return;
            Runner.Step();
        }
    }
}
