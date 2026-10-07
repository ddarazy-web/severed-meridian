using UnityEngine;

namespace Elements
{
    /// <summary>제작 전용 정의 원본. 실행에는 검증된 불변 값만 전달한다.</summary>
    [CreateAssetMenu(menuName = "MATCH/요소 정의")]
    public sealed class ElementDefinitionAsset : ScriptableObject
    {
        [SerializeField] private PackedElementDefinition definition = new PackedElementDefinition();
        [SerializeField] private string planningDocument;
        [SerializeField] private string planningSection;
        public string PlanningDocument => planningDocument;
        public string PlanningSection => planningSection;
        public ElementDefinition ToDefinition() => definition == null
            ? throw new System.ArgumentException($"요소 정의 ID '<null>': {name}의 정의가 없습니다.")
            : definition.ToDefinition();
    }
}
