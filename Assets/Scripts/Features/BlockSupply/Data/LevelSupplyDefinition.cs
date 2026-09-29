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

    [Serializable]
    public struct SupplyItem
    {
        [SerializeField] private SupplyKind kind;
        [SerializeField] private int count;
        [SerializeField] private RabbitColor color;
        [SerializeField] private RocketDirection direction;
        [SerializeField] private int durability;
        public SupplyKind Kind => kind;
        public int Count => count;
        public RabbitColor Color => color;
        public RocketDirection Direction => direction;
        public int Durability => durability;

        public SupplyItem(SupplyKind kind, int count = 1, RabbitColor color = RabbitColor.Type1,
            RocketDirection direction = RocketDirection.Horizontal, int durability = 1)
        { this.kind = kind; this.count = count; this.color = color; this.direction = direction; this.durability = durability; }
    }

    [Serializable]
    public struct SupplySourceDefinition
    {
        [SerializeField] private BoardCoordinate coordinate;
        [SerializeField] private SupplyMode mode;
        [SerializeField] private SupplyExhaustion exhaustion;
        // 저장 순서 0번이 먼저 공급된다. 화면에서는 생성구에 가까운 아래쪽에 표시한다.
        [SerializeField] private List<SupplyItem> items;
        public BoardCoordinate Coordinate => coordinate;
        public SupplyMode Mode => mode;
        public SupplyExhaustion Exhaustion => exhaustion;
        public IReadOnlyList<SupplyItem> Items => items;
    }

    [Serializable]
    public sealed class LevelSupplyDefinition
    {
        [SerializeField] private List<SupplySourceDefinition> sources = new List<SupplySourceDefinition>();
        [SerializeField] private int scrapTarget;
        [SerializeField] private int scrapLimit;
        [SerializeField] private int scrapDurability = 1;
        [SerializeField] private int recoveryTarget;
        public IReadOnlyList<SupplySourceDefinition> Sources => sources;
        public int ScrapTarget => scrapTarget;
        public int ScrapLimit => scrapLimit;
        public int ScrapDurability => scrapDurability;
        public int RecoveryTarget => recoveryTarget;
    }
}
