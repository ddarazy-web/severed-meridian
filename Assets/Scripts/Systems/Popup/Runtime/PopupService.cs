using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopupUI
{
    public sealed partial class PopupService
    {
        internal sealed class Instance
        {
            internal PopupHandle Handle;
            internal PopupCatalog.Entry Entry;
            internal PopupView View;
            internal GameObject Selection;
            internal bool RestorePrepared;
        }
        private static long nextHandle;
        private readonly Dictionary<string, PopupCatalog.Entry> definitions = new Dictionary<string, PopupCatalog.Entry>(StringComparer.Ordinal);
        private readonly List<Instance> instances = new List<Instance>();
        private PopupHost host;
        private bool applying;
        internal IReadOnlyList<Instance> Instances => instances;
        public PopupService(PopupCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            foreach (PopupCatalog.Entry entry in catalog.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Prefab == null)
                    throw new InvalidOperationException("종류 ID와 팝업 프리팹이 필요합니다.");
                if (!definitions.TryAdd(entry.Id, entry)) throw new InvalidOperationException("중복 팝업 ID: " + entry.Id);
            }
        }
        public int Count => instances.Count;
        public PopupHandle? Top => Count == 0 ? null : instances[Count - 1].Handle;
        public event Action Changed;
        internal void Attach(PopupHost value)
        {
            if (host != null && host != value) throw new InvalidOperationException("한 서비스에는 표시 영역 하나만 연결하세요.");
            host = value;
        }
        internal void Detach(PopupHost value)
        {
            if (host != value) return;
            if (pendingExit != null) { pendingExit.Consume(); pendingExit = null; }
            host = null;
            // 복원 후보와 캡처 중인 목록은 각 트랜잭션의 종료 경로가 정리한다.
            if (!restoring && !capturing) CloseAll();
        }
        public PopupView GetView(PopupHandle handle)
        { foreach (Instance instance in instances) if (instance.Handle == handle) return instance.View; return null; }

        public PopupHandle Open(string id, PopupState state)
        {
            if (applying || pendingExit != null) throw new InvalidOperationException("내용 적용 또는 이동 준비 중에는 팝업 목록을 변경할 수 없습니다.");
            if (host == null || !host.isActiveAndEnabled) throw new InvalidOperationException("활성 팝업 표시 영역을 먼저 연결하세요.");
            if (id == null || !definitions.TryGetValue(id, out PopupCatalog.Entry entry))
                throw new InvalidOperationException("등록되지 않은 팝업: " + id);
            if (!entry.AllowMultiple)
            {
                foreach (Instance existing in instances)
                {
                    if (existing.Entry.Id != id) continue;
                    if (state != null)
                    {
                        PopupState previous = existing.View.CaptureState()?.Copy();
                        applying = true;
                        try { existing.View.ApplyState(state.Copy()); }
                        catch { existing.View.ApplyState(previous); throw; }
                        finally { applying = false; }
                    }
                    instances.Remove(existing); instances.Add(existing);
                    existing.View.transform.SetAsLastSibling(); Notify();
                    return existing.Handle;
                }
            }
            PopupView candidate = null;
            applying = true;
            try
            {
                // 비활성 임시 부모에서 생성해 상태 적용 전 OnEnable 부작용을 막는다.
                GameObject staging = new GameObject("Popup-staging", typeof(RectTransform));
                staging.SetActive(false);
                try { candidate = UnityEngine.Object.Instantiate(entry.Prefab, staging.transform, false); }
                finally
                {
                    if (candidate != null) { candidate.gameObject.SetActive(false); candidate.transform.SetParent(host.Surface, false); }
                    DestroyObject(staging);
                }
                candidate.gameObject.SetActive(false);
                RectTransform rect = candidate.transform as RectTransform;
                if (rect == null) throw new InvalidOperationException("팝업 프리팹은 RectTransform이 필요합니다.");
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                candidate.ApplyState(state?.Copy());
                PopupHandle handle = new PopupHandle(++nextHandle);
                candidate.Bind(this, handle);
                instances.Add(new Instance { Handle = handle, Entry = entry, View = candidate });
                candidate.gameObject.SetActive(true);
                return handle;
            }
            catch { if (candidate != null) DestroyObject(candidate.gameObject); throw; }
            finally { applying = false; Notify(); }
        }
        public bool Close(PopupHandle handle)
        {
            if (applying || pendingExit != null) throw new InvalidOperationException("내용 적용 또는 이동 준비 중에는 팝업 목록을 변경할 수 없습니다.");
            for (int index = 0; index < instances.Count; index++)
            {
                if (instances[index].Handle != handle) continue;
                Instance closed = instances[index];
                instances.RemoveAt(index);
                ReleaseView(closed);
                Notify(); return true;
            }
            return false;
        }
        public void CloseAll()
        {
            if (applying || pendingExit != null) throw new InvalidOperationException("내용 적용 또는 이동 준비 중에는 팝업 목록을 변경할 수 없습니다.");
            Instance[] closed = instances.ToArray(); instances.Clear();
            foreach (Instance instance in closed)
                ReleaseView(instance);
            Notify();
        }
        private void Notify() { if (host != null) host.Refresh(); Changed?.Invoke(); }
        private static void ReleaseView(Instance instance)
        {
            if (instance.View == null) return;
            try { if (instance.RestorePrepared) instance.View.ReleaseRestore(); }
            finally { instance.View.gameObject.SetActive(false); DestroyObject(instance.View.gameObject); }
        }
        private static void DestroyObject(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
