using Assets.Scripts.Main;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.View
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject lobbyPanel;      // Главная панель лобби
        public GameObject playerRowPrefab; // Контейнер для списка игроков
        public Transform rowsContainer;

        public Button btnReady;     // Кнопка "Готов" (для клиентов)
        public Button btnStartGame; // Кнопка "Начать игру" (для Хоста)

        public Button btnLeaveLobby; // Кнопка выхода из лобби

        [Header("Cinematic Start")]
        public GameObject visualShield3D;      // Перекрывающая плашка позади лобби
        public float shieldDropDepth = -2f;    // Насколько уходит вниз
        public float shieldFlyBackDist = -20f; // Насколько улетает назад
        public float animationDuration = 1.5f;

        private CarcaGameManager _gm;

        // Статический флаг для блокировки управления
        public static bool IsLobbyActive = true;

        void Awake()
        {

            IsLobbyActive = true; // При старте сцены мы всегда в лобби

            // Привязка кнопки выхода (использует старый надежный метод)
            if (btnLeaveLobby != null)
            {
                btnLeaveLobby.onClick.AddListener(() =>
                {
                    if (Mirror.NetworkServer.active && Mirror.NetworkClient.isConnected)
                        Mirror.NetworkManager.singleton.StopHost();
                    else
                        Mirror.NetworkManager.singleton.StopClient();
                });
            }
        }

        void OnEnable() { CarcaGameManager.OnUIUpdateRequired += RefreshLobby; }
        void OnDisable() { CarcaGameManager.OnUIUpdateRequired -= RefreshLobby; }

        private void RefreshLobby()
        {
            if (_gm == null) _gm = FindFirstObjectByType<CarcaGameManager>();
            if (_gm == null) return;

            // Если игра уже началась - прячем Лобби навсегда
            if (_gm.isGameStarted || _gm.isGameFinished)
            {
                if (lobbyPanel.activeSelf)
                {
                    
                    lobbyPanel.SetActive(false);

                    if (visualShield3D != null)
                    {
                        Sequence sec = DOTween.Sequence();

                        Vector3 startPos = visualShield3D.transform.position;

                        // Создаем массив точек-ориентиров для траектории полета
                        // Точка 1 (Промежуточная): щит опускается наполовину и уже начинает лететь назад
                        Vector3 midPoint = startPos + new Vector3(0, shieldDropDepth * 0.5f, shieldFlyBackDist * 0.2f);

                        // Точка 2 (Конечная): щит полностью опустился и улетел назад
                        Vector3 endPoint = startPos + new Vector3(0, shieldDropDepth, shieldFlyBackDist);

                        Vector3[] pathWaypoints = new Vector3[] { midPoint, endPoint };

                        // Запускаем принудительную очистку предыдущих анимаций
                        visualShield3D.transform.DOKill();

                        // DOPath строит плавную кривую CatmullRom между всеми точками!
                        sec.Append(visualShield3D.transform.DOPath(pathWaypoints, animationDuration, PathType.CatmullRom)
                                     .SetEase(Ease.InOutQuad) // Плавный разгон в начале и плавное торможение в конце
                                     .OnComplete(() => visualShield3D.SetActive(false))); // Выключаем в конце

                        sec.OnComplete(() =>
                        {
                            IsLobbyActive = false;
                        }
                        );
                    }
                }
                return;
            }

            // Показываем Лобби
            if (!lobbyPanel.activeSelf) lobbyPanel.SetActive(true);

            // 1. Очищаем старые плашки
            foreach (Transform child in rowsContainer)
            {
                Destroy(child.gameObject);
            }

            bool allReady = true;
            bool amIHost = Mirror.NetworkServer.active;

            // 2. Спавним новые плашки
            foreach (var p in _gm.LobbyPlayers)
            {
                string hexColor = ColorUtility.ToHtmlStringRGB(p.PlayerColor);
                string readyTag = p.IsReady ? "<color=green>ГОТОВ</color>" : "<color=red>ЖДЕМ...</color>";

                GameObject rowObj = Instantiate(playerRowPrefab, rowsContainer);
                LobbyRowView rowView = rowObj.GetComponent<LobbyRowView>();

                // Передаем метод кика прямо в плашку!
                rowView.Initialize(p.SessionToken, hexColor, readyTag, p.IsHost, amIHost, p.SessionToken, KickPlayerAction);

                if (!p.IsReady) allReady = false;
            }

            // Бот
            if (_gm.currentMode == GameMode.PlayersAndBot)
            {
                GameObject rowObj = Instantiate(playerRowPrefab, rowsContainer);
                LobbyRowView rowView = rowObj.GetComponent<LobbyRowView>();
                rowView.Initialize("БОТ", "FF0000", "<color=green>ГОТОВ</color>", false, false, "", null);
            }

            // 2. ОТРИСОВКА КНОПОК
            string myToken = MainMenuUI.PlayerName; // Берем имя из меню

            bool isHost = Mirror.NetworkServer.active;

            if (isHost)
            {
                btnReady.gameObject.SetActive(false);
                btnStartGame.gameObject.SetActive(true);

                // ИСПРАВЛЕНИЕ: Проверяем количество игроков в зависимости от режима!
                bool canStart = false;

                if (_gm.currentMode == GameMode.PlayersOnly)
                {
                    // В PvP режиме нужно минимум 2 человека, и ВСЕ они должны быть готовы
                    canStart = (_gm.LobbyPlayers.Count >= 2) && allReady;
                }
                else if (_gm.currentMode == GameMode.PlayersAndBot)
                {
                    // В режиме с ботом достаточно 1 живого игрока (самого Хоста)
                    canStart = (_gm.LobbyPlayers.Count >= 1) && allReady;
                }

                // Кнопка активна только при соблюдении этих условий
                btnStartGame.interactable = canStart;
            }
            else
            {
                btnStartGame.gameObject.SetActive(false);
                btnReady.gameObject.SetActive(true);

                // Меняем текст кнопки Готов
                bool iAmReady = false;
                foreach (var p in _gm.LobbyPlayers) if (p.SessionToken == myToken) iAmReady = p.IsReady;

                btnReady.GetComponentInChildren<TextMeshProUGUI>().text = iAmReady ? "ОТМЕНИТЬ ГОТОВНОСТЬ" : "ГОТОВ!";
            }
        }

        public void OnReadyButtonClicked()
        {
            string myToken = MainMenuUI.PlayerName;
            _gm.CmdSetPlayerReady(myToken);
        }

        public void OnStartGameButtonClicked()
        {
            _gm.ServerStartGame();
        }

        private void KickPlayerAction(string targetToken)
        {
            Debug.Log($"[Лобби] Выгоняем: {targetToken}");
            if (_gm != null) _gm.CmdKickPlayer(targetToken);
        }
    }
}