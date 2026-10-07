using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Simulation;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleStabilityVerification
    {
        private struct FrameSample
        {
            internal int Frame, Kind, Repeat;
            internal float Milliseconds;
            internal double ActionSeconds;
            internal long CpuNanoseconds, AllocatedBytes, HeapBytes;
            internal bool Board, Progress, Ready;
            internal BoardActionPhase Phase;
        }
        public static void Observe()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output); SessionState.SetBool("Stage11.Observe", true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static async UniTask ObservationAsync()
        {
            results.Clear(); int exit = 0; float previous = Time.timeScale;
            List<FrameSample> samples = new List<FrameSample>(65536);
            List<string> summaries = new List<string> { "fixture,repeat,warmup,prepare_seconds,action_seconds,input_or_result_ready_seconds,frames,board_ms,progress_only_ms,pool_before,pool_after" };
            using ProfilerRecorder cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            using ProfilerRecorder allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            using ProfilerRecorder heap = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", 1);
            try
            {
                Time.timeScale = 1; PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                Stopwatch startup = Stopwatch.StartNew();
                while (!session.CanAcceptInput && !session.HasFailed && startup.Elapsed.TotalSeconds < 30) await UniTask.NextFrame();
                Check(session.CanAcceptInput, "실제 Update 초기 게임 준비");
                File.WriteAllText(Output + "observation-environment.txt", "UTC=" + DateTime.UtcNow.ToString("O") + "\nUnity=" + Application.unityVersion +
                    "\nOS=" + SystemInfo.operatingSystem + "\nCPU=" + SystemInfo.processorType + "\nGPU=" + SystemInfo.graphicsDeviceName +
                    "\nScreen=" + Screen.width + "x" + Screen.height + "\nBatch=" + Application.isBatchMode + "\nTimeScale=1\nSeed=" + Field(session, "seed") +
                    "\nInitialReadyWaitSeconds=" + startup.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture) +
                    "\nConcurrentEditor=NCloud_Unit_CV preserved; see environment-processes.txt\nMainThreadRecorder=" + cpu.Valid +
                    "\nGCAllocatedRecorder=" + allocations.Valid + "\nGCUsedRecorder=" + heap.Valid +
                    "\nMethod=actual enabled session Update; unique NextFrame/LastPostLateUpdate; unscaledDeltaTime; recorder LastValue is preceding completed frame.\n" +
                    "Preparation and repeat0 warmup excluded from measured frame statistics. Direct replay occurs after measurement; initial snapshot instrumentation remains. Instrumentation memory/Editor/render/audio/other process costs remain. No player/mobile FPS claim. Missing recorder samples stored as -1. No hearing/device verification.\n");
                foreach (int kind in new[] { 0, 5, 4 })
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        Stopwatch preparation = Stopwatch.StartNew(); var level = await PrepareFixture(session, kind); preparation.Stop();
                        try
                        {
                            int[] before = PoolCounts(session); int first = samples.Count;
                            await UniTask.NextFrame();
                            Stopwatch action = Stopwatch.StartNew();
                            BoardActionExecutor direct = BeginFixture(session, kind, false); session.enabled = true;
                            int priorFrame = Time.frameCount;
                            while (Busy(session) && !session.HasFailed && action.Elapsed.TotalSeconds < 90)
                            {
                                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                                if (Time.frameCount <= priorFrame) throw new InvalidOperationException("실제 프레임 중복 표본");
                                priorFrame = Time.frameCount;
                                samples.Add(new FrameSample { Frame = Time.frameCount, Kind = kind, Repeat = repeat, Milliseconds = Time.unscaledDeltaTime * 1000,
                                    ActionSeconds = action.Elapsed.TotalSeconds,
                                    CpuNanoseconds = cpu.Valid && cpu.Count > 0 ? cpu.LastValue : -1,
                                    AllocatedBytes = allocations.Valid && allocations.Count > 0 ? allocations.LastValue : -1,
                                    HeapBytes = heap.Valid && heap.Count > 0 ? heap.LastValue : -1,
                                    Board = session.IsPresenting, Progress = session.HasProgressFeedback, Ready = session.CanAcceptInput || session.ResultReady, Phase = session.Phase });
                            }
                            action.Stop(); session.enabled = false;
                            if (kind == 0) direct.Swap(new Board.BoardCoordinate(2, 3), new Board.BoardCoordinate(3, 3));
                            else if (kind == 4) direct.Swap(new Board.BoardCoordinate(4, 4), new Board.BoardCoordinate(4, 5));
                            else direct.Activate(new Board.BoardCoordinate(4, 4));
                            while (direct.HasPendingCascade) direct.AdvanceCascade();
                            Check(!session.HasFailed && !Busy(session) && Snapshot(session.State) == Snapshot(direct.State) && session.Phase == direct.Phase && Snapshot(session.Outcome) == Snapshot(direct.Outcome),
                                "실제 Update 상태·Phase·승패 직접 동등 " + kind + "/" + repeat);
                            int[] after = PoolCounts(session); FrameSample[] current = samples.Skip(first).ToArray();
                            summaries.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F6},{4:F6},{5:F6},{6},{7:F3},{8:F3},\"{9}\",\"{10}\"", kind, repeat, repeat == 0,
                                preparation.Elapsed.TotalSeconds, action.Elapsed.TotalSeconds, current.First(frame => frame.Ready).ActionSeconds, current.Length, current.Where(frame => frame.Board).Sum(frame => frame.Milliseconds),
                                current.Where(frame => !frame.Board && frame.Progress).Sum(frame => frame.Milliseconds), string.Join("/", before), string.Join("/", after)));
                            Check(after[7] == 8 && after[8] == 14, "실제 Update 소리 풀 상한 " + kind + "/" + repeat);
                        }
                        finally { UnityEngine.Object.Destroy(level); }
                    }
                FrameSample[] measured = samples.Where(sample => sample.Repeat > 0).ToArray();
                Check(measured.Length >= 300 && measured.Select(sample => sample.Kind).Distinct().Count() == 3, "실제 Update 3종 사례·측정 표본300 이상 " + measured.Length);
                foreach (int kind in new[] { 0, 5, 4 })
                {
                    FrameSample[] current = measured.Where(sample => sample.Kind == kind).ToArray();
                    Check(current.Length > 0 && current.Any(sample => sample.Board) && current.All(sample => sample.Milliseconds > 0), "실제 활성 플레이 표본 " + kind);
                    double[] times = current.Select(sample => (double)sample.Milliseconds).OrderBy(value => value).ToArray();
                    results.Add(string.Format(CultureInfo.InvariantCulture, "OBS fixture={0} frames={1} p50_ms={2:F3} p95_ms={3:F3} max_ms={4:F3} GCmax_bytes={5} CPUmax_ns={6}",
                        kind, times.Length, Percentile(times, .5), Percentile(times, .95), Percentile(times, 1), current.Max(sample => sample.AllocatedBytes), current.Max(sample => sample.CpuNanoseconds)));
                }
                BaselineInvariantChecks();
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                List<string> rows = new List<string> { "fixture,repeat,warmup,frame,frame_ms,main_thread_ns,gc_allocated_bytes,gc_used_bytes,board,progress,ready,phase,action_seconds" };
                foreach (FrameSample sample in samples)
                    rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:F6},{5},{6},{7},{8},{9},{10},{11},{12:F6}", sample.Kind, sample.Repeat, sample.Repeat == 0,
                        sample.Frame, sample.Milliseconds, sample.CpuNanoseconds, sample.AllocatedBytes, sample.HeapBytes, sample.Board, sample.Progress, sample.Ready, sample.Phase, sample.ActionSeconds));
                File.WriteAllLines(Output + "observation.csv", rows); File.WriteAllLines(Output + "action-observation.csv", summaries);
                Time.timeScale = previous; File.WriteAllLines(Output + "observation-results.txt", results); EditorApplication.Exit(exit);
            }
        }
        private static double Percentile(double[] sorted, double fraction) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * fraction) - 1)];
    }
}
