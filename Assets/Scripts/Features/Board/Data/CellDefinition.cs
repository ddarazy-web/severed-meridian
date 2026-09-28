using System;
using UnityEngine;

namespace Board
{
    [Serializable]
    public struct CellDefinition
    {
        [SerializeField] private bool isActive;

        public bool IsActive => isActive;

        public CellDefinition(bool isActive)
        {
            this.isActive = isActive;
        }
    }
}
