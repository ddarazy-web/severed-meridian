using MemoryPack;
using System;
using UnityEngine;

namespace Board
{
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct CellDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private bool isActive;

        [MemoryPackIgnore] public bool IsActive => isActive;

        public CellDefinition(bool isActive)
        {
            this.isActive = isActive;
        }
    }
}
