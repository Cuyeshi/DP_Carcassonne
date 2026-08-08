using Assets.Scripts.Main;
using Assets.Scripts.Network;
using DG.Tweening;
using Mirror;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Scripts.View
{
    public class UIManager : MonoBehaviour
    {
        [Header("Game Over UI")]
        public GameObject gameOverPanel;
        public TextMeshProUGUI winnerText;

        // public TextMeshProUGUI scoreText;

        public TextMeshProUGUI aiStatsText;

        [Header("Настройки панели телеметрии бота")]
        [Header("Positions (По оси X)")]
        [Tooltip("Позиция по X, когда панель спрятана за экраном")]
        public float hiddenPosX = 310f;
        [Tooltip("Позиция по X, когда панель выехала на экран")]
        public float visiblePosX = -280f;
        private RectTransform _rectTransform;
        public GameObject botTelemetryPanel;
        [SerializeField] private float _openingSpeed = 1f;
        private bool _isOpen = false;
        private bool _isAnimating = false; // Флаг для защиты от спама

        private CarcaGameManager _gm;

        // Используем Awake вместо Start, он вызывается самым первым
        void Awake()
        {
            _gm = FindFirstObjectByType<CarcaGameManager>();

            if (botTelemetryPanel != null)
                _rectTransform = botTelemetryPanel.GetComponentInChildren<RectTransform>();
        }

        void OnEnable()
        {
            CarcaGameManager.OnUIUpdateRequired += RedrawUI;
        }

        void OnDisable()
        {
            CarcaGameManager.OnUIUpdateRequired -= RedrawUI;
        }

        void Start()
        {
            if (!NetworkServer.active)
            {
                botTelemetryPanel.SetActive(false);
            }
        }

        void Update()
        {
            // Проверка нажатия кнопки 'B' (через новую систему ввода)
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            {
                EnableTelemetryUI();
            }
        }

        public void OnDisconnectButtonClicked()
        {
            // Запоминаем на жестком диске, что мы совершили выход из игры
            PlayerPrefs.SetInt("ReturnedFromGame", 1);
            PlayerPrefs.Save();

            // Если мы Хост (Сервер + Клиент)
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopHost();
            }
            // Если мы просто Клиент
            else if (NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopClient();
            }

            // Важно: Mirror сам загрузит Offline Scene (MenuScene) после вызова Stop
        }

        private void RedrawUI()
        {
            // ЗАЩИТА: Если Awake не сработал или менеджер только что заспавнился
            if (_gm == null) _gm = FindFirstObjectByType<CarcaGameManager>();

            // Если менеджера вообще нет на сцене - выходим, не ломая игру
            if (_gm == null) return;

            // ОТРИСОВКА СТАТИСТИКИ БОТА
            if (aiStatsText != null)
            {
                if (Mirror.NetworkServer.active && _gm.AIBotInstance != null)
                {
                    if (_gm.IsBotThinking)
                    {
                        System.Text.StringBuilder aiSb = new System.Text.StringBuilder();
                        aiSb.AppendLine("<color=#FFFF00><b>[ТЕЛЕМЕТРИЯ БОТА]</b></color>\n");
                        //aiSb.AppendLine($"Статус: <color=white>ВЫЧИСЛЕНИЕ...</color>");

                        // Читаем напрямую из памяти бота!
                        var tel = _gm.AIBotInstance.Telemetry;
                        aiSb.AppendLine($"Всего симуляций: <color=white>{tel.TotalSimulationsCompleted}</color>");
                        aiSb.AppendLine($"Лучший ход: <color=#00FF00>{tel.BestMoveCoord}</color>");
                        aiSb.AppendLine($"Вероятность победы: <color=#00FF00>{(tel.BestMoveWinRate * 100f):F1}%</color>");

                        aiStatsText.text = aiSb.ToString();
                    }
                    //else
                    //{
                    //    aiStatsText.text = "<color=red>ИИ ожидает своего хода...</color>";
                    //}
                }
                else
                {
                    aiStatsText.text = "";
                }
            }
        }

    
        public void EnableTelemetryUI()
        {
            // ЗАЩИТА ОТ СПАМА
            if (_isAnimating) return;

            _isAnimating = true; // Блокируем новые нажатия
            _isOpen = !_isOpen;  // Меняем состояние (открыто/закрыто)


            // Выбираем целевую позицию
            float targetX = _isOpen ? visiblePosX : hiddenPosX;

            if (botTelemetryPanel != null)
            {
                _rectTransform.DOAnchorPosX(targetX, _openingSpeed).SetEase(Ease.InOutCubic);
            }
            else
                return;

            _isAnimating = false;
        }

        // Этот метод будет вызывать CarcaGameManager при конце игры
        public void ShowGameOverScreen(string message)
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            if (winnerText != null)
            {
                winnerText.text = message;
            }
        }
    }
}