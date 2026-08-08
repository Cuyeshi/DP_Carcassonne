using Assets.Scripts.Core_logic;
using Assets.Scripts.Media;
using Assets.Scripts.View;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        [Header("References")]
        public TutorialGuide guide;
        public Camera mainCamera;
        public float tileSize = 1.0f;

        [Header("3D Action Panel")]
        public GameObject actionPanel3D; // 3D панель с миплами
        public Vector3 panelHiddenPos = new Vector3(0, -5, 5);
        public Vector3 panelVisiblePos = new Vector3(0, -1, 5);

        [Header("Prefabs")]
        public GameObject defaultPrefab;
        public List<TutorialTileBinding> tilePrefabs;
        public GameObject meeplePrefab;
        public GameObject botMeeplePrefab;
        public GameObject tileDustVFX; // Пыль
        public GameObject meepleDustVFX;
        public float propScaleMultiplier = 0.5f;

        [Header("Prop Settings")]
        public Transform propsPivot; // Пустой объект в воздухе рядом с Большим Миплом

        private Board _board;
        private DeckManager _deck;
        private Player _player;
        private Player _bot;

        // --- СОСТОЯНИЯ СЮЖЕТА ---
        private TileData _tileInHand;
        private bool _isPlayerTurnActive = false;
        private Vector2Int? _expectedTilePos = null;
        private int? _expectedTileRotation = null;

        // Для мипла
        private bool _waitingForMeeple = false;
        private int _expectedMeepleRegionId = -1;

        // --- ВИЗУАЛ ГОЛОГРАММЫ ---
        private GameObject _ghostTile;
        private Renderer[] _ghostRenderers;
        private Vector3 _ghostTargetWorldPos;
        private Vector2Int _lastHoveredGridPos = new Vector2Int(-999, -999);

        // --- DRAG & DROP МИПЛА ---
        private GameObject _draggedMeeple;
        private int _draggedMeepleType;
        private bool _isDragging = false;
        private GameObject _spawnedPropGroup;

        void Start()
        {
            // Указание цвета для мипла игрока и мипла бота
            InstallingСertainColor(meeplePrefab, "#3b699e");
            InstallingСertainColor(botMeeplePrefab, "#A83232");

            _board = new Board();
            _deck = new DeckManager();
            _player = new Player(1);
            _bot = new Player(2);

            TileData startTile = _deck.CreateClassicStartTile();
            _board.PlaceTile(startTile, Vector2Int.zero);
            SpawnTileVisually(startTile, Vector2Int.zero);

            StartCoroutine(TutorialFlow());
        }

        // ------------------------------------------
        // ГЛАВНЫЙ СЦЕНАРИЙ ОБУЧЕНИЯ
        // ------------------------------------------
        private IEnumerator TutorialFlow()
        {
            // Задержка перед тем, как уйдёт перекрывающая панель
            yield return new WaitForSeconds(2.0f);

            // --- ЭТАП 1: Приветствие ---
            bool dialogueDone = false;
            guide.StartDialogue(new string[] {
                "Здравствуйте, юный господин! Добро пожаловать в Каркассон.",
                "Игра представляет собой пошаговую стратегию, где мы строим средневековое княжество.",
                "Сейчас Ваш ход. Поставьте тайл дороги СПРАВА от стартовой плитки!"
            }, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            GiveTileToPlayer("Tile23_RoadTurn");
            _expectedTilePos = new Vector2Int(1, 0);
            _expectedTileRotation = 0;

            _isPlayerTurnActive = true;
            yield return new WaitUntil(() => !_isPlayerTurnActive);

            // --- ЭТАП 2: Рассказ про города и Щиты (Вращающийся проп) ---
            dialogueDone = false;

            // 1. Спавним проп города со щитом сбоку
            ShowPropTile("Tile12_CornerRoadShield", out GameObject shieldProp, true);

            guide.StartDialogue(new string[] {
                "Отлично! Обратите внимание: в игре есть разные города.",
                "Некоторые города имеют небольшие статуи. Присмотритесь в угол этой плитки.",
                "Города с такой статуей приносят дополнительные очки при завершении строительства.",
                "Я дам Вам обычный город. Поставьте его СПРАВА от Вашего поворота дороги.",
                "Если не получится поставить плиточку, то её можно повернуть на правую кнопку мыши (ПКМ)!"
            }, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            // 2. Убираем проп
            HideAndDestroyProp(shieldProp);

            // --- ЭТАП 3: Установка Города ---
            GiveTileToPlayer("Tile17_CityCap");
            _expectedTilePos = new Vector2Int(2, 0);
            _expectedTileRotation = 1;

            _isPlayerTurnActive = true;
            yield return new WaitUntil(() => !_isPlayerTurnActive);

            // --- ЭТАП 4: Установка Мипла (Иммерсивная) ---
            dialogueDone = false;
            guide.StartDialogue(new string[] {
                "А теперь перетащите своего Рыцаря прямо в этот город!",
                "Возьмите его с первой ячейки панели внизу экрана и бросьте на город."
            }, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            // Ищем ID региона Города на только что поставленном тайле
            TileData placedCityTile = _board.GetTileAt(new Vector2Int(2, 0));
            TileRegion cityReg = placedCityTile.Regions.Find(r => r.Type == TerrainType.City);
            _expectedMeepleRegionId = cityReg.Id;

            // Выдвигаем панель и ждем установки
            actionPanel3D.transform.DOLocalMove(panelVisiblePos, 0.5f).SetEase(Ease.OutBack);
            _waitingForMeeple = true;
            yield return new WaitUntil(() => !_waitingForMeeple);

            // Прячем панель
            actionPanel3D.transform.DOLocalMove(panelHiddenPos, 0.5f).SetEase(Ease.InBack);

            // --- ЭТАП 5: Монастыри и Орбита ---
            dialogueDone = false;
            ShowPropTile("Tile1_Monastery", out GameObject monProp, true); // true = заставляем крутиться

            guide.StartDialogue(new string[] {
                "Идеально! Помимо городов и дорог, в игре есть Монастыри.",
                "Да, на монастыри можно ставить пешки, даже Аббатов!",
                "Монастырь приносит очки, когда он полностью окружен другими тайлами."
            }, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            // Во время диалога стыкуем два поля к крутящемуся монастырю
            yield return new WaitForSeconds(0.5f);
            AttachTileToPropOrbit("Tile22_StraightRoad", new Vector3(1, 0, 0));
            yield return new WaitForSeconds(0.5f);
            AttachTileToPropOrbit("Tile19_CityCap_CurveSE", new Vector3(1, 0, 1));
            yield return new WaitForSeconds(0.5f);
            AttachTileToPropOrbit("Tile17_CityCap", new Vector3(0, 0, 1));
            yield return new WaitForSeconds(0.5f);
            AttachTileToPropOrbit("Tile16_TwoCitiesOpp", new Vector3(-1, 0, 1));
            yield return new WaitForSeconds(0.5f);
            AttachTileToPropOrbit("Tile16_TwoCitiesOpp", new Vector3(-1, 0, 0));
            yield return new WaitForSeconds(0.5f);
            AttachTileToPropOrbit("Tile12_CityCorner_Road", new Vector3(-1, 0, -1));
            yield return new WaitForSeconds(0.5f);
            AttachTileToPropOrbit("Tile24_Cross3", new Vector3(0, 0, -1));
            yield return new WaitForSeconds(0.5f);
            AttachTileToPropOrbit("Tile25_Cross4", new Vector3(1, 0, -1));

            dialogueDone = false;

            // Текст пристыковки тайлов к монастырю
            guide.StartDialogue(new string[] {
                "Вот так тайлы выкладываются вокруг него, формируя квадрат.",
                "Когда квадрат 3х3 будет заполнен, Вы получите 9 очков!",
                "Преимущество Аббата здесь в том, что его можно убрать доcрочно",
                "К примеру, если вокруг монастыря только 1 плитка, то Вы получите 2 очка (1+1)"
            }, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);
            
            HideAndDestroyProp(_spawnedPropGroup); // Удаляем всю орбиту

            // --- ЭТАП 6: Закрытие города ---
            dialogueDone = false;
            guide.StartDialogue(new string[] {
                "Вернемся к Вашему городу. Закройте его этой крышечкой."
            }, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            GiveTileToPlayer("Tile17_CityCap_2");
            _expectedTilePos = new Vector2Int(3, 0);
            _expectedTileRotation = 3;

            _isPlayerTurnActive = true;
            yield return new WaitUntil(() => !_isPlayerTurnActive);

            yield return new WaitForSeconds(1.5f);

            // --- ЭТАП 7: Ход Бота ---
            dialogueDone = false;
            guide.StartDialogue(new string[] {
                $"Город достроен! Вы получили очки. Теперь мой ход.",
                "Я пристрою дорогу и выставлю разбойника."
            }, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            yield return new WaitForSeconds(1.5f);
            SimulateBotTurn();
            yield return new WaitForSeconds(1f);

            // --- ЭТАП 8: Финал ---
            dialogueDone = false;
            guide.StartDialogue(new string[] {
                "Запомните: НЕЛЬЗЯ ставить человечка на объект, если там УЖЕ стоит кто-то другой!",
                "На этом основы закончены. Удачи в реальном бою!"
            }, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            yield return new WaitForSeconds(1f);

            SceneManager.LoadScene("MenuScene");
        }

        // ------------------------------------------
        // МАГИЯ ПРОПОВ (ОРБИТА)
        // ------------------------------------------

        private void ShowPropTile(string tileId, out GameObject propObj, bool spinGroup = false)
        {
            GameObject prefab = GetPrefabForTile(tileId);

            // Создаем группу, которая будет висеть возле Большого Мипла
            _spawnedPropGroup = new GameObject("PropOrbitGroup");

            // ИСПРАВЛЕНИЕ: Теперь проп вылетает СБОКУ (по оси X влево на 10 метров), а не снизу
            _spawnedPropGroup.transform.position = propsPivot.position + new Vector3(10f, 0f, 0f);

            // Спавним сам тайл внутри группы
            propObj = Instantiate(prefab, _spawnedPropGroup.transform);
            propObj.transform.localPosition = Vector3.zero;
            // НОВОЕ: Уменьшаем масштаб летящего тайла
            propObj.transform.localScale = prefab.transform.localScale * propScaleMultiplier;

            // Летим к пивоту возле мипла
            _spawnedPropGroup.transform.DOMove(propsPivot.position, 1f)
                .SetEase(Ease.OutBack)
                .SetDelay(3f)
                .OnComplete(() => {
                    // ИСПРАВЛЕНИЕ: Запускаем бесконечное вращение ТОЛЬКО когда проп долетел до места
                    // Это полностью убирает конфликт двух анимаций DOTween на одном трансформе.
                    if (spinGroup)
                    {
                        _spawnedPropGroup.transform.DORotate(new Vector3(0, 360, 0), 6f, RotateMode.FastBeyond360)
                                         .SetLoops(-1, LoopType.Incremental)
                                         .SetEase(Ease.Linear);
                    }
                });
        }

        private void AttachTileToPropOrbit(string tileId, Vector3 localOffset)
        {
            if (_spawnedPropGroup == null) return;

            GameObject prefab = GetPrefabForTile(tileId);

            // Спавним высоко над группой
            GameObject newProp = Instantiate(prefab, _spawnedPropGroup.transform);
            newProp.transform.localPosition = localOffset + new Vector3(0, 3f, 0);

            // НОВОЕ: Уменьшаем масштаб пристыковывающихся тайлов
            newProp.transform.localScale = prefab.transform.localScale * propScaleMultiplier;

            // Если мы уменьшили тайл в 2 раза, то и расстояние между их центрами должно уменьшиться в 2 раза,
            // иначе между летающими тайлами будет огромная пустая щель.
            Vector3 scaledOffset = localOffset * propScaleMultiplier;

            newProp.transform.localPosition = scaledOffset + new Vector3(0, 3f, 0);

            // Падение на свое место внутри крутящейся орбиты
            newProp.transform.DOLocalMove(localOffset, 0.5f).SetEase(Ease.InCubic).OnComplete(() => {
                if (tileDustVFX != null)
                {
                    // Спавним пыль прямо в точке соприкосновения
                    GameObject dust = Instantiate(tileDustVFX, newProp.transform.position, Quaternion.identity);
                    dust.transform.SetParent(_spawnedPropGroup.transform); // Пыль крутится вместе с орбитой
                    Destroy(dust, 2f);
                }
            });
        }

        private void HideAndDestroyProp(GameObject prop)
        {
            if (prop == null) return;

            prop.transform.DOKill();

            prop.transform.DOMoveX(prop.transform.position.x + 30f, 1f).SetEase(Ease.InCubic).OnComplete(() => Destroy(prop));
        }

        // ------------------------------------------
        // DRAG & DROP МИПЛОВ (ЛОКАЛЬНО)
        // ------------------------------------------
        public void StartDraggingMeeple(int type, GameObject prefab)
        {
            if (!_waitingForMeeple) return;

            _draggedMeepleType = type;
            _isDragging = true;
            _draggedMeeple = Instantiate(prefab);

            Collider[] colliders = _draggedMeeple.GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = false;
        }

        // ------------------------------------------
        // ЛОГИКА ВВОДА (UPDATE)
        // ------------------------------------------
        void Update()
        {
            if (Mouse.current == null) return;

            UpdateGhostTile();

            if (_isPlayerTurnActive && _tileInHand != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                _tileInHand.RotateRight();
                _lastHoveredGridPos = new Vector2Int(-999, -999);
            }

            // --- ОБРАБОТКА DRAG & DROP ---
            if (_waitingForMeeple && _isDragging && _draggedMeeple != null)
            {
                Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                Ray ray = mainCamera.ScreenPointToRay(mousePos);

                int activeTileLayer = LayerMask.NameToLayer("ActiveTile");
                int layerMask = 1 << activeTileLayer;

                if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask))
                {
                    _draggedMeeple.transform.position = hit.point + new Vector3(0, 0.5f, 0);

                    RegionCollider hitRegion = hit.collider.GetComponent<RegionCollider>();
                    int hoveredRegionId = (hitRegion != null) ? hitRegion.regionId : -1;

                    // Зеленый только если навел на ПРАВИЛЬНЫЙ по сюжету регион
                    bool isValid = (hoveredRegionId == _expectedMeepleRegionId);

                    var ghostScript = _draggedMeeple.GetComponent<MeepleDragGhost>();
                    if (ghostScript != null)
                    {
                        ghostScript.SetStatus(isValid, isValid ? "Отличное место!" : "Нам нужен Город!", Color.blue);
                    }
                }
                else if (Physics.Raycast(ray, out RaycastHit defaultHit, 100f))
                {
                    _draggedMeeple.transform.position = defaultHit.point + new Vector3(0, 0.5f, 0);
                    var ghostScript = _draggedMeeple.GetComponent<MeepleDragGhost>();
                    if (ghostScript != null) ghostScript.SetStatus(false, "Мимо!", Color.blue);
                }

                // БРОСОК
                if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    _isDragging = false;
                    bool dropSuccess = false;

                    if (Physics.Raycast(ray, out RaycastHit dropHit, 100f, layerMask))
                    {
                        RegionCollider hitRegion = dropHit.collider.GetComponent<RegionCollider>();
                        if (hitRegion != null && hitRegion.regionId == _expectedMeepleRegionId)
                        {
                            // УСПЕХ! Ставим мипла
                            TileData placedCityTile = _board.GetTileAt(_expectedTilePos.Value);
                            TileRegion targetRegion = placedCityTile.Regions.Find(r => r.Id == _expectedMeepleRegionId);
                            _board.PlaceMeeple(_player, targetRegion);

                            Vector3 tileWorldPos = new Vector3(_expectedTilePos.Value.x * tileSize, 0, _expectedTilePos.Value.y * tileSize);
                            Transform tileRoot = dropHit.collider.transform.root;
                            Vector3 localDropOffset = tileRoot.InverseTransformPoint(dropHit.point);

                            GameObject tilePrefab = GetPrefabForTile(placedCityTile.Id);
                            float baseRotY = tilePrefab.transform.rotation.eulerAngles.y;
                            Quaternion tileRotation = Quaternion.Euler(0, baseRotY + (placedCityTile.Rotation * 90f), 0);

                            Vector3 worldPos = tileWorldPos + (tileRotation * localDropOffset) + new Vector3(0, 0.2f, 0);

                            GameObject newMeeple = Instantiate(meeplePrefab, worldPos, Quaternion.Euler(0, 0, 0));

                            if (meepleDustVFX) Destroy(Instantiate(meepleDustVFX, worldPos, Quaternion.identity), 2f);

                            Destroy(_draggedMeeple);
                            _waitingForMeeple = false;
                            dropSuccess = true;
                        }
                    }

                    if (!dropSuccess)
                    {
                        // ОТСКОК
                        var ghostScript = _draggedMeeple.GetComponent<MeepleDragGhost>();
                        if (ghostScript != null) ghostScript.ResetToReject(Color.gray);
                        PlayRejectAnimation(_draggedMeeple);
                    }
                }
            }

            // --- УСТАНОВКА ТАЙЛА ---
            if (_isPlayerTurnActive && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Ray ray = mainCamera.ScreenPointToRay(mousePos);
                Plane ground = new Plane(Vector3.up, Vector3.zero);

                if (ground.Raycast(ray, out float enter))
                {
                    Vector3 hit = ray.GetPoint(enter);
                    Vector2Int gridPos = new Vector2Int(Mathf.RoundToInt(hit.x / tileSize), Mathf.RoundToInt(hit.z / tileSize));

                    if (gridPos == _expectedTilePos.Value)
                    {
                        if (_tileInHand.Rotation == _expectedTileRotation.Value)
                        {
                            if (_board.CanPlaceTile(_tileInHand, gridPos))
                            {
                                _board.PlaceTile(_tileInHand, gridPos);
                                SpawnTileVisually(_tileInHand, gridPos);
                                _board.CheckAndScoreCompletedFeatures(_tileInHand, new List<Player> { _player, _bot });
                                HideGhostTile();
                                _isPlayerTurnActive = false;
                            }
                        }
                        else guide.ShowHint("Тайл нужно повернуть! Кликни ПКМ.");
                    }
                    else
                    {
                        AudioManager.Instance.PlaySFX("TileError");

                        guide.ShowHint("Не сюда! Поставь тайл в правильное место.");
                    }
                }
            }
        }

        // ------------------------------------------
        // БОТ, ВИЗУАЛ И УТИЛИТЫ
        // ------------------------------------------
        private void SimulateBotTurn()
        {
            TileData botTile = _deck.GetTileById("Tile22_StraightRoad");
            Vector2Int pos = new Vector2Int(-1, 0);
            botTile.SetRotation(1);

            //_board.PlaceTile(botTile, pos);
            SpawnTileVisually(botTile, pos);

            TileRegion roadRegion = botTile.Regions.Find(r => r.Type == TerrainType.Road);
            //_board.PlaceMeeple(_bot, roadRegion);

            Vector3 worldPos = new Vector3(pos.x * tileSize, 0.7f, pos.y * tileSize);
            Instantiate(botMeeplePrefab, worldPos, Quaternion.Euler(0, 0, 0));
            if (meepleDustVFX) Destroy(Instantiate(meepleDustVFX, worldPos, Quaternion.identity), 2f);
        }

        private void GiveTileToPlayer(string tileId)
        {
            _tileInHand = _deck.GetTileById(tileId);
            if (_ghostTile != null) Destroy(_ghostTile);
            GameObject prefabToUse = GetPrefabForTile(tileId);
            _ghostTile = Instantiate(prefabToUse);
            foreach (var col in _ghostTile.GetComponentsInChildren<Collider>()) col.enabled = false;
            _ghostRenderers = _ghostTile.GetComponentsInChildren<Renderer>();
            _ghostTile.SetActive(true);
        }

        private void UpdateGhostTile()
        {
            if (_ghostTile == null || !_ghostTile.activeSelf || Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Plane ground = new Plane(Vector3.up, Vector3.zero);

            if (ground.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                Vector2Int gridPos = new Vector2Int(Mathf.RoundToInt(hit.x / tileSize), Mathf.RoundToInt(hit.z / tileSize));

                // Ограничиваем gridPos, чтобы голограмма не могла выйти за пределы +-11
                int maxGridSize = 11;
                gridPos.x = Mathf.Clamp(gridPos.x, -maxGridSize, maxGridSize);
                gridPos.y = Mathf.Clamp(gridPos.y, -maxGridSize, maxGridSize);

                if (_board.GetTileAt(gridPos) == null)
                {
                    _ghostTargetWorldPos = new Vector3(gridPos.x * tileSize, 0, gridPos.y * tileSize);

                    if (gridPos != _lastHoveredGridPos)
                    {
                        _lastHoveredGridPos = gridPos;

                        float targetYRotation = _tileInHand.Rotation * 90f;
                        Vector3 prefabEuler = GetPrefabForTile(_tileInHand.Id).transform.rotation.eulerAngles;
                        Vector3 finalEuler = new Vector3(prefabEuler.x, prefabEuler.y + targetYRotation, prefabEuler.z);

                        _ghostTile.transform.DOKill();
                        _ghostTile.transform.DORotate(finalEuler, 0.15f).SetEase(Ease.OutBack).SetOptions(true);

                        bool isCorrectStoryMove = (gridPos == _expectedTilePos.Value && _tileInHand.Rotation == _expectedTileRotation.Value);
                        Color targetColor = isCorrectStoryMove ? Color.green : Color.red;
                        targetColor.a = 0.6f;

                        foreach (var r in _ghostRenderers) r.material.color = targetColor;
                    }
                }
                _ghostTile.transform.position = Vector3.Lerp(_ghostTile.transform.position, _ghostTargetWorldPos, Time.deltaTime * 15f);
            }
        }

        private void HideGhostTile() { if (_ghostTile != null) _ghostTile.SetActive(false); }

        private void PlayRejectAnimation(GameObject meeple)
        {
            AudioManager.Instance.PlaySFX("MeepleBounce");

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

        private void SpawnTileVisually(TileData data, Vector2Int pos)
        {
            Vector3 worldPos = new Vector3(pos.x * tileSize, 0, pos.y * tileSize);
            GameObject prefab = GetPrefabForTile(data.Id);
            GameObject tileObj = Instantiate(prefab, worldPos, prefab.transform.rotation);
            tileObj.name = $"Tile_{data.Id}_{pos.x}_{pos.y}";

            float yRotation = data.Rotation * 90f;
            Vector3 currentEuler = tileObj.transform.rotation.eulerAngles;
            tileObj.transform.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y + yRotation, currentEuler.z);

            // Переводим коллайдер в ActiveTile для Drag&Drop
            int activeTileLayer = LayerMask.NameToLayer("ActiveTile");
            int defaultLayer = LayerMask.NameToLayer("Default");

            // Сначала сбрасываем все тайлы на столе в Default (чтобы они перестали быть активными)
            RegionCollider[] allColliders = FindObjectsByType<RegionCollider>(FindObjectsSortMode.None);
            foreach (var col in allColliders)
            {
                if (col.gameObject.layer == activeTileLayer) col.gameObject.layer = defaultLayer;
            }

            // Теперь переводим только НОВЫЙ тайл в ActiveTile
            RegionCollider[] newColliders = tileObj.GetComponentsInChildren<RegionCollider>();
            foreach (var col in newColliders) col.gameObject.layer = activeTileLayer;

            if (tileDustVFX != null)
            {
                AudioManager.Instance.PlaySFX("TilePlace");

                Destroy(Instantiate(tileDustVFX, worldPos, Quaternion.identity), 2f);
            }
        }

        private GameObject GetPrefabForTile(string tileId)
        {
            foreach (var binding in tilePrefabs)
                if (tileId.StartsWith(binding.tileName)) return binding.prefab;
            return defaultPrefab;
        }

        // Метод установки цветов для учебных миплов
        private void InstallingСertainColor(GameObject meeplePrefab, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color parsedColor);

            Renderer r = meeplePrefab.GetComponentInChildren<Renderer>();

            if (r != null) r.sharedMaterial.color = parsedColor;

        }

        void OnDestroy()
        {
            // Жестко тушим все вращения пропов и движение камеры перед возвратом в меню
            DOTween.KillAll();
        }
    }
}