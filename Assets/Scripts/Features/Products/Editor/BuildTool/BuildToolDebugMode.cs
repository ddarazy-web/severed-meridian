using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace Products.Editor
{
    public sealed class BuildToolDebugMode : IBuildToolOptionHandler
    {
        public const string Id = "debug-mode";
        public BuildToolOptionDefinition Definition { get; } = new BuildToolOptionDefinition
        {
            Id = Id, Label = "디버그 모드", DefaultEnabled = false,
            Tooltip = "ON: Development Build로 빌드하고 GameLog 디버그 로그를 출력합니다. OFF: 일반 빌드로 만들고 해당 호출과 인수 계산을 제거합니다. 에디터 로그·경고·오류는 유지합니다."
        };
        public bool Handles(BuildToolOptionStage stage) => stage == BuildToolOptionStage.BeforeBuild;
        public void Execute(BuildToolOptionStage stage, BuildToolOptionContext context)
        {
            if (stage != BuildToolOptionStage.BeforeBuild) throw new InvalidOperationException("디버그 모드는 빌드 직전에만 확정합니다.");
            context.Request.debugMode = context.Enabled;
        }

        /// <summary>프로필에서 가져온 옵션 중 개발 빌드 관련 플래그만 이번 요청에 맞춰 조정한다.</summary>
        /// <param name="inherited">Unity가 활성 프로필에서 계산한 옵션.</param>
        /// <param name="enabled">이번 요청의 디버그 모드.</param>
        /// <returns>압축 등 다른 옵션을 보존한 최종 빌드 옵션.</returns>
        public static BuildOptions Apply(BuildOptions inherited, bool enabled)
        {
            if (enabled) return inherited | BuildOptions.Development;
            return inherited & ~(BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler |
                BuildOptions.EnableDeepProfilingSupport | BuildOptions.CustomConnectionID | BuildOptions.WaitForPlayerConnection);
        }

        internal static MethodInfo ProfileOptionsFactory
        {
            get
            {
                var utility = typeof(BuildPipeline).Assembly.GetType("UnityEditor.Build.Profile.BuildProfileModuleUtil") ??
                    typeof(BuildProfile).Assembly.GetType("UnityEditor.Build.Profile.BuildProfileModuleUtil");
                var method = utility?.GetMethod("GetBuildPlayerOptionsFromActiveProfile", BindingFlags.Static | BindingFlags.NonPublic,
                    null, new[] { typeof(string), typeof(string), typeof(BuildOptions) }, null);
                if (method == null || method.ReturnType != typeof(BuildPlayerOptions))
                    throw new InvalidOperationException("현재 Unity의 프로필 빌드 옵션 API를 확인해야 합니다. 디버그 OFF를 추측 적용하지 않습니다.");
                return method;
            }
        }

        /// <summary>Unity의 프로필 기반 옵션 계산을 재사용하고 디버그 플래그만 변경한다. 실제 빌드는 실행하지 않는다.</summary>
        /// <param name="request">이미 활성 제품·플랫폼을 확인한 확정 요청.</param>
        /// <returns>같은 활성 프로필의 씬·타깃·서브타깃·출력을 가진 실행 옵션.</returns>
        public static BuildPlayerOptions CreateBuildOptions(BuildToolRequest request)
        {
            var active = BuildProfile.GetActiveBuildProfile();
            if (!request.debugMode.HasValue || active == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(active)) != request.profileGuid ||
                EditorUserBuildSettings.activeBuildTarget != request.target)
                throw new InvalidOperationException("디버그 모드를 적용할 활성 제품·플랫폼을 먼저 확인하세요.");
            // BuildPlayerWithProfileOptions는 프로필의 Development를 OR하므로 OFF로 강제할 수 없다.
            // 같은 Unity 계산 API를 읽기 전용으로 호출하고 최종 플래그만 조정한다. API 변경 시 추측하지 않고 중단한다.
            var options = (BuildPlayerOptions)ProfileOptionsFactory.Invoke(null, new object[] { request.output, null, BuildOptions.None });
            if (options.target != request.target || options.locationPathName != request.output)
                throw new InvalidOperationException("Unity 프로필에서 계산한 빌드 대상이 요청과 다릅니다.");
            options.options = Apply(options.options, request.debugMode.Value);
            ProductProfiles.ValidateBuildOptions(options.options);
            return options;
        }
    }
}
