using UnityEngine;

namespace Assets.Scripts.Core_logic
{
    /// <summary>
    /// Класс объекта, который хранит ссылки на регионы, к которым он привязан
    /// </summary>
    public class TileEdge
    {
        // Смотрим из центра тайла наружу
        public TileRegion Left { get; private set; }
        public TileRegion Center { get; private set; }
        public TileRegion Right { get; private set; }

        public TileEdge(TileRegion left, TileRegion center, TileRegion right)
        {
            Left = left;
            Center = center;
            Right = right;
        }

        // Совпадает ли наша грань с гранью соседа?
        // ВАЖНО: Когда мы прикладываем тайл к тайлу, они смотрят друг на друга.
        // Поэтому наше Лево касается его Права! А Центр касается Центра.
        public bool Matches(TileEdge other)
        {
            return this.Left.Type == other.Right.Type &&
                   this.Center.Type == other.Center.Type &&
                   this.Right.Type == other.Left.Type;
        }
    }
}