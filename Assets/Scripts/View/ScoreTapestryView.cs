using Assets.Scripts.View;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.View
{
    public class ScoreTapestryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI Elements")]
        public Image backgroundImage;
        public TextMeshProUGUI namePlaceText;
        public TextMeshProUGUI infoText;

        [Header("Skins")]
        public TapestrySkin[] skins;

        public string TargetToken { get; private set; }
        private string _currentSpriteSuffix = "default";
        private ScoreBoardUI _boardUI;

        public void Initialize(string token, Color playerColor, ScoreBoardUI boardUI)
        {
            TargetToken = token;
            _boardUI = boardUI;

            string playerHex = "#" + ColorUtility.ToHtmlStringRGB(playerColor).ToUpper();
            bool skinFound = false;

            foreach (var skin in skins)
            {
                if (skin.hexColor.ToUpper() == playerHex)
                {
                    _currentSpriteSuffix = skin.spriteColorSuffix;
                    if (backgroundImage != null && skin.tapestrySprite != null)
                    {
                        backgroundImage.sprite = skin.tapestrySprite;
                        backgroundImage.color = Color.white;
                    }
                    skinFound = true;
                    break;
                }
            }

            if (!skinFound)
            {
                if (backgroundImage != null) backgroundImage.color = playerColor;
                _currentSpriteSuffix = "blue";
            }
        }

        public void UpdateData(int score, int meeples, bool hasAbbot, bool isMyTurn)
        {
            string meepleIconTag = $"<sprite name=\"meeple_{_currentSpriteSuffix}\">";
            string abbotIconTag = hasAbbot ? $"<sprite name=\"abbot_{_currentSpriteSuffix}\">" : "";

            string nameFormat = $"<b><align=center>{TargetToken}</align></b>";
            if (namePlaceText != null) namePlaceText.text = nameFormat;

            // --- ИСПРАВЛЕННАЯ СБОРКА ТЕКСТА ---
            string row1 = "";
            string row2 = "";

            for (int i = 0; i < meeples; i++)
            {
                if (i < 4) row1 += meepleIconTag + " ";
                else row2 += meepleIconTag + " ";
            }

            // Аббат позиционируется на 75% ширины поля и поднимается на половину строки вверх, 
            // чтобы быть между первым и вторым рядом миплов.
            string abbotFormat = hasAbbot ? $"<pos=75%><voffset=-20px><size=150%>{abbotIconTag}</size></voffset></pos>" : "";

            // Собираем всё вместе. Используем <nobr> чтобы пробелы между миплами не ломали строку.
            infoText.text = $"<line-height=110%><size=90%>Очки: {score}</size>\n" +
                            $"<cspace=-3px><size=110%><align=left><margin-left=5%><nobr>{row1}</nobr>{abbotFormat}\n" +
                            $"<margin-left=5%><nobr>{row2}</nobr></align></size></cspace>";
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_boardUI != null) _boardUI.OnTapestryHovered(TargetToken, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_boardUI != null) _boardUI.OnTapestryHovered(TargetToken, false);
        }
    }
}