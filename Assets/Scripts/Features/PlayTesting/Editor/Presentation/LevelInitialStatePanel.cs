using System;
using System.Linq;
using System.Text;
using Board;
using Simulation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    [Serializable]
    public sealed partial class LevelInitialStatePanel
    {
        public VisualElement rootVisualElement { get; private set; }
        internal LevelEditorWindow Owner { get; private set; }
        private bool visible;
        private bool resumeCascade;
        [SerializeField] private LevelDefinition level;
        [SerializeField] private int seed = 1;
        private Label status;
        private Label details;
        private Label overview;
        private VisualElement grid;
        private IntegerField seedField;
        private IVisualElementScheduledItem inputSchedule;
        private string inputFingerprint;
        private Button selectedButton;
        private TurnEffectContext displayedGeneratorContext;
        private int displayedGeneratorRecords;
        internal LevelRuntimeState CurrentState { get; private set; }
        internal double LastBuildMilliseconds { get; private set; }

        internal void Initialize(LevelEditorWindow owner, LevelDefinition target, bool manual)
        {
            if (manual && !manualMode) seed = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
            Owner = owner; level = target; manualMode = manual;
            if (manual) startingMode = true;
            CreateGUI();
        }

        internal void SetLevel(LevelDefinition target)
        {
            if (level == target) return;
            level = target;
            rootVisualElement.Q<ObjectField>("initial-level")?.SetValueWithoutNotify(target);
            if (batchSession != null) { UpdateBatchControls(); return; }
            Invalidate("레벨이 바뀌었습니다. 다시 구성하세요.");
        }

        internal void SetVisible(bool value)
        {
            // 탭을 숨겨도 실행 객체는 유지한다. 숨겨진 상태에서 연쇄나 시작 보드 탐색이 진행되면
            // 사용자가 보지 못한 동안 결과가 바뀌므로 예약 작업과 측정 시간을 함께 멈춘다.
            // 재개는 이전에 실제 실행 중이었던 연쇄에만 적용한다.
            if (visible && !value)
            {
                PauseBatch();
                StopBot();
                resumeCascade = cascadeRunning;
                StopCascadeRun(); OnLostFocus();
                searchSchedule?.Pause(); searchWatch?.Stop();
            }
            visible = value;
            rootVisualElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            CheckInput();
            if (value)
            {
                searchSchedule?.Resume();
                if (search != null) searchWatch?.Start();
                if (resumeCascade && execution?.HasPendingCascade == true)
                {
                    cascadeOwner = execution; cascadeRunning = true;
                    EditorApplication.update -= CascadeTick;
                    EditorApplication.update += CascadeTick;
                }
                resumeCascade = false;
                UpdateExecutionControls();
            }
        }

        internal void PrepareManual()
        {
            if (batchSession != null) return;
            CheckInput();
            if (CurrentState == null && !IsSearching && level != null) Build();
        }

        public void CreateGUI()
        {
            // 직렬화 복원 중에는 Unity UI를 만들 수 없으므로 창의 CreateGUI에서 생성한다.
            rootVisualElement ??= new VisualElement();
            ClearBatch(); ClearBot(); ClearQuery(); ClearExecution();
            VisualElement root = rootVisualElement; root.Clear(); root.RemoveFromClassList("manual-play"); root.AddToClassList("match-editor"); root.AddToClassList("initial-state");
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/LevelEditor/Editor/Styles/LevelEditor.uss"));
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/PlayTesting/Editor/Styles/LevelInitialState.uss"));
            Label title = new Label("MATCH / 초기 보드 확인"); title.AddToClassList("editor-title"); root.Add(title);
            Label boundary = new Label("초기 배치 후보 · 시작 조건 미검사 · 플레이 미지원") { name = "initial-boundary" }; root.Add(boundary);
            VisualElement toolbar = new VisualElement { name = "initial-toolbar" }; toolbar.AddToClassList("initial-toolbar"); root.Add(toolbar);
            ObjectField asset = new ObjectField("레벨") { name = "initial-level", objectType = typeof(LevelDefinition), allowSceneObjects = false, value = level };
            asset.RegisterValueChangedCallback(evt => Owner.SetLevel(evt.newValue as LevelDefinition)); toolbar.Add(asset);
            asset.style.display = DisplayStyle.None;
            seedField = new IntegerField("시드") { name = "initial-seed", value = seed };
            seedField.RegisterValueChangedCallback(evt => { seed = evt.newValue; Invalidate("시드가 바뀌었습니다. 다시 구성하세요."); }); toolbar.Add(seedField);
            toolbar.Add(new Button(Build) { name = "initial-build", text = "같은 시드로 구성" });
            toolbar.Add(new Button(() =>
            {
                int next;
                do { next = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0); } while (next == seed);
                // 버튼 이벤트 중 지연된 ChangeEvent가 방금 만든 후보를 지우지 않게 함께 갱신한다.
                seed = next; seedField.SetValueWithoutNotify(next); Build();
            }) { name = "initial-new-seed", text = "새 시드로 구성" });
            CreateQueryUI(root);
            CreateExecutionUI(root);
            ScrollView diagnostics = new ScrollView { name = "initial-diagnostics" }; root.Add(diagnostics);
            status = new Label { name = "initial-status" }; diagnostics.Add(status);
            VisualElement body = new VisualElement(); body.AddToClassList("initial-body"); root.Add(body);
            ScrollView board = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { name = "initial-board-scroll" };
            grid = new VisualElement { name = "initial-grid" }; board.Add(grid); body.Add(board);
            ScrollView inspector = new ScrollView { name = "initial-inspector" }; body.Add(inspector);
            details = new Label("칸을 선택하면 내용이 표시됩니다.") { name = "initial-details" }; inspector.Add(details);
            overview = new Label { name = "initial-overview" }; inspector.Add(overview);
            AttachQueryResults(inspector);
            CreateManualUI();
            CreateBotUI();
            Invalidate(manualMode ? "레벨을 지정하고 검사·구성을 누르세요. 창 재생성 후에는 다시 시작해야 합니다." : "레벨과 시드를 지정하고 초기 후보를 구성하세요.");
            inputSchedule?.Pause();
            inputSchedule = root.schedule.Execute(CheckInput).Every(250);
            LevelEditorHelp.Apply(root);
        }

        private void Invalidate(string message)
        {
            ClearBot();
            resumeCascade = false;
            ClearQuery();
            ClearExecution();
            CurrentState = null; selectedButton = null; inputFingerprint = null;
            displayedGeneratorContext = null; displayedGeneratorRecords = 0;
            grid?.Clear();
            if (details != null) details.text = "표시할 후보가 없습니다.";
            if (overview != null) overview.text = "";
            if (status != null) status.text = message;
            UpdateManualControls();
        }

        private void CheckInput()
        {
            if (batchSession != null) { UpdateBatchControls(); return; }
            if (inputFingerprint != null && inputFingerprint != LevelStateBuilder.Fingerprint(level))
                Invalidate("원본 내용이 바뀌었습니다. 이전 후보를 지웠습니다. 다시 구성하세요.");
        }

        private void Build()
        {
            if (batchSession?.CanContinue == true) return;
            ClearBatch();
            Invalidate("구성 중…");
            inputFingerprint = LevelStateBuilder.Fingerprint(level);
            if (startingMode)
            {
                StartSearch();
                return;
            }
            // 메모리의 미저장 내용도 입력으로 사용하며 에셋을 저장하거나 Undo에 등록하지 않는다.
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            watch.Stop(); LastBuildMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (!result.IsBuilt) { status.text = "구성 불가\n" + string.Join("\n", result.Issues.Select(issue => issue.ToString())); return; }
            CurrentState = result.State;
            DisplayState();
        }

        private void DisplayState(LevelRuntimeState shown = null)
        {
            LevelRuntimeState display = shown ?? CurrentState;
            grid.Clear(); selectedButton = null;
            status.text = $"레벨 {display.LevelNumber} · 시드 {seed} · 구성 {LastBuildMilliseconds:F2} ms · 입력 {inputFingerprint.Substring(0, 10)}";
            for (int row = 0; row < display.Rows; row++)
            {
                VisualElement line = new VisualElement(); line.AddToClassList("initial-row"); grid.Add(line);
                for (int column = 0; column < display.Columns; column++)
                {
                    RuntimeCell cell = display.CellAt(new BoardCoordinate(row, column));
                    Button button = new Button { name = $"initial-cell-{row}-{column}", text = CellText(cell, display), tooltip = cell.Coordinate.ToString() };
                    button.AddToClassList("initial-cell");
                    if (!cell.IsActive) button.AddToClassList("inactive");
                    if (cell.Color.HasValue && cell.Cover != CoverKind.Mold) button.AddToClassList("rabbit-" + (int)cell.Color.Value);
                    if (cell.ObstacleIndex.HasValue && display.Obstacles[cell.ObstacleIndex.Value].Definition.Kind == ObstacleKind.ColorLock)
                        button.AddToClassList("rabbit-" + (int)display.Obstacles[cell.ObstacleIndex.Value].Definition.Color);
                    if (cell.Cover.HasValue) button.AddToClassList("covered");
                    button.clicked += () => ShowCell(cell, button); line.Add(button);
                }
            }
            TurnEffectContext generatorContext = execution?.TurnEffects;
            int skip = displayedGeneratorContext?.Turn == generatorContext?.Turn ? displayedGeneratorRecords : 0;
            int[] pulsing = generatorContext == null ? Array.Empty<int>() : generatorContext.Generators.Skip(skip)
                .Where(r => r.Event == GeneratorEvent.Charged).Select(r => r.GeneratorIndex).Distinct().ToArray();
            grid.Add(new LevelGeneratorOverlay(display, pulsing));
            displayedGeneratorContext = generatorContext; displayedGeneratorRecords = generatorContext?.Generators.Count ?? 0;
            details.text = "칸을 선택해 본체·덮개·바닥·연결을 확인하세요.\n일반 블록은 숫자로도 색 종류를 구분합니다.";
            overview.text = $"남은 이동 {display.MovesRemaining}\n본체 {display.Cells.Count(cell => cell.Content == RuntimeContent.Obstacle)} · 회수 부품 {display.Cells.Count(cell => cell.Content == RuntimeContent.Recovery)}\n" +
                $"미션 {display.Missions.Count}\n" + string.Join("\n", display.Missions.Select(mission => mission.Definition.Kind == MissionKind.Mold ? "남은 곰팡이 " + mission.Remaining : $"{LevelMissionRules.Name(mission.Definition.Kind)}{(mission.Definition.Kind == MissionKind.Color ? " / 토" + ((int)mission.Definition.Color + 1) : "")}: {mission.Progress}/{mission.Target}")) +
                $"\n\n공급 {display.Supply.Sources.Count}곳\n고철 현재 {display.LiveScrapCount} / 유지 {display.Supply.ScrapTarget}\n추가 생성 {display.Supply.ScrapGenerated} / 한도 {display.Supply.ScrapLimit} · 남음 {display.Supply.ScrapRemaining}\n공급 내구도 {display.Supply.ScrapDurability}\n회수 유지 {display.Supply.RecoveryTarget}\n\n" +
                RecoverySummary(display) + $"\n\n난수 {display.Random.Version}\n추출 {display.Random.DrawCount}회\n같은 입력/시드/런타임에서 재현\n\n" +
                (execution == null ? "직접 구성한 후보입니다. 낙하·공급·매칭을 실행하지 않습니다." : EndingSummary());
            if (shown == null) ShowQueryResults();
            UpdateExecutionControls();
        }

        /// <param name="cell">표시할 칸.</param><param name="state">이 칸이 속한 실행 또는 재생 사본.</param>
        /// <returns>플레이와 사례 재생이 공유하는 블록·장애물·바닥 표시.</returns>
        internal static string CellText(RuntimeCell cell, LevelRuntimeState state)
        {
            if (!cell.IsActive) return "×";
            if (cell.Cover == CoverKind.Mold) return "곰팡이" + (cell.DustDurability > 0 ? "*" + cell.DustDurability : "");
            string text = cell.Content switch
            {
                RuntimeContent.Normal => "토" + ((int)cell.Color.Value + 1),
                RuntimeContent.Rocket => cell.RocketDirection == Levels.RocketDirection.Horizontal ? "로↔" : "로↕",
                RuntimeContent.Bomb => "폭탄", RuntimeContent.Drone => "드론", RuntimeContent.Magnet => "자석",
                RuntimeContent.Recovery => "회수", RuntimeContent.Obstacle => state.Obstacles[cell.ObstacleIndex.Value].Definition.Kind switch
                { ObstacleKind.Crate => "상자", ObstacleKind.Scrap => "고철", ObstacleKind.Safe => "금고", ObstacleKind.ColorLock => "잠금", ObstacleKind.Appliance => "가전", _ => "발전" },
                _ => "·"
            };
            if (cell.Content == RuntimeContent.Obstacle)
            {
                RuntimeObstacle body = state.Obstacles[cell.ObstacleIndex.Value];
                text += body.Definition.Kind == ObstacleKind.Generator ? " " + body.Charge + "/" + body.Definition.RequiredCharge : body.Durability.ToString();
                if (body.Definition.Kind == ObstacleKind.Appliance) text += "\n#" + cell.ObstacleIndex;
                if (body.Definition.Kind == ObstacleKind.ColorLock) text += "\n토" + ((int)body.Definition.Color + 1);
            }
            if (cell.Cover.HasValue) text = (cell.Cover == CoverKind.Web ? "줄" : "곰") + cell.CoverDurability + "\n" + text;
            if (cell.DustDurability > 0) text += "*" + cell.DustDurability;
            if (state.Flow.Arrivals.Contains(cell.Coordinate)) text += "\n▽도착";
            return text;
        }

        private static string RecoverySummary(LevelRuntimeState state) =>
            $"회수 누적 {state.Recoveries.Count} · 남은 목표 {RecoveryRules.Remaining(state)}\n보드 부품 {RecoveryRules.OnBoard(state)} · 유지 부족 {RecoveryRules.Needed(state)}\n" +
            string.Join("\n", state.Recoveries.Select(r => $"회수 {r.Coordinate} · 수 {r.Turn} / 묶음 {r.Batch}"));

        private void ShowCell(RuntimeCell cell, Button button)
        {
            LevelRuntimeState display = execution?.State ?? CurrentState;
            selectedButton?.RemoveFromClassList("selected"); selectedButton = button; button.AddToClassList("selected");
            StringBuilder text = new StringBuilder(); text.AppendLine(cell.Coordinate.ToString());
            text.AppendLine(cell.IsActive ? CellText(cell, display).Replace("\n", " / ") : "비활성 칸");
            if (cell.ObstacleIndex.HasValue)
            {
                RuntimeObstacle obstacle = display.Obstacles[cell.ObstacleIndex.Value];
                text.AppendLine($"본체 #{cell.ObstacleIndex} · {obstacle.Definition.Id}\n기준 {obstacle.Definition.Coordinate}\n내구도 {obstacle.Durability}");
                if (obstacle.Definition.Kind == ObstacleKind.ColorLock) text.AppendLine("색 토" + ((int)obstacle.Definition.Color + 1));
                if (obstacle.Definition.Kind == ObstacleKind.Generator) text.AppendLine($"충전 {obstacle.Charge}/{obstacle.Definition.RequiredCharge}");
                foreach (RuntimeConnection connection in GeneratorRules.ActiveConnections(display).Where(item => item.GeneratorId == obstacle.Definition.Id || item.TargetId == obstacle.Definition.Id))
                    text.AppendLine($"연결 {connection.GeneratorId} → {connection.TargetId}\n전선 {string.Join(" → ", connection.Vertices)}");
            }
            string cover = cell.Cover == CoverKind.Web ? "거미줄" : cell.Cover == CoverKind.Mold ? "곰팡이" : "없음";
            string gravity = cell.Gravity switch { GravityDirection.Up => "위", GravityDirection.Left => "왼쪽", GravityDirection.Right => "오른쪽", _ => "아래" };
            text.AppendLine($"덮개 {cover} / {cell.CoverDurability}\n먼지 {cell.DustDurability}\n중력 {gravity}");
            foreach (FlowPathCell path in display.Flow.Paths.Where(item => item.Coordinate.Equals(cell.Coordinate)))
                text.AppendLine(path.IsEnd ? "직접 경로 끝칸" : "다음 칸 " + path.Next);
            foreach (RuntimeMerge merge in display.Flow.Merges.Where(item => item.Coordinate.Equals(cell.Coordinate)))
                text.AppendLine("합류 우선 " + string.Join(" → ", merge.Sources));
            foreach (FlowPortal portal in display.Flow.Portals.Where(item => item.Entrance.Equals(cell.Coordinate) || item.Exit.Equals(cell.Coordinate)))
                text.AppendLine($"통로 {portal.Entrance} → {portal.Exit}");
            foreach (BoardEdge wall in display.Flow.Walls.Where(item => item.A.Equals(cell.Coordinate) || item.B.Equals(cell.Coordinate))) text.AppendLine("벽 " + wall);
            if (display.Flow.Arrivals.Contains(cell.Coordinate)) text.AppendLine("회수 도착 바닥");
            foreach (RuntimeSource source in display.Supply.Sources.Where(item => item.Coordinate.Equals(cell.Coordinate)))
            {
                string mode = source.Mode switch { SupplyMode.Fixed => "고정 목록", SupplyMode.MaintainScrap => "고철 유지", SupplyMode.MaintainRecovery => "회수 부품 유지", _ => "무작위" };
                text.AppendLine($"공급 {mode} / 소진 후 {(source.Exhaustion == SupplyExhaustion.Stop ? "중단" : "무작위")}\n다음 항목 {source.ItemIndex + 1}번 / 소비 {source.ItemConsumed}");
                for (int i = 0; i < source.Items.Count; i++)
                {
                    SupplyItem item = source.Items[i]; text.AppendLine($"{i + 1}번 공급: {LevelSupplyRules.Name(item.Kind)} ×{item.Count}");
                    if (item.Kind == SupplyKind.FixedNormal) text.AppendLine("색 토" + ((int)item.Color + 1));
                    if (item.Kind == SupplyKind.Rocket) text.AppendLine("방향 " + (item.Direction == RocketDirection.Horizontal ? "가로" : "세로"));
                    if (item.Kind == SupplyKind.Scrap) text.AppendLine("내구도 " + item.Durability);
                }
            }
            details.text = text.ToString();
            SelectExecutionCell(cell.Coordinate);
        }
    }
}
