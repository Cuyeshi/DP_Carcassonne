using Assets.Scripts.Media;
using Assets.Scripts.Network;
using Assets.Scripts.View.MainMenu;
using DG.Tweening;
using Mirror;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro; // Для TextMeshPro
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.View
{
    public class MainMenuUI : MonoBehaviour
    {
        public TMP_InputField ipInputField; // Поле ввода IP
        public TMP_InputField nameInputField; // Поле для никнейма

        [Header("Панели (CanvasGroups)")]
        public CanvasGroup panelStart;
        public CanvasGroup panelSelectMode;
        public CanvasGroup panelHostSetup;
        public CanvasGroup panelClientSetup;
        public CanvasGroup panelAdditionalSellect;
        public CanvasGroup panelGameSettings;
        public MenuFader fader;

        [Header("Error Texts")]
        public TextMeshProUGUI nameErrorText;
        public TextMeshProUGUI networkErrorText;

        [Header("Кнопки (Для блокировки)")]
        public Button btnCreateRoom; // Кнопка, открывающая Panel_HostSetup
        public Button btnConnect;    // Кнопка, запускающая NetworkClient

        [Header("Camera Cinematic Transition")]
        public MenuCameraController cameraController;
        [Tooltip("Имя триггера для запуска поворота камеры")]
        public string cameraRotateTrigger = "Rotate";
        [Tooltip("Имя триггера для возврата камеры назад (при ошибке подключения)")]
        public string cameraResetTrigger = "Reset";
        [Tooltip("Длительность полета камеры в секундах")]
        public float transitionDuration = 2.0f;

        // Статическая переменная для передачи имени в Аутентификатор
        public static string PlayerName = "Unknown";

        // Выпадающий список для режимов
        public TMP_Dropdown modeDropdown;

        // Выпадающий список для режимов бота
        public TMP_Dropdown difficultyDropdown;

        void Awake()
        {
            if (fader == null)
                fader = GetComponent<MenuFader>();

            // Мгновенно очищаем тексты ошибок при старте сцены
            if (nameErrorText != null) nameErrorText.text = "";
            if (networkErrorText != null) networkErrorText.text = "";
            PlayerPrefs.DeleteKey("NetworkErrorReason"); // На всякий случай чистим мусор в памяти

            // Первым делом гасим ВСЕ панели, чтобы гарантировать отсутствие наложений
            fader.SetPanelInstant(panelStart, false);
            fader.SetPanelInstant(panelSelectMode, false);
            fader.SetPanelInstant(panelHostSetup, false);
            fader.SetPanelInstant(panelClientSetup, false);
            fader.SetPanelInstant(panelAdditionalSellect, false);
            fader.SetPanelInstant(panelGameSettings, false);

            // --- ЧТЕНИЕ СЕТЕВЫХ ОШИБОК ИЗ АУТЕНТИФИКАТОРА ---
            string networkError = PlayerPrefs.GetString("NetworkErrorReason", "");
            if (!string.IsNullOrEmpty(networkError))
            {
                PlayerPrefs.DeleteKey("NetworkErrorReason"); // Очищаем

                // 1. Мгновенно ставим камеру к столу (будто она туда прилетела)
                if (cameraController != null) cameraController.SetToTableInstant();

                // 2. Выводим ошибку на панель выбора режима
                ShowNetworkError(networkError);

                // ShowNetworkError внутри себя вызывает cameraController.FlyToIdle();
                // Таким образом, игрок увидит красивый отлет камеры с ошибкой
                return; // Прерываем дальнейший Awake
            }

            // Проверяем, вернулись ли мы только что из игры?
            bool returnedFromGame = PlayerPrefs.GetInt("ReturnedFromGame", 0) == 1;

            if (returnedFromGame)
            {
                fader.SetPanelInstant(panelStart, false);
                fader.SetPanelInstant(panelSelectMode, true);
            }
            else
            {
                fader.SetPanelInstant(panelStart, true);
                fader.SetPanelInstant(panelSelectMode, false);
            }

            // Подписываемся на изменение текста в поле ввода
            if (nameInputField != null)
            {
                nameInputField.onValueChanged.AddListener(OnNameTextChanged);
            }

            // Вызываем один раз при старте, чтобы проверить дефолтное имя
            OnNameTextChanged(nameInputField != null ? nameInputField.text : "");
        }

        private void Start()
        {
        #if !UNITY_EDITOR
            // Билд будет использовать ключ "CarcaPlayerToken_Build"
            FindFirstObjectByType<TokenAuthenticator>().localPlayerTokenKey = "CarcaPlayerToken_Build";
        #endif
        }

        // --- МЕТОДЫ ДЛЯ КНОПОК ПЕРЕХОДА ---

        // Кнопка "ИГРАТЬ" на стартовом экране
        public void OnPlayBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelSelectMode, panelStart);
        }

        // Кнопка "НАЗАД" в выборе режима
        public void OnBackFromSelectBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelStart, panelSelectMode);
        }

        // Кнопка "Создать комнату" в выборе режима
        public void OnCreateRoomBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelHostSetup, panelSelectMode);
        }

        // Кнопка "Назад" в настройках хоста
        public void OnBackFromHostSetupBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelSelectMode, panelHostSetup);
        }

        // Кнопка "Подключиться" в выборе режима
        public void OnConnectRoomBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelClientSetup, panelSelectMode);
        }

        // Кнопка "Назад" в панели подключения
        public void OnBackFromConnectRoomBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelSelectMode, panelClientSetup);
        }

        // Кнопка "Дополнительно" на начальной панели
        public void OnAdditionalSellectBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelAdditionalSellect, panelStart);
        }

        // Кнопка "Назад" в панели "дополнительно"
        public void OnBackFromAdditionalSellectBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelStart, panelAdditionalSellect);
        }

        // Кнопка "Настройки" в начальной панельке
        public void OnSettingsBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelGameSettings, panelStart);
        }

        // Кнопка "Назад" из настроек
        public void OnBackFromSettingsBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            fader.SwitchToPanel(panelStart, panelGameSettings);
        }

        // Кнопка "ВЫХОД" на стартовом экране
        public void OnExitBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            Application.Quit();
            // Для работы в редакторе (чтобы видеть, что кнопка сработала):
            #if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }

        // --- МЕТОДЫ ЗАПУСКА ИГРЫ (MIRROR) ---

        // Кнопка "СТАРТ ХОСТА" (В панели HostSetup)
        public void OnHostStartGameClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            SavePlayerName();

            int selectedModeIndex = modeDropdown != null ? modeDropdown.value : 1;
            PlayerPrefs.SetInt("SelectedGameMode", selectedModeIndex);

            // НОВОЕ: Сохраняем сложность
            int selectedDiffIndex = difficultyDropdown != null ? difficultyDropdown.value : 1;
            PlayerPrefs.SetInt("SelectedBotDifficulty", selectedDiffIndex);

            OnNameTextChanged(nameInputField != null ? nameInputField.text : "");

            StartCoroutine(HostTransitionRoutine());
        }

        // Кнопка "ПОДКЛЮЧИТЬСЯ"
        public void OnClientConnectClicked()
        {
            AudioManager.Instance.PlaySFX("Click");

            SavePlayerName();
            OnNameTextChanged(nameInputField != null ? nameInputField.text : "");

            string ipAddress = ipInputField != null ? ipInputField.text : "";
            if (string.IsNullOrEmpty(ipAddress)) ipAddress = "localhost";

            if (networkErrorText != null) networkErrorText.text = "Подключение...";

            NetworkManager.singleton.networkAddress = ipAddress;

            // Запускаем кинематографичный полет ДО подключения
            StartCoroutine(ClientTransitionRoutine());
        }

        private IEnumerator ClientTransitionRoutine()
        {
            // Убиваем все предыдущие анимации Fade, чтобы они не перекрыли друг друга
            DOTween.Kill(panelSelectMode);
            DOTween.Kill(panelClientSetup);

            // 1. Плавно гасим меню
            fader.SwitchToPanel(null, panelSelectMode);

            // 2. Камера летит к столу
            if (cameraController != null)
            {
                cameraController.FlyToTable();
            }

            // 3. Ждем, пока она долетит
            yield return new WaitForSeconds(transitionDuration);

            DOTween.KillAll();

            // 4. И ТОЛЬКО ТЕПЕРЬ стучимся на сервер
            NetworkManager.singleton.StartClient();
        }

        private void SavePlayerName()
        {
            if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text))
            {
                PlayerName = nameInputField.text;
            }
            else
            {
                PlayerName = "Player_" + Random.Range(1000, 9999);
            }
        }

        /// <summary>
        /// Кнопка перехода на сцену с обучением.
        /// </summary>
        public void OnTutorialBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            StartCoroutine(AdditionalTransitionRoutine(true));
        }

        /// <summary>
        /// Кнопка перехода на сцену симуляции ИИ.
        /// </summary>
        public void OnSimulationBtnClicked()
        {
            AudioManager.Instance.PlaySFX("Click");
            StartCoroutine(AdditionalTransitionRoutine(false));
        }

        // НОВОЕ: Публичный метод для отображения сетевых ошибок (будем вызывать из Аутентификатора)
        public void ShowNetworkError(string errorMsg)
        {
            // Возвращаем камеру назад
            if (cameraController != null) cameraController.FlyToIdle();

            // Жестко гасим все панели перед тем, как показать нужную
            fader.SetPanelInstant(panelStart, false);
            fader.SetPanelInstant(panelHostSetup, false);
            fader.SetPanelInstant(panelAdditionalSellect, false);
            fader.SetPanelInstant(panelGameSettings, false);
            fader.SetPanelInstant(panelSelectMode, false);

            // Возвращаем панель подключения, если она исчезла
            fader.SetPanelInstant(panelClientSetup, true);
            if (panelHostSetup != null) fader.SetPanelInstant(panelHostSetup, false);

            if (networkErrorText != null)
            {
                networkErrorText.text = errorMsg;
            }
        }

        // Логика валидации имени в реальном времени
        private void OnNameTextChanged(string inputName)
        {
            if (nameErrorText != null) nameErrorText.text = "";

            bool isValid = true;

            if (string.IsNullOrWhiteSpace(inputName))
            {
                if (nameErrorText != null) nameErrorText.text = "Введите имя!";
                isValid = false;
            }
            else if (inputName.Length > 18)
            {
                if (nameErrorText != null) nameErrorText.text = "Имя слишком длинное (макс 18 символов)!";
                isValid = false;
            }
            else if (!Regex.IsMatch(inputName, @"^[\wА-Яа-яЁё]+$"))
            {
                if (nameErrorText != null) nameErrorText.text = "Разрешены только буквы, цифры и _";
                isValid = false;
            }
            else if (inputName == "BOT" || inputName.ToUpper() == "БОТ")
            {
                if (nameErrorText != null) nameErrorText.text = "Это имя зарезервировано!";
                isValid = false;
            }

            // БЛОКИРУЕМ ИЛИ РАЗБЛОКИРУЕМ КНОПКИ
            if (btnCreateRoom != null) btnCreateRoom.interactable = isValid;
            if (btnConnect != null) btnConnect.interactable = isValid;

            if (isValid) PlayerName = inputName;
        }

        private IEnumerator HostTransitionRoutine()
        {
            // 1. Плавно гасим меню настроек
            fader.SwitchToPanel(null, panelHostSetup);

            // 2. Запускаем анимацию пролета камеры
            if (cameraController != null) cameraController.FlyToTable();

            // 3. Ждем окончания пролета камеры
            yield return new WaitForSeconds(transitionDuration);

            DOTween.KillAll();

            // 4. Только теперь запускаем Mirror
            NetworkManager.singleton.StartHost();
        }

        /// <summary>
        /// Метод курутины, который запускает пролёт камеры и открывает выбранную сцену.
        /// </summary>
        /// <param name="choice">Выбор сцены; true - обучение; false - симуляция ИИ</param>
        /// <returns></returns>
        private IEnumerator AdditionalTransitionRoutine(bool choice)
        {
            // 1. Гасим меню Дополнительно
            fader.SwitchToPanel(null, panelAdditionalSellect);

            // 2. Запускаем пролет камеры
            if (cameraController != null) cameraController.FlyToTable();

            // 3. Ждем окончания полета
            yield return new WaitForSeconds(transitionDuration);

            DOTween.KillAll();

            if (choice)
                SceneManager.LoadScene("TutorialScene");
            else
                SceneManager.LoadScene("BotArenaScene");
        }
    }
}