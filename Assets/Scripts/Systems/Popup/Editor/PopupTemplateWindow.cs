using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace PopupUI.Editor
{
    public sealed class PopupTemplateWindow : EditorWindow
    {
        private IVisualElementScheduledItem polling;
        private string observedDetails;
        [MenuItem("Tools/Popup/템플릿 생성")]
        public static void OpenWindow() { GetWindow<PopupTemplateWindow>("팝업 템플릿").Show(); }
        public void CreateGUI()
        {
            polling?.Pause(); rootVisualElement.Clear();
            TextField feature = new TextField("기능 이름") { name = "popup-template-feature", value = "MyFeature" };
            TextField name = new TextField("팝업 이름") { name = "popup-template-name", value = "Example" };
            TextField id = new TextField("등록 ID") { name = "popup-template-id", value = "ui.example" };
            observedDetails = PopupTemplateGenerator.Details;
            Label status = new Label(observedDetails); status.style.whiteSpace = WhiteSpace.Normal;
            Button generate = new Button(() => {
                try { PopupTemplateGenerator.Request(feature.value, name.value, id.value); observedDetails = PopupTemplateGenerator.Details; status.text = observedDetails; }
                catch (Exception error) { status.text = error.Message; }
            }) { name = "popup-template-generate", text = "State/View 생성 · 컴파일 후 프리팹 생성" };
            rootVisualElement.Add(feature); rootVisualElement.Add(name); rootVisualElement.Add(id); rootVisualElement.Add(generate); rootVisualElement.Add(status);
            polling = rootVisualElement.schedule.Execute(() => {
                generate.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && PopupTemplateGenerator.Status != "Pending");
                // 새 요청의 실제 진행이 바뀔 때만 갱신해 이전 요청이 검증 오류를 덮지 않는다.
                string details = PopupTemplateGenerator.Details;
                if (details != observedDetails) { observedDetails = details; status.text = details; }
            }).Every(250);
        }
        private void OnDisable() { polling?.Pause(); }
    }
}
