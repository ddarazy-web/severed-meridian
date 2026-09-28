using System;
using System.Collections.Generic;

namespace Simulation
{
    public sealed class SimulationRandom
    {
        // 같은 Unity/Mono 런타임에서 재현한다. 다른 런타임의 System.Random 알고리즘까지 보장하지 않는다.
        public const string Implementation = "System.Random/initial-state-v1";
        private readonly Random random;
        private readonly List<int> bounds = new List<int>();
        public int Seed { get; }
        public int DrawCount { get; private set; }
        public string Version => Implementation;
        internal SimulationRandom(int seed) { Seed = seed; random = new Random(seed); }
        internal int Next(int count) { int value = random.Next(count); bounds.Add(count); DrawCount++; return value; }
        // 시작 구성 이후의 난수 진행까지 복제한다. 전역 난수나 System.Random 내부 구현에 접근하지 않는다.
        internal SimulationRandom Copy()
        {
            SimulationRandom copy = new SimulationRandom(Seed);
            foreach (int count in bounds) copy.Next(count);
            return copy;
        }
    }
}
