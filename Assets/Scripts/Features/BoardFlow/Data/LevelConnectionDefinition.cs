using MemoryPack;
using System;
using System.Collections.Generic;
using Board;
using UnityEngine;

namespace Levels
{
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct LevelConnectionDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private string generatorId;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private string targetId;
        // 전선 첫/끝 꼭짓점이 각 본체 외곽 단자다. 미완성 경로도 원본으로 보존한다.
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private List<BoardCoordinate> vertices;
        [MemoryPackIgnore] public string GeneratorId => generatorId;
        [MemoryPackIgnore] public string TargetId => targetId;
        [MemoryPackIgnore] public IReadOnlyList<BoardCoordinate> Vertices => vertices;
    }
}
