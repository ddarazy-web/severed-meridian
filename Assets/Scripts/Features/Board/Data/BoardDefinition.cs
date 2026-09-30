using MemoryPack;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Board
{
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public sealed partial class BoardDefinition
    {
        public const int DefaultRows = 10;
        public const int DefaultColumns = 10;

        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private int rows;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private int columns;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private List<CellDefinition> cells = new List<CellDefinition>();

        [MemoryPackIgnore] public int Rows => rows;
        [MemoryPackIgnore] public int Columns => columns;
        [MemoryPackIgnore] public IReadOnlyList<CellDefinition> Cells => cells;

        public static BoardDefinition CreateDefault()
        {
            BoardDefinition board = new BoardDefinition { rows = DefaultRows, columns = DefaultColumns };
            for (int i = 0; i < DefaultRows * DefaultColumns; i++)
                board.cells.Add(new CellDefinition(true));
            return board;
        }

        public bool Contains(BoardCoordinate coordinate)
        {
            return coordinate.Row >= 0 && coordinate.Row < rows &&
                coordinate.Column >= 0 && coordinate.Column < columns;
        }

        public bool TryGetCell(BoardCoordinate coordinate, out CellDefinition cell)
        {
            cell = default;
            if (!Contains(coordinate) || cells == null)
                return false;

            long index = (long)coordinate.Row * columns + coordinate.Column;
            if (index >= cells.Count)
                return false;

            cell = cells[(int)index];
            return true;
        }
    }
}
