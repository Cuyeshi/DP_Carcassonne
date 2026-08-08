using UnityEngine;
using System.Collections.Generic;
using Assets.Scripts.Main;
using Assets.Scripts.Network;
using DG.Tweening;

namespace Assets.Scripts.View
{
    public class ScoreBoardUI : MonoBehaviour
    {
        [Header("Settings")]
        public GameObject tapestryPrefab; // Префаб ScoreTapestryView

        [Header("Layout Settings")]
        public float spacingX = 150f; // Расстояние между гобеленами по горизонтали
        public float idleYPos = 0f;   // Y позиция в спрятанном состоянии
        public float activeYPos = -30f; // Y позиция при выдвижении вниз

        [Header("Animation Settings")]
        public float animationDuration = 0.5f;
        public Ease animationEase = Ease.OutBack;
        private string _hoveredToken = ""; // Какой гобелен сейчас под мышкой?

        private CarcaGameManager _gm;
        private Dictionary<string, ScoreTapestryView> _activeTapestries = new Dictionary<string, ScoreTapestryView>();

        void OnEnable() { CarcaGameManager.OnUIUpdateRequired += RefreshBoard; }
        void OnDisable() { CarcaGameManager.OnUIUpdateRequired -= RefreshBoard; }

        private void RefreshBoard()
        {
            if (_gm == null) _gm = FindFirstObjectByType<CarcaGameManager>();
            if (_gm == null || !_gm.isGameStarted)
            {
                ClearAllTapestries();
                return;
            }

            List<string> tokensToKeep = new List<string>();

            // ==========================================
            // 1. ИГРОКИ (Читаем напрямую из объектов на сцене!)
            // ==========================================
            CarcaPlayer[] networkPlayers = FindObjectsByType<CarcaPlayer>(FindObjectsSortMode.None);

            foreach (var p in networkPlayers)
            {
                if (string.IsNullOrEmpty(p.SessionToken)) continue; // Защита от недоспавненных

                string token = p.SessionToken;
                tokensToKeep.Add(token);

                if (!_activeTapestries.ContainsKey(token))
                {
                    CreateTapestry(token, p.PlayerColor);
                }

                bool isTurn = (_gm.currentTurnPlayerToken == token);

                // Передаем точные данные прямо из SyncVar игрока
                _activeTapestries[token].UpdateData(p.Score, p.MeeplesAvailable, p.HasAbbot, isTurn);
            }

            // ==========================================
            // 2. БОТ
            // ==========================================
            if (_gm.currentMode == GameMode.PlayersAndBot)
            {
                string botToken = "BOT";
                tokensToKeep.Add(botToken);

                if (!_activeTapestries.ContainsKey(botToken))
                {
                    ColorUtility.TryParseHtmlString("#A83232", out Color botColor);
                    CreateTapestry(botToken, botColor);
                }

                bool isBotTurn = (_gm.currentTurnPlayerToken == botToken);
                // Читаем новые SyncVar бота
                _activeTapestries[botToken].UpdateData(_gm.BotScore, _gm.BotMeeples, _gm.BotHasAbbot, isBotTurn);
            }

            // --- 3. Удаление отключенных ---
            List<string> currentKeys = new List<string>(_activeTapestries.Keys);
            foreach (string key in currentKeys)
            {
                if (!tokensToKeep.Contains(key))
                {
                    GameObject tapObj = _activeTapestries[key].gameObject;
                    CanvasGroup cg = tapObj.GetComponent<CanvasGroup>();

                    if (cg != null)
                    {
                        // ИСПРАВЛЕНИЕ: Перед удалением обязательно убиваем все анимации на этом объекте!
                        tapObj.GetComponent<RectTransform>().DOKill();

                        cg.DOFade(0f, 0.3f).OnComplete(() => Destroy(tapObj));
                    }
                    else
                    {
                        tapObj.GetComponent<RectTransform>().DOKill();
                        Destroy(tapObj);
                    }

                    _activeTapestries.Remove(key);
                }
            }

            ArrangeTapestries();
        }

        private void CreateTapestry(string token, Color color)
        {
            if (tapestryPrefab == null)
            {
                tapestryPrefab = Resources.Load<GameObject>("TapestryPrefab");
                if (tapestryPrefab == null) return;
            }

            GameObject obj = Instantiate(tapestryPrefab, transform);

            // Если добавить CanvasGroup на префаб, мы сможем его плавно проявить
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0;
                cg.DOFade(1f, 0.5f);
            }

            ScoreTapestryView view = obj.GetComponent<ScoreTapestryView>();
            view.Initialize(token, color, this);
            _activeTapestries.Add(token, view);
        }

        private void ArrangeTapestries()
        {
            int count = _activeTapestries.Count;
            if (count == 0) return;

            // Вычисляем общую ширину для центрирования
            float totalWidth = (count - 1) * spacingX;
            float startX = -totalWidth / 2f;

            int index = 0;
            foreach (var kvp in _activeTapestries)
            {
                string token = kvp.Key;
                RectTransform rectTransform = kvp.Value.GetComponent<RectTransform>();

                // Вычисляем целевую позицию X (в горизонтальной линии)
                float targetX = startX + (index * spacingX);

                // Выдвигаем вниз, если это ход игрока ИЛИ если мы навели на него мышку!
                bool isTargeted = (_gm.currentTurnPlayerToken == token) || (_hoveredToken == token);
                float targetY = isTargeted ? activeYPos : idleYPos;

                // Убиваем предыдущую анимацию этого объекта (если она не успела закончиться)
                rectTransform.DOKill();

                // Запускаем новую плавную анимацию перемещения
                rectTransform.DOAnchorPos(new Vector2(targetX, targetY), animationDuration)
                             .SetEase(animationEase); // Эффект отскока

                index++;
            }
        }

        // Удаление карточек игроков
        private void ClearAllTapestries()
        {
            foreach (var kvp in _activeTapestries)
            {
                if (kvp.Value != null)
                {
                    // Сначала жестко убиваем анимации на RectTransform, 
                    // чтобы DOTween больше не пытался двигать этот объект
                    RectTransform rt = kvp.Value.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.DOKill();
                    }

                    Destroy(kvp.Value.gameObject);
                }
            }
            _activeTapestries.Clear();
        }

        /// <summary>
        /// Метод, который вызывает гобелен (карточка игрока) при наведении.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="isHovered"></param>
        public void OnTapestryHovered(string token, bool isHovered)
        {
            if (isHovered) _hoveredToken = token;
            else if (_hoveredToken == token) _hoveredToken = ""; // Сбрасываем, если мышка ушла

            // Пересчитываем позиции гобеленов!
            ArrangeTapestries();
        }

        void OnDestroy()
        {
            // Убиваем все анимации гобеленов при выходе в меню
            foreach (var kvp in _activeTapestries)
            {
                if (kvp.Value != null) kvp.Value.GetComponent<RectTransform>().DOKill();
            }
        }
    }
}