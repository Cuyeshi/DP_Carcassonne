using Assets.Scripts.Core_logic;
using Assets.Scripts.Core_logic.AI;
using Assets.Scripts.Main;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.View
{
    public class BotMatchManager : MonoBehaviour
    {
        [Header("Настройки")]
        public float tileSize = 1.0f;
        public List<TileBinding> tilePrefabs;
        [Tooltip("Ссылка на визуализатор бота 1")] public BotBrainVisualizer bot1Visualizer; // Верхний бот
        [Tooltip("Ссылка на визуализатор бота 2")] public BotBrainVisualizer bot2Visualizer; // Нижний бот

        [Header("Prefabs")]
        public GameObject defaultPrefab;
        public GameObject meeplePrefab;
        public GameObject abbotPrefab;

        [Header("UI Game Over")]
        public GameObject gameOverPanel;
        public TextMeshProUGUI winnerText;

        [Header("Настройки визуальных элементов")]
        [Tooltip("Панель статистики бота 1")] public GameObject topPanel;
        [Tooltip("Панель статистики бота 2")] public GameObject bottomPanel;
        [Tooltip("Текст статистики бота 1")] public TextMeshProUGUI bot1StatsText;
        [Tooltip("Текст статистики бота 2")] public TextMeshProUGUI bot2StatsText;

        [Header("VFX")]
        public GameObject tileDustVFX;
        public GameObject meepleDustVFX;
        public GameObject floatingScoreTextPrefab; // Для вылетающих очков

        // ЯДРО
        private Board _board;
        private DeckManager _deck;
        private MCTSBot _bot1;
        private MCTSBot _bot2;
        private Player _player1;
        private Player _player2;

        private Color _colorBot1 = new Color(0.2f, 0.4f, 0.8f); // Синий
        private Color _colorBot2 = new Color(0.8f, 0.2f, 0.2f); // Красный

        private RectTransform _rectTransformTop;
        private RectTransform _rectTransformBottom;
        private RectTransform _rectTransformGameOverPanel;

        // Словарь для хранения фигурок на столе (чтобы удалять их при завершении объектов)
        private Dictionary<TileRegion, GameObject> _boardMeeples = new Dictionary<TileRegion, GameObject>();

        private bool _isBot1Turn = true;
        private bool _isGameOver = false;

        void Start()
        {
            if (topPanel != null)
            {
                _rectTransformTop = topPanel.GetComponentInChildren<RectTransform>();
                _rectTransformTop.DOAnchorPos(new Vector2(-431.6f, 268f), 1f).SetEase(Ease.OutCubic).SetDelay(2.5f);
            }

            if (bottomPanel != null)
            {
                _rectTransformBottom = bottomPanel.GetComponentInChildren<RectTransform>();
                _rectTransformBottom.DOAnchorPos(new Vector2(-431.6f, -262.7f), 1f).SetEase(Ease.OutCubic).SetDelay(3f);
            }

            if (gameOverPanel != null)
                _rectTransformGameOverPanel = gameOverPanel.GetComponentInChildren<RectTransform>();

            _board = new Board();
            // Подписываемся на закрытие объектов (чтобы убирать миплов и показывать +очки)
            _board.OnFeatureCompleted += HandleFeatureCompleted;

            _deck = new DeckManager();

            _bot1 = new MCTSBot { Difficulty = BotDifficulty.Hard };
            _bot2 = new MCTSBot { Difficulty = BotDifficulty.Hard };

            _player1 = new Player(1);
            _player2 = new Player(2);

            TileData startTile = _deck.CreateClassicStartTile();
            _board.PlaceTile(startTile, Vector2Int.zero);
            SpawnTileVisually(startTile, Vector2Int.zero);

            if (bot1Visualizer != null) bot1Visualizer.Initialize(_bot1.Telemetry);
            if (bot2Visualizer != null) bot2Visualizer.Initialize(_bot2.Telemetry);

            StartCoroutine(BotBattleLoop());
        }

        void Update()
        {
            UpdateStatsText(bot1StatsText, _bot1.Telemetry, _player1, "Синий Бот", _colorBot1);
            UpdateStatsText(bot2StatsText, _bot2.Telemetry, _player2, "Красный Бот", _colorBot2);
        }

        private IEnumerator BotBattleLoop()
        {
            yield return new WaitForSeconds(5f);

            while (!_isGameOver)
            {
                TileData currentTile = _deck.DrawTile();
                if (currentTile == null)
                {
                    Debug.Log("КОЛОДА ПУСТА! КОНЕЦ БОЯ!");
                    _board.ScoreEndGameFields(new List<Player> { _player1, _player2 });
                    _isGameOver = true;

                    _rectTransformTop.DOAnchorPos(new Vector2(471.6f, 268f), 1f).SetEase(Ease.OutCubic);
                    _rectTransformBottom.DOAnchorPos(new Vector2(471.6f, 268f), 1f).SetEase(Ease.OutCubic);

                    // === НОВОЕ: ВЫВОД ФИНАЛЬНОГО СЧЕТА ===
                    if (gameOverPanel != null)
                    {
                        gameOverPanel.SetActive(true);
                        _rectTransformGameOverPanel.DOAnchorPos(new Vector2(0, -260f), 1f).SetEase(Ease.OutCubic);
                    };

                    if (winnerText != null)
                    {
                        string result = "<b>ФИНАЛЬНЫЙ СЧЕТ:</b>\n\n";
                        if (_player1.Score > _player2.Score) result += $"<color=#3b699e>ПОБЕДИЛ СИНИЙ БОТ!</color>\n";
                        else if (_player2.Score > _player1.Score) result += $"<color=#8b3a3a>ПОБЕДИЛ КРАСНЫЙ БОТ!</color>\n";
                        else result += "НИЧЬЯ!\n";

                        result += $"\nСиний Бот: {_player1.Score} очков";
                        result += $"\nКрасный Бот: {_player2.Score} очков";
                        winnerText.text = result;
                    }
                    break;
                }

                if (_board.GetAllValidMoves(currentTile).Count == 0) continue;

                MCTSBot activeBot = _isBot1Turn ? _bot1 : _bot2;
                Player activePlayer = _isBot1Turn ? _player1 : _player2;
                Player opponent = _isBot1Turn ? _player2 : _player1;
                Color activeColor = _isBot1Turn ? _colorBot1 : _colorBot2;

                // Запускаем фоновые раздумья
                Player copyPlayer = activePlayer.Clone();
                Task<Move> thinkTask = Task.Run(() => activeBot.FindBestMove(_board, currentTile, _deck.GetDeckCopy(), copyPlayer, opponent));

                yield return new WaitUntil(() => thinkTask.IsCompleted);

                Move bestMove = thinkTask.Result;

                if (bestMove != null)
                {
                    // 1. СТАВИМ ТАЙЛ
                    currentTile.SetRotation(bestMove.Rotation);
                    _board.PlaceTile(currentTile, bestMove.Position);
                    SpawnTileVisually(currentTile, bestMove.Position);

                    yield return new WaitForSeconds(0.4f);

                    // 2. ДЕЙСТВИЯ С ФИГУРКАМИ
                    if (bestMove.RetrieveAbbot)
                    {
                        // БОТ СНИМАЕТ АББАТА
                        RetrieveAbbot(activePlayer);
                    }
                    else if (bestMove.MeepleRegionId != -1)
                    {
                        // БОТ СТАВИТ ФИГУРКУ
                        TileRegion targetRegion = currentTile.Regions.Find(r => r.Id == bestMove.MeepleRegionId);

                        // Временный "обман" Ядра для проверки аббата
                        if (bestMove.MeepleType == 1) activePlayer.MeeplesAvailable++;

                        if (targetRegion != null && _board.CanPlaceMeeple(activePlayer, targetRegion))
                        {
                            _board.PlaceMeeple(activePlayer, targetRegion);

                            if (bestMove.MeepleType == 1)
                            {
                                activePlayer.HasAbbot = false;
                                activePlayer.MeeplesAvailable++; // Возвращаем счетчик в норму
                            }

                            // Визуал спавна
                            Vector3 tileWorldPos = new Vector3(bestMove.Position.x * tileSize, 0, bestMove.Position.y * tileSize);
                            Vector3 localPos = GetRegionLocalPositionFromPrefab(currentTile.Id, targetRegion.Id);

                            GameObject tilePrefab = GetPrefabForTile(currentTile.Id);
                            float baseRotY = tilePrefab.transform.rotation.eulerAngles.y;
                            Quaternion tileRotation = Quaternion.Euler(0, baseRotY + (currentTile.Rotation * 90f), 0);
                            localPos = tileRotation * localPos;

                            Vector3 worldPos = tileWorldPos + localPos + new Vector3(0, -0.2f, 0);
                            Quaternion meepleRot = CalculateMeepleRotation(currentTile, targetRegion);

                            if (targetRegion.Type == TerrainType.Field) worldPos -= new Vector3(0, -0.2f, 0);

                            GameObject prefabToSpawn = (bestMove.MeepleType == 1) ? abbotPrefab : meeplePrefab;
                            GameObject meepleObj = Instantiate(prefabToSpawn, worldPos, meepleRot);

                            // КРАСИМ ФИГУРКУ
                            Renderer[] renderers = meepleObj.GetComponentsInChildren<Renderer>();
                            foreach (var r in renderers) 
                                r.material.color = activeColor;

                            _boardMeeples[targetRegion] = meepleObj;

                            if (meepleDustVFX) Destroy(Instantiate(meepleDustVFX, worldPos, Quaternion.identity), 2f);
                        }
                    }

                    // 3. ПОДСЧЕТ ОЧКОВ
                    _board.CheckAndScoreCompletedFeatures(currentTile, new List<Player> { _player1, _player2 });
                }

                System.GC.Collect(); // Очищаем мусор после раздумий MCTS

                yield return new WaitForSeconds(1.0f);
                _isBot1Turn = !_isBot1Turn;
            }
        }

        // ==========================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ЛОГИКИ
        // ==========================================

        private void RetrieveAbbot(Player player)
        {
            Vector2Int? foundPos = null;
            TileRegion foundRegion = null;

            foreach (var kvp in _board.ActiveMonasteries)
            {
                GlobalFeature feature = _board.GraphManager.GetFeature(kvp.Value);
                if (feature.Meeples.ContainsKey(player.Id))
                {
                    foundPos = kvp.Key;
                    foundRegion = kvp.Value;
                    break;
                }
            }

            if (foundPos.HasValue)
            {
                int points = 1;
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        if ((x != 0 || y != 0) && _board.GetTileAt(foundPos.Value + new Vector2Int(x, y)) != null) points++;

                player.Score += points;
                player.HasAbbot = true;

                GlobalFeature feature = _board.GraphManager.GetFeature(foundRegion);
                feature.Meeples.Remove(player.Id);

                if (_boardMeeples.TryGetValue(foundRegion, out GameObject meepleObj))
                {
                    ShowFloatingText(meepleObj.transform.position, points, player.Id == 1 ? _colorBot1 : _colorBot2);
                    Destroy(meepleObj);
                    _boardMeeples.Remove(foundRegion);
                }
            }
        }

        private void HandleFeatureCompleted(List<TileRegion> completedRegions, Dictionary<int, int> pointsAwarded)
        {
            foreach (var region in completedRegions)
            {
                if (_boardMeeples.TryGetValue(region, out GameObject meepleObj))
                {
                    // Ищем, сколько очков дать (берем первые попавшиеся для простоты)
                    int points = 0;
                    Color color = Color.white;
                    foreach (var kvp in pointsAwarded)
                    {
                        points = kvp.Value;
                        color = (kvp.Key == 1) ? _colorBot1 : _colorBot2;
                        break;
                    }

                    ShowFloatingText(meepleObj.transform.position, points, color);
                    Destroy(meepleObj);
                    _boardMeeples.Remove(region);
                }
            }
        }

        private void ShowFloatingText(Vector3 pos, int points, Color color)
        {
            if (floatingScoreTextPrefab != null && points > 0)
            {
                GameObject textObj = Instantiate(floatingScoreTextPrefab, pos + Vector3.up, Camera.main.transform.rotation);
                var tmp = textObj.GetComponentInChildren<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = $"+{points}";
                    tmp.color = color;
                }
                // Простая анимация через скрипт (без DOTween для надежности)
                StartCoroutine(FloatTextRoutine(textObj));
            }
        }

        private IEnumerator FloatTextRoutine(GameObject obj)
        {
            float elapsed = 0f;
            Vector3 startPos = obj.transform.position;
            var tmp = obj.GetComponentInChildren<TextMeshPro>();

            while (elapsed < 1.5f && obj != null)
            {
                obj.transform.position = startPos + new Vector3(0, elapsed * 2f, 0);
                if (tmp != null) tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f - (elapsed / 1.5f));
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (obj != null) Destroy(obj);
        }

        // ==========================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ВИЗУАЛА
        // ==========================================

        private void UpdateStatsText(TextMeshProUGUI tmp, AITelemetry tel, Player p, string name, Color c)
        {
            if (tmp == null) return;
            string hex = ColorUtility.ToHtmlStringRGB(c);
            string status = tel.IsThinking ? "<color=green>СЧИТАЕТ...</color>" : "<color=red>Ожидает</color>";
            string abbotStatus = p.HasAbbot ? "В руке" : "На столе";

            tmp.text = $"<color=#{hex}><b>{name}</b></color>\nСчет: {p.Score}\nМиплы: {p.MeeplesAvailable} | Аббат: {abbotStatus}\nСтатус: {status}\nСимуляций: {tel.TotalSimulationsCompleted}\nШанс победы: {(tel.BestMoveWinRate * 100):F1}%";
        }

        private void SpawnTileVisually(TileData data, Vector2Int pos)
        {
            Vector3 worldPos = new Vector3(pos.x * tileSize, 0, pos.y * tileSize);
            GameObject prefab = GetPrefabForTile(data.Id);

            GameObject tileObj = Instantiate(prefab, worldPos, prefab.transform.rotation);

            // ИСПРАВЛЕНИЕ: Берем оригинальный поворот префаба и прибавляем к нему математический поворот
            float yRotation = data.Rotation * 90f;
            Vector3 currentEuler = tileObj.transform.rotation.eulerAngles;
            tileObj.transform.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y + yRotation, currentEuler.z);

            if (tileDustVFX) Destroy(Instantiate(tileDustVFX, worldPos, Quaternion.identity), 2f);
        }

        private GameObject GetPrefabForTile(string tileId)
        {
            foreach (var binding in tilePrefabs)
                if (tileId.StartsWith(binding.tileName)) return binding.prefab;
            return defaultPrefab;
        }

        /// <summary>
        /// Используется ТОЛЬКО БОТАМИ для нахождения идеальной точки спавна
        /// </summary>
        private Vector3 GetRegionLocalPositionFromPrefab(string tileId, int regionId)
        {
            GameObject prefab = GetPrefabForTile(tileId);
            if (prefab != null)
            {
                // ИЩЕМ СПЕЦИАЛЬНЫЙ ЯКОРЬ (Пустой GameObject с RegionAnchor)
                RegionAnchor[] anchors = prefab.GetComponentsInChildren<RegionAnchor>(true);
                foreach (var anchor in anchors)
                {
                    if (anchor.regionId == regionId)
                    {
                        // Вычисляем точную позицию якоря относительно КОРНЯ префаба
                        Vector3 localPos = prefab.transform.InverseTransformPoint(anchor.transform.position);

                        // Учитываем масштаб префаба (если он не равен 1,1,1)
                        localPos.Scale(prefab.transform.localScale);

                        return localPos;
                    }
                }
            }

            Debug.LogWarning($"[BotArena] На префабе тайла {tileId} не найден ЯКОРЬ (RegionAnchor) с ID {regionId}!");

            // Резервный вариант, если якорь забыли поставить: ставим в центр (0,0,0)
            return Vector3.zero;
        }

        private Quaternion CalculateMeepleRotation(TileData tile, TileRegion region)
        {
            System.Random rnd = new();

            // 1. Лежачие миплы (Крестьяне на полях)
            if (region.Type == TerrainType.Field)
                return Quaternion.Euler(-90, (float)rnd.Next(-20, 20), 0);

            // 2. Центральные миплы (Монахи и т.д.)
            if (tile.CenterRegion == region)
                return Quaternion.Euler(0, (float)rnd.Next(-20, 20), 0);

            // 3. Собираем все направления (грани), на которые выходит этот регион
            List<Direction> connectedDirections = new List<Direction>();

            for (int i = 0; i < 4; i++)
            {
                Direction dir = (Direction)i;
                TileEdge edge = tile.GetEdge(dir);

                // Если регион касается этой грани (слева, по центру или справа)
                if (edge.Center == region || edge.Left == region || edge.Right == region)
                {
                    connectedDirections.Add(dir);
                }
            }

            // 4. Если регион выходит ровно на ДВЕ грани - это может быть поворот или прямая!
            if (connectedDirections.Count == 2)
            {
                int dir1 = (int)connectedDirections[0]; // Например, North (0)
                int dir2 = (int)connectedDirections[1]; // Например, West (3)

                // Проверяем, являются ли эти грани СОСЕДНИМИ (то есть разница между ними 1 или 3)
                // Если разница 2 (например, Север(0) и Юг(2)) - это ПРЯМАЯ дорога, угол усреднять не нужно.
                int diff = Mathf.Abs(dir1 - dir2);
                if (diff == 1 || diff == 3)
                {
                    // Это поворот! Вычисляем биссектрису (угол в 45 градусов).
                    float angle1 = dir1 * 90f;
                    float angle2 = dir2 * 90f;

                    // Особый случай: переход от Запада (270°) к Северу (0°).
                    // Чтобы среднее не стало 135° (Юго-Восток), мы считаем Север как 360°.
                    if ((dir1 == 0 && dir2 == 3) || (dir1 == 3 && dir2 == 0))
                    {
                        if (angle1 == 0f) angle1 = 360f;
                        if (angle2 == 0f) angle2 = 360f;
                    }

                    float averageAngle = (angle1 + angle2) / 2f + 90f + (float)rnd.Next(-20, 20); // Добавление 90f для корректного отображения префаба
                    return Quaternion.Euler(0, averageAngle, 0);
                }
            }

            // 5. Если это не поворот (например, город на одну грань, или прямая дорога),
            // просто берем ПЕРВОЕ попавшееся направление.
            if (connectedDirections.Count > 0)
            {
                float yRotation = (int)connectedDirections[0] * 90f + (float)rnd.Next(-20, 20);
                return Quaternion.Euler(0, yRotation, 0);
            }

            // Резервный вариант, если что-то пошло не так
            return Quaternion.Euler(0, (float)rnd.Next(-20, 20), 0);
        }
    }
}