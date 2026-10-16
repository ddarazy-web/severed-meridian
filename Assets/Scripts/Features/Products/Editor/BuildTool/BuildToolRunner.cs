using System;
using System.IO;
using System.Collections.Generic;

namespace Products.Editor
{
    public sealed class BuildToolRunner
    {
        private readonly IBuildToolBackend backend;
        private readonly string session;
        private readonly BuildToolOptions options;
        public readonly BuildToolRunStore Store;
        public BuildToolRequest Current { get; private set; }
        public string StoreError { get; private set; }
        public bool IsLocked => StoreError != null || Current != null && !Current.IsTerminal;
        public bool CanCancel => Current != null && !Current.IsTerminal && Current.phase != BuildToolRunPhase.NeedsAttention && !Current.callStarted;

        public BuildToolRunner(IBuildToolBackend backend, BuildToolRunStore store, string session, BuildToolOptions options = null)
        {
            this.backend = backend; Store = store; this.session = session; this.options = options ?? BuildToolOptions.Default;
            try
            {
                Current = store.Load();
                if (Current != null && !Current.IsTerminal && (Current.session != session || Current.isMock != backend.IsMock))
                    Attention("다른 에디터 세션 또는 실행 방식의 미완료 요청입니다. 자동 실행하지 않습니다.");
            }
            catch (Exception error) { StoreError = error.Message; }
        }

        public void Start(string guid, bool userConfirmed = false, IReadOnlyDictionary<string, bool> choices = null)
        {
            if (IsLocked) throw new InvalidOperationException("진행 중이거나 정리가 필요한 요청이 있습니다.");
            if (!backend.IsMock && !userConfirmed) throw new InvalidOperationException("실제 빌드는 사용자의 명시적 실행 확인이 필요합니다.");
            if (backend.IsBusy || backend.HasCompilationErrors) throw new InvalidOperationException("에디터 처리·다른 빌드·컴파일 오류를 먼저 해결하세요.");
            var profile = backend.Read(guid); backend.Validate(profile);
            var product = profile.Product;
            var resolvedOptions = options.Resolve(product, choices);
            var request = new BuildToolRequest
            {
                id = Guid.NewGuid().ToString("N"), session = session, isMock = backend.IsMock, userConfirmed = userConfirmed,
                profileGuid = guid, profilePath = profile.Path, product = product.Name, symbol = product.Symbol, target = profile.Target,
                output = Path.GetFullPath(BuildToolPlatforms.Default.Output(product, resolvedOptions)), fingerprint = backend.Fingerprint(profile), startedUtc = DateTime.UtcNow.ToString("O"),
                phase = BuildToolRunPhase.Preparing, status = "빌드 요청을 확정했습니다.", options = resolvedOptions
            };
            request.logPath = Path.GetFullPath(Path.Combine(Store.Root, request.id + ".log"));
            request.selectionSnapshot = backend.CaptureSelection(request);
            Current = request;
            try { Store.Save(request); Store.Log(request, request.status); }
            catch (Exception error)
            {
                // 일부 기록만 저장되어도 재로드 뒤 숨은 요청이 실행되지 않도록 잠근다.
                Attention("요청 기록 실패. 자동 실행하지 않습니다: " + error.Message);
                throw new InvalidOperationException(Current.status, error);
            }
        }

        public void Step()
        {
            if (StoreError != null || Current == null || Current.IsTerminal || Current.phase == BuildToolRunPhase.NeedsAttention || backend.IsBusy) return;
            try
            {
                if (Current.session != session || Current.isMock != backend.IsMock) { Attention("요청 소유 세션이 달라 자동 실행을 중단했습니다."); return; }
                if (Current.phase == BuildToolRunPhase.Building)
                {
                    Attention("빌드 호출 시작 기록은 있지만 완료 증거가 없습니다. 재호출하지 않습니다. 결과 확인 후 정리하세요."); return;
                }
                if (Current.phase == BuildToolRunPhase.Finalizing) { FinalizeRun(); return; }
                if (backend.HasCompilationErrors) throw new InvalidOperationException("스크립트 컴파일 오류로 실행을 중단했습니다.");
                if (Current.phase == BuildToolRunPhase.Postprocessing)
                {
                    options.CheckFingerprint(Current, backend);
                    options.RunStage(BuildToolOptionStage.AfterBuild, Current, Store, backend);
                    if (!options.StageComplete(BuildToolOptionStage.AfterBuild, Current) || backend.IsBusy) return;
                    BeginFinalization("후처리 완료. 소유 임시 설정을 정리합니다."); return;
                }
                var profile = backend.Read(Current.profileGuid); backend.Validate(profile);
                if (profile.Path != Current.profilePath || profile.Product.Name != Current.product || profile.Target != Current.target ||
                    Path.GetFullPath(BuildToolPlatforms.Default.Output(profile.Product, Current.options)) != Current.output)
                    throw new InvalidOperationException("확정 후 프로필 설정이 변경됐습니다. 새 요청으로 다시 검사하세요.");
                options.CheckFingerprint(Current, backend);
                if (Current.phase == BuildToolRunPhase.Preparing)
                {
                    options.RunStage(BuildToolOptionStage.PrepareCompilation, Current, Store, backend);
                    if (!options.StageComplete(BuildToolOptionStage.PrepareCompilation, Current) || backend.IsBusy) return;
                    Current.selectionStarted = true;
                    Transition(BuildToolRunPhase.Selecting, "제품 프로필과 콘텐츠 경로를 적용합니다.");
                    backend.Select(Current);
                    Transition(BuildToolRunPhase.WaitingForEditor, "플랫폼 전환·재컴파일 완료를 기다립니다.");
                    return;
                }
                if (Current.phase == BuildToolRunPhase.Selecting)
                {
                    Transition(BuildToolRunPhase.WaitingForEditor, "선택 중 재로드가 발생했습니다. 재선택하지 않고 실제 상태를 확인합니다."); return;
                }
                if (Current.phase == BuildToolRunPhase.WaitingForEditor)
                {
                    if (!backend.IsReady(Current, out string reason))
                    {
                        Current.status = "전환 대기: " + reason;
                        if (Current.ElapsedSeconds > 900) throw new TimeoutException("프로필 전환·재컴파일 대기 시간이 초과됐습니다. " + reason);
                        return;
                    }
                    Current.selectionEstablished = true;
                    Transition(BuildToolRunPhase.Ready, "활성 제품·플랫폼·출력·콘텐츠 설정 확인 완료."); return;
                }
                if (Current.phase == BuildToolRunPhase.Ready)
                {
                    if (!backend.IsReady(Current, out string reason)) throw new InvalidOperationException("실행 직전 상태가 달라졌습니다: " + reason);
                    options.RunStage(BuildToolOptionStage.BeforeBuild, Current, Store, backend);
                    if (!options.StageComplete(BuildToolOptionStage.BeforeBuild, Current) || backend.IsBusy) return;
                    options.CheckFingerprint(Current, backend);
                    if (Current.callStarted) { Attention("이미 호출한 요청이므로 다시 실행하지 않습니다."); return; }
                    Current.callStarted = true; Current.modeTouched = true;
                    Transition(BuildToolRunPhase.Building, "빌드 API 호출 시작. 동기 빌드 중에는 이 도구에서 강제 취소하지 않습니다.");
                    var result = backend.Build(Current);
                    if (result == null || result.outcome != BuildToolRunPhase.Succeeded && result.outcome != BuildToolRunPhase.Failed && result.outcome != BuildToolRunPhase.Canceled)
                        throw new InvalidDataException("정상 빌드 완료 결과를 받지 못했습니다.");
                    Current.result = result; Current.error = result.error; Current.pendingOutcome = result.outcome;
                    if (result.outcome == BuildToolRunPhase.Succeeded && Current.options.Count > 0)
                    { Transition(BuildToolRunPhase.Postprocessing, "Player 성공 결과 수신. 옵션 후처리를 진행합니다."); return; }
                    BeginFinalization("빌드 결과 수신. 소유 임시 설정을 정리합니다.");
                }
            }
            catch (Exception error)
            {
                if (Current.phase == BuildToolRunPhase.Postprocessing) Current.postprocessError = error.Message;
                if (Current.phase == BuildToolRunPhase.Building && Current.result == null && !(error is InvalidDataException))
                    Current.error = "빌드 예외: " + error.Message;
                else Current.error = error.Message;
                Current.pendingOutcome = Current.callStarted && Current.result == null ? BuildToolRunPhase.NeedsAttention : BuildToolRunPhase.Failed;
                BeginFinalization("실패 또는 결과 미확인 정리: " + Current.error);
            }
        }

        public void Cancel()
        {
            if (!CanCancel) throw new InvalidOperationException("현재 단계는 안전한 취소 대상이 아닙니다.");
            Current.pendingOutcome = BuildToolRunPhase.Canceled;
            BeginFinalization("준비·대기 중 사용자가 취소했습니다.");
        }

        public void ResolveInterrupted()
        {
            if (Current?.phase != BuildToolRunPhase.NeedsAttention || backend.IsBusy || Current.isMock != backend.IsMock)
                throw new InvalidOperationException("현재 요청의 안전한 정리 가능 여부를 먼저 확인하세요.");
            StoreError = null;
            if (Current.result?.outcome == BuildToolRunPhase.Succeeded && Current.options.Count > 0 &&
                !options.StageComplete(BuildToolOptionStage.AfterBuild, Current))
                Current.postprocessError = Current.postprocessError ?? "후처리를 완료하지 못한 중단 요청입니다. Player 성공과 별개로 전체 작업은 실패로 정리합니다.";
            Current.pendingOutcome = Current.postprocessError != null ? BuildToolRunPhase.Failed : Current.result?.outcome ?? BuildToolRunPhase.Failed;
            if (Current.result == null && Current.error == null) Current.error = "빌드 완료 결과 미확인. 재호출하지 않고 정리했습니다.";
            BeginFinalization("사용자 요청으로 미완료 작업의 소유 임시 상태를 정리합니다.");
        }

        private void BeginFinalization(string status)
        {
            // 이력·로그 실패가 정리를 가로막으면 다음 제품에 임시 설정이 유출된다.
            try { Transition(BuildToolRunPhase.Finalizing, status); }
            catch (Exception error) { StoreError = "정리 전 기록 실패: " + error.Message; }
            FinalizeRun();
        }

        private void FinalizeRun()
        {
            var cleanupErrors = new List<string>();
            try { options.Cleanup(Current, Store, backend); } catch (Exception error) { cleanupErrors.Add(error.Message); }
            try { backend.Cleanup(Current); } catch (Exception error) { cleanupErrors.Add(error.Message); }
            Current.cleanupError = cleanupErrors.Count == 0 ? null : string.Join("\n", cleanupErrors);
            if (Current.cleanupError != null) { Attention("정리 실패: " + Current.cleanupError); return; }
            if (StoreError != null) { Attention(StoreError); return; }
            if (Current.pendingOutcome == BuildToolRunPhase.NeedsAttention)
            {
                Attention("완료 증거가 없어 결과 확인이 필요합니다. 소유 임시 상태는 정리했으며 빌드를 재호출하지 않습니다. " + Current.error); return;
            }
            Current.finishedUtc = DateTime.UtcNow.ToString("O");
            try { Transition(Current.pendingOutcome, (Current.isMock ? "모의 실행 " : "실제 빌드 ") + Current.pendingOutcome + (string.IsNullOrEmpty(Current.error) ? "" : " · " + Current.error)); }
            catch (Exception error) { Attention("정리는 완료했지만 결과 기록에 실패했습니다: " + error.Message); }
        }

        private void Transition(BuildToolRunPhase phase, string status)
        {
            Current.phase = phase; Current.status = status;
            Store.Save(Current); Store.Log(Current, status);
        }

        private void Attention(string reason)
        {
            Current.phase = BuildToolRunPhase.NeedsAttention; Current.status = reason;
            try { Store.Save(Current); Store.Log(Current, reason); }
            catch (Exception error) { StoreError = reason + " / " + error.Message; }
        }
    }
}
