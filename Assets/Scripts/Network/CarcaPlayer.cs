using Assets.Scripts.Core_logic;
using Assets.Scripts.Main;
using Assets.Scripts.Media;
using Assets.Scripts.View;
using DG.Tweening;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Network
{
    public class CarcaPlayer : NetworkBehaviour
    {
        private CarcaGameManager _gameManager;

        [SyncVar(hook = nameof(OnPlayerStatsChanged))]
        public int Score = 0;
        [SyncVar(hook = nameof(OnMeeplesChanged))]
        public int MeeplesAvailable = 7;
        [SyncVar(hook = nameof(OnColorChanged))]
        public Color PlayerColor = Color.white;

        [SyncVar(hook = nameof(OnAbbotChanged))]
        public bool HasAbbot = true; // У каждого игрока 1 аббат на всю игру

        [SyncVar] public string SessionToken = "";

        [SyncVar] public int PersistentId;

        // Настройки перетаскивания из панели выбора действий
        private GameObject _draggedMeeple;
        private int _draggedMeepleType;
        private bool _isDragging = false;

        void Start()
        {
            _gameManager = FindFirstObjectByType<CarcaGameManager>();
            Debug.Log($"[Client {netId}] Start: Найден GameManager = {_gameManager != null}");

            // Когда этот префаб спавнится (даже у чужих игроков по сети),
            // мы заставляем UI перерисовать список гобеленов
            CarcaGameManager.RequestUIUpdate();
        }

        void Update()
        {
            if (!isLocalPlayer || Mouse.current == null) return;
            if (!_gameManager.isGameStarted) return;

            if (_gameManager.currentTurnPlayerToken != this.SessionToken)
            {
                if (_gameManager.GhostTile != null) _gameManager.GhostTile.SetActive(false);
                return;
            }

            // ФАЗА 0: ТАЙЛ
            if (_gameManager.currentPhase == 0)
            {
                if (_gameManager.GhostTile != null) _gameManager.GhostTile.SetActive(true);

                if (Mouse.current.rightButton.wasPressedThisFrame)
                    _gameManager.RotateCurrentTileLocal();

                Vector2 mousePos = Mouse.current.position.ReadValue();
                Ray ray = Camera.main.ScreenPointToRay(mousePos);
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    int gridX = Mathf.RoundToInt(hitPoint.x / _gameManager.TileSize);
                    int gridY = Mathf.RoundToInt(hitPoint.z / _gameManager.TileSize);
                    Vector2Int targetPos = new Vector2Int(gridX, gridY);

                    _gameManager.UpdateGhostTile(targetPos);

                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        if (_gameManager.LocalBoard.CanPlaceTile(_gameManager.LocalTileInHand, targetPos))
                        {
                            Debug.Log($"[Client {netId}] Отправляю CmdPlaceTile: X={targetPos.x}, Y={targetPos.y}");
                            CmdPlaceTile(targetPos.x, targetPos.y, _gameManager.LocalTileInHand.Rotation);
                        }
                        else
                        {
                            // Озвучка ошибки
                            AudioManager.Instance.PlaySFX("TileError");
                        }
                    }
                }
            }
            // ФАЗА 1: МИПЛ
            else if (_gameManager.currentPhase == 1)
            {
                if (_isDragging && _draggedMeeple != null)
                {
                    // --- 1. ПРОВЕРКА ОТПУСКАНИЯ КНОПКИ (БРОСОК) ---
                    // (Используем метод wasReleasedThisFrame, так как isPressed может не успеть отработать)
                    if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame)
                    {
                        _isDragging = false;

                        Vector2 dropMousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                        Ray dropRay = Camera.main.ScreenPointToRay(dropMousePos);

                        // ИСПРАВЛЕНИЕ: Создаем LayerMask для слоя ActiveTile
                        int layerMask = 1 << LayerMask.NameToLayer("ActiveTile");

                        // Бросаем луч ТОЛЬКО в слой ActiveTile
                        if (!Physics.Raycast(dropRay, out RaycastHit dropHit, 100f, layerMask))
                        {
                            // Если мы отпустили кнопку над старым тайлом или пустотой - отскок
                            PlayRejectAnimation(_draggedMeeple);
                            return;
                        }

                        RegionCollider hitRegion = dropHit.collider.GetComponent<RegionCollider>();
                        int targetRegionId = (hitRegion != null) ? hitRegion.regionId : -1;

                        _gameManager.GetHoveredRegionStatus(targetRegionId, _draggedMeepleType, out int finalTargetId);

                        if (finalTargetId != -1)
                        {
                            // Находим локальную точку броска относительно корня тайла
                            Transform tileRoot = dropHit.collider.transform.root;
                            Vector3 localDropOffset = tileRoot.InverseTransformPoint(dropHit.point);

                            // Отправляем ID региона и локальное смещение на сервер
                            CmdPlaceMeeple(targetRegionId, _draggedMeepleType, localDropOffset);

                            Destroy(_draggedMeeple);
                        }
                        else
                        {
                            var ghostScript = _draggedMeeple.GetComponent<MeepleDragGhost>();
                            if (ghostScript != null) ghostScript.ResetToReject(this.PlayerColor);

                            PlayRejectAnimation(_draggedMeeple);
                        }
                        return;
                    }

                    // --- 2. ДВИГАЕМ МИПЛА ЗА МЫШКОЙ ---
                    Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                    Ray ray = Camera.main.ScreenPointToRay(mousePos);

                    // ИСПРАВЛЕНИЕ: Движение голограммы
                    // Чтобы голограмма не "падала" сквозь стол, когда мы ведем её над старыми тайлами,
                    // мы будем бросать луч в любую геометрию (без маски), чтобы просто получить высоту стола.
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        Vector3 hitPoint = hit.point;
                        _draggedMeeple.transform.position = hitPoint + new Vector3(0, 0.7f, 0);

                        // НО ПРОВЕРКУ СТАТУСА мы делаем ТОЛЬКО если попали в ActiveTile
                        int hoveredRegionId = -1;
                        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("ActiveTile"))
                        {
                            RegionCollider hitRegion = hit.collider.GetComponent<RegionCollider>();
                            if (hitRegion != null) hoveredRegionId = hitRegion.regionId;
                        }

                        string statusMsg = _gameManager.GetHoveredRegionStatus(hoveredRegionId, _draggedMeepleType, out int finalRegionId);
                        bool isValid = (finalRegionId != -1);

                        var ghostScript = _draggedMeeple.GetComponent<MeepleDragGhost>();
                        if (ghostScript != null)
                        {
                            ghostScript.SetStatus(isValid, statusMsg, this.PlayerColor);
                        }
                    }
                }
            }
        }

        [Command]
        public void CmdPlaceTile(int posX, int posY, int rotation)
        {
            Debug.Log($"[Server] Получил CmdPlaceTile от {netId}");
            Vector2Int pos = new Vector2Int(posX, posY);
            _gameManager.ServerTryPlaceTile(pos, rotation, this);
        }

        [Command]
        public void CmdSkipMeeple()
        {
            Debug.Log($"[Server] Получил CmdSkipMeeple от {netId}");
            _gameManager.ServerEndTurn();
        }

        /// <summary>
        /// Метод запроса на размещение мипла.
        /// </summary>
        /// <param name="regionId"> Id региона</param>
        /// <param name="meepleType"> Тип мипла: 0 - обычный; 1 - аббат</param>
        [Command]
        public void CmdPlaceMeeple(int regionId, int meepleType, Vector3 localOffset)
        {
            Debug.Log($"[Server] Получил CmdPlaceMeeple от {netId}. Смещение: {localOffset}");
            _gameManager.ServerTryPlaceMeeple(regionId, meepleType, localOffset, this);
        }


        // ------------------------------------------
        //                  ХУКИ
        // ------------------------------------------

        // Хук для очков
        private void OnPlayerStatsChanged(int oldVal, int newVal)
        {
            // Убеждаемся, что значение точно применилось
            Score = newVal; // Принудительно
            CarcaGameManager.RequestUIUpdate();
        }

        // Хук для миплов
        private void OnMeeplesChanged(int oldVal, int newVal)
        {
            MeeplesAvailable = newVal; // Принудительно
            CarcaGameManager.RequestUIUpdate();
        }

        // Хук для аббата
        private void OnAbbotChanged(bool oldVal, bool newVal)
        {
            HasAbbot = newVal;
            CarcaGameManager.RequestUIUpdate();
        }

        // В хуке цвета (если он уже есть) тоже дергаем UI
        private void OnColorChanged(Color oldColor, Color newColor)
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = newColor;

            CarcaGameManager.RequestUIUpdate();
        }

        /// <summary>
        /// Метод запроса на снятие аббата.
        /// </summary>
        /// <param name="monasteryPos"></param>
        [Command]
        public void CmdRetrieveAbbot()
        {
            _gameManager.ServerTryRetrieveAbbot(this);
        }

        // ------------------------------------------
        //               ДРУГИЕ МЕТОДЫ
        // ------------------------------------------

        public void StartDraggingMeeple(int type, GameObject prefab)
        {
            Debug.Log($"[PLAYER] Метод StartDraggingMeeple вызван! Тип фигурки: {type}");

            // Проверка 1: Фаза игры
            if (_gameManager.currentPhase != 1)
            {
                Debug.LogWarning($"[PLAYER] Отказ: Сейчас фаза {_gameManager.currentPhase}, а должна быть 1 (Установка мипла)!");
                return;
            }

            // Проверка 2: Наличие префаба
            if (prefab == null)
            {
                Debug.LogError("[PLAYER] Отказ: Префаб голограммы равен NULL! Проверь, перетащил ли ты DragMeepleGhost в слот 'Drag Prefab' на скрипте DragSource в инспекторе!");
                return;
            }

            _draggedMeepleType = type;
            _isDragging = true;

            // Спавним локальную голограмму
            _draggedMeeple = Instantiate(prefab);
            Debug.Log($"[PLAYER] Голограмма '{_draggedMeeple.name}' успешно создана в памяти!");

            // Красим в цвет игрока
            _draggedMeeple.GetComponentInChildren<Renderer>().material.color = this.PlayerColor;

            // Отключаем физику, чтобы фигурка не мешала лучам мышки
            Collider[] colliders = _draggedMeeple.GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = false;

            Debug.Log("[PLAYER] Подготовка голограммы завершена. Теперь она должна двигаться в Update!");
        }

        // Анимация отказа (через DOTween)
        private void PlayRejectAnimation(GameObject meeple)
        {
            AudioManager.Instance.PlaySFX("MeepleBounce");

            meeple.GetComponentInChildren<Renderer>().material.color = this.PlayerColor;

            // Прыгаем в случайную сторону назад от места сброса
            Vector3 randomDirection = new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
            Vector3 targetPos = meeple.transform.position + randomDirection;

            // 1. ПЛАВНЫЙ ПРЫЖОК
            meeple.transform.DOJump(targetPos, jumpPower: 1.5f, numJumps: 1, duration: 0.8f)
                  .SetEase(Ease.OutQuad);

            // 2. ИСПРАВЛЕНИЕ ВРАЩЕНИЯ:
            // Вместо хаотичных трех осей, мы заставляем мипла сделать ровно 
            // один красивый оборот (на 360 градусов) назад по оси Z
            // Используем стандартный RotateMode.Fast для чистоты вращения.
            meeple.transform.DORotate(new Vector3(0, 0, 360f), 0.8f, RotateMode.FastBeyond360)
                  .SetEase(Ease.OutQuad);

            // Плавно уменьшаем его в воздухе в конце прыжка, чтобы он исчез красиво
            meeple.transform.DOScale(0f, 0.2f).SetDelay(0.6f);

            Destroy(meeple, 0.85f);
        }
    }
}