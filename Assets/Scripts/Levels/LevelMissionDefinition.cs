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
    }
}
