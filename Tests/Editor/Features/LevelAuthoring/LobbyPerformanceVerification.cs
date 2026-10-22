using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Levels;
using Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Unity.Profiling;

namespace LevelAuthoring.Editor
{
    // 제작 데이터/프리팹을 변경하지 않는 Editor 전용 목록 계측.
    public static class LobbyPerformanceVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Action install;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeSceneLoad() => install?.Invoke();

        public static void Run()
        {
            string output = "Logs/LobbyStage03/list-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(output);
            bool enabled = EditorSettings.enterPlayModeOptionsEnabled;
            var options = EditorSettings.enterPlayModeOptions;
            var previousScene = EditorSceneManager.playModeStartScene;
            var previousTransform = Addressables.InternalIdTransformFunc;
            var reads = new List<string>();
            double sceneStart = 0, firstCanvas = -1;
            install = () =>
            {
                sceneStart = EditorApplication.timeSinceStartup;
                Addressables.InternalIdTransformFunc = location =>
                {
                    reads.Add(location.InternalId);
                    return previousTransform == null ? location.InternalId : previousTransform(location);
                };
            };
            void CanvasFrame()
            {
                if (Application.isPlaying && sceneStart > 0 && firstCanvas < 0)
                    firstCanvas = (EditorApplication.timeSinceStartup - sceneStart) * 1000;
            }
            Canvas.willRenderCanvases += CanvasFrame;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Lobby.unity");
            EditorApplication.playModeStateChanged += Started;
            EditorApplication.EnterPlaymode();

            async void Started(PlayModeStateChange state)
            {
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                EditorApplication.playModeStateChanged -= Started;
                int exit = 1;
                var report = new List<string>();
                var csv = new List<string> { "count,run,bytes,cells,prepareMs,prepareAllocatedBytes,scroll201Ms,scrollAllocatedBytes,managedDeltaBytes,nativeDeltaBytes,selectMs,scrollFrameAllocatedBytes" };
                try
                {
                    using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(45));
                    LobbyScreen screen = null;
                    await UniTask.WaitUntil(() => (screen = UnityEngine.Object.FindFirstObjectByType<LobbyScreen>()) != null && screen.Snapshot != null, cancellationToken: timeout.Token);
                    await UniTask.DelayFrame(3);
                    Check(reads.Count > 0 && reads.All(path => path.EndsWith("content-generation.bytes") || path.EndsWith("lobby-index.bytes")), "Unexpected startup asset reads");
                    Check(firstCanvas >= 0, "No lobby canvas rendering callback observed");
                    report.Add($"Unity={Application.unityVersion}; OS={SystemInfo.operatingSystem}; graphics={SystemInfo.graphicsDeviceName}; screen={Screen.width}x{Screen.height}; generation={screen.Snapshot.Generation}");
                    report.Add($"Editor BeforeSceneLoad→first Canvas render callback={firstCanvas:F3}ms; LobbyScreen ready={screen.LastReadyMilliseconds:F3}ms; levels={screen.Snapshot.Levels.Count}");
                    report.Add("Canvas callback is an Editor rendering boundary, not Android process start or display presentation latency. List samples run within one initialized Editor lobby; run0 is first fixture bind, run1-3 are warm repeats.");
                    report.AddRange(reads.Select(path => "READ " + path));
                    for (int warm = 1; warm <= 3; warm++)
                    {
                        screen.enabled = false; screen.enabled = true;
                        await UniTask.WaitUntil(() => screen.Snapshot != null, cancellationToken: timeout.Token);
                        report.Add(FormattableString.Invariant($"WARM lobby metadata reload={warm}; readyMs={screen.LastReadyMilliseconds:F3}"));
                    }
                    long calibrationStart = GC.GetAllocatedBytesForCurrentThread();
                    var allocationProbe = new byte[65536]; GC.KeepAlive(allocationProbe);
                    bool threadAllocationsSupported = GC.GetAllocatedBytesForCurrentThread() - calibrationStart >= 65536;
                    report.Add("Thread allocation counter supported=" + threadAllocationsSupported + "; unsupported values use -1, not zero allocations.");
                    using var frameAllocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                    report.Add("Unity frame allocation recorder valid=" + frameAllocations.Valid + "; frame values include test instrumentation and UI frame work, not only the measured loop.");
                    var list = screen.GetComponentInChildren<LobbyLevelList>();
                    var scroll = list.GetComponent<ScrollRect>();
                    float rowHeight = (float)typeof(LobbyLevelList).GetField("rowHeight", Private).GetValue(list);
                    foreach (int count in new[] { 15, 1000, 10000 })
                    {
                        var entries = Enumerable.Range(1, count).Select(number => new LevelLobbyEntry { LevelId = "performance-" + number, LevelNumber = number, DisplayName = "성능 검사 레벨 " + number }).ToArray();
                        byte[] bytes = LevelLobbyIndexCodec.Encode(entries);
                        for (int run = 0; run < 4; run++)
                        {
                            string selected = null;
                            Action<LobbyLevel> choose = level => selected = level.Id;
                            long managed = GC.GetTotalMemory(false), native = Profiler.GetTotalAllocatedMemoryLong();
                            long allocations = GC.GetAllocatedBytesForCurrentThread();
                            var timer = System.Diagnostics.Stopwatch.StartNew();
                            var snapshot = (LobbyIndexSnapshot)Activator.CreateInstance(typeof(LobbyIndexSnapshot), Private, null,
                                new object[] { "performance", LevelLobbyIndexCodec.Decode(bytes) }, null);
                            list.SetItems(snapshot.Levels, choose);
                            timer.Stop();
                            double prepareMs = timer.Elapsed.TotalMilliseconds;
                            long prepareAllocated = threadAllocationsSupported ? GC.GetAllocatedBytesForCurrentThread() - allocations : -1;
                            long managedDelta = GC.GetTotalMemory(false) - managed, nativeDelta = Profiler.GetTotalAllocatedMemoryLong() - native;
                            await UniTask.DelayFrame(2);
                            int cap = Math.Min(count, Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 4);
                            Check(list.CellCount == cap, "Cell cap exceeded: " + count);
                            int[] ids = list.GetComponentsInChildren<LobbyLevelCell>().Select(cell => cell.GetInstanceID()).OrderBy(id => id).ToArray();
                            allocations = GC.GetAllocatedBytesForCurrentThread(); timer.Restart();
                            for (int step = 0; step <= 200; step++)
                            {
                                scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, count * rowHeight - scroll.viewport.rect.height) * step / 200f);
                                scroll.onValueChanged.Invoke(Vector2.zero);
                            }
                            timer.Stop();
                            double scrollMs = timer.Elapsed.TotalMilliseconds;
                            long scrollAllocated = threadAllocationsSupported ? GC.GetAllocatedBytesForCurrentThread() - allocations : -1;
                            var cells = list.GetComponentsInChildren<LobbyLevelCell>();
                            Check(ids.SequenceEqual(cells.Select(cell => cell.GetInstanceID()).OrderBy(id => id)), "Scroll recreated cells");
                            var button = cells.Single(cell => cell.BoundIndex == count - 1).GetComponentInChildren<Button>();
                            timer.Restart(); button.onClick.Invoke(); timer.Stop();
                            Check(selected == "performance-" + count, "Recycled last cell selected wrong ID");
                            double selectMs = timer.Elapsed.TotalMilliseconds;
                            await UniTask.NextFrame();
                            long frameAllocated = frameAllocations.Valid ? frameAllocations.LastValue : -1;
                            csv.Add(FormattableString.Invariant($"{count},{run},{bytes.Length},{list.CellCount},{prepareMs:F3},{prepareAllocated},{scrollMs:F3},{scrollAllocated},{managedDelta},{nativeDelta},{selectMs:F3},{frameAllocated}"));
                        }
                    }
                    var frameCsv = new List<string> { "mode,frame,allocatedBytes" };
                    foreach (bool moving in new[] { false, true, false })
                    {
                        for (int frame = 0; frame < 40; frame++)
                        {
                            if (moving)
                            {
                                scroll.content.anchoredPosition = new Vector2(0, frame * rowHeight);
                                scroll.onValueChanged.Invoke(Vector2.zero);
                            }
                            await UniTask.NextFrame();
                            long allocated = frameAllocations.Valid ? frameAllocations.LastValue : -1;
                            // 샘플 문자열 작성 비용이 다음 프레임에 포함될 수 있어 정지/이동 모두 같은 계측을 사용한다.
                            frameCsv.Add((moving ? "one-row-scroll" : "idle") + "," + frame + "," + allocated);
                        }
                    }
                    File.WriteAllLines(Path.Combine(output, "frames.csv"), frameCsv);
                    report.Add("PASS 15/1000/10000 × first+3 warm samples; bounded stable cells; correct final selection; startup reads only metadata.");
                    exit = 0;
                }
                catch (Exception error) { report.Add("FAIL " + error); }
                finally
                {
                    File.WriteAllLines(Path.Combine(output, "result.txt"), report);
                    File.WriteAllLines(Path.Combine(output, "list.csv"), csv);
                    Canvas.willRenderCanvases -= CanvasFrame;
                    install = null;
                    Addressables.InternalIdTransformFunc = previousTransform;
                    EditorSettings.enterPlayModeOptionsEnabled = enabled;
                    EditorSettings.enterPlayModeOptions = options;
                    EditorSceneManager.playModeStartScene = previousScene;
                    EditorApplication.Exit(exit);
                }
            }
        }

        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

        public static void RunDependencies()
        {
            string output = "Logs/LobbyStage03/dependencies.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var lines = new List<string> { "Source file sizes, not runtime memory or packed build sizes. AssetDatabase dependency closure; no product build." };
            foreach (string root in new[] { "Assets/Scenes/Lobby.unity", "Assets/Scenes/PuzzleGame.unity", "Assets/Prefabs/UI/Puzzle/PuzzleScreen.prefab" })
            {
                lines.Add("ROOT " + root);
                foreach (string path in AssetDatabase.GetDependencies(root, true).OrderBy(path => path))
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    if (extension == ".ttf" || extension == ".otf" || extension == ".wav" || extension == ".mp3" || extension == ".ogg" ||
                        path.Contains("Atlas") || path.Contains("LevelPacks") || path.Contains("ElementPacks"))
                        lines.Add("DEPENDENCY " + path + "; sourceBytes=" + (File.Exists(path) ? new FileInfo(path).Length : -1));
                }
            }
            File.WriteAllLines(output, lines);
            EditorApplication.Exit(0);
        }
    }
}
