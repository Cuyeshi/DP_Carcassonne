using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Core_logic.AI
{
    /// <summary>
    /// Статистика одного конкретного хода
    /// </summary>
    public class MoveNodeStat
    {
        public Vector2Int Position;
        public int TotalSimulations;
        public int Wins;
        public bool IsCurrentlySimulating;
    }
}
