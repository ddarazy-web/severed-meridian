using UnityEditor;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    internal sealed partial class MultiLevelTestPanel
    {
        /// <summary>조작의 소유 영역을 나눈다. 왼쪽은 새 시험 준비, 오른쪽은 저장 결과 조회이며 세션 로직은 바꾸지 않는다.</summary>
        /// <param name="guide">공통 안내.</param><param name="title">새 시험 제목.</param>
        /// <param name="options">시험 방식과 판 수.</param><param name="controls">기존 실행 조작.</param>
        /// <param name="resultTools">결과 조회 조작.</param><param name="details">선택 결과 스크롤 영역.</param>
        private void ArrangeWorkspace(Label guide, Label title, VisualElement options, VisualElement controls,
            VisualElement resultTools, ScrollView details)
        {
            VisualElement historyTools = Root.Q("multi-history-tools");
            // 도움말 버튼은 안내문과 같은 행에 붙어 있으므로 행 전체를 옮긴다.
            VisualElement guideContent = guide.parent != null && guide.parent != Root ? guide.parent : guide;
            Root.Clear(); Root.AddToClassList("trial-workspace");
            Root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/AutoPlay/Editor/Styles/TrialWorkspace.uss"));
            guide.AddToClassList("trial-intro"); Root.Add(guideContent);
            VisualElement columns = new VisualElement { name = "multi-columns" }; columns.AddToClassList("trial-columns"); Root.Add(columns);
            VisualElement setup = new VisualElement { name = "multi-setup-card" }; setup.AddToClassList("trial-card"); setup.AddToClassList("trial-setup"); columns.Add(setup);
            title.style.marginTop = 0; title.AddToClassList("trial-card-title"); setup.Add(title);

            VisualElement selectionHeader = new VisualElement(); selectionHeader.AddToClassList("trial-section-row");
            Label selectTitle = new Label("1  레벨 선택"); selectTitle.AddToClassList("trial-section-title"); selectionHeader.Add(selectTitle);
            selectionHeader.Add(selectionTools); setup.Add(selectionHeader);
            Label selectHint = new Label("Ctrl / Shift로 여러 레벨 선택"); selectHint.AddToClassList("trial-muted"); setup.Add(selectHint);
            choices.style.width = StyleKeyword.Auto; choices.style.flexGrow = 1; choices.style.minHeight = 140;
            choices.AddToClassList("trial-list"); setup.Add(choices);

            Label settingsTitle = new Label("2  시험 설정"); settingsTitle.AddToClassList("trial-section-title"); settingsTitle.AddToClassList("trial-divider"); setup.Add(settingsTitle);
            options.style.flexDirection = FlexDirection.Column; options.style.flexShrink = 0;
            mode.labelElement.style.width = 82; mode.labelElement.style.minWidth = 82;
            setup.Add(options); estimate.AddToClassList("trial-estimate"); setup.Add(estimate);
            start.AddToClassList("trial-primary"); setup.Add(start);
            controls.AddToClassList("trial-run-controls"); setup.Add(controls);

            VisualElement review = new VisualElement { name = "multi-review-card" }; review.AddToClassList("trial-card"); review.AddToClassList("trial-review"); columns.Add(review);
            Label reviewTitle = new Label("시험 결과 확인"); reviewTitle.AddToClassList("trial-card-title"); review.Add(reviewTitle);
            historyTools.style.borderBottomWidth = 0; historyTools.style.paddingTop = 0;
            review.Add(historyTools); historyNotice.AddToClassList("trial-muted"); review.Add(historyNotice);
            progress.AddToClassList("trial-progress"); review.Add(progress);
            Label resultTitle = new Label("레벨별 결과"); resultTitle.AddToClassList("trial-section-title"); review.Add(resultTitle);
            results.style.flexGrow = 0; results.style.height = 164; results.style.minHeight = 100;
            results.AddToClassList("trial-list"); review.Add(results);
            resultTools.AddToClassList("trial-result-tools"); resultTools.style.flexWrap = Wrap.Wrap; review.Add(resultTools);
            Label detailsTitle = new Label("선택한 레벨의 결과"); detailsTitle.AddToClassList("trial-section-title"); review.Add(detailsTitle);
            details.style.minHeight = 115; details.AddToClassList("trial-details"); review.Add(details);
        }
    }
}
