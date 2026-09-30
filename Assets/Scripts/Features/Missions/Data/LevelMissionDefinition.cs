using MemoryPack;
using System;
using UnityEngine;

namespace Levels
{
    public enum MissionKind { Color, Crate, Web, Scrap, Dust, Safe, ColorLock, Appliance, Mold, Recovery }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct LevelMissionDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private MissionKind kind;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private RabbitColor color;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private int count;
        [MemoryPackIgnore] public MissionKind Kind => kind;
        [MemoryPackIgnore] public RabbitColor Color => color;
        [MemoryPackIgnore] public int Count => count;

        /// <summary>에셋 생성 없이 현재 목표량을 값으로 구성한다.</summary>
        /// <param name="kind">목표 종류.</param><param name="color">색 목표.</param><param name="count">남은 목표량.</param>
        internal LevelMissionDefinition(MissionKind kind, RabbitColor color, int count)
        { this.kind = kind; this.color = color; this.count = count; }
    }
}
