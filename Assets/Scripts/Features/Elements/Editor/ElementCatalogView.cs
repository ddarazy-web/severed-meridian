using System;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Elements.Editor
{
    /// <summary>카탈로그 상태를 표시하고 선택 명령만 전달하는 화면.</summary>
    public sealed class ElementCatalogView : VisualElement, IDisposable
    {
        private readonly ElementCatalogViewModel model;
        private readonly VisualElement entries = new VisualElement();
        private readonly Label selection = new Label();
        private readonly Button place;
        public ElementCatalogView(ElementCatalogViewModel model, Action<ElementDefinition> selected)
        {
            this.model = model;
            name = "element-catalog-view";
            ToolbarSearchField search = new ToolbarSearchField { name = "element-catalog-search" };
            search.SetValueWithoutNotify(model.Search);
            search.RegisterValueChangedCallback(evt => model.SetSearch(evt.newValue));
            Add(search); Add(entries); Add(selection);
            place = new Button(() => { if (model.CanPlace) selected(model.SelectedDefinition); })
                { text = "선택 정의 배치", name = "element-catalog-place" };
            Add(place); model.Changed += Render; Render();
        }
        private void Render()
        {
            entries.Clear();
            foreach (ElementDefinition definition in model.Visible)
            {
                Button entry = new Button(() => model.Select(definition.Id))
                    { text = definition.DisplayName, tooltip = definition.Id.Value, name = "definition-" + definition.Id.Value };
                entries.Add(entry);
            }
            selection.text = model.SelectedDefinition == null ? "정의를 선택하세요." : model.SelectedDefinition.DisplayName + "\n" + model.SelectedDefinition.Id.Value +
                (model.SelectionError == null ? "" : "\n" + model.SelectionError);
            place.SetEnabled(model.CanPlace);
        }
        public void Dispose() { model.Changed -= Render; }
    }
}
