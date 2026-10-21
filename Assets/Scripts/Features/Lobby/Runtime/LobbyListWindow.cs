using UnityEngine;

namespace Lobby
{
    public readonly struct LobbyListWindow
    {
        public int First { get; }
        public int Count { get; }

        private LobbyListWindow(int first, int count)
        {
            First = first;
            Count = count;
        }

        public static LobbyListWindow Calculate(int total, float rowHeight, float viewportHeight, float offset)
        {
            if (total <= 0) return new LobbyListWindow(0, 0);
            int capacity = Mathf.Min(total, Mathf.CeilToInt(viewportHeight / rowHeight) + 4);
            float clamped = Mathf.Clamp(offset, 0, Mathf.Max(0, total * rowHeight - viewportHeight));
            int first = Mathf.Clamp(Mathf.FloorToInt(clamped / rowHeight) - 2, 0, total - capacity);
            return new LobbyListWindow(first, capacity);
        }
    }
}
