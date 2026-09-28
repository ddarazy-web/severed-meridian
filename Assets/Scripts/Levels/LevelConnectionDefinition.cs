using System;
using System.Collections.Generic;
using Board;
using UnityEngine;

namespace Levels
{
    [Serializable]
    public struct LevelConnectionDefinition
    {
        [SerializeField] private string generatorId;
        [SerializeField] private string targetId;
        // 전선 첫/끝 꼭짓점이 각 본체 외곽 단자다. 미완성 경로도 원본으로 보존한다.
        [SerializeField] private List<BoardCoordinate> vertices;
        public string GeneratorId => generatorId;
        public string TargetId => targetId;
        public IReadOnlyList<BoardCoordinate> Vertices => vertices;
    }
}
