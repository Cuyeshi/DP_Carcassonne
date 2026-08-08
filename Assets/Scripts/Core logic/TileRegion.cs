using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core_logic
{
    /// <summary>
    /// Класс региона (внутренний объект тайла). Каждый отдельный кусочек ландшафта на тайле — это регион. У региона есть свой уникальный ID в рамках тайла.
    /// </summary>
    public class TileRegion
    {
        public int Id { get; private set; }
        public TerrainType Type { get; private set; }

        // Специальные свойства (например, для подсчета очков)
        public bool HasShield { get; private set; } // Щит в городе (+2 очка)
        public bool IsCloister { get; private set; } // Монастырь ли это?

        // Количество выходов на край тайла
        public int OpenEdges { get; private set; }

        // Можно ли ставить сюда мипла? (По умолчанию - да)
        public bool IsPlaceable { get; private set; }

        // Ссылка на родительский тайл (чтобы не считать один тайл дважды)
        public TileData ParentTile { get; set; }

        // НОВОЕ: Список кусочков, с которыми этот кусочек соприкасается внутри тайла
        public HashSet<TileRegion> AdjacentRegions { get; private set; }

        public TileRegion(int id, TerrainType type, int openEdges, bool hasShield = false, bool isCloister = false, bool isPlaceable = true)
        {
            Id = id;
            Type = type;
            OpenEdges = openEdges;
            HasShield = hasShield;
            IsCloister = isCloister;
            IsPlaceable = isPlaceable; // Сохраняем флаг

            AdjacentRegions = new HashSet<TileRegion>();
        }

        // Метод для связывания двух кусочков друг с другом
        public void AddAdjacent(TileRegion other)
        {
            this.AdjacentRegions.Add(other);
            other.AdjacentRegions.Add(this); // Связь двусторонняя
        }
    }
}
