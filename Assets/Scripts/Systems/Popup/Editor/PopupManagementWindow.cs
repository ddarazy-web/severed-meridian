using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PopupUI.Editor
{
    public sealed class PopupManagementWindow : EditorWindow
    {
        private PopupHost target;
        private PopupService observed;
        private PopupCatalog catalog;
        private VisualElement catalogEditor, liveList;
        private Label message, summary;
        private TextField trialId;
        private Button save, trialOpen;
        private IVisualElementScheduledItem polling;
        private bool shownPending;
        private int shownStored;

        [MenuItem("Tools/Popup/관리")]
        public static void OpenWindow() { GetWindow<PopupManagementWindow>("팝업 관리").Show(); }

        public void CreateGUI()
        {
            polling?.Pause(); rootVisualElement.Clear();
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; rootVisualElement.Add(scroll);
            var catalogField = new ObjectField("등록 카탈로그") { name = "popup-catalog", objectType = typeof(PopupCatalog), allowSceneObjects = false, value = catalog };
            catalogField.RegisterValueChangedCallback(evt => { catalog = evt.newValue as PopupCatalog; DrawCatalog(); }); scroll.Add(catalogField);
            catalogEditor = new VisualElement(); scroll.Add(catalogEditor);
            save = new Button(SaveCatalog) { name = "popup-save", text = "검사 후 카탈로그 저장" }; scroll.Add(save);
            var hostField = new ObjectField("실행 Host") { name = "popup-host", objectType = typeof(PopupHost), allowSceneObjects = true, value = target };
            hostField.RegisterValueChangedCallback(evt => SelectHost(evt.newValue as PopupHost)); scroll.Add(hostField);
            summary = new Label { name = "popup-summary" }; scroll.Add(summary);
            trialId = new TextField("시험 ID") { name = "popup-trial-id" }; scroll.Add(trialId);
            trialOpen = new Button(() => TryOpen(trialId.value)) { name = "popup-trial-open", text = "시험 열기 (초기 상태 없음)" }; scroll.Add(trialOpen);
            liveList = new VisualElement(); scroll.Add(liveList);
            message = new Label { name = "popup-message" }; message.style.whiteSpace = WhiteSpace.Normal; scroll.Add(message);
            DrawCatalog(); RefreshTarget();
            polling = rootVisualElement.schedule.Execute(PollTarget).Every(250);
        }
        private void OnEnable()
        { Undo.undoRedoPerformed += DrawCatalog; EditorApplication.playModeStateChanged += OnPlayModeChanged; }
        private void OnPlayModeChanged(PlayModeStateChange value) { DrawCatalog(); RefreshTarget(); }
        private void DrawCatalog()
        {
            if (catalogEditor == null) return;
            catalogEditor.Unbind(); catalogEditor.Clear();
            if (catalog != null)
            {
                var data = new SerializedObject(catalog);
                catalogEditor.Add(new PropertyField(data.FindProperty("entries"), "등록 목록"));
                // SerializedProperty 바인딩이 Undo를 기록하며 저장은 아래 버튼에서만 한다.
                catalogEditor.Bind(data);
            }
            catalogEditor.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            save?.SetEnabled(catalog != null && !EditorApplication.isPlayingOrWillChangePlaymode);
        }
        public bool SaveSelectedCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Report("Play Mode에서는 카탈로그를 저장하지 않습니다."); return false; }
            string[] errors = PopupCatalogValidation.Validate(catalog);
            if (errors.Length != 0) { Report(string.Join("\n", errors)); return false; }
            if (!AssetDatabase.Contains(catalog)) { Report("저장할 카탈로그 에셋을 선택하세요."); return false; }
            AssetDatabase.SaveAssetIfDirty(catalog); Report("카탈로그 저장 완료"); return true;
        }
        private void SaveCatalog() { SaveSelectedCatalog(); }
        public void SelectHost(PopupHost host)
        {
            if (observed != null) observed.Changed -= RefreshTarget;
            target = host; observed = host == null ? null : host.Service;
            if (observed != null) observed.Changed += RefreshTarget;
            RefreshTarget();
        }
        public PopupInspection ReadTarget() => target == null || target.Service == null ? null : target.Service.Inspect();
        private void PollTarget()
        {
            PopupService current = target == null ? null : target.Service;
            if (current != observed) SelectHost(target);
            else if (current != null)
            {
                // 이동 ticket 준비/취소는 목록 Changed 없이 바뀔 수 있다.
                PopupInspection info = current.Inspect();
                if (info.HasPendingExit != shownPending || info.Stored.Count != shownStored) RefreshTarget();
            }
        }
        public bool TryOpen(string id)
        {
            if (!CanTrial()) { Report("활성 Play Mode Host에서 이동 준비가 아닐 때만 시험할 수 있습니다."); return false; }
            try { target.Service.Open(id, null); Report("시험 열기 완료"); return true; }
            catch (Exception error) { Report("시험 열기 실패 (필수 초기 상태가 있는 종류는 게임에서 시험하세요): " + error.Message); return false; }
        }
        public bool TryClose(PopupHandle handle)
        {
            if (!CanTrial()) { Report("현재 Host를 시험할 수 없습니다."); return false; }
            try { bool closed = target.Service.Close(handle); Report(closed ? "시험 닫기 완료" : "이미 닫힌 핸들입니다."); return closed; }
            catch (Exception error) { Report(error.Message); return false; }
        }
        private bool CanTrial() => EditorApplication.isPlaying && target != null && target.isActiveAndEnabled && target.Service != null && !target.Service.Inspect().HasPendingExit;
        private void RefreshTarget()
        {
            if (liveList == null) return;
            liveList.Clear(); PopupInspection info = ReadTarget();
            shownPending = info != null && info.HasPendingExit; shownStored = info == null ? 0 : info.Stored.Count;
            summary.text = info == null ? "연결된 서비스 없음" : "Count " + info.Count + " / Top " + info.Top?.Value + " / 입력 차단 " + (info.Count > 0) + " / 이동 준비 " + info.HasPendingExit;
            trialOpen.SetEnabled(CanTrial());
            if (info == null) return;
            bool pause = false;
            foreach (PopupInspectionItem item in info.Items)
            {
                pause |= item.PauseGameplay;
                liveList.Add(new Label(item.Id + " #" + item.Handle.Value + (item.IsTop ? " [Top]" : "") + " / 정지 " + item.PauseGameplay + " / 취소 " + item.CloseOnCancel + " / 복원 " + item.Restorable + " / 복수 " + item.AllowMultiple));
                PopupHandle handle = item.Handle;
                var close = new Button(() => TryClose(handle)) { text = "이 핸들 닫기" }; close.SetEnabled(CanTrial()); liveList.Add(close);
            }
            liveList.Add(new Label("팝업 정지 요청 " + pause));
            foreach (PopupStoredInfo stored in info.Stored)
                liveList.Add(new Label("보관: " + stored.Context.SceneKey + " / " + stored.Context.FeatureKey + " / " + stored.Context.SessionKey + " — " + stored.Count));
        }
        private void Report(string text) { if (message != null) message.text = text; }
        private void OnDisable()
        {
            polling?.Pause(); catalogEditor?.Unbind();
            Undo.undoRedoPerformed -= DrawCatalog; EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (observed != null) observed.Changed -= RefreshTarget; observed = null; target = null;
        }
    }
}
