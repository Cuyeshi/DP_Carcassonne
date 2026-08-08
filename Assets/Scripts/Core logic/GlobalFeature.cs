using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Core_logic
{
    /// <summary>
    /// Класс, который представляет целую дорогу, целый город или целое поле.
    /// </summary>
    public class GlobalFeature
    {
        public TerrainType Type { get; private set; }
        public int OpenEdges { get; private set; }

        // Список всех маленьких регионов, из которых состоит эта махина
        public List<TileRegion> Regions { get; private set; }

        // НОВОЕ: Словарь миплов (ID Игрока -> Количество миплов)
        public Dictionary<int, int> Meeples { get; private set; }

        public GlobalFeature(TileRegion initialRegion)
        {
            Type = initialRegion.Type;
            OpenEdges = initialRegion.OpenEdges;
            Regions = new List<TileRegion> { initialRegion };
            Meeples = new Dictionary<int, int>(); // Изначально миплов нет
        }

        // Метод слияния двух глобальных объектов в один
        public void MergeWith(GlobalFeature other)
        {
            this.Regions.AddRange(other.Regions);
            this.OpenEdges = this.OpenEdges + other.OpenEdges - 2;

            // НОВОЕ: Сливаем миплов при объединении корпораций
            foreach (var kvp in other.Meeples)
            {
                int playerId = kvp.Key;
                int meepleCount = kvp.Value;

                if (this.Meeples.ContainsKey(playerId))
                {
                    this.Meeples[playerId] += meepleCount; // Суммируем
                }
                else
                {
                    this.Meeples[playerId] = meepleCount; // Добавляем новых
                }
            }
        }

        // Проверка: завершен ли объект?
        public bool IsCompleted => OpenEdges == 0;


        // Вспомогательный метод: узнать, кто побеждает (у кого больше миплов)
        // Возвращает список ID игроков (их может быть несколько при ничьей)
        public List<int> GetWinners()
        {
            if (Meeples.Count == 0) return new List<int>();

            int maxMeeples = Meeples.Values.Max(); // Находим максимальное число миплов
            return Meeples.Where(kvp => kvp.Value == maxMeeples)
                          .Select(kvp => kvp.Key)
                          .ToList();
        }




        /// <summary>
        /// Подсчет очков по правилам закрытых объектов Каркассона
        /// </summary>
        public int CalculatePoints()
        {
            // Берем все регионы, достаем из них родительские тайлы, 
            // и с помощью Distinct() оставляем только уникальные.
            int uniqueTilesCount = Regions.Select(r => r.ParentTile).Distinct().Count();

            if (Type == TerrainType.Road)
            {
                // Дорога: 1 очко за каждый уникальный тайл
                return uniqueTilesCount * 1;
            }
            else if (Type == TerrainType.City)
            {
                // Город: 2 очка за тайл + 2 очка за каждый щит
                int shieldsCount = Regions.Count(r => r.HasShield);
                return (uniqueTilesCount * 2) + (shieldsCount * 2);
            }

            return 0; // Поля пока не считаем
        }


        public GlobalFeature Clone()
        {
            // Поверхностное копирование базовых типов (Type, OpenEdges)
            GlobalFeature copy = (GlobalFeature)this.MemberwiseClone();

            // Глубокое копирование коллекций
            copy.Regions = new List<TileRegion>(this.Regions);
            copy.Meeples = new Dictionary<int, int>(this.Meeples);

            return copy;
        }
    }
}