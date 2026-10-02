using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace PopupUI
{
    public sealed partial class PopupService
    {
        private readonly PopupSnapshotStore snapshots = new PopupSnapshotStore();
        private PopupExitTicket pendingExit;
        private bool restoring;
        private bool capturing;
        public PopupExitTicket BeginSceneExit(bool preserve)
        {
            if (applying || pendingExit != null) throw new InvalidOperationException("이동 준비 또는 내용 적용이 진행 중입니다.");
            if (host == null || !host.isActiveAndEnabled || string.IsNullOrEmpty(host.Context.SceneKey))
                throw new InvalidOperationException("유효한 씬 문맥의 활성 표시 영역이 필요합니다.");
            List<PopupSnapshotStore.Item> captured = new List<PopupSnapshotStore.Item>();
            PopupHost source = host;
            PopupContext context = source.Context;
            applying = true; capturing = true;
            try
            {
                if (preserve)
                {
                    foreach (Instance instance in instances)
                    {
                        if (!instance.Entry.Restorable) continue;
                        GameObject selected = instance.Selection;
                        GameObject current = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
                        if (current != null && current.transform.IsChildOf(instance.View.transform)) selected = current;
                        captured.Add(new PopupSnapshotStore.Item {
                            Id = instance.Entry.Id, OriginalHandle = instance.Handle.Value,
                            State = instance.View.CaptureState()?.Copy(), FocusKey = instance.View.CaptureFocusKey(selected)
                        });
                        if (host != source || source == null || !source.isActiveAndEnabled || !source.Context.Equals(context))
                            throw new InvalidOperationException("캡처 중 표시 영역이 종료되었습니다.");
                    }
                }
                pendingExit = new PopupExitTicket(this, source, context, preserve, captured.ToArray());
                return pendingExit;
            }
            finally
            {
                applying = false; capturing = false;
                // 캡처 콜백의 Host 종료는 열거를 중단한 뒤 표시와 연결을 정리한다.
                if (host != source || source == null || !source.isActiveAndEnabled) CloseAll();
            }
        }
        public void CommitSceneExit(PopupExitTicket ticket)
        {
            ValidateExit(ticket);
            PopupHost previous = ticket.Host;
            if (ticket.Preserve) snapshots.Set(ticket.Context, ticket.Items);
            else snapshots.RemoveScene(ticket.Context.SceneKey);
            ticket.Consume(); pendingExit = null;
            previous.Detach();
        }
        public void RollbackSceneExit(PopupExitTicket ticket)
        { ValidateExit(ticket); ticket.Consume(); pendingExit = null; }
        private void ValidateExit(PopupExitTicket ticket)
        {
            if (applying || ticket == null || ticket.Owner != this || ticket != pendingExit || ticket.Consumed || host != ticket.Host || host == null || !host.isActiveAndEnabled)
                throw new InvalidOperationException("유효한 현재 이동 티켓이 아닙니다.");
        }
        public PopupRestoreResult Restore(PopupHost target, PopupContext context)
        {
            if (applying || pendingExit != null || target == null || host != target || !target.isActiveAndEnabled ||
                !target.Context.Equals(context) || Count != 0 || string.IsNullOrEmpty(context.SceneKey))
                return new PopupRestoreResult { Status = PopupRestoreStatus.Failed, Error = "같은 문맥의 준비된 빈 표시 영역이 필요합니다." };
            if (!snapshots.TryGet(context.SceneKey, out PopupSnapshotStore.Snapshot snapshot))
                return new PopupRestoreResult { Status = PopupRestoreStatus.None };
            if (!snapshot.Context.Equals(context))
            {
                snapshots.RemoveScene(context.SceneKey);
                return new PopupRestoreResult { Status = PopupRestoreStatus.ContextMismatch };
            }
            List<Instance> prepared = new List<Instance>();
            GameObject staging = new GameObject("Popup-restore-staging", typeof(RectTransform));
            staging.SetActive(false); staging.transform.SetParent(target.Surface, false);
            applying = true; restoring = true;
            try
            {
                foreach (PopupSnapshotStore.Item item in snapshot.Items)
                {
                    if (!definitions.TryGetValue(item.Id, out PopupCatalog.Entry entry))
                        throw new InvalidOperationException("복원 종류가 등록되지 않았습니다: " + item.Id);
                    PopupView view = UnityEngine.Object.Instantiate(entry.Prefab, staging.transform, false);
                    Instance instance = new Instance { Handle = new PopupHandle(++nextHandle), Entry = entry, View = view };
                    prepared.Add(instance); view.gameObject.SetActive(false);
                    RectTransform rect = view.transform as RectTransform;
                    if (rect == null) throw new InvalidOperationException("복원 프리팹은 RectTransform이 필요합니다.");
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                    CanvasGroup group = view.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
                    view.ApplyState(item.State?.Copy()); view.Bind(this, instance.Handle);
                    instance.RestorePrepared = true; view.PrepareRestore(context);
                    instance.Selection = view.ResolveFocusKey(item.FocusKey);
                    if (host != target || target == null || !target.isActiveAndEnabled || !target.Context.Equals(context))
                        throw new InvalidOperationException("복원 중 표시 영역이 종료되었습니다.");
                }
                // 모든 값/현재 기능 연결이 준비된 뒤 목록을 한 번에 교체한다.
                foreach (Instance instance in prepared) instance.View.transform.SetParent(target.Surface, false);
                instances.AddRange(prepared);
                foreach (Instance instance in prepared)
                {
                    instance.View.gameObject.SetActive(true);
                    if (host != target || target == null || !target.isActiveAndEnabled || !target.Context.Equals(context))
                        throw new InvalidOperationException("복원 활성화 중 표시 영역이 종료되었습니다.");
                }
                snapshots.Discard(context);
            }
            catch (Exception error)
            {
                instances.Clear();
                foreach (Instance instance in prepared)
                    ReleaseView(instance);
                return new PopupRestoreResult { Status = PopupRestoreStatus.Failed, Error = error.Message };
            }
            finally { applying = false; restoring = false; DestroyObject(staging); }
            PopupHandle[] handles = new PopupHandle[prepared.Count];
            for (int index = 0; index < prepared.Count; index++) handles[index] = prepared[index].Handle;
            Notify(); return new PopupRestoreResult { Status = PopupRestoreStatus.Restored, Handles = Array.AsReadOnly(handles) };
        }
        public void Discard(PopupContext context)
        {
            if (applying || pendingExit != null) throw new InvalidOperationException("이동 준비 또는 내용 적용이 진행 중입니다.");
            snapshots.Discard(context);
        }
    }
}
