using Assets.Scripts.Core_logic;
using Assets.Scripts.Main;
using Assets.Scripts.Network;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Scripts.View
{
    public class MeepleActionUI : MonoBehaviour
    {
        [Header("3D Panel Settings")]
        public GameObject actionPanel3D;
        public Camera mainCamera;

        [Header("Local Positions")]
        public Vector3 hiddenLocalPos = new Vector3(0f, -5f, 0f);
        public Vector3 visibleLocalPos = new Vector3(0f, -2f, 0f);

        [Header("Panel Animation Settings")]
        public float animationSpeed = 8f;
        public float referenceFOV = 60f;

        [Header("3D Buttons Settings")]
        public float buttonHoverDepth = -0.2f; // Насколько сильно кнопка "утапливается" по локальной оси Y
        public float buttonAnimSpeed = 15f;    // Скорость нажатия/отжатия кнопки

        [Header("3D Buttons References")]
        public Interactive3DButton btnPlaceMeeple;
        public Interactive3DButton btnPlaceAbbot;
        public Interactive3DButton btnRetrieveAbbot;
        public Interactive3DButton btnSkip;

        private CarcaGameManager _gm;
        public static int SelectedTool = 0;
        private bool _isPanelVisible = false;
        private Vector3 _originalScale;

        void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (actionPanel3D != null) _originalScale = actionPanel3D.transform.localScale;

            transform.localPosition = hiddenLocalPos;

            // Инициализируем стартовые позиции кнопок
            btnPlaceMeeple.Initialize();
            btnPlaceAbbot.Initialize();
            btnRetrieveAbbot.Initialize();
            btnSkip.Initialize();
        }

        void Update()
        {
            if (_gm == null) _gm = FindFirstObjectByType<CarcaGameManager>();
            if (_gm == null || mainCamera == null) return;

            CarcaPlayer localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<CarcaPlayer>();
            bool isMyTurnPhase1 = (_gm.currentPhase == 1 && localPlayer != null && _gm.currentTurnPlayerToken == localPlayer.SessionToken);

            if (isMyTurnPhase1 && !_isPanelVisible) ShowPanel(localPlayer);
            else if (!isMyTurnPhase1 && _isPanelVisible) _isPanelVisible = false;

            // --- 1. АНИМАЦИЯ САМОЙ ПАНЕЛИ (SmoothFollow) ---
            Vector3 targetLocalPos = _isPanelVisible ? visibleLocalPos : hiddenLocalPos;
            float fovFactor = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(referenceFOV * 0.5f * Mathf.Deg2Rad);
            targetLocalPos *= fovFactor;

            if (actionPanel3D != null)
            {
                actionPanel3D.transform.localScale = _originalScale * fovFactor;
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, Time.deltaTime * animationSpeed);
            }

            // --- 2. ЛОГИКА 3D-КНОПОК (Hover и Click) ---
            if (_isPanelVisible && Mouse.current != null)
            {
                GameObject hoveredObj = null;

                // Пускаем луч, чтобы понять, на что смотрит мышка
                Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    hoveredObj = hit.collider.gameObject;
                }

                // Обновляем состояние каждой кнопки
                UpdateButtonState(btnPlaceMeeple, hoveredObj, () => SelectTool(0));
                UpdateButtonState(btnPlaceAbbot, hoveredObj, () => SelectTool(1));
                UpdateButtonState(btnRetrieveAbbot, hoveredObj, RetrieveAbbotAction);
                UpdateButtonState(btnSkip, hoveredObj, SkipAction);
            }
        }

        // Обработка наведения, клика и анимации утапливания
        private void UpdateButtonState(Interactive3DButton btn, GameObject hoveredObj, Action onClickAction)
        {
            if (btn.buttonObject == null) return;

            Vector3 targetPos = btn.baseLocalPos;

            // Проверяем, попал ли луч в саму кнопку или её дочерние объекты (например, меш внутри пустышки)
            bool isHovered = hoveredObj != null && hoveredObj.transform.IsChildOf(btn.buttonObject.transform);

            if (btn.isInteractable && isHovered)
            {
                // Эффект наведения: "утапливаем" кнопку по локальной оси Y
                targetPos.y += buttonHoverDepth;

                // Если в этот момент нажали ЛКМ - выполняем действие
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    onClickAction?.Invoke();
                }
            }

            // Плавное движение кнопки вверх-вниз
            btn.buttonObject.transform.localPosition = Vector3.Lerp(
                btn.buttonObject.transform.localPosition,
                targetPos,
                Time.deltaTime * buttonAnimSpeed
            );
        }

        private void ShowPanel(CarcaPlayer player)
        {
            _isPanelVisible = true;
            SelectedTool = 0; // Сбрасываем выбор на Мипла по умолчанию

            // Красим модельки мипла и аббата в цвет игрока
            btnPlaceMeeple.SetColor(player.PlayerColor);
            btnPlaceAbbot.SetColor(player.PlayerColor);

            bool hasMonastery = false;
            if (_gm.LocalTileInHand != null)
            {
                foreach (var region in _gm.LocalTileInHand.Regions)
                {
                    if (region.Type == TerrainType.Monastery) { hasMonastery = true; break; }
                }
            }

            bool canPlaceMeepleAnywhere = _gm.LocalValidRegionIds != null && _gm.LocalValidRegionIds.Length > 0;

            // Настраиваем интерактивность кнопок (устанавливаем прозрачность, если нельзя нажать)
            btnPlaceMeeple.SetInteractable(player.MeeplesAvailable > 0 && canPlaceMeepleAnywhere);
            btnPlaceAbbot.SetInteractable(player.HasAbbot && hasMonastery && canPlaceMeepleAnywhere);
            btnRetrieveAbbot.SetInteractable(!player.HasAbbot);
            btnSkip.SetInteractable(true); // Пропустить ход можно всегда

            // Автовыбор инструмента
            if (!btnPlaceMeeple.isInteractable && btnPlaceAbbot.isInteractable) SelectTool(1);
            else if (!btnPlaceMeeple.isInteractable && !btnPlaceAbbot.isInteractable) SelectTool(-1);
        }

        private void SelectTool(int toolType)
        {
            SelectedTool = toolType;
            Debug.Log(toolType == 0 ? "Выбран Мипл" : "Выбран Аббат");
        }

        private void SkipAction()
        {
            CarcaPlayer p = Mirror.NetworkClient.localPlayer?.GetComponent<CarcaPlayer>();
            if (p != null) 
            { 
                _gm.ClearMeepleTargets(); 
                p.CmdSkipMeeple(); 
            }
        }

        private void RetrieveAbbotAction()
        {
            CarcaPlayer p = Mirror.NetworkClient.localPlayer?.GetComponent<CarcaPlayer>();
            if (p != null) 
            { 
                _gm.ClearMeepleTargets(); 
                p.CmdRetrieveAbbot(); 
            }
        }

        // Вызывается Сервером в конце каждого хода, чтобы гарантированно убить зависшие панели
        public void ForceHidePanel()
        {
            _isPanelVisible = false;
        }
    }
}