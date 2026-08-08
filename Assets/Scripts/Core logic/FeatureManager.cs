using System.Collections.Generic;

namespace Assets.Scripts.Core_logic
{
    public class FeatureManager
    {
        // Словарь для поиска "Корня" (Root). Кто чей босс?
        private Dictionary<TileRegion, TileRegion> _parent = new Dictionary<TileRegion, TileRegion>();

        // Словарь, хранящий данные Глобального объекта только для "Корневых" регионов
        private Dictionary<TileRegion, GlobalFeature> _features = new Dictionary<TileRegion, GlobalFeature>();

        // 1. Зарегистрировать новые регионы (вызывается, когда тайл кладется на стол)
        public void RegisterRegions(IEnumerable<TileRegion> regions)
        {
            foreach (var region in regions)
            {
                _parent[region] = region; // Сначала каждый сам себе босс
                _features[region] = new GlobalFeature(region);
            }
        }

        // 2. Метод Find (Поиск сжатием пути)
        public TileRegion FindRoot(TileRegion region)
        {
            if (_parent[region] != region)
            {
                // Рекурсивно находим самого главного босса и перепривязываем напрямую к нему (оптимизация)
                _parent[region] = FindRoot(_parent[region]);
            }
            return _parent[region];
        }

        // 3. Метод Union (Объединение двух регионов, когда тайлы соприкоснулись)
        public void Union(TileRegion regionA, TileRegion regionB)
        {
            TileRegion rootA = FindRoot(regionA);
            TileRegion rootB = FindRoot(regionB);

            // Если боссы разные, значит это были две разные дороги. Сливаем их!
            if (rootA != rootB)
            {
                // Подчиняем B боссу A
                _parent[rootB] = rootA;

                // Сливаем их глобальные данные
                _features[rootA].MergeWith(_features[rootB]);

                // Удаляем старые данные B, так как теперь всем рулит A
                _features.Remove(rootB);
            }
            else
            {
                // РЕДКИЙ СЛУЧАЙ КАРКАССОНА: 
                // Дорога завернулась в кольцо! Оба региона уже принадлежат одному объекту.
                // Мы не сливаем объекты (они уже слиты), но мы ОБЯЗАНЫ вычесть 2 открытых края, 
                // так как кольцо замкнулось.
                _features[rootA].MergeWith(new GlobalFeature(new TileRegion(-1, rootA.Type, 0))); // Хак: прибавляем пустышку, чтобы просто сработала математика -2
            }
        }

        // Вспомогательный метод для получения Глобального объекта по любому кусочку
        public GlobalFeature GetFeature(TileRegion region)
        {
            TileRegion root = FindRoot(region);
            return _features[root];
        }

        public FeatureManager Clone()
        {
            FeatureManager copy = new FeatureManager();

            // Копируем телефонную книгу "Кто чей босс"
            copy._parent = new Dictionary<TileRegion, TileRegion>(this._parent);

            // Копируем сейфы с документами (Глобальные объекты)
            copy._features = new Dictionary<TileRegion, GlobalFeature>();
            foreach (var kvp in this._features)
            {
                copy._features[kvp.Key] = kvp.Value.Clone();
            }

            return copy;
        }

        // НОВОЕ: Получить все уникальные глобальные объекты на доске
        public HashSet<GlobalFeature> GetAllFeatures()
        {
            return new HashSet<GlobalFeature>(_features.Values);
        }
    }
}