using MemoryPack;
using System;
using System.Collections.Generic;
using Board;
using UnityEngine;

namespace Levels
{
    public enum SupplyMode { Random, Fixed, MaintainScrap, MaintainRecovery }
    public enum SupplyExhaustion { Stop, Random }
    // 기존 에셋의 숫자 값을 유지하기 위해 새로운 공급 종류는 끝에 추가한다.
    public enum SupplyKind { RandomNormal, FixedNormal, Rocket, Bomb, Drone, Magnet, Scrap, Recovery, RandomPower }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct SupplyItem
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private SupplyKind kind;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private int count;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private RabbitColor color;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(3)] private RocketDirection direction;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(4)] private int durability;
        [MemoryPackIgnore] public SupplyKind Kind => kind;
        [MemoryPackIgnore] public int Count => count;
        [MemoryPackIgnore] public RabbitColor Color => color;
        [MemoryPackIgnore] public RocketDirection Direction => direction;
        [MemoryPackIgnore] public int Durability => durability;

        public SupplyItem(SupplyKind kind, int count = 1, RabbitColor color = RabbitColor.Type1,
            RocketDirection direction = RocketDirection.Horizontal, int durability = 1)
        { this.kind = kind; this.count = count; this.color = color; this.direction = direction; this.durability = durability; }
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct SupplySourceDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate coordinate;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private SupplyMode mode;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private SupplyExhaustion exhaustion;
        // 저장 순서 0번이 먼저 공급된다. 화면에서는 생성구에 가까운 아래쪽에 표시한다.
        [SerializeField, MemoryPackInclude, MemoryPackOrder(3)] private List<SupplyItem> items;
        [MemoryPackIgnore] public BoardCoordinate Coordinate => coordinate;
        [MemoryPackIgnore] public SupplyMode Mode => mode;
        [MemoryPackIgnore] public SupplyExhaustion Exhaustion => exhaustion;
        [MemoryPackIgnore] public IReadOnlyList<SupplyItem> Items => items;
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public sealed partial class LevelSupplyDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private List<SupplySourceDefinition> sources = new List<SupplySourceDefinition>();
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private int scrapTarget;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private int scrapLimit;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(3)] private int scrapDurability = 1;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(4)] private int recoveryTarget;
        [MemoryPackIgnore] public IReadOnlyList<SupplySourceDefinition> Sources => sources;
        [MemoryPackIgnore] public int ScrapTarget => scrapTarget;
        [MemoryPackIgnore] public int ScrapLimit => scrapLimit;
        [MemoryPackIgnore] public int ScrapDurability => scrapDurability;
        [MemoryPackIgnore] public int RecoveryTarget => recoveryTarget;
        internal void CropTo(BoardDefinition board) => sources?.RemoveAll(item => !board.Contains(item.Coordinate));
    }
}
