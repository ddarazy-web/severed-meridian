using MemoryPack;
using System;
using UnityEngine;

namespace Board
{
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct BoardCoordinate : IEquatable<BoardCoordinate>
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private int row;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private int column;

        [MemoryPackIgnore] public int Row => row;
        [MemoryPackIgnore] public int Column => column;

        public BoardCoordinate(int row, int column)
        {
            this.row = row;
            this.column = column;
        }

        public bool Equals(BoardCoordinate other) => row == other.row && column == other.column;
        public override bool Equals(object obj) => obj is BoardCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(row, column);

        // 저장 좌표는 0부터, 제작자에게 표시하는 좌표는 1부터 시작한다.
        public override string ToString() => $"(행 {(long)row + 1}, 열 {(long)column + 1})";
    }
}
