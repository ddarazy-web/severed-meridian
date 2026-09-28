using Board;
using Levels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    [CustomPropertyDrawer(typeof(BoardCoordinate))]
    public sealed class BoardCoordinateDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            string path = property.propertyPath;
            SerializedObject owner = property.serializedObject;
            foreach (string axis in new[] { "row", "column" })
            {
                string fieldPath = path + "." + axis;
                LongField field = new LongField(axis == "row" ? "행 (1부터)" : "열 (1부터)");
                field.name = axis;
                field.SetValueWithoutNotify((long)owner.FindProperty(fieldPath).intValue + 1);
                field.RegisterValueChangedCallback(evt =>
                {
                    // 범위를 벗어난 좌표도 저장한다. int 표현 범위만 지키고 보드 범위는 검사기에 맡긴다.
                    long raw = System.Math.Clamp(evt.newValue, (long)int.MinValue + 1, (long)int.MaxValue + 1) - 1;
                    owner.Update();
                    owner.FindProperty(fieldPath).intValue = (int)raw;
                    owner.ApplyModifiedProperties();
                    field.SetValueWithoutNotify(raw + 1);
                });
                field.TrackPropertyValue(owner.FindProperty(fieldPath), value =>
                    field.SetValueWithoutNotify((long)value.intValue + 1));
                root.Add(field);
            }
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(BoardDefinition))]
    public sealed class BoardDefinitionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            Foldout root = new Foldout { text = "보드", value = property.isExpanded };
            PropertyField rows = new PropertyField(property.FindPropertyRelative("rows"), "행 수");
            PropertyField columns = new PropertyField(property.FindPropertyRelative("columns"), "열 수");
            rows.SetEnabled(false);
            columns.SetEnabled(false);
            root.Add(rows);
            root.Add(columns);
            root.Add(new PropertyField(property.FindPropertyRelative("cells"), "칸 (행 우선 순서)"));
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(CellDefinition))]
    public sealed class CellDefinitionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            string path = property.propertyPath;
            int bracket = path.LastIndexOf('[');
            int index = int.Parse(path.Substring(bracket + 1, path.Length - bracket - 2));
            int columns = property.serializedObject.FindProperty("board.columns").intValue;
            string label = columns > 0
                ? new BoardCoordinate(index / columns, index % columns).ToString() + " 활성"
                : $"칸 {index + 1} 활성 (열 수 오류)";
            return new PropertyField(property.FindPropertyRelative("isActive"), label);
        }
    }

    [CustomPropertyDrawer(typeof(InitialBlockDefinition))]
    public sealed class InitialBlockDefinitionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            SerializedProperty kind = property.FindPropertyRelative("kind");
            root.Add(new PropertyField(property.FindPropertyRelative("coordinate"), "좌표"));
            root.Add(new PropertyField(kind, "배치 유형"));
            PropertyField color = new PropertyField(property.FindPropertyRelative("fixedColor"), "고정 색");
            color.SetEnabled(kind.intValue == (int)InitialBlockKind.FixedNormal);
            root.Add(color);
            PropertyField direction = new PropertyField(property.FindPropertyRelative("rocketDirection"), "로켓 제거 방향");
            direction.style.display = kind.intValue == (int)InitialBlockKind.Rocket ? DisplayStyle.Flex : DisplayStyle.None;
            root.Add(direction);
            root.TrackPropertyValue(kind, value => direction.style.display = value.intValue == (int)InitialBlockKind.Rocket ? DisplayStyle.Flex : DisplayStyle.None);
            root.TrackPropertyValue(kind, value => color.SetEnabled(value.intValue == (int)InitialBlockKind.FixedNormal));
            root.Add(new Label("무작위 유형에서는 고정 색을 사용하지 않습니다."));
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(ObstaclePlacementDefinition))]
    public sealed class ObstaclePlacementDefinitionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            SerializedProperty kind = property.FindPropertyRelative("kind");
            root.Add(new PropertyField(property.FindPropertyRelative("coordinate"), "기준 좌표"));
            root.Add(new PropertyField(property.FindPropertyRelative("id"), "연결 식별 ID (중복/누락 오류 수정용)"));
            root.Add(new PropertyField(kind, "종류"));
            PropertyField durability = new PropertyField(property.FindPropertyRelative("durability"), "초기 내구도");
            PropertyField color = new PropertyField(property.FindPropertyRelative("color"), "자물쇠 색");
            PropertyField charge = new PropertyField(property.FindPropertyRelative("requiredCharge"), "필요 충전량 (3~5)");
            root.Add(durability); root.Add(color); root.Add(charge);
            void Display(int value)
            {
                durability.style.display = value == (int)ObstacleKind.Generator ? DisplayStyle.None : DisplayStyle.Flex;
                color.style.display = value == (int)ObstacleKind.ColorLock ? DisplayStyle.Flex : DisplayStyle.None;
                charge.style.display = value == (int)ObstacleKind.Generator ? DisplayStyle.Flex : DisplayStyle.None;
            }
            Display(kind.intValue);
            root.TrackPropertyValue(kind, value => Display(value.intValue));
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(CoverPlacementDefinition))]
    public sealed class CoverPlacementDefinitionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            root.Add(new PropertyField(property.FindPropertyRelative("coordinate"), "좌표"));
            root.Add(new PropertyField(property.FindPropertyRelative("kind"), "덮개 종류"));
            root.Add(new PropertyField(property.FindPropertyRelative("durability"), "내구도 (거미줄 1~3, 곰팡이 1)"));
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(DustPlacementDefinition))]
    public sealed class DustPlacementDefinitionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            root.Add(new PropertyField(property.FindPropertyRelative("coordinate"), "좌표"));
            root.Add(new PropertyField(property.FindPropertyRelative("durability"), "먼지 내구도 (1~3)"));
            return root;
        }
    }
}
