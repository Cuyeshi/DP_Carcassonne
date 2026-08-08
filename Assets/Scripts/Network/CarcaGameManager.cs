using Assets.Scripts.Core_logic;
using Assets.Scripts.Core_logic.AI;
using Assets.Scripts.Media;
using Assets.Scripts.Network;
using Assets.Scripts.View;
using DG.Tweening;
using Mirror;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Main
{
    /// <summary>
    /// Глобальный менеджер игрового процесса "Каркассон".
    /// Отвечает за состояние игры на сервере, симуляции ИИ (MCTS), сетевую синхронизацию и визуализацию на клиентах.
    /// </summary>
    public class CarcaGameManager : NetworkBehaviour
    {
        /// <summary>
        /// Событие, вызываемое на клиентах для принудительного обновления всех элементов интерфейса.
        /// </summary>
        public static event Action OnUIUpdateRequired;
        private CarcaNetworkManager _netManager;

        [Header("Game Settings")]
        [Tooltip("Размер одного игрового тайла в метрах Unity")] 
        [SerializeField] private float _tileSize = 1.0f;
        /// <summary>
        /// Текущий режим игры (с ботом или только люди). Изменяется только сервером.
        /// </summary>
        [SyncVar] public GameMode currentMode = GameMode.PlayersAndBot; // По умолчанию с ботом
        /// <summary>
        /// Текущая сложность ИИ-бота. Изменяется только сервером.
        /// </summary>
        [SyncVar] public BotDifficulty botDifficulty = BotDifficulty.Medium;

        [Header("Prefabs")]
        [SerializeField] private GameObject _defaultPrefab;
        [SerializeField] private List<TileBinding> _tilePrefabs;
        [SerializeField] private GameObject _meeplePrefab;
        [SerializeField] private GameObject _abbotPrefab;

        [Header("VFX Prefabs")]
        [SerializeField] private GameObject _floatingScoreTextPrefab;
        [SerializeField] private GameObject _tileDustVFX;
        public GameObject meepleDustVFX;




        // --- СЕРВЕРНЫЕ ДАННЫЕ ---
        private Board _serverBoard;
        private DeckManager _serverDeck;
        private TileData _serverCurrentTile;
        private MCTSBot _aiBot;
        private Player _botLogicPlayer; // Логический бот




        // --- НАСТРОЙКИ БОТА ---
        /// <summary>
        /// Текущий счет ИИ-бота на сервере. Синхронизируется с клиентами.
        /// </summary>
        [SyncVar(hook = nameof(OnStateChangedInt))]
        public int BotScore = 0;

        /// <summary>
        /// Количество доступных миплов у ИИ-бота. Синхронизируется с клиентами.
        /// </summary>
        [SyncVar(hook = nameof(OnStateChangedInt))]
        public int BotMeeples = 7;

        /// <summary>
        /// Наличие фигурки Аббата у ИИ-бота. Синхронизируется с клиентами.
        /// </summary>
        [SyncVar(hook = nameof(OnStateChangedBool))]
        public bool BotHasAbbot = true;

        // Словарь для хранения заспавненных миплов (чтобы удалять их по сети)
        private Dictionary<TileRegion, GameObject> _serverMeeples = new Dictionary<TileRegion, GameObject>();




        // --- СИНХРОНИЗИРУЕМЫЕ ДАННЫЕ ---
        /// <summary>
        /// Флаг старта игры. Если true — игроки находятся на игровом поле, если false — в лобби.
        /// </summary>
        [SyncVar(hook = nameof(OnStateChangedBool))] 
        public bool isGameStarted = false;

        /// <summary>
        /// Токен игрока, чей сейчас ход. Синхронизируется со всеми клиентами.
        /// </summary>
        [SyncVar(hook = nameof(OnStateChangedString))]
        public string currentTurnPlayerToken = "";

        /// <summary>
        /// Текущая фаза хода: 0 — укладка тайла, 1 — установка/возврат мипла.
        /// </summary>
        [SyncVar] public int currentPhase = 0;

        /// <summary>
        /// ID тайла, который сейчас находится в руке активного игрока.
        /// </summary>
        [SyncVar(hook = nameof(OnCurrentTileIdChanged))] 
        public string currentTileId = "";

        private int _currentPlayerServerIndex = 0;
        private TileData _serverLastPlacedTile;
        private Vector2Int _serverLastPlacedPos;




        // --- ТЕЛЕМЕТРИЯ БОТА ---
        /// <summary>
        /// Флаг активности вычислений бота. Если true — бот в фоне симулирует матчи.
        /// </summary>
        [SyncVar] public bool IsBotThinking = false;




        // --- КЛИЕНТСКИЕ ДАННЫЕ (отображение) ---
        /// <summary>
        /// Локальная копия доски для предсказаний и проверок зеленой/красной голограммы.
        /// </summary>
        public Board LocalBoard { get; private set; }

        /// <summary>
        /// Структура тайла, который локальный игрок сейчас вращает в руке.
        /// </summary>
        public TileData LocalTileInHand { get; private set; }

        /// <summary>
        /// Ссылка на 3D-объект голограммы на сцене клиента.
        /// </summary>
        public GameObject GhostTile { get; private set; }
        private Renderer[] _ghostRenderers;
        private List<GameObject> _localActiveTargets = new List<GameObject>();

        /// <summary>
        /// Локальные координаты тайла, который ставит активный игрок.
        /// </summary>
        public Vector2Int ClientActiveTilePos { get; private set; }

        /// <summary>
        /// Локальный поворот (0, 1, 2, 3) тайла в руке клиента.
        /// </summary>
        public int ClientActiveTileRotation { get; private set; }

        /// <summary>
        /// Массив ID регионов, куда сервер разрешил поставить мипла в текущий ход.
        /// </summary>
        public int[] LocalValidRegionIds { get; private set; } = new int[0];




        // --- ДАННЫЕ ЛОББИ ---
        /// <summary>
        /// Синхронизируемый список подключенных в лобби игроков для отрисовки карточек.
        /// </summary>
        public readonly SyncList<LobbyPlayerData> LobbyPlayers = new SyncList<LobbyPlayerData>();

        // Для оптимизации
        private Vector2Int _lastHoveredGridPos = new Vector2Int(-999, -999);
        private float _statUpdateTimer = 0f;

        // Переменная, которая хранит координаты, куда Голограмма ДОЛЖНА приехать
        private Vector3 _ghostTargetWorldPos;

        [SyncVar] public bool isGameFinished = false;




        // --- Гетеры ---
        #region Геттеры префабов (Инкапсуляция)
        /// <summary>
        /// Публичный геттер для размера тайла.
        /// </summary>
        public float TileSize => _tileSize;
        /// <summary>
        /// Публичный доступ к боту для локального визуализатора Хоста
        /// </summary>
        public MCTSBot AIBotInstance => _aiBot;
        public GameObject DefaultPrefab => _defaultPrefab;
        public List<TileBinding> TilePrefabs => _tilePrefabs;
        public GameObject MeeplePrefab => _meeplePrefab;
        public GameObject AbbotPrefab => _abbotPrefab;
        public GameObject FloatingScoreTextPrefab => _floatingScoreTextPrefab;
        public GameObject TileDustVFX => _tileDustVFX;
        //public GameObject MeepleDustVFX => _meepleDustVFX;
        #endregion






        private void Awake()
        {
            Application.targetFrameRate = 100;
        }

        [ServerCallback] // Метод Update выполняется только на сервере (заменяет `if (isServer)`)
        void Update()
        {
            if (isGameStarted)
            {
                // 1. ПРОВЕРКА ОТКЛЮЧЕННЫХ (Таймеры)
                int activeHumanPlayersCount = 0;
                //List<string> sessionsToRemove = new List<string>();

                foreach (var kvp in _netManager.PlayerSessions)
                {
                    PlayerSessionData session = kvp.Value;

                    if (session.IsDead) continue; // Мертвых не считаем и таймер им не крутим

                    if (session.IsDisconnected)
                    {
                        session.DisconnectTimer -= Time.deltaTime;
                        if (session.DisconnectTimer <= 0f)
                        {
                            // ИСПРАВЛЕНИЕ: Убиваем сессию, но оставляем в словаре
                            session.IsDead = true;
                            _netManager.ReturnColorToPool(kvp.Key);
                            Debug.Log($"[СЕРВЕР] Игрок {kvp.Key} не переподключился вовремя. Сессия мертва.");
                        }
                    }
                    else
                    {
                        activeHumanPlayersCount++; // Считаем живых
                    }
                }

                // 2. ПРОВЕРКА "ОСТАЛСЯ ЛИШЬ ОДИН"
                // Если мы играем без бота, и остался 1 или 0 человек - завершаем игру.
                // Если играем с ботом, и осталось 0 людей - завершаем игру.
                if (currentMode == GameMode.PlayersOnly && activeHumanPlayersCount <= 1)
                {
                    ServerForceEndGame("Недостаточно игроков для продолжения!");
                }
                else if (currentMode == GameMode.PlayersAndBot && activeHumanPlayersCount == 0)
                {
                    ServerForceEndGame("Все игроки покинули игру. Бот победил досрочно!");
                }
            }

            // Больше не гоняем массивы по сети.
            // Просто раз в секунду просим локальный UI хоста обновить текстовую статистику.
            if (IsBotThinking)
            {
                _statUpdateTimer += Time.deltaTime;
                if (_statUpdateTimer >= 0.5f) // Можно обновлять пореже, раз в полсекунды
                {
                    _statUpdateTimer = 0f;
                    // Вызываем обновление UI только на Хосте (так как мы в ServerCallback)
                    RequestUIUpdate();
                }
            }
        }

        /// <summary>
        /// Инициализация сервера. Создание логического Ядра и колоды.
        /// </summary>
        public override void OnStartServer()
        {
            _serverBoard = new Board();
            // Подписываем СЕРВЕР на событие закрытия городов/дорог
            _serverBoard.OnFeatureCompleted += ServerHandleFeatureCompleted;
            _serverDeck = new DeckManager();

            // Читаем режим из меню (0 = PlayersOnly, 1 = PlayersAndBot)
            int savedMode = PlayerPrefs.GetInt("SelectedGameMode", 1);
            currentMode = (GameMode)savedMode;

            // НОВОЕ: Читаем сложность
            int savedDiff = PlayerPrefs.GetInt("SelectedBotDifficulty", 1);
            botDifficulty = (BotDifficulty)savedDiff;

            // Если режим с ботом - создаем бота
            if (currentMode == GameMode.PlayersAndBot)
            {
                _aiBot = new MCTSBot();
                _aiBot.Difficulty = botDifficulty; // Передаем сложность в мозг
                _botLogicPlayer = new Player(999); // ID бота = 999
            }

            _netManager = NetworkManager.singleton as CarcaNetworkManager;

            RequestUIUpdate(); // Отрисуем UI при входе

        }

        /// <summary>
        /// Инициализация сетевого клиента. Нахождение 3D визуализатора и создание локальной доски.
        /// </summary>
        public override void OnStartClient()
        {
            LocalBoard = new Board();

            // НОВОЕ: Ищем визуализатор ОДИН РАЗ и передаем ему себя (this)
            AIBrainVisualizer brainVis = FindFirstObjectByType<AIBrainVisualizer>();
            if (brainVis != null)
            {
                brainVis.Initialize(this);
            }

            // Подписываемся на изменения списка лобби (если кто-то зашел или нажал "Готов")
            LobbyPlayers.Callback += OnLobbyPlayersChanged;

            RequestUIUpdate(); // Отрисуем UI при входе

            Debug.Log("[КЛИЕНТ] Локальная доска создана. Визуализатор инициализирован.");
        }

        void OnDestroy()
        {
            if (_serverBoard != null) _serverBoard.OnFeatureCompleted -= ServerHandleFeatureCompleted;

            DOTween.KillAll();
        }



        // ------------------------------------------
        // СЕРВЕРНАЯ ЛОГИКА
        // ------------------------------------------

        // Синхронизирует финальные очки из Ядра обратно в сетевые SyncVars
        [Server]
        private void ServerSyncEndGameScores(List<Player> logicPlayers)
        {
            List<CarcaPlayer> networkPlayers = new List<CarcaPlayer>(FindObjectsByType<CarcaPlayer>(FindObjectsSortMode.None));
            foreach (var netPlayer in networkPlayers)
            {
                Player logicP = logicPlayers.Find(p => p.Id == netPlayer.PersistentId);
                if (logicP != null)
                {
                    netPlayer.Score = logicP.Score;
                    netPlayer.MeeplesAvailable = logicP.MeeplesAvailable;
                    netPlayer.HasAbbot = logicP.HasAbbot;

                    // Обновляем базу данных сессий, чтобы при просмотре результатов всё было честно
                    if (_netManager.PlayerSessions.ContainsKey(netPlayer.SessionToken))
                    {
                        var session = _netManager.PlayerSessions[netPlayer.SessionToken];
                        session.Score = logicP.Score;
                        session.MeeplesAvailable = logicP.MeeplesAvailable;
                        session.HasAbbot = logicP.HasAbbot;
                    }
                }
            }

            // Обновляем очки бота
            if (currentMode == GameMode.PlayersAndBot && _botLogicPlayer != null)
            {
                BotScore = _botLogicPlayer.Score;
                BotMeeples = _botLogicPlayer.MeeplesAvailable;
                BotHasAbbot = _botLogicPlayer.HasAbbot;
            }
        }

        /// <summary>
        /// Досрочное принудительное завершение игры сервером.
        /// </summary>
        [Server]
        private void ServerForceEndGame(string reason)
        {
            if (!isGameStarted) return;

            // Игра завершена досрочно
            isGameFinished = true; 

            isGameStarted = false;

            List<Player> logicPlayers = GetLogicPlayers();

            // Финальный подсчет очков (недострой + поля)
            _serverBoard.ScoreEndGameFields(logicPlayers);

            // Переносим финальные очки в сеть
            ServerSyncEndGameScores(logicPlayers);

            // Собираем текст с результатами и отправляем клиентам
            string finalResults = GenerateGameOverText(reason);
            RpcShowGameOverScreen(finalResults);
            Debug.Log($"[СЕРВЕР] Сгенерированный текст: \n{finalResults}");
        }

        /// <summary>
        /// Запуск раунда Хостом из лобби. Размещение стартового тайла.
        /// </summary>
        [Server]
        public void ServerStartGame()
        {
            // Игра началась, сбрасываем финиш
            isGameFinished = false;

            // 1. Проверяем, все ли готовы в лобби
            foreach (var p in LobbyPlayers)
            {
                if (!p.IsReady) return;
            }

            // 2. НОВАЯ ЗАЩИТА: Блокируем запуск PvP в одиночку на уровне сервера
            if (currentMode == GameMode.PlayersOnly && LobbyPlayers.Count < 2)
            {
                Debug.LogWarning("[СЕРВЕР] Отказ старта: В режиме PvP должно быть минимум 2 игрока!");
                return;
            }

            if (isGameStarted) return;
            isGameStarted = true;

            TileData startTile = _serverDeck.CreateClassicStartTile();
            _serverBoard.PlaceTile(startTile, Vector2Int.zero);
            RpcSpawnTileVisually(startTile.Id, 0, 0, startTile.Rotation);

            Invoke(nameof(ServerStartNextTurn), 0.5f);
        }

        /// <summary>
        /// Извлечение следующей карты из колоды и передача хода по карусели.
        /// </summary>
        [Server]
        public void ServerStartNextTurn()
        {
            // --- Цикл вытягивания тайла ---
            bool validTileFound = false;

            while (!validTileFound)
            {
                _serverCurrentTile = _serverDeck.DrawTile();

                // Если колода опустела в процессе поиска - конец игры
                if (_serverCurrentTile == null)
                {
                    isGameStarted = false;

                    List<Player> logicPlayers = GetLogicPlayers();
                    _serverBoard.ScoreEndGameFields(logicPlayers);

                    // Перенос финальных очков в сеть
                    ServerSyncEndGameScores(logicPlayers);

                    string finalResults = GenerateGameOverText("КОЛОДА ПУСТА! ИГРА ОКОНЧЕНА");

                    // Игра завершена естественно
                    isGameFinished = true; 

                    RpcShowGameOverScreen(finalResults);
                    return;
                }

                // Проверяем, есть ли вообще на столе место для этого тайла?
                List<Move> possibleMoves = _serverBoard.GetAllValidMoves(_serverCurrentTile);

                if (possibleMoves.Count > 0)
                {
                    validTileFound = true; // Нашли. Выходим из цикла.
                }
                else
                {
                    Debug.LogWarning($"[СЕРВЕР] Тайл {_serverCurrentTile.Id} некуда поставить. Сбрасываем и тянем новый.");
                    // Цикл while автоматически повторится и вытянет следующую карту
                }
            }

            // ------------------------------------------

            currentTileId = _serverCurrentTile.Id;

            List<string> sessionTokens = new List<string>(_netManager.PlayerSessions.Keys);
            sessionTokens.Sort(); // Сортируем по алфавиту для стабильности

            int totalEntities = (currentMode == GameMode.PlayersAndBot) ? sessionTokens.Count + 1 : sessionTokens.Count;

            // ИЩЕМ СЛЕДУЮЩЕГО ИГРОКА В ЦИКЛЕ (чтобы перепрыгивать через "отключенных")
            int attempts = 0;
            while (attempts < totalEntities)
            {
                _currentPlayerServerIndex = (_currentPlayerServerIndex + 1) % totalEntities;

                if (_currentPlayerServerIndex < sessionTokens.Count)
                {
                    string potentialToken = sessionTokens[_currentPlayerServerIndex];
                    PlayerSessionData session = _netManager.PlayerSessions[potentialToken];

                    if (!session.IsDisconnected && !session.IsDead)
                    {
                        currentTurnPlayerToken = potentialToken; // Задаем ход по Имени

                        Debug.Log($"[СЕРВЕР] Игрок {currentTurnPlayerToken} вытянул тайл: {currentTileId}");

                        return;
                    }
                }
                else if (currentMode == GameMode.PlayersAndBot)
                {
                    currentTurnPlayerToken = "BOT";

                    Debug.Log($"[СЕРВЕР] Бот вытянул тайл: {currentTileId}");

                    ProcessAITurn();
                    return; // Ходит Бот. Выходим.
                }

                attempts++;
            }

            // Если цикл закончился и мы никого не нашли, значит все живые игроки отключились
            Debug.Log("[СЕРВЕР] Все живые игроки отключены!");
            // Можно либо закончить игру, либо (что мы сделаем в Update) ждать.
        }

        /// <summary>
        /// Генерирует итоговый текст результатов для экрана окончания игры.
        /// </summary>
        [Server]
        private string GenerateGameOverText(string header)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=yellow><b>{header}</b></color>\n");
            sb.AppendLine("ФИНАЛЬНЫЙ СЧЕТ:\n");

            // Собираем всех живых игроков
            List<CarcaPlayer> players = new List<CarcaPlayer>(FindObjectsByType<CarcaPlayer>(FindObjectsSortMode.None));

            // Сортируем их по очкам (по убыванию)
            players.Sort((a, b) => b.Score.CompareTo(a.Score));

            int place = 1;
            foreach (var p in players)
            {
                string colorHex = ColorUtility.ToHtmlStringRGB(p.PlayerColor);
                sb.AppendLine($"{place}. <color=#{colorHex}>{p.SessionToken}</color>: {p.Score} очков");
                place++;
            }

            // Добавляем бота
            if (currentMode == GameMode.PlayersAndBot)
            {
                sb.AppendLine($"\n<color=red>БОТ</color>: {BotScore} очков");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Асинхронный запуск вычислений ИИ-бота в фоновом потоке.
        /// </summary>
        [Server]
        private async void ProcessAITurn()
        {
            Debug.Log("[СЕРВЕР] Ход передается Боту...");

            // Заглушка для человеческого оппонента (чтобы бот знал, против кого играет)
            // Берем первого попавшегося человека
            CarcaPlayer humanTarget = FindFirstObjectByType<CarcaPlayer>();
            Player logicHumanTarget = new Player(humanTarget != null ? humanTarget.PersistentId : 1);

            List<TileData> deckCopy = _serverDeck.GetDeckCopy();

            // Включаем статус в главном потоке ДО начала расчетов
            IsBotThinking = true;
            RequestUIUpdate(); // Форсируем обновление UI, чтобы надпись сменилась сразу
            

            Player botCopyForAI = _botLogicPlayer.Clone();

            // Фоновый поток. Бот думает
            Move bestMove = await Task.Run(() => _aiBot.FindBestMove(_serverBoard, _serverCurrentTile, deckCopy, botCopyForAI, logicHumanTarget));

            // Если игрок нажал "Выйти в меню", пока бот думал, этот объект уже уничтожен.
            // Прекращаем выполнение метода, чтобы не было ошибки
            if (this == null) return;

            // Выключаем статус ПОСЛЕ расчетов
            IsBotThinking = false;
            RequestUIUpdate();

            if (bestMove != null)
            {
                // 1. Ставим ТАЙЛ
                _serverCurrentTile.SetRotation(bestMove.Rotation);
                _serverBoard.PlaceTile(_serverCurrentTile, bestMove.Position);
                RpcSpawnTileVisually(_serverCurrentTile.Id, bestMove.Position.x, bestMove.Position.y, bestMove.Rotation);

                _serverLastPlacedTile = _serverCurrentTile;
                _serverLastPlacedPos = bestMove.Position;

                // 2. Ставим МИПЛА (если решил)
                await Task.Delay(300);

                if (this == null) return;

                if (bestMove.RetrieveAbbot)
                {
                    // Бот решил снять Аббата
                    ServerTryRetrieveAbbot(_botLogicPlayer);
                }
                else if (bestMove.MeepleRegionId != -1)
                {
                    TileRegion targetRegion = _serverLastPlacedTile.Regions.Find(r => r.Id == bestMove.MeepleRegionId);

                    // Запоминаем, сколько миплов было до выставления аббата
                    int realBotMeeples = _botLogicPlayer.MeeplesAvailable;

                    if (bestMove.MeepleType == 1)
                    {
                        _botLogicPlayer.MeeplesAvailable++; // Временно даем мипла для прохождения проверки
                    }

                    if (targetRegion != null && _serverBoard.CanPlaceMeeple(_botLogicPlayer, targetRegion))
                    {
                        _serverBoard.PlaceMeeple(_botLogicPlayer, targetRegion);

                        if (bestMove.MeepleType == 1)
                        {
                            _botLogicPlayer.HasAbbot = false;
                            // Восстанавление настоящих миплов
                            _botLogicPlayer.MeeplesAvailable = realBotMeeples;
                        }

                        Vector3 tileWorldPos = new Vector3(bestMove.Position.x * _tileSize, 0, bestMove.Position.y * _tileSize);
                        
                        Vector3 localPos = GetRegionLocalPositionFromPrefab(_serverLastPlacedTile.Id, targetRegion.Id);
                        
                        // Точно так же, как и в ServerTryPlaceMeeple
                        GameObject tilePrefab = GetPrefabForTile(_serverLastPlacedTile.Id);
                        float baseRotY = tilePrefab.transform.rotation.eulerAngles.y;

                        Quaternion tileRotation = Quaternion.Euler(0, baseRotY + (_serverLastPlacedTile.Rotation * 90f), 0);
                        localPos = tileRotation * localPos;

                        Vector3 worldPos = tileWorldPos + localPos + new Vector3(0, -0.2f, 0);

                        Quaternion meepleRot = CalculateMeepleRotation(_serverLastPlacedTile, targetRegion);

                        // Если это крестьянин, немного опускаем его, чтобы он не левитировал
                        if (targetRegion.Type == TerrainType.Field)
                        {
                            worldPos -= new Vector3(0, -0.2f, 0);
                        }

                        GameObject prefabToSpawn = (bestMove.MeepleType == 1) ? _abbotPrefab : _meeplePrefab;

                        GameObject meepleObj = Instantiate(prefabToSpawn, worldPos, meepleRot);

                        // Парсинг HEX цвета для бота
                        if (ColorUtility.TryParseHtmlString("#A83232", out Color botColor))
                        {
                            meepleObj.GetComponent<MeepleView>().MeepleColor = botColor;
                        }

                        NetworkServer.Spawn(meepleObj);
                        _serverMeeples[targetRegion] = meepleObj;
                    }
                }
            }

            // Третья проверка
            if (this == null) return;

            await Task.Delay(1000);
            ServerEndTurn(); // Бот закончил, передает ход дальше

            // Заставляет Unity вычистить мусор от симуляций, пока игрок
            // думает над следующим ходом, а не случайно посреди игры
            System.GC.Collect();
        }

        /// <summary>
        /// Сетевой запрос от клиента на установку тайла. Проверяется сервером.
        /// </summary>
        [Server]
        public void ServerTryPlaceTile(Vector2Int pos, int rotation, CarcaPlayer player)
        {
            if (player.SessionToken != currentTurnPlayerToken || currentPhase != 0) return;

            _serverCurrentTile.SetRotation(rotation);

            if (_serverBoard.CanPlaceTile(_serverCurrentTile, pos))
            {
                _serverBoard.PlaceTile(_serverCurrentTile, pos);
                RpcSpawnTileVisually(_serverCurrentTile.Id, pos.x, pos.y, rotation);

                _serverLastPlacedTile = _serverCurrentTile;
                _serverLastPlacedPos = pos;
                currentPhase = 1;

                // СЕРВЕР сам вычисляет список разрешенных регионов
                List<int> validRegionIds = new List<int>();

                // Не забываем передать HasAbbot в тестового игрока
                var tempLogicPlayer = new Player(player.PersistentId) { MeeplesAvailable = player.MeeplesAvailable, HasAbbot = player.HasAbbot }; 

                foreach (var region in _serverLastPlacedTile.Regions)
                {
                    bool canPlace = _serverBoard.CanPlaceMeeple(tempLogicPlayer, region);

                    // Если можно поставить обычного мипла
                    if (player.MeeplesAvailable > 0 && canPlace)
                    {
                        if (!validRegionIds.Contains(region.Id)) 
                            validRegionIds.Add(region.Id);
                    }
                    // Проверка аббата
                    // Аббата можно ставить только на Монастырь.
                    if (player.HasAbbot && region.Type == TerrainType.Monastery)
                    {
                        // МАГИЯ ОБМАНА ЯДРА:
                        // Так как CanPlaceMeeple возвращает false при MeeplesAvailable == 0,
                        // мы временно даем тестовому игроку 1 мипла, чтобы Ядро просто проверило, 
                        // не занят ли монастырь кем-то другим
                        int oldMeeples = tempLogicPlayer.MeeplesAvailable;
                        tempLogicPlayer.MeeplesAvailable = 1;

                        if (_serverBoard.CanPlaceMeeple(tempLogicPlayer, region))
                        {
                            if (!validRegionIds.Contains(region.Id)) 
                                validRegionIds.Add(region.Id);
                        }

                        // Возвращаем как было
                        tempLogicPlayer.MeeplesAvailable = oldMeeples;
                    }
                }

                // Переходим в фазу 1, если есть куда ставить, ИЛИ если можно ЗАБРАТЬ аббата
                if (validRegionIds.Count > 0 || !player.HasAbbot)
                {
                    TargetStartMeeplePhase(player.connectionToClient, pos.x, pos.y, _serverLastPlacedTile.Id, rotation, validRegionIds.ToArray());
                }
                else
                {
                    ServerEndTurn();
                }
            }
        }

        /// <summary>
        /// Сетевой запрос от клиента на спавн мипла. Рассчитывается точка и спавнится сетевой объект.
        /// </summary>
        [Server]
        public void ServerTryPlaceMeeple(int regionId, int meepleType, Vector3 localOffset, CarcaPlayer player)
        {
            if (player.SessionToken != currentTurnPlayerToken || currentPhase != 1) return;

            TileRegion targetRegion = _serverLastPlacedTile.Regions.Find(r => r.Id == regionId);
            if (targetRegion != null)
            {
                var tempLogicPlayer = new Player(player.PersistentId) { MeeplesAvailable = player.MeeplesAvailable };

                if (meepleType == 1 && (!player.HasAbbot || targetRegion.Type != TerrainType.Monastery)) return;

                // Запоминаем настоящие миплы, прежде чем дать временного для проверки
                int realMeeples = tempLogicPlayer.MeeplesAvailable;
                if (meepleType == 1)
                {
                    tempLogicPlayer.MeeplesAvailable = 1; // Временно даем 1 мипла для прохождения проверок Ядра
                }

                if (_serverBoard.CanPlaceMeeple(tempLogicPlayer, targetRegion))
                {
                    _serverBoard.PlaceMeeple(tempLogicPlayer, targetRegion); // Ядро забирает этого 1 мипла

                    // Восстанавливаем реальные запасы игрока
                    if (meepleType == 1)
                    {
                        player.HasAbbot = false; // Потратили Аббата
                        // Миплы остаются нетронутыми
                        player.MeeplesAvailable = realMeeples;
                    }
                    else
                    {
                        // Потратили обычного мипла (берем то, что насчитало Ядро)
                        player.MeeplesAvailable = tempLogicPlayer.MeeplesAvailable;
                    }

                    // --- СПАВН ПРЕФАБА ---
                    Vector3 tileWorldPos = new Vector3(_serverLastPlacedPos.x * _tileSize, 0, _serverLastPlacedPos.y * _tileSize);

                    GameObject tilePrefab = GetPrefabForTile(_serverLastPlacedTile.Id);
                    float baseRotY = tilePrefab.transform.rotation.eulerAngles.y;

                    Quaternion tileRotation = Quaternion.Euler(0, baseRotY + (_serverLastPlacedTile.Rotation * 90f), 0);
                    localOffset = Vector3.ClampMagnitude(localOffset, _tileSize * 0.5f);
                    Vector3 worldPos = tileWorldPos + (tileRotation * localOffset) + new Vector3(0, 0.21f, 0);

                    Quaternion meepleRot = CalculateMeepleRotation(_serverLastPlacedTile, targetRegion);
                    if (targetRegion.Type == TerrainType.Field) worldPos -= new Vector3(0, 0.21f, 0);

                    GameObject prefabToSpawn = (meepleType == 1) ? _abbotPrefab : _meeplePrefab;

                    GameObject meepleObj = Instantiate(prefabToSpawn, worldPos, meepleRot);
                    meepleObj.GetComponent<MeepleView>().MeepleColor = player.PlayerColor;

                    NetworkServer.Spawn(meepleObj);
                    _serverMeeples[targetRegion] = meepleObj;
                }
            }

            ServerEndTurn();
        }

        /// <summary>
        /// Метод завершения хода. Считает промежуточные очки, синхронизирует и передает ход.
        /// </summary>
        [Server]
        public void ServerEndTurn()
        {
            List<Player> logicPlayers = GetLogicPlayers();
            _serverBoard.CheckAndScoreCompletedFeatures(_serverLastPlacedTile, logicPlayers);

            // Синхронизация очков обратно сетевым игрокам
            List<CarcaPlayer> networkPlayers = new List<CarcaPlayer>(FindObjectsByType<CarcaPlayer>(FindObjectsSortMode.None));
            foreach (var netPlayer in networkPlayers)
            {
                Player logicP = logicPlayers.Find(p => p.Id == netPlayer.PersistentId);
                if (logicP != null)
                {
                    netPlayer.Score = logicP.Score;
                    netPlayer.MeeplesAvailable = logicP.MeeplesAvailable;
                    netPlayer.HasAbbot = logicP.HasAbbot;

                    // Обновление базы данных сессий
                    if (_netManager.PlayerSessions.ContainsKey(netPlayer.SessionToken))
                    {
                        var session = _netManager.PlayerSessions[netPlayer.SessionToken];
                        session.Score = logicP.Score;
                        session.MeeplesAvailable = logicP.MeeplesAvailable;
                        session.HasAbbot = logicP.HasAbbot;
                    }
                }
            }

            // Обновляем бота ТОЛЬКО если он существует в этом режиме
            if (currentMode == GameMode.PlayersAndBot && _botLogicPlayer != null)
            {
                BotScore = _botLogicPlayer.Score;
                BotMeeples = _botLogicPlayer.MeeplesAvailable;
                BotHasAbbot = _botLogicPlayer.HasAbbot;
            }

            currentPhase = 0;
            ServerStartNextTurn(); // Передаем ход

            RpcForceUIUpdate();
        }

        // Вызывается событием из Ядра, когда достраивается город
        [Server]
        private void ServerHandleFeatureCompleted(List<TileRegion> completedRegions, Dictionary<int, int> pointsAwarded)
        {
            // Упаковка словаря в сетевой массив
            ScoreAwardData[] awardsArray = new ScoreAwardData[pointsAwarded.Count];
            int i = 0;
            foreach (var kvp in pointsAwarded)
            {
                awardsArray[i] = new ScoreAwardData { PlayerId = kvp.Key, Points = kvp.Value };
                i++;
            }

            foreach (var region in completedRegions)
            {
                if (_serverMeeples.TryGetValue(region, out GameObject meepleObj))
                {
                    // 1. Отправляем приказ клиентам начать анимацию
                    RpcAnimateMeepleExit(meepleObj, awardsArray);

                    // 2. Убираем мипла из словаря сервера, чтобы больше его не трогать
                    _serverMeeples.Remove(region);

                    // 3. Сервер ждет 1.5 секунды (пока идет анимация на клиентах) 
                    // и только потом уничтожает сетевой объект
                    StartCoroutine(DestroyMeepleAfterDelay(meepleObj, 1.5f));
                }
            }
        }

        // Вспомогательная корутина для Сервера
        [Server]
        private System.Collections.IEnumerator DestroyMeepleAfterDelay(GameObject meeple, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (meeple != null)
            {
                NetworkServer.Destroy(meeple);
            }
        }

        private List<Player> GetLogicPlayers()
        {
            List<Player> list = new List<Player>();
            foreach (var p in FindObjectsByType<CarcaPlayer>(FindObjectsSortMode.None))
            {
                list.Add(new Player(p.PersistentId) { Score = p.Score, MeeplesAvailable = p.MeeplesAvailable, HasAbbot = p.HasAbbot });
            }

            // Добавляем бота в Ядро ТОЛЬКО если играем с ботом
            if (currentMode == GameMode.PlayersAndBot && _botLogicPlayer != null)
            {
                list.Add(_botLogicPlayer);
            }

            return list;
        }

        // Вызывается сервером для конкретного игрока
        [Server]
        public void ServerSendBoardHistory(NetworkConnectionToClient conn)
        {
            List<PlacedTileNetData> history = new List<PlacedTileNetData>();

            // _serverBoard должен иметь метод GetAllPlacedTiles()
            foreach (var kvp in _serverBoard.GetAllPlacedTiles())
            {
                history.Add(new PlacedTileNetData
                {
                    tileId = kvp.Value.Id,
                    posX = kvp.Key.x,
                    posY = kvp.Key.y,
                    rotation = kvp.Value.Rotation
                });
            }

            TargetReceiveBoardHistory(conn, history.ToArray());
        }

        // Выполняется только у того клиента, который только что зашел
        [TargetRpc]
        public void TargetReceiveBoardHistory(NetworkConnectionToClient target, PlacedTileNetData[] history)
        {
            Debug.Log($"[КЛИЕНТ] Получаю историю доски: {history.Length} тайлов...");
            foreach (var data in history)
            {
                // Не используется RpcSpawnTileVisually, так как он рассылается всем
                // Вызывает просто локальную логику спавна.
                Vector2Int pos = new Vector2Int(data.posX, data.posY);
                Vector3 worldPos = new Vector3(pos.x * _tileSize, 0, pos.y * _tileSize);
                GameObject prefabToUse = GetPrefabForTile(data.tileId);

                GameObject tileObj = Instantiate(prefabToUse, worldPos, prefabToUse.transform.rotation);
                float yRotation = data.rotation * 90f;
                Vector3 currentEuler = tileObj.transform.rotation.eulerAngles;
                tileObj.transform.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y + yRotation, currentEuler.z);

                TileData mockTile = CreateMockTileData(data.tileId);
                mockTile.SetRotation(data.rotation);
                LocalBoard.PlaceTile(mockTile, pos);
            }
        }

        /// <summary>
        /// Обработчик снятия аббата с монастыря (для клиента).
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="player"></param>
        [Server]
        public void ServerTryRetrieveAbbot(CarcaPlayer player)
        {
            if (player.SessionToken != currentTurnPlayerToken || currentPhase != 1) return;
            if (player.HasAbbot) return; // Защита: аббат еще в руке

            Vector2Int? foundPos = null;
            TileRegion foundRegion = null;

            // Ищем монастырь
            foreach (var kvp in _serverBoard.ActiveMonasteries)
            {
                GlobalFeature feature = _serverBoard.GraphManager.GetFeature(kvp.Value);
                if (feature.Meeples.ContainsKey(player.PersistentId))
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
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0) continue;
                        if (_serverBoard.GetTileAt(foundPos.Value + new Vector2Int(x, y)) != null) points++;
                    }
                }

                // === ИСПРАВЛЕНИЕ: МЫ МЕНЯЕМ СОСТОЯНИЕ В ЛОГИКЕ ЯДРА! ===
                Player logicPlayerToUpdate = new Player(player.PersistentId) { Score = player.Score, HasAbbot = player.HasAbbot, MeeplesAvailable = player.MeeplesAvailable };

                logicPlayerToUpdate.Score += points;
                logicPlayerToUpdate.HasAbbot = true; // Возвращение в руку

                // Просим Ядро снять фигурку
                _serverBoard.RemoveMeeple(logicPlayerToUpdate, foundRegion);

                // Обязательно переносим новые данные логики в сетевой объект СРАЗУ, до ServerEndTurn!
                player.Score = logicPlayerToUpdate.Score;
                player.HasAbbot = logicPlayerToUpdate.HasAbbot;

                // Уничтожение префаба на сервере
                if (_serverMeeples.TryGetValue(foundRegion, out GameObject meepleObj))
                {
                    ScoreAwardData[] award = new ScoreAwardData[1];
                    award[0] = new ScoreAwardData { PlayerId = player.PersistentId, Points = points };

                    RpcAnimateMeepleExit(meepleObj, award);
                    _serverMeeples.Remove(foundRegion);
                    StartCoroutine(DestroyMeepleAfterDelay(meepleObj, 1.5f));
                }

                ServerEndTurn(); // Ход завершен
            }
        }


        /// <summary>
        /// Обработчик снятия аббата с монастыря (для ИИ бота).
        /// </summary>
        [Server]
        public void ServerTryRetrieveAbbot(Player logicPlayer)
        {
            if (currentPhase != 1) return;
            if (logicPlayer.HasAbbot) return; // Защита: аббат еще в руке

            Vector2Int? foundPos = null;
            TileRegion foundRegion = null;

            // Ищем монастырь, на котором стоит фигурка бота (его ID = BOT_ID = 999)
            foreach (var kvp in _serverBoard.ActiveMonasteries)
            {
                GlobalFeature feature = _serverBoard.GraphManager.GetFeature(kvp.Value);
                if (feature.Meeples.ContainsKey(logicPlayer.Id))
                {
                    foundPos = kvp.Key;
                    foundRegion = kvp.Value;
                    break;
                }
            }

            if (foundPos.HasValue)
            {
                // Нашли! Считаем очки за монастырь (по 1 за каждый тайл в сетке 3х3)
                int points = 1;
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0) continue;
                        if (_serverBoard.GetTileAt(foundPos.Value + new Vector2Int(x, y)) != null) points++;
                    }
                }

                // Изменяем состояние бота в логическом ядре напрямую
                logicPlayer.Score += points;
                logicPlayer.HasAbbot = true; // Возвращаем в руку

                // Просим Ядро снять фигурку бота с монастыря
                _serverBoard.RemoveMeeple(logicPlayer, foundRegion);

                // Запускаем красивый визуал ухода мипла для всех клиентов
                if (_serverMeeples.TryGetValue(foundRegion, out GameObject meepleObj))
                {
                    // Сбор массива для Rpc (используем константу BOT_ID)
                    ScoreAwardData[] award = new ScoreAwardData[1];
                    award[0] = new ScoreAwardData { PlayerId = 999, Points = points };

                    RpcAnimateMeepleExit(meepleObj, award);
                    _serverMeeples.Remove(foundRegion);
                    StartCoroutine(DestroyMeepleAfterDelay(meepleObj, 1.5f));
                }

                ServerEndTurn(); // Ход завершен
            }
        }

        /// <summary>
        /// Вычисляет локальную позицию региона относительно префаба через компоненты RegionCollider. Используется только ИИ ботом.
        /// </summary>
        private Vector3 GetRegionLocalPositionFromPrefab(string tileId, int regionId)
        {
            GameObject prefab = GetPrefabForTile(tileId);
            if (prefab != null)
            {
                // Поиск специального якоря (RegionAnchor)
                RegionAnchor[] anchors = prefab.GetComponentsInChildren<RegionAnchor>(true);
                foreach (var anchor in anchors)
                {
                    if (anchor.regionId == regionId)
                    {
                        Vector3 localPos = prefab.transform.InverseTransformPoint(anchor.transform.position);
                        localPos.Scale(prefab.transform.localScale);
                        return localPos;
                    }
                }
            }
            return Vector3.zero; // Если якорь не найден, установка в центр тайла
        }

        /// <summary>
        /// Метод добавления игрока в лобби при подключении к хосту.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="color"></param>
        /// <param name="isHost"></param>
        [Server]
        public void ServerAddPlayerToLobby(string token, Color color, bool isHost)
        {
            // Проверка, нет ли его уже в списке
            foreach (var p in LobbyPlayers) if (p.SessionToken == token) return;

            LobbyPlayers.Add(new LobbyPlayerData
            {
                SessionToken = token,
                PlayerColor = color,
                IsReady = isHost, // Хост всегда готов по умолчанию
                IsHost = isHost
            });
        }

        /// <summary>
        /// Вызывается клиентом (Command), когда он жмет "Готов"
        /// </summary>
        /// <param name="token"></param>
        [Command(requiresAuthority = false)]
        public void CmdSetPlayerReady(string token)
        {
            for (int i = 0; i < LobbyPlayers.Count; i++)
            {
                if (LobbyPlayers[i].SessionToken == token)
                {
                    LobbyPlayerData pd = LobbyPlayers[i];
                    pd.IsReady = !pd.IsReady; // Переключение статуса
                    LobbyPlayers[i] = pd;     // Обновление в SyncList
                    break;
                }
            }
        }

        /// <summary>
        /// Метод исключения игрока из игрового лобби.
        /// </summary>
        /// <param name="tokenToKick"></param>
        /// <param name="sender"></param>
        [Command(requiresAuthority = false)]
        public void CmdKickPlayer(string tokenToKick, NetworkConnectionToClient sender = null)
        {
            if (!isServer) return;

            bool isSenderHost = (sender == null || sender.connectionId == 0);
            if (!isSenderHost) return;

            Debug.Log($"[СЕРВЕР] Хост приказал выгнать игрока: {tokenToKick}");

            CarcaNetworkManager netManager = Mirror.NetworkManager.singleton as CarcaNetworkManager;
            if (netManager != null)
            {
                CarcaPlayer[] players = FindObjectsByType<CarcaPlayer>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p.SessionToken == tokenToKick)
                    {
                        if (p.connectionToClient != null)
                        {
                            // Рвем соединение (base.OnServerDisconnect сработает автоматически!)
                            p.connectionToClient.Disconnect();
                        }
                        break;
                    }
                }

                // ВНИМАНИЕ: Мы БОЛЬШЕ НИЧЕГО ЗДЕСЬ НЕ УДАЛЯЕМ!
                // Ни PlayerSessions, ни LobbyPlayers.
                // Метод p.connectionToClient.Disconnect() автоматически триггерит 
                // метод OnServerDisconnect в CarcaNetworkManager, а он уже сделает всю чистовую работу
                // через корутину DelayedLobbyCleanup.
            }
        }

        [Server]
        public void HandleActivePlayerDisconnect(string disconnectedToken)
        {
            // Мгновенно вызывается UI на клиентах, чтобы убрать карточку
            RpcForceUIUpdate();

            // Если отключился тот, чей сейчас ход - требуется пропустить его ход
            if (isGameStarted && currentTurnPlayerToken == disconnectedToken)
            {
                Debug.Log($"[СЕРВЕР] Активный игрок {disconnectedToken} покинул игру! Авто-скип хода.");

                // Сбрасываем фазы
                currentPhase = 0;

                // Передача хода следующему игроку (тайл, который он держал в руке, просто исчезает)
                ServerStartNextTurn();
            }
        }




        // ------------------------------------------
        // КЛИЕНТСКАЯ ЛОГИКА
        // ------------------------------------------

        // Метод для клиентов
        [ClientRpc]
        private void RpcForceUIUpdate()
        {
            RequestUIUpdate();

            // В конце хода все "Активные" коллайдеры становятся обычными
            int activeTileLayer = LayerMask.NameToLayer("ActiveTile");
            int defaultLayer = LayerMask.NameToLayer("Default");

            RegionCollider[] allColliders = FindObjectsByType<RegionCollider>(FindObjectsSortMode.None);
            foreach (var col in allColliders)
            {
                if (col.gameObject.layer == activeTileLayer)
                {
                    col.gameObject.layer = defaultLayer;
                }
            }

            // Ипсправление для PVP: Жесткий сброс локальных стейтов Клиента ---
            ClearMeepleTargets();
            if (GhostTile != null) GhostTile.SetActive(false);

            // Насильно прячем панель миплов у всех, чтобы она не зависала
            MeepleActionUI actionUI = FindFirstObjectByType<MeepleActionUI>();
            if (actionUI != null) actionUI.ForceHidePanel();
        }

        // Клиент получает приказ красиво убрать мипла
        [ClientRpc]
        private void RpcAnimateMeepleExit(GameObject meepleObj, ScoreAwardData[] awards)
        {
            // Защита от NullReference
            if (meepleObj == null) return;

            // 1. Анимация выхода мипла
            // try-catch на всякий случай для DOTWEEN
            try
            {
                meepleObj.transform.DOMoveY(meepleObj.transform.position.y + 2f, 1f)
                    .SetEase(DG.Tweening.Ease.InBack)
                    .SetLink(meepleObj);
                meepleObj.transform.DORotate(new Vector3(0, 180, 0), 1f, DG.Tweening.RotateMode.LocalAxisAdd)
                    .SetLink(meepleObj);
                meepleObj.transform.DOScale(0f, 1f)
                    .SetEase(DG.Tweening.Ease.InBack)
                    .SetLink(meepleObj);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI] Ошибка анимации мипла (не критично): {e.Message}");
            }

            // 2. Всплывающий текст с очками
            if (_floatingScoreTextPrefab != null && awards.Length > 0)
            {
                int points = awards[0].Points;
                int winnerId = awards[0].PlayerId;

                // Создание текста
                GameObject textObj = Instantiate(_floatingScoreTextPrefab, meepleObj.transform.position + Vector3.up, Quaternion.identity);

                var tmp = textObj.GetComponentInChildren<TMPro.TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = $"+{points}";

                    Color textColor = Color.white;
                    if (winnerId == 999)
                    {
                        ColorUtility.TryParseHtmlString("#8B3A3A", out textColor);
                    }
                    else
                    {
                        foreach (var p in FindObjectsByType<CarcaPlayer>(FindObjectsSortMode.None))
                        {
                            if (p.PersistentId == winnerId) { textColor = p.PlayerColor; break; }
                        }
                    }
                    tmp.color = textColor;

                    // Текст летит вверх и плавно исчезает
                    textObj.transform.DOMoveY(textObj.transform.position.y + 3f, 1.5f)
                        .SetEase(DG.Tweening.Ease.OutCubic)
                        .SetLink(textObj);

                    AudioManager.Instance.PlaySFX("ScorePop");

                    tmp.DOFade(0f, 1.5f);
                }
                else
                {
                    Debug.LogError("[UI] Компонент TextMeshPro не найден на префабе _floatingScoreTextPrefab!");
                }

                // Уничтожение объекта текста через 1.6 сек (как раз когда сервер удалит мипла)
                Destroy(textObj, 1.6f);
            }
        }

        [ClientRpc]
        private void RpcShowGameOverScreen(string finalMessage)
        {
            // Поиск UIManager и передача ему оформленного текста
            UIManager ui = FindFirstObjectByType<UIManager>();
            if (ui != null)
            {
                ui.ShowGameOverScreen(finalMessage);
            }

            Debug.Log($"[КЛИЕНТ] ИГРА ОКОНЧЕНА:\n{finalMessage}");
        }

        [ClientRpc]
        private void RpcSpawnTileVisually(string tileId, int posX, int posY, int rotation)
        {
            Vector2Int pos = new Vector2Int(posX, posY);

            // Запоминание для фазы миплов
            ClientActiveTilePos = pos;
            ClientActiveTileRotation = rotation;

            Vector3 worldPos = new Vector3(pos.x * _tileSize, 0, pos.y * _tileSize);
            GameObject prefabToUse = GetPrefabForTile(tileId);

            GameObject tileObj = Instantiate(prefabToUse, worldPos, prefabToUse.transform.rotation);
            float yRotation = rotation * 90f;
            Vector3 currentEuler = tileObj.transform.rotation.eulerAngles;
            tileObj.transform.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y + yRotation, currentEuler.z);

            TileData mockTile = CreateMockTileData(tileId);
            mockTile.SetRotation(rotation);

            // Перемещение всех RegionCollider этого тайла в слой "ActiveTile"
            int activeTileLayer = LayerMask.NameToLayer("ActiveTile");
            RegionCollider[] colliders = tileObj.GetComponentsInChildren<RegionCollider>();
            foreach (var col in colliders)
            {
                col.gameObject.layer = activeTileLayer;
            }

            LocalBoard.PlaceTile(mockTile, pos);

            // Спавн пыли и уничтожение её через 2 секунды
            if (_tileDustVFX != null)
            {
                AudioManager.Instance.PlaySFX("TilePlace");
                GameObject vfx = Instantiate(_tileDustVFX, worldPos, Quaternion.identity);
                Destroy(vfx, 2f);
            }
        }

        [TargetRpc]
        public void TargetStartMeeplePhase(NetworkConnectionToClient target, int posX, int posY, string tileId, int rotation, int[] validRegionIds)
        {
            if (GhostTile != null) GhostTile.SetActive(false);

            // Просто запоминание разрешенных ID для UI
            LocalValidRegionIds = validRegionIds;
        }




        // --- Вспомогательные методы клиента ---
        private void OnCurrentTileIdChanged(string oldId, string newId)
        {
            if (string.IsNullOrEmpty(newId)) return;
            LocalTileInHand = CreateMockTileData(newId);
            RefreshGhostTile();
        }

        public void RotateCurrentTileLocal() 
        { 
            if (LocalTileInHand != null) 
                LocalTileInHand.RotateRight();

            _lastHoveredGridPos = new Vector2Int(-999, -999);
        }
        public void ClearMeepleTargets() 
        { 
            foreach (var t in _localActiveTargets) 
                Destroy(t); 
            _localActiveTargets.Clear();

            LocalValidRegionIds = new int[0];
        }

        public void UpdateGhostTile(Vector2Int gridPos)
        {
            if (GhostTile == null || LocalTileInHand == null) return;

            // Ограничивание gridPos, чтобы голограмма не могла выйти за пределы +-11
            int maxGridSize = 11;
            gridPos.x = Mathf.Clamp(gridPos.x, -maxGridSize, maxGridSize);
            gridPos.y = Mathf.Clamp(gridPos.y, -maxGridSize, maxGridSize);

            // 1. ЛОГИКА ПРОВЕРКИ МЫШКИ
            // Если клетка свободна - обновление целевой позиции
            if (LocalBoard.GetTileAt(gridPos) == null)
            {
                _ghostTargetWorldPos = new Vector3(gridPos.x * _tileSize, 0, gridPos.y * _tileSize);

                // Смена цвета и поворот только при смене клетки
                if (gridPos != _lastHoveredGridPos)
                {
                    _lastHoveredGridPos = gridPos;

                    float yRotation = LocalTileInHand.Rotation * 90f;
                    Vector3 currentEuler = GetPrefabForTile(currentTileId).transform.rotation.eulerAngles;
                    GhostTile.transform.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y + yRotation, currentEuler.z);

                    Color targetColor = LocalBoard.CanPlaceTile(LocalTileInHand, gridPos) ? Color.green : Color.red;
                    targetColor.a = 0.6f;

                    foreach (var r in _ghostRenderers)
                    {
                        r.material.color = targetColor;
                    }
                }
            }

            // 2. ЛОГИКА ФИЗИЧЕСКОГО ДВИЖЕНИЯ (Выполняется всегда)
            // Даже если курсор наведён на занятую клетку, 
            // _ghostTargetWorldPos сохраняет свои последние легальные координаты
            GhostTile.transform.position = Vector3.Lerp(
                GhostTile.transform.position,
                _ghostTargetWorldPos,
                Time.deltaTime * 15f
            );

            // Плавный поворот с DOTWEEN
            float targetYRotation = LocalTileInHand.Rotation * 90f;
            Vector3 prefabEuler = GetPrefabForTile(currentTileId).transform.rotation.eulerAngles;
            Vector3 finalEuler = new Vector3(prefabEuler.x, prefabEuler.y + targetYRotation, prefabEuler.z);

            GhostTile.transform.DORotate(finalEuler, 0.15f)
                .SetEase(Ease.OutBack)
                .SetOptions(true) // Включение ShortestRoute в DOTween
                .SetLink(GhostTile); 
        }

        private void RefreshGhostTile()
        {
            if (GhostTile != null) Destroy(GhostTile);
            GameObject prefabToUse = GetPrefabForTile(currentTileId);
            GhostTile = Instantiate(prefabToUse);
            foreach (var col in GhostTile.GetComponentsInChildren<Collider>()) col.enabled = false;
            _ghostRenderers = GhostTile.GetComponentsInChildren<Renderer>();
            GhostTile.SetActive(false);
        }

        // Получение префаба тайла по индексу
        private GameObject GetPrefabForTile(string tileId)
        {
            foreach (var binding in _tilePrefabs)
                if (tileId.StartsWith(binding.tileName)) return binding.prefab;
            return _defaultPrefab;
        }

        private TileData CreateMockTileData(string id)
        {
            DeckManager tempDeck = new DeckManager();
            return tempDeck.GetTileById(id);
        }

        /// <summary>
        /// Возвращает ID региона (out int) и строку со статусом (return string)
        /// </summary>
        /// <param name="hoveredRegionId"></param>
        /// <param name="meepleType"></param>
        /// <param name="closestId"></param>
        /// <returns></returns>
        public string GetHoveredRegionStatus(int hoveredRegionId, int meepleType, out int closestId)
        {
            closestId = -1;

            if (hoveredRegionId == -1)
                return "Наведите на объект";

            TileData mockTile = CreateMockTileData(currentTileId);
            TileRegion hoveredRegion = mockTile.Regions.Find(r => r.Id == hoveredRegionId);

            if (hoveredRegion == null)
                return "Ошибка региона";

            // 1. Проверка Аббата
            if (meepleType == 1 && hoveredRegion.Type != TerrainType.Monastery)
            {
                return "Только для монастыря!";
            }

            // 2. Проверка легальности по массиву от Сервера
            bool isAllowedByServer = false;
            foreach (int validId in LocalValidRegionIds)
            {
                if (validId == hoveredRegion.Id)
                {
                    isAllowedByServer = true;
                    break;
                }
            }

            if (!isAllowedByServer)
            {
                if (!hoveredRegion.IsPlaceable) return "Сюда ставить нельзя";
                return "Объект уже занят!";
            }

            // ВСЁ ОК!
            closestId = hoveredRegion.Id;
            return TranslateTerrainType(hoveredRegion.Type);
        }

        private string TranslateTerrainType(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.City: return "Город";
                case TerrainType.Road: return "Дорога";
                case TerrainType.Field: return "Поле";
                case TerrainType.Monastery: return "Монастырь";
                default: return "Объект";
            }
        }

        // Универсальные методы-хуки для Mirror. Они просто "дергают" событие.
        private void OnStateChangedInt(int oldVal, int newVal) { OnUIUpdateRequired?.Invoke(); }
        private void OnStateChangedString(string oldVal, string newVal) { RequestUIUpdate(); }
        private void OnStateChangedBool(bool oldVal, bool newVal) { OnUIUpdateRequired?.Invoke(); }

        
        
        private void OnLobbyPlayersChanged(SyncList<LobbyPlayerData>.Operation op, int index, LobbyPlayerData oldItem, LobbyPlayerData newItem)
        {
            // Вызов UI, чтобы перерисовать Лобби
            RequestUIUpdate();
        }

        // Публичный статический метод, чтобы другие скрипты могли пнуть UI
        public static void RequestUIUpdate()
        {
            OnUIUpdateRequired?.Invoke();
        }

        /// <summary>
        /// Вычисляет поворот мипла так, чтобы он смотрел вдоль дороги или города
        /// </summary>
        /// <param name="tile">Конкретная плитка</param>
        /// <param name="region">Регион, на котором высчитывается поворот</param>
        /// <returns></returns>
        private Quaternion CalculateMeepleRotation(TileData tile, TileRegion region)
        {
            System.Random rnd = new();

            // 1. Лежачие миплы (Крестьяне на полях)
            if (region.Type == TerrainType.Field)
                return Quaternion.Euler(-90, (float)rnd.Next(-20, 20), 0);

            // 2. Центральные миплы (Монахи и т.д.)
            if (tile.CenterRegion == region)
                return Quaternion.Euler(0, (float)rnd.Next(-20, 20), 0);

            // 3. Сбор всех направлений (грани), на которые выходит этот регион
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

            // 4. Если регион выходит ровно на ДВЕ грани - это может быть поворот или прямая
            if (connectedDirections.Count == 2)
            {
                int dir1 = (int)connectedDirections[0]; // Например, North (0)
                int dir2 = (int)connectedDirections[1]; // Например, West (3)

                // Проверяем, являются ли эти грани соседними (то есть разница между ними 1 или 3)
                // Если разница 2 (например, Север(0) и Юг(2)) - это прямая дорога, угол усреднять не нужно.
                int diff = Mathf.Abs(dir1 - dir2);
                if (diff == 1 || diff == 3)
                {
                    // Это поворот. Вычисляем биссектрису (угол в 45 градусов).
                    float angle1 = dir1 * 90f;
                    float angle2 = dir2 * 90f;

                    // Особый случай: переход от Запада (270°) к Северу (0°).
                    // Чтобы среднее не стало 135° (Юго-Восток), считаем Север как 360°.
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
            // просто берем первое попавшееся направление.
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