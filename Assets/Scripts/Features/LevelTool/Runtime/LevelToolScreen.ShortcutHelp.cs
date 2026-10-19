#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private void OpenShortcutHelp()
        {
            Dictionary<ToolCommandId, string> unavailable = new Dictionary<ToolCommandId, string>();
            foreach (ToolCommand command in ToolCommands)
                if (!CanExecuteCommand(command.Id, out string reason)) unavailable[command.Id] = reason;
            VisualElement panel = OpenModal("단축키 안내");
            panel.ElementAt(0).style.flexShrink = 0;
            panel.ElementAt(0).style.marginBottom = 8;
            TextField search = new TextField("검색") { name = "shortcut-search", tooltip = "명령 이름, 키, 적용 범위 또는 설명을 입력하세요." };
            panel.Add(search);
            ScrollView entries = new ScrollView { name = "shortcut-entries" }; entries.style.flexGrow = 1; panel.Add(entries);
            void Draw(string query)
            {
                entries.Clear();
                foreach (ToolCommand command in ToolCommands)
                {
                    if (UsesInternalProjects && (command.Id == ToolCommandId.Windowed || command.Id == ToolCommandId.Fullscreen)) continue;
                    string text = command.Label + "  " + command.Gesture + "\n적용: " + command.Scope + "\n" + command.Description;
                    if (unavailable.TryGetValue(command.Id, out string reason)) text += "\n현재 사용 불가: " + reason;
                    if (text.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Label item = new Label(text); item.AddToClassList("shortcut-entry"); entries.Add(item);
                }
                string[] notes =
                {
                    "Ctrl+C / Ctrl+X / Ctrl+V: 보드에서는 현재 층의 요소를 복사·잘라내기·붙여넣기합니다. 글 입력 중에는 필드의 기본 편집이 우선합니다. 보드 클립보드는 같은 작업 세션 안에서만 유지됩니다.",
                    "Delete · V / B / E · Space: 글 입력 중에는 기본 필드 동작을 유지합니다. 보드 선택·배치·삭제는 화면의 도구와 버튼을 사용하세요.",
                    "Tab / Shift+Tab: 다음 / 이전 컨트롤로 포커스를 옮깁니다. Esc는 가장 위 창이나 진행 중 조작 하나만 닫습니다.",
                    "Ctrl + 마우스 휠: 보드 위에서 커서 위치를 기준으로 확대·축소합니다. Ctrl 없이 휠을 돌리면 스크롤합니다. 화면 맞춤으로 보드 전체를 다시 볼 수 있습니다.",
                    "터치: 짧게 누르고 떼면 선택·배치, 0.6초 길게 누르면 작업 메뉴입니다. 두 손가락은 확대·이동만 하며 남은 손가락으로 배치하지 않습니다. 영역은 시작 칸과 끝 칸을 차례로 선택합니다. 추가 선택·교체 옵션과 +/−·이동·작업·취소 버튼도 사용할 수 있습니다.",
                    "Windows 레벨툴: Alt+Enter, F11 또는 보기 메뉴의 ‘창 모드 전환’으로 전체 화면과 16:10 창을 전환합니다. 시험 중이나 팝업에서도 사용할 수 있습니다.",
                    "전체 화면은 모니터를 여백 없이 가득 채웁니다. 창으로 돌아오면 마지막으로 저장한 크기와 위치를 복원하고, 크기 정보가 없으면 1280×800으로 엽니다. 창 크기는 테두리로 조절하며 최소 1280×800과 16:10 비율을 유지합니다. 마지막 창 모드도 재실행 시 복원됩니다. 최소 창을 담을 수 없는 모니터는 전체 화면으로 전환됩니다.",
                    "최대화 모드는 제공하지 않습니다. Shift+Space는 앱 안의 보드 영역만 넓힙니다. Unity 에디터에서는 실행 파일 전용 창 기능이 비활성화됩니다.",
                    "Unity Editor: 레벨툴 Game View에 포커스가 있을 때 사용합니다. Editor가 먼저 처리하는 키는 앱에서 받을 수 없으므로 상단 메뉴를 사용하세요."
                };
                foreach (string note in notes)
                {
                    if (UsesInternalProjects && (note.StartsWith("Windows") || note.StartsWith("전체 화면") || note.StartsWith("최대화") || note.StartsWith("Unity Editor"))) continue;
                    if (note.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                    { Label item = new Label(note); item.AddToClassList("shortcut-entry"); entries.Add(item); }
                }
                if (UsesInternalProjects)
                {
                    entries.Add(new Label("Android: 레벨·소재·속성·더보기 버튼으로 패널을 열고 닫습니다. 더보기에서 파일·보기·도움말과 작업 화면을 선택합니다. 상단 저장·취소·다시는 저장·실행 취소·다시 실행이고, 시험은 게임 시험입니다."));
                    entries.Add(new Label("파일 메뉴에서 내부 프로젝트를 만들고 열 수 있습니다. 창 전환은 제공하지 않으며, 단축키는 외부 키보드에서 적용됩니다. 롱터치·핀치·보드 복사/붙여넣기는 후속 단계입니다."));
                }
                if (entries.childCount == 0) entries.Add(new Label("일치하는 안내가 없습니다."));
            }
            search.RegisterValueChangedCallback(evt => Draw(evt.newValue)); Draw("");
            Button(panel, "close-help", "닫기 (Esc)", CloseModal);
            search.schedule.Execute(search.Focus);
        }
    }
}
#endif
