using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    internal static class LevelContextMenuVerification
    {
        private sealed class SilentMenuManager : ContextualMenuManager
        {
            public override void DisplayMenuIfEventMatches(EventBase evt, IEventHandler target) { }
            protected override void DoDisplayMenu(DropdownMenu menu, EventBase triggerEvent) { }
        }

        internal static DropdownMenuAction Action(LevelEditorWindow window, string name, VisualElement target = null, Vector2? position = null)
        {
            target ??= window.rootVisualElement.Q("selected-cell-properties");
            DropdownMenu menu = new DropdownMenu();
            Event input = new Event { type = EventType.MouseUp, button = 1, mousePosition = position ?? target.worldBound.center };
            using MouseUpEvent trigger = MouseUpEvent.GetPooled(input);
            using ContextualMenuPopulateEvent evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, target, new SilentMenuManager());
            // 실제 메뉴 구성 이벤트를 사용하되 자동 검증 중 네이티브 팝업은 열지 않는다.

            target.SendEvent(evt);
            menu.PrepareForDisplay(trigger);
            DropdownMenuAction action = menu.MenuItems().OfType<DropdownMenuAction>().FirstOrDefault(item => item.name == name);
            if (action == null || action.status != DropdownMenuAction.Status.Normal) throw new InvalidOperationException("컨텍스트 메뉴 사용 불가: " + name);
            return action;
        }
    }
}

