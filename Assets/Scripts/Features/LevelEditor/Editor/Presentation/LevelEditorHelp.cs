using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>
    /// 편집기 설명과 문서 위치를 한곳에서 연결한다. 게임 실행 코드에는 의존하지 않는다.
    /// 입력 필드 전체의 클릭을 가로채지 않고 제목 옆 ! 아이콘에만 문서 링크를 붙인다.
    /// 제목 자체의 기존 선택·접기 동작은 유지한다.
    /// </summary>
    internal static class LevelEditorHelp
    {
        /// <summary>설명서의 지정 페이지와 문단을 기본 브라우저로 연다.</summary>
        /// <param name="route">코드에 지정한 상대 HTML 파일과 선택적 #문단 이름.</param>
        internal static void Open(string route)
        {
            string[] parts = route.Split('#');
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/MoonRabbitJunkyard/Manual/", parts[0]));
            if (File.Exists(path)) Application.OpenURL(new Uri(path).AbsoluteUri + (parts.Length > 1 ? "#" + Uri.EscapeDataString(parts[1]) : ""));
            else EditorUtility.DisplayDialog("사용 설명서", "설명서 파일이 없습니다. Docs/MoonRabbitJunkyard/Manual 폴더를 확인하세요.", "확인");
        }

        /// <summary>제목 오른쪽에 툴팁과 문서 링크를 가진 도움말 버튼을 붙인다.</summary>
        /// <param name="title">아이콘을 표시할 제목. 제목 자체의 클릭 동작은 바꾸지 않는다.</param>
        /// <param name="description">쉬운 말로 쓴 기능 설명.</param>
        /// <param name="route">열 문서의 상대 위치.</param>
        internal static void Link(Label title, string description, string route)
        {
            if (title == null || title.ClassListContains("manual-help-host")) return;
            title.AddToClassList("manual-help-host");
            Button help = new Button(() => Open(route)) { text = "!", tooltip = description + "\n클릭: 해당 사용 설명서 열기", name = "manual-help-" + route.Replace('#', '-') };
            help.AddToClassList("manual-help-icon");
            // Label은 자식을 추가하면 본문 글자가 사라질 수 있으므로 버튼을 형제로 둔다.
            // 이벤트가 부모 Toggle까지 올라가 접기 상태를 바꾸지 않도록 아이콘에서 끝낸다.
            help.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            help.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            help.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            if (title.parent != null) Attach();
            else title.RegisterCallback<AttachToPanelEvent>(_ => title.schedule.Execute(Attach));

            void Attach()
            {
                if (help.parent != null || title.parent == null) return;
                VisualElement parent = title.parent;
                int index = parent.IndexOf(title);
                if (title.ClassListContains("unity-base-field__label") || title.ClassListContains("unity-toggle__text"))
                {
                    parent.AddToClassList("manual-help-field");
                    parent.Insert(index + 1, help);
                    return;
                }
                // 독립 제목은 한 줄 컨테이너로 감싸 제목 바로 옆에 아이콘을 둔다.
                // 입력 필드의 기존 라벨 폭·드롭다운 구조에는 손대지 않는다.
                VisualElement row = new VisualElement(); row.AddToClassList("manual-help-row");
                row.style.flexGrow = title.style.flexGrow;
                if (title.ClassListContains("editor-title")) row.style.flexGrow = 1;
                title.style.flexGrow = 0;
                title.RemoveFromHierarchy(); parent.Insert(index, row); row.Add(title); row.Add(help);
            }
        }

        /// <summary>재생성된 메뉴에 도움말을 붙인다. 반복 호출해도 콜백을 중복 등록하지 않는다.</summary>
        /// <param name="root">검사할 작업창 또는 다시 만든 속성 영역.</param>
        internal static void Apply(VisualElement root)
        {
            if (root == null) return;
            // 이름은 화면 문구가 아니라 UI의 고유 name이다. 번역이나 숫자 표시 변경과 분리한다.
            (string Name, string Text, string Route)[] entries =
            {
                ("menu-board", "블록을 놓을 수 있는 칸을 켜거나 끕니다. 이미 놓은 데이터는 자동 삭제하지 않습니다.", "board.html#shape"),
                ("menu-normal", "처음 놓일 달토끼를 무작위 또는 지정한 색으로 설정합니다.", "board.html#normal"),
                ("menu-power", "시작부터 놓여 있을 청소로켓·달폭탄·수거드론·무지개 자석을 고릅니다.", "board.html#power"),
                ("menu-obstacle", "블록 공간을 차지하는 장애물을 고릅니다. 큰 장애물은 2×2 공간이 필요합니다.", "obstacles.html#types"),
                ("menu-cover", "블록 위에 거미줄이나 우주 곰팡이를 덮습니다.", "obstacles.html#layers"),
                ("menu-floor", "칸 바닥에 먼지를 배치합니다. 블록과 별도 층입니다.", "obstacles.html#layers"),
                ("menu-device", "발전기를 배치합니다. 배치 후 보드의 연결점을 끌어 대상 장치와 연결하세요.", "flow.html#generator"),
                ("menu-supply", "블록이 나오는 생성구와 회수할 부품을 배치합니다.", "supply.html#source"),
                ("menu-flow", "구역의 중력 방향, 직접 낙하 경로, 이동 통로와 도착 바닥을 정합니다.", "flow.html#path"),
                ("menu-terrain", "칸과 칸 사이에 이동을 막는 고철 벽을 배치합니다.", "flow.html#terrain"),
                ("placement-layer", "선택하거나 지울 대상을 블록·장애물·덮개·바닥 층으로 구분합니다.", "obstacles.html#layers"),
                ("flow-heading", "중력·이동 경로·발전기 연결을 편집합니다.", "flow.html#gravity"),
                ("rename-level", "데이터 파일 이름을 바꿉니다. 현재 수정한 레벨 내용도 함께 저장합니다.", "files.html#rename"),
                ("workspace-level", "편집과 테스트에 공통으로 사용할 레벨 파일을 고릅니다.", "files.html#open-save"),
                ("save-level", "현재 레벨 파일의 변경 내용을 저장합니다. 레벨 편집 탭에서 Ctrl+S (Mac: Cmd+S)로도 저장합니다. 제목의 *는 저장하지 않은 변경이 있다는 뜻입니다.", "files.html#open-save"),
                ("validate-level", "배치와 연결, 공급, 미션의 데이터 오류를 찾습니다. 클리어 가능성은 판정하지 않습니다.", "play.html#check"),
                ("tool-tab-0", "블록·장애물·덮개·바닥을 배치합니다.", "board.html#tools"),
                ("tool-tab-1", "중력·경로·통로·장치 연결 도구를 표시합니다.", "flow.html#gravity"),
                ("tool-tab-2", "현재 배치된 장애물과 생성구를 찾아 선택합니다.", "obstacles.html#used"),
                ("inspector-tab-0", "보드에서 선택한 대상의 설정을 편집합니다.", "board.html#select"),
                ("inspector-tab-1", "레벨 번호·이동 횟수·색 종류·미션을 설정합니다.", "missions.html#basic"),
                ("mission-settings", "레벨의 목표를 1~4종류 지정합니다. 종류에 맞는 대상과 수량을 설정하세요.", "missions.html#goals"),
                ("supply-maintenance", "고철과 회수 부품을 얼마나 유지·추가할지 지정합니다.", "supply.html#modes"),
                ("flow-connections", "배치된 중력·경로·통로·연결을 찾아 해당 위치로 이동합니다.", "flow.html#path"),
                ("flow-properties", "선택한 칸의 중력·통로·전선·합류 우선순위를 확인합니다.", "flow.html#merge"),
                ("used-sources", "배치된 생성구를 모아 보여줍니다. 위치를 눌러 공급 설정을 편집하세요.", "supply.html#source"),
                ("source-mode", "무작위 공급, 고정 순서, 고철 수량 유지, 회수 부품 유지 중 하나를 고릅니다.", "supply.html#modes"),
                ("source-exhaustion", "고정 목록을 모두 사용한 뒤 공급을 멈출지 무작위 블록으로 전환할지 정합니다.", "supply.html#order"),
                ("supply-item-kind", "선택한 공급 순서에서 생성할 블록 종류를 정합니다.", "supply.html#order"),
                ("supply-item-count", "이 항목을 몇 개 연속 공급할지 정합니다.", "supply.html#order"),
                ("supply-item-durability", "생성할 고철의 내구도를 1~5로 정합니다.", "supply.html#order"),
                ("supply-item-direction", "생성할 청소로켓의 제거 방향입니다.", "board.html#power"),
                ("supply-item-color", "생성할 고정 달토끼의 색을 정합니다.", "supply.html#order"),
                ("supply-scrapTarget", "고철 유지 생성구가 보드에 유지할 목표 수량입니다.", "missions.html#maintenance"),
                ("supply-scrapLimit", "고철 유지 생성구가 함께 사용하는 추가 생성 한도입니다.", "missions.html#maintenance"),
                ("supply-scrapDurability", "유지 방식으로 추가할 고철의 내구도입니다.", "missions.html#maintenance"),
                ("supply-recoveryTarget", "회수 유지 생성구가 보드에 유지할 부품 수량입니다.", "missions.html#maintenance"),
                ("new-level", "파일 이름을 입력해 기본 9×9 레벨을 만듭니다.", "files.html#create"),
                ("duplicate-level", "현재 레벨을 별도의 새 파일로 복제합니다.", "files.html#duplicate"),
                ("play-level", "현재 편집 내용을 반영하고 수동 플레이 탭으로 이동합니다.", "play.html#manual"),
                ("erase-layer", "현재 편집 층의 내용만 지웁니다. 다른 층은 그대로 둡니다.", "board.html#tools"),
                ("tool-Select", "배치를 멈추고 칸이나 장애물을 선택해 설정을 봅니다.", "board.html#select"),
                ("manual-start", "현재 레벨로 직접 플레이를 시작합니다.", "play.html#manual"),
                ("bot-trial", "공개 정보만 읽는 기본/계획 봇을 시험합니다. 계획은 가정 보드의 두 수를 비교합니다. 같은 조건 재시험으로 전략을 바꿔 확인할 수 있습니다.", "play.html#bot"),
                ("batch-count", "기본·계획 봇을 각각 지정한 횟수만큼 시험합니다. 100이면 총 200판입니다. 탭 이동은 일시정지하며 기록은 프로젝트 로컬에 남습니다.", "play.html#batch"),
                ("manual-restart", "같은 시작값으로 다시 플레이합니다.", "play.html#restart"),
                ("manual-new-seed", "다른 무작위 시작값으로 다시 플레이합니다.", "play.html#restart"),
                ("initial-seed", "같은 레벨과 시작값을 사용하면 무작위 결과를 재현할 수 있습니다.", "play.html#diagnostic")
            };
            foreach (var entry in entries)
            {
                VisualElement element = root.Q(entry.Name);
                if (element == null) continue;
                element.tooltip = entry.Text;
                // 접기 제목도 아이콘만 별도로 연결한다. 내부 입력 필드보다 접기 제목을 먼저 찾는다.
                if (element is Button) continue;
                Label title = element is Foldout || element is Toggle
                    ? element.Q<Label>(className: "unity-toggle__text")
                    : element as Label ?? element.Q<Label>(className: "unity-base-field__label");
                Link(title, entry.Text, entry.Route);
            }
            // PropertyField의 제목은 바인딩 후 생기므로 이름 없는 기본 설정 라벨도 처리한다.
            foreach (Label title in root.Query<Label>().ToList())
            {
                switch (title.text)
                {
                    case "레벨 번호": Link(title, "진행 순서에 사용하는 번호입니다. 파일 이름과 별개입니다.", "missions.html#basic"); break;
                    case "이동 횟수": Link(title, "이 레벨을 시작할 때 주어지는 이동 횟수입니다.", "missions.html#basic"); break;
                    case "사용 종류": Link(title, "이 레벨에서 사용할 달토끼 색을 3~5종 선택합니다.", "missions.html#basic"); break;
                    case "목표 수량": Link(title, "클리어를 위해 모으거나 제거해야 하는 수량입니다.", "missions.html#goals"); break;
                    case "합류 우선순위 · 핸들을 드래그 (1번 우선)": Link(title, "여러 경로가 한 칸에 합류할 때 먼저 들어올 경로를 정합니다.", "flow.html#merge"); break;
                }
            }
        }
    }
}
