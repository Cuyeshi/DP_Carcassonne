using Assets.Scripts.Main; // Чтобы видеть CarcaGameManager
using Mirror;
using UnityEngine;
using System.Collections.Generic;

namespace Assets.Scripts.Network
{
    /// <summary>
    /// Пользовательский сетевой менеджер игры "Каркассон".
    /// Управляет подключениями, сессиями игроков, пулом цветов и сменой сцен.
    /// </summary>
    public class CarcaNetworkManager : NetworkManager
    {
        [Header("Custom settings")]
        [Tooltip("Ссылка на префаб игрового менеджера")]
        [SerializeField] private GameObject _gameManagerPrefab;


        // Свойство доступно для чтения { get; }, но его нельзя переписать извне.
        /// <summary>
        /// Активные сессии игроков. Ключ — уникальный токен игрока.
        /// </summary>
        public Dictionary<string, PlayerSessionData> PlayerSessions { get; } = new Dictionary<string, PlayerSessionData>();

        // Пул цветов. Эти цвета будут выдаваться живым игрокам
        private List<Color> _availableColors = new List<Color>();

        // Запоминаем, какой цвет у кого (на случай удаления сессии)
        private Dictionary<string, Color> _usedColors = new Dictionary<string, Color>();

        // Время в секундах для переподключения (например, 2 минуты = 120f)
        [SerializeField] private float _reconnectTimeout = 120f;

        /// <summary>
        /// Публичный геттер для времени переподключения (только для чтения).
        /// </summary>
        public float ReconnectTimeout => _reconnectTimeout;


        /// <summary>
        /// Переопределение метода Awake для реализации паттерна Singleton на сетевом менеджере.
        /// </summary>
        public override void Awake()
        {
            // Если синглтон уже существует и это НЕ мы (значит, здесь дубликат из заново загруженной сцены)
            if (singleton != null && singleton != this)
            {
                Destroy(gameObject);
                return;
            }

            // Если это первый объект сетевого менеджера — запускаем стандартную инициализацию Mirror
            base.Awake();
        }

        /// <summary>
        /// Вызывается на сервере при его запуске. Инициализирует пул цветов и загружает настройки.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            PlayerSessions.Clear();
            _usedColors.Clear();

            // Восстанавление пула красивых, "деревянных" цветов
            InitializeColorPool();

            // Синхронизируем сетевой таймаут с глобальными настройками игры
            if (SettingsManager.Instance != null)
            {
                _reconnectTimeout = SettingsManager.Instance.TurnTimeout;
                Debug.Log($"[СЕРВЕР] Тайм-аут переподключения установлен на {_reconnectTimeout} секунд.");
            }
        }


        /// <summary>
        /// Вызывается на сервере, когда клиент успешно устанавливает сетевое соединение.
        /// </summary>
        /// <param name="conn">Сетевое соединение подключившегося клиента.</param>
        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            
            base.OnServerConnect(conn);
        }

        /// <summary>
        /// Вызывается на сервере и клиентах после полной загрузки игровой сцены.
        /// </summary>
        /// <param name="sceneName">Имя загруженной сцены.</param>
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            // Если загружена сцена игры (GameScene) - спавним GameManager
            if (sceneName.Contains("GameScene"))
            {
                GameObject gmInstance = Instantiate(_gameManagerPrefab);
                NetworkServer.Spawn(gmInstance); // Спавним GameManager на всех клиентах
            }
        }

        /// <summary>
        /// Вызывается на сервере, когда клиент запрашивает создание персонажа игрока.
        /// Отвечает за восстановление сессии при переподключении или создание новой.
        /// </summary>
        /// <param name="conn">Сетевое соединение клиента.</param>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            CarcaGameManager gm = FindFirstObjectByType<CarcaGameManager>();
            string playerToken = (string)conn.authenticationData;

            // Больше не нужно проверять IsDead или выкидывать игрока здесь.
            // Аутентификатор уже сделал всю работу.
            if (gm != null && gm.isGameStarted)
            {
                if (PlayerSessions.TryGetValue(playerToken, out PlayerSessionData session))
                {
                    Debug.Log($"[СЕРВЕР] Игрок {playerToken} успешно восстановлен в сессии.");
                    session.IsDisconnected = false;
                    session.DisconnectTimer = 0f;
                }
            }

            GameObject playerObj = Instantiate(playerPrefab);
            CarcaPlayer cPlayer = playerObj.GetComponent<CarcaPlayer>();

            if (!PlayerSessions.ContainsKey(playerToken))
            {
                PlayerSessions[playerToken] = new PlayerSessionData
                {
                    PersistentId = conn.connectionId,
                    Score = 0,
                    MeeplesAvailable = 7,
                    HasAbbot = true,
                    PlayerColor = GetNextAvailableColor(playerToken),
                    IsDisconnected = false
                };
            }

            PlayerSessionData currentSession = PlayerSessions[playerToken];
            cPlayer.Score = currentSession.Score;
            cPlayer.MeeplesAvailable = currentSession.MeeplesAvailable;
            cPlayer.HasAbbot = currentSession.HasAbbot;
            cPlayer.PlayerColor = currentSession.PlayerColor;
            cPlayer.SessionToken = playerToken;

            // Передаем постоянный ID в сетевой префаб!
            cPlayer.PersistentId = currentSession.PersistentId;

            NetworkServer.AddPlayerForConnection(conn, playerObj);

            bool isHost = (conn.connectionId == 0);
            if (gm != null) gm.ServerAddPlayerToLobby(playerToken, currentSession.PlayerColor, isHost);

            if (gm != null && gm.isGameStarted)
            {
                StartCoroutine(SendBoardWithDelay(conn, gm));
            }
        }

        /// <summary>
        /// Вызывается на сервере, когда клиент отключается. Запускает таймер ожидания переподключения.
        /// </summary>
        /// <param name="conn">Сетевое соединение отключившегося клиента.</param>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            CarcaGameManager gm = FindFirstObjectByType<CarcaGameManager>();
            string playerToken = (string)conn.authenticationData;

            if (playerToken != null && PlayerSessions.ContainsKey(playerToken))
            {
                if (gm != null && gm.isGameStarted)
                {
                    Debug.Log($"[СЕРВЕР] Игрок {playerToken} отключился. Ждем возвращения.");
                    PlayerSessions[playerToken].IsDisconnected = true;
                    PlayerSessions[playerToken].DisconnectTimer = _reconnectTimeout;

                    gm.HandleActivePlayerDisconnect(playerToken);
                }
                else
                {
                    ReturnColorToPool(playerToken);
                    PlayerSessions.Remove(playerToken);

                    // ИСПРАВЛЕНИЕ: Мы не удаляем из SyncList мгновенно!
                    // Мы запускаем корутину, которая подождет конец кадра, 
                    // чтобы сокет окончательно умер, и только потом обновит Лобби для остальных.
                    if (gm != null)
                    {
                        StartCoroutine(DelayedLobbyCleanup(gm, playerToken));
                    }
                }
            }

            base.OnServerDisconnect(conn);
        }



        // Вспомогательный метод: Задержка перед восстановлением стола
        /// <summary>
        /// Вспомогательная корутина для задержки отправки истории поля новому клиенту.
        /// </summary>
        private System.Collections.IEnumerator SendBoardWithDelay(NetworkConnectionToClient conn, CarcaGameManager gm)
        {
            yield return null; // Ждем 1 кадр

            // Если игрок всё еще подключен
            if (conn != null && conn.isReady)
            {
                Debug.Log($"[СЕРВЕР] Отправляю историю доски игроку {conn.connectionId}");
                gm.ServerSendBoardHistory(conn);
            }
        }

        // Вспомогательный метод: Выдать цвет
        /// <summary>
        /// Выдает следующий свободный цвет из пула цветов.
        /// </summary>
        private Color GetNextAvailableColor(string token)
        {
            if (_availableColors.Count > 0)
            {
                Color c = _availableColors[0];
                _availableColors.RemoveAt(0);
                _usedColors[token] = c;
                return c;
            }
            return Color.white; // Если зашло больше 5 игроков - даем белый
        }

        // Вспомогательный метод: Вернуть цвет в пул
        /// <summary>
        /// Возвращает цвет отключенного игрока обратно в общий пул цветов.
        /// </summary>
        public void ReturnColorToPool(string token)
        {
            if (_usedColors.ContainsKey(token))
            {
                // Освободившийся цвет встает на первое место и будет выдан следующему игроку.
                _availableColors.Insert(0, _usedColors[token]);
                _usedColors.Remove(token);
            }
        }

        /// <summary>
        /// Обновляет тайм-аут ожидания переподключения на основе настроек игры.
        /// </summary>
        public void UpdateTurnTimeOut(float vallue)
        {
            if (vallue > 0 && vallue < 1000)
            {
                _reconnectTimeout = vallue;
            }
        }

        /// <summary>
        /// Инициализирует пул фиксированных цветов для живых игроков.
        /// </summary>
        private void InitializeColorPool()
        {
            _availableColors.Clear();

            string[] hexColors = new string[]
            {
                "#3b699e", // Джинсовый синий
                "#4a7c59", // Лесной зеленый
                "#d4b84c", // Горчичный 
                //"#e88935", // Оранжевый (вместо красного)
                "#333333", // Угольный
                "#a3a3a3"  // Светло-серый (на случай, если игроков много)
            };

            foreach (string hex in hexColors)
            {
                // Пытаемся конвертировать строку в цвет
                if (ColorUtility.TryParseHtmlString(hex, out Color parsedColor))
                {
                    _availableColors.Add(parsedColor);
                }
                else
                {
                    Debug.LogWarning($"[СЕРВЕР] Не удалось прочитать цвет: {hex}");
                }
            }
        }


        /// <summary>
        /// Вызывается на клиенте, когда он не смог подключиться или связь оборвалась.
        /// </summary>
        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            // Находим скрипт главного меню на сцене
            Assets.Scripts.View.MainMenuUI menu = FindFirstObjectByType<Assets.Scripts.View.MainMenuUI>();

            if (menu != null)
            {
                // Выводим красивую ошибку на экран
                menu.ShowNetworkError("Не удалось подключиться к хосту! Проверьте IP-адрес или игра еще не создана.");
            }
        }

        // НОВАЯ КОРУТИНА
        private System.Collections.IEnumerator DelayedLobbyCleanup(CarcaGameManager gm, string playerToken)
        {
            // Ждем до конца кадра (к этому моменту base.OnServerDisconnect уже отработает
            // и вычистит мертвый connectionId из списков рассылки Mirror)
            yield return new WaitForEndOfFrame();

            if (gm != null)
            {
                for (int i = 0; i < gm.LobbyPlayers.Count; i++)
                {
                    if (gm.LobbyPlayers[i].SessionToken == playerToken)
                    {
                        gm.LobbyPlayers.RemoveAt(i);
                        Debug.Log($"[СЕРВЕР] Игрок {playerToken} удален из списка Лобби.");
                        break;
                    }
                }

                // Теперь безопасно пинаем UI для оставшихся живых клиентов
                CarcaGameManager.RequestUIUpdate();
            }
        }
    }
}