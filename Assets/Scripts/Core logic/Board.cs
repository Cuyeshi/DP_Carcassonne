using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Core_logic
{
    public class Board
    {
        // Флаг. Если true - мы в уме ИИ, писать в консоль запрещено
        public bool IsSimulation { get; set; } = false;

        // Добавляем словарь очков (PlayerId -> Points)
        public event Action<List<TileRegion>, Dictionary<int, int>> OnFeatureCompleted;

        /// <summary>
        /// Бесконечная сетка. Ключ - координаты X,Y. Значение - Тайл.
        /// </summary>
        private Dictionary<Vector2Int, TileData> _placedTiles = new Dictionary<Vector2Int, TileData>();

        public FeatureManager GraphManager = new FeatureManager();

        // Координаты монастыря -> Его регион
        public Dictionary<Vector2Int, TileRegion> ActiveMonasteries { get; private set; } = new Dictionary<Vector2Int, TileRegion>();

        private int _maxGridSize = 11;

        /// <summary>
        /// Метод для получения индекса противоположного направления
        /// </summary>
        private Direction GetOpposite(Direction dir)
        {
            return (Direction)(((int)dir + 2) % 4);
        }

        /// <summary>
        /// Вспомогательный метод: координаты соседей.
        /// </summary>
        private Vector2Int GetNeighborPos(Vector2Int pos, Direction dir)
        {
            return dir switch
            {
                Direction.North => new Vector2Int(pos.x, pos.y + 1),
                Direction.East => new Vector2Int(pos.x + 1, pos.y),
                Direction.South => new Vector2Int(pos.x, pos.y - 1),
                Direction.West => new Vector2Int(pos.x - 1, pos.y),
                _ => pos
            };
        }

        /// <summary>
        /// Главынй метод: определяет, можно ли поставить тайл.
        /// </summary>
        public bool CanPlaceTile(TileData tile, Vector2Int pos)
        {
            // 0. Максимальный размер поля
            if (Mathf.Abs(pos.x) > _maxGridSize || Mathf.Abs(pos.y) > _maxGridSize)
            {
                return false; 
            }

            // 1. Место уже занято?
            if (_placedTiles.ContainsKey(pos))
                return false;

            bool hasAtLeastOneNeighbor = false;
            bool isFirstTile = _placedTiles.Count == 0;

            // 2. Проверяем всех 4-х возможных соседей
            for (int i = 0; i < 4; i++)
            {
                Direction currentDir = (Direction)i;
                Vector2Int neighborPos = GetNeighborPos(pos, currentDir);

                if (_placedTiles.TryGetValue(neighborPos, out TileData neighborTile))
                {
                    hasAtLeastOneNeighbor = true;

                    // Используем сложный объект TileEdge
                    TileEdge ourEdge = tile.GetEdge(currentDir);
                    TileEdge neighborEdge = neighborTile.GetEdge(GetOpposite(currentDir));

                    // Используем метод Matches, который проверяет совпадение по Лево-Центр-Право
                    if (!ourEdge.Matches(neighborEdge))
                    {
                        return false; // Несовпадение ландшафта!
                    }
                }
            }

            // Если это не первый тайл, он обязан касаться хотя бы одного другого
            if (!isFirstTile && !hasAtLeastOneNeighbor)
                return false;

            return true;
        }

        /// <summary>
        /// Метод для установки тайла на поле.
        /// </summary>
        public void PlaceTile(TileData tile, Vector2Int pos)
        {
            if (CanPlaceTile(tile, pos))
            {
                _placedTiles[pos] = tile;

                // 0. НОВОЕ: Если на тайле есть Монастырь, запоминаем его координаты
                foreach (var region in tile.Regions)
                {
                    if (region.IsCloister) ActiveMonasteries[pos] = region;
                }

                // 1. Регистрируем все кусочки нового тайла в графе
                GraphManager.RegisterRegions(tile.Regions);

                // 2. Сшиваем тайл с соседями
                for (int i = 0; i < 4; i++)
                {
                    Direction currentDir = (Direction)i;
                    Vector2Int neighborPos = GetNeighborPos(pos, currentDir);

                    if (_placedTiles.TryGetValue(neighborPos, out TileData neighborTile))
                    {
                        TileEdge ourEdge = tile.GetEdge(currentDir);
                        TileEdge neighborEdge = neighborTile.GetEdge(GetOpposite(currentDir));

                        // Сшиваем попарно: наше Лево и их Право, наш Центр и их Центр и т.д.
                        // ВНИМАНИЕ: Чтобы не сшить один и тот же большой город трижды, 
                        // мы используем HashSet для уникальных пар
                        HashSet<(TileRegion, TileRegion)> connections = new HashSet<(TileRegion, TileRegion)>
                        {
                            (ourEdge.Left, neighborEdge.Right),
                            (ourEdge.Center, neighborEdge.Center),
                            (ourEdge.Right, neighborEdge.Left)
                        };

                        foreach (var pair in connections)
                        {
                            // Вызываем наше объединение!
                            GraphManager.Union(pair.Item1, pair.Item2);
                        }
                    }
                }

                // (Опционально) Здесь можно добавить проверку: 
                // Если GraphManager.GetFeature(какой-то_регион).IsCompleted == true,
                // значит что-то достроилось, и можно начислить очки!
            }
            else
            {
                Debug.LogError($"Невозможно поставить тайл {tile.Id} на позицию {pos}");
            }
        }



        /// <summary>
        /// Проверка: Может ли игрок поставить мипла на этот кусочек тайла?
        /// </summary>
        public bool CanPlaceMeeple(Player player, TileRegion region)
        {
            // 1. Есть ли у игрока свободные миплы?
            if (player.MeeplesAvailable <= 0)
                return false;

            // НОВОЕ: Если на этот кусочек вообще нельзя ставить миплов
            if (!region.IsPlaceable) return false;

            // 2. Получаем глобальный объект (всю дорогу или весь город), к которому принадлежит этот кусочек
            GlobalFeature feature = GraphManager.GetFeature(region);

            // 3. ПРАВИЛО КАРКАССОНА: Нельзя ставить мипла, если на этом объекте уже есть ХОТЯ БЫ ОДИН мипл (даже свой)
            if (feature.Meeples.Count > 0)
                return false;

            return true;
        }

        /// <summary>
        /// Поставить мипла на регион.
        /// </summary>
        public void PlaceMeeple(Player player, TileRegion region)
        {
            if (CanPlaceMeeple(player, region))
            {
                player.MeeplesAvailable--; // Забираем мипла из запаса

                GlobalFeature feature = GraphManager.GetFeature(region);
                feature.Meeples[player.Id] = 1; // Записываем мипла в глобальный объект

                if (!IsSimulation) 
                {
                    //Debug.Log($"Игрок {player.Id} поставил мипла на {feature.Type}");
                }
            }
            else
            {
                if (!IsSimulation)
                    Debug.LogError("Нарушение правил: сюда нельзя ставить мипла!");
            }
        }



        /// <summary>
        /// Проверить завершенные объекты вокруг только что положенного тайла
        /// </summary>
        /// <param name="newlyPlacedTile">Тайл, который только что положили</param>
        /// <param name="allPlayers">Список всех игроков в сессии</param>
        public void CheckAndScoreCompletedFeatures(TileData newlyPlacedTile, List<Player> allPlayers)
        {
            // Собираем все ГЛОБАЛЬНЫЕ объекты, которых коснулся этот новый тайл.
            // Используем HashSet, чтобы не проверять один и тот же город несколько раз.
            HashSet<GlobalFeature> featuresToCheck = new HashSet<GlobalFeature>();

            // Фильтруем на уровне локальных регионов тайла
            foreach (var region in newlyPlacedTile.Regions)
            {
                // Если это Поле или Монастырь - мы даже не пытаемся искать их в Графе!
                if (region.Type == TerrainType.Field || region.Type == TerrainType.Monastery)
                    continue;

                // В Граф идут только дороги и города
                featuresToCheck.Add(GraphManager.GetFeature(region));
            }

            // Теперь в featuresToCheck лежат только Дороги и Города. 
            // Лишняя проверка if (feature.Type == Field) больше не нужна!
            foreach (var feature in featuresToCheck)
            {
                if (feature.IsCompleted && feature.Meeples.Count > 0)
                {
                    ScoreFeature(feature, allPlayers);
                }
            }

            CheckMonasteries(allPlayers);
        }

        /// <summary>
        /// Проверка на то, окружен ли како-либо монастырь.
        /// </summary>
        /// <param name="allPlayers"></param>
        private void CheckMonasteries(List<Player> allPlayers)
        {
            List<Vector2Int> completedMonasteries = new List<Vector2Int>();

            // Проверяем каждый монастырь на доске
            foreach (var kvp in ActiveMonasteries)
            {
                Vector2Int cloisterPos = kvp.Key;
                TileRegion cloisterRegion = kvp.Value;

                // Считаем соседей в квадрате 3х3 (от -1 до +1 по осям X и Y)
                int surroundingTilesCount = 0;
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (_placedTiles.ContainsKey(cloisterPos + new Vector2Int(x, y)))
                        {
                            surroundingTilesCount++;
                        }
                    }
                }

                // Если вокруг тайла лежат 8 тайлов + 1 сам монастырь = 9
                if (surroundingTilesCount == 9)
                {
                    GlobalFeature feature = GraphManager.GetFeature(cloisterRegion);

                    // Если на монастыре стоит мипл
                    if (feature.Meeples.Count > 0)
                    {
                        Dictionary<int, int> pointsAwarded = new Dictionary<int, int>();
                        List<int> winners = feature.GetWinners();

                        foreach (int winnerId in winners)
                        {
                            Player winner = allPlayers.Find(p => p.Id == winnerId);
                            if (winner != null)
                            {
                                winner.Score += 9; // Ровно 9 очков по правилам!
                                pointsAwarded[winnerId] = 9;
                                if (!IsSimulation) Debug.Log($"Игрок {winner.Id} получает 9 очков за достроенный Монастырь!");
                            }
                        }

                        // Возвращаем мипла
                        foreach (var meeple in feature.Meeples)
                        {
                            Player p = allPlayers.Find(x => x.Id == meeple.Key);
                            if (p != null) p.MeeplesAvailable += meeple.Value;
                        }

                        feature.Meeples.Clear();

                        // Сообщаем визуалу, чтобы снял фигурку со сцены!
                        if (!IsSimulation) OnFeatureCompleted?.Invoke(new List<TileRegion> { cloisterRegion }, pointsAwarded);
                    }

                    // Помечаем монастырь как "обработанный", чтобы не считать его каждый ход
                    completedMonasteries.Add(cloisterPos);
                }
            }

            // Удаляем завершенные монастыри из списка активных
            foreach (var pos in completedMonasteries)
            {
                ActiveMonasteries.Remove(pos);
            }
        }

        /// <summary>
        /// Начислить очки и вернуть миплов
        /// </summary>
        private void ScoreFeature(GlobalFeature feature, List<Player> allPlayers)
        {
            int points = feature.CalculatePoints();
            List<int> winners = feature.GetWinners(); // Получаем ID победителей (у кого больше миплов)
            Dictionary<int, int> pointsAwarded = new Dictionary<int, int>(); // Собираем очки!

            // 1. НАЧИСЛЯЕМ ОЧКИ ПОБЕДИТЕЛЯМ
            foreach (int winnerId in winners)
            {
                // Находим игрока по ID и даем ему очки
                Player winner = allPlayers.Find(p => p.Id == winnerId);
                if (winner != null)
                {
                    winner.Score += points;
                    pointsAwarded[winnerId] = points; // Записываем!
                    // Пишем в лог ТОЛЬКО если это реальная игра
                    if (!IsSimulation)
                    {
                        Debug.Log($"Игрок {winner.Id} получает {points} очков за закрытый {feature.Type}!");
                    }
                }
            }

            // 2. ВОЗВРАЩАЕМ МИПЛОВ ВСЕМ (Даже тем, кто проиграл по большинству)
            foreach (var kvp in feature.Meeples)
            {
                int playerId = kvp.Key;
                int meeplesToReturn = kvp.Value;

                Player player = allPlayers.Find(p => p.Id == playerId);
                if (player != null)
                {
                    player.MeeplesAvailable += meeplesToReturn;
                    if (!IsSimulation)
                    {
                        Debug.Log($"Игрок {player.Id} вернул {meeplesToReturn} миплов в запас. (Стало: {player.MeeplesAvailable})");
                    }
                }
            }

            // 3. ОЧИЩАЕМ МИПЛОВ С ОБЪЕКТА (Чтобы не начислить очки повторно)
            feature.Meeples.Clear();

            // Вызываем событие для Визуала (только в реальной игре)
            if (!IsSimulation)
            {
                // Передаем все кусочки (Regions), из которых состоит закрытая дорога/город
                OnFeatureCompleted?.Invoke(feature.Regions, pointsAwarded);
            }
        }

        /// <summary>
        /// Получить все возможные варианты установки тайла на доску.
        /// </summary>
        public List<Move> GetAllValidMoves(TileData tile)
        {
            List<Move> validMoves = new List<Move>();

            // СПЕЦ-СЛУЧАЙ: Если доска абсолютно пустая (первый ход в игре)
            if (_placedTiles.Count == 0)
            {
                // Можно поставить только в центр (0,0), но можно покрутить 4 раза
                for (int r = 0; r < 4; r++)
                {
                    validMoves.Add(new Move(Vector2Int.zero, r));
                }
                return validMoves;
            }

            // 1. НАХОДИМ ВСЕ ПУСТЫЕ СОСЕДНИЕ КЛЕТКИ
            HashSet<Vector2Int> emptyAdjacentSpots = new HashSet<Vector2Int>();

            foreach (Vector2Int placedPos in _placedTiles.Keys)
            {
                for (int i = 0; i < 4; i++) // Проверяем 4 направления вокруг каждого тайла
                {
                    Vector2Int neighborPos = GetNeighborPos(placedPos, (Direction)i);

                    // Если там нет тайла, значит это потенциальное место для хода
                    if (!_placedTiles.ContainsKey(neighborPos))
                    {
                        emptyAdjacentSpots.Add(neighborPos);
                    }
                }
            }

            // Запоминаем текущий поворот тайла, чтобы не сломать его после проверок
            int originalRotation = tile.Rotation;

            // 2. ПРИМЕРЯЕМ ТАЙЛ ВО ВСЕ ПУСТЫЕ КЛЕТКИ СО ВСЕМИ ПОВОРОТАМИ
            foreach (Vector2Int spot in emptyAdjacentSpots)
            {
                for (int r = 0; r < 4; r++)
                {
                    tile.SetRotation(r); // Крутим тайл в памяти

                    // Если правила соблюдаются - это легальный ход!
                    if (CanPlaceTile(tile, spot))
                    {
                        validMoves.Add(new Move(spot, r));
                    }
                }
            }

            // Восстанавливаем оригинальный поворот тайла
            tile.SetRotation(originalRotation);

            return validMoves;
        }

        /// <summary>
        /// Создать независимую копию доски для симуляций в уме
        /// </summary>
        public Board Clone()
        {
            Board copy = new Board();

            // Копия всегда является симуляцией
            copy.IsSimulation = true;

            // Копируем словарь выложенных тайлов. 
            // Сами тайлы на доске клонировать не нужно, так как их состояние (поворот) больше не меняется.
            copy._placedTiles = new Dictionary<Vector2Int, TileData>(this._placedTiles);

            // Копируем графы дорог и городов
            copy.GraphManager = this.GraphManager.Clone();

            // Все монастыри
            copy.ActiveMonasteries = new Dictionary<Vector2Int, TileRegion>(this.ActiveMonasteries);

            return copy;
        }

        /// <summary>
        /// Вызывается в самом конце игры для подсчета очков за Поля (Крестьян)
        /// </summary>
        public void ScoreEndGameFields(List<Player> allPlayers)
        {
            HashSet<GlobalFeature> allFeatures = GraphManager.GetAllFeatures();

            foreach (var feature in allFeatures)
            {
                // Если там нет миплов - пропускаем (очки давать некому)
                if (feature.Meeples.Count == 0) continue;

                // 1. СЧИТАЕМ ПОЛЯ (Как было)
                if (feature.Type == TerrainType.Field)
                {
                    ScoreSingleField(feature, allPlayers);
                }

                // 2. НОВОЕ: СЧИТАЕМ НЕДОСТРОЕННЫЕ ДОРОГИ И ГОРОДА
                else if (!feature.IsCompleted)
                {
                    int points = 0;
                    int uniqueTilesCount = feature.Regions.Select(r => r.ParentTile).Distinct().Count();

                    if (feature.Type == TerrainType.Road)
                        points = uniqueTilesCount * 1; // Недостроенная дорога: 1 очко за тайл
                    else if (feature.Type == TerrainType.City)
                        points = (uniqueTilesCount * 1) + (feature.Regions.Count(r => r.HasShield) * 1); // Недостроенный город: по 1 очку (а не 2) за тайл и щит!

                    GivePointsToWinners(feature, allPlayers, points, "Недострой");
                }
            }

            // 3. НОВОЕ: СЧИТАЕМ НЕДОСТРОЕННЫЕ МОНАСТЫРИ
            foreach (var kvp in ActiveMonasteries)
            {
                GlobalFeature feature = GraphManager.GetFeature(kvp.Value);
                if (feature.Meeples.Count > 0)
                {
                    int points = 1; // Сам монастырь
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            if ((x != 0 || y != 0) && GetTileAt(kvp.Key + new Vector2Int(x, y)) != null)
                                points++; // +1 за каждый тайл вокруг
                        }
                    }

                    // Используем универсальный метод для подсчёта очков
                    GivePointsToWinners(feature, allPlayers, points, "Недостроенный Монастырь");
                }
            }
        }

        // Вспомогательный метод раздачи очков
        private void GivePointsToWinners(GlobalFeature feature, List<Player> allPlayers, int points, string reason)
        {
            if (points <= 0) return;

            List<int> winners = feature.GetWinners();

            // НОВОЕ: Собираем словарь для отправки в UI
            Dictionary<int, int> pointsAwarded = new Dictionary<int, int>();

            foreach (int winnerId in winners)
            {
                Player winner = allPlayers.Find(p => p.Id == winnerId);
                if (winner != null)
                {
                    winner.Score += points;
                    pointsAwarded[winnerId] = points; // Запоминаем очки

                    if (!IsSimulation) Debug.Log($"КОНЕЦ ИГРЫ: Игрок {winner.Id} получает {points} очков за {reason}!");
                }
            }

            // Запускаем событие, чтобы циферки красиво улетели вверх
            // (Даже в конце игры мы хотим показать игрокам, за что они получили очки)
            if (!IsSimulation && pointsAwarded.Count > 0)
            {
                OnFeatureCompleted?.Invoke(feature.Regions, pointsAwarded);
            }
        }

        private void ScoreSingleField(GlobalFeature field, List<Player> allPlayers)
        {
            // Сет для хранения уникальных завершенных городов (чтобы не посчитать один город дважды)
            HashSet<GlobalFeature> adjacentCompletedCities = new HashSet<GlobalFeature>();

            // 1. Перебираем все маленькие кусочки поля, из которых состоит это огромное поле
            foreach (TileRegion fieldRegion in field.Regions)
            {
                // 2. Смотрим, с какими кусочками он соприкасается ВНУТРИ своего тайла
                foreach (TileRegion adjacentRegion in fieldRegion.AdjacentRegions)
                {
                    // Если сосед - это город...
                    if (adjacentRegion.Type == TerrainType.City)
                    {
                        GlobalFeature cityFeature = GraphManager.GetFeature(adjacentRegion);

                        // И если этот город ЗАВЕРШЕН (достроен)
                        if (cityFeature.IsCompleted)
                        {
                            adjacentCompletedCities.Add(cityFeature); // Добавляем в список
                        }
                    }
                }
            }

            // 3. СЧИТАЕМ ОЧКИ: 3 очка за каждый достроенный город, касающийся этого поля!
            int points = adjacentCompletedCities.Count * 3;
            if (points == 0) return;

            // 4. Кто получает очки? Тот, у кого больше миплов на этом поле!
            List<int> winners = field.GetWinners();

            // НОВОЕ: Собираем словарь
            Dictionary<int, int> pointsAwarded = new Dictionary<int, int>();

            foreach (int winnerId in winners)
            {
                Player winner = allPlayers.Find(p => p.Id == winnerId);
                if (winner != null)
                {
                    winner.Score += points;
                    pointsAwarded[winnerId] = points; // Запоминаем

                    if (!IsSimulation)
                    {
                        Debug.Log($"КОНЕЦ ИГРЫ: Игрок {winner.Id} получает {points} очков за Ферму! (Городов: {adjacentCompletedCities.Count})");
                    }
                }

                // НОВОЕ: Запускаем событие
                if (!IsSimulation && pointsAwarded.Count > 0)
                {
                    OnFeatureCompleted?.Invoke(field.Regions, pointsAwarded);
                }
            }
        }

        /// <summary>
        /// Получить тайл по координатам (возвращает null, если пусто)
        /// </summary>
        public TileData GetTileAt(Vector2Int pos)
        {
            if (_placedTiles.TryGetValue(pos, out TileData tile))
            {
                return tile;
            }
            return null;
        }

        /// <summary>
        /// Возвращает все выложенные тайлы на доске (для сетевой синхронизации)
        /// </summary>
        public Dictionary<Vector2Int, TileData> GetAllPlacedTiles()
        {
            return _placedTiles;
        }

        /// <summary>
        /// Принудительно снимает мипла с указанного региона (используется для ручного возврата Аббата)
        /// </summary>
        public void RemoveMeeple(Player player, TileRegion region)
        {
            GlobalFeature feature = GraphManager.GetFeature(region);

            // Удаляем мипла этого игрока из глобального объекта
            if (feature.Meeples.ContainsKey(player.Id))
            {
                feature.Meeples.Remove(player.Id);

                if (!IsSimulation)
                {
                    Debug.Log($"Игрок {player.Id} забрал фигурку с {feature.Type}");
                }
            }
        }
    }
}