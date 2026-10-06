using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Levels;
using Simulation;

namespace Elements
{
    public enum ElementSupplyBehavior { RandomNormal = 1, FixedNormal = 2, Power = 3, Recovery = 4, Obstacle = 5, RandomPower = 6 }

    /// <summary>공급 목록의 수량/커서와 독립된 생성 행동. enum 참조는 구형 입력 연결에만 사용한다.</summary>
    public sealed class ElementSupplyProfile
    {
        public ElementSupplyBehavior Behavior { get; }
        public RuntimeContent Content { get; }
        public ObstacleKind? Obstacle { get; }
        public string BodyIdPrefix { get; }
        public ReadOnlyCollection<SupplyKind> Choices { get; }
        public ElementId? ObstacleDefinitionId { get; }
        public ReadOnlyCollection<ElementId> ChoiceDefinitionIds { get; }
        public ElementSupplyProfile(ElementSupplyBehavior behavior, RuntimeContent content, ObstacleKind? obstacle = null,
            string bodyIdPrefix = null, IEnumerable<SupplyKind> choices = null)
            : this(behavior, content, obstacle, bodyIdPrefix, choices, null, null) { }

        public static ElementSupplyProfile ForObstacle(ElementId definitionId, string bodyIdPrefix) =>
            new ElementSupplyProfile(ElementSupplyBehavior.Obstacle, RuntimeContent.Obstacle, null, bodyIdPrefix, null, definitionId, null);
        public static ElementSupplyProfile ForRandomPower(IEnumerable<ElementId> definitions) =>
            new ElementSupplyProfile(ElementSupplyBehavior.RandomPower, RuntimeContent.Empty, null, null, null, null, definitions);

        public ElementSupplyProfile(ElementSupplyBehavior behavior, RuntimeContent content, ObstacleKind? obstacle,
            string bodyIdPrefix, IEnumerable<SupplyKind> choices, ElementId? obstacleDefinitionId, IEnumerable<ElementId> choiceDefinitionIds)
        {
            if (!Enum.IsDefined(typeof(ElementSupplyBehavior), behavior)) throw new ArgumentOutOfRangeException(nameof(behavior));
            SupplyKind[] selected = choices?.ToArray() ?? Array.Empty<SupplyKind>();
            ElementId[] selectedIds = choiceDefinitionIds?.ToArray() ?? Array.Empty<ElementId>();
            if (obstacleDefinitionId.HasValue && (!obstacleDefinitionId.Value.IsValid || obstacle.HasValue || behavior != ElementSupplyBehavior.Obstacle))
                throw new ArgumentException("공급 본체 정의 ID가 잘못됐거나 구형 참조와 중복됩니다.");
            if (selectedIds.Length > 0 && (behavior != ElementSupplyBehavior.RandomPower || selected.Length > 0 || selectedIds.Any(id => !id.IsValid)))
                throw new ArgumentException("공급 선택 정의 ID가 잘못됐거나 구형 선택과 중복됩니다.");
            bool valid = behavior switch
            {
                ElementSupplyBehavior.RandomNormal or ElementSupplyBehavior.FixedNormal => content == RuntimeContent.Normal,
                ElementSupplyBehavior.Power => content == RuntimeContent.Rocket || content == RuntimeContent.Bomb || content == RuntimeContent.Drone || content == RuntimeContent.Magnet,
                ElementSupplyBehavior.Recovery => content == RuntimeContent.Recovery,
                ElementSupplyBehavior.Obstacle => content == RuntimeContent.Obstacle &&
                    (obstacleDefinitionId.HasValue || obstacle.HasValue && Enum.IsDefined(typeof(ObstacleKind), obstacle.Value)) && !string.IsNullOrWhiteSpace(bodyIdPrefix),
                ElementSupplyBehavior.RandomPower => content == RuntimeContent.Empty && (selectedIds.Length > 0 || selected.Length > 0 &&
                    selected.All(kind => kind == SupplyKind.Rocket || kind == SupplyKind.Bomb || kind == SupplyKind.Drone || kind == SupplyKind.Magnet)),
                _ => false
            };
            if (!valid) throw new ArgumentException("공급 행동과 생성 설정이 일치하지 않습니다.");
            Behavior = behavior; Content = content; Obstacle = obstacle; BodyIdPrefix = bodyIdPrefix;
            Choices = Array.AsReadOnly(selected);
            ObstacleDefinitionId = obstacleDefinitionId; ChoiceDefinitionIds = Array.AsReadOnly(selectedIds);
        }
    }
}
