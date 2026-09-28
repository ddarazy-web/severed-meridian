using System;
using UnityEngine;

namespace Levels
{
    public enum MissionKind { Color, Crate, Web, Scrap, Dust, Safe, ColorLock, Appliance, Mold, Recovery }

    [Serializable]
    public struct LevelMissionDefinition
    {
        [SerializeField] private MissionKind kind;
        [SerializeField] private RabbitColor color;
        [SerializeField] private int count;
        public MissionKind Kind => kind;
        public RabbitColor Color => color;
        public int Count => count;

        /// <summary>에셋 생성 없이 현재 목표량을 값으로 구성한다.</summary>
        /// <param name="kind">목표 종류.</param><param name="color">색 목표.</param><param name="count">남은 목표량.</param>
        internal LevelMissionDefinition(MissionKind kind, RabbitColor color, int count)
        { this.kind = kind; this.color = color; this.count = count; }
    }
}
