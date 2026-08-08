using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverSpriteText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Настройки текста")]
    [SerializeField] private TextMeshProUGUI tmpText;

    [Header("Спрайт")]
    [Tooltip("Индекс спрайта в TMP Sprite Asset (начиная с 0)")]
    [SerializeField] private int spriteIndex = 0;

    [Tooltip("Относительный размер спрайта (1.0 = высота строки)")]
    [SerializeField, Range(60, 200)] private int spriteScale = 120;

    [Tooltip("Опционально: свой спрайтовый ассет (если не задан, используется дефолтный у TMP)")]
    [SerializeField] private TMP_SpriteAsset customSpriteAsset;

    private string originalText;
    private string hoverText;

    private void Awake()
    {
        // Автоматический поиск, если не назначен
        if (tmpText == null)
            tmpText = GetComponentInChildren<TextMeshProUGUI>();

        if (tmpText == null)
        {
            Debug.LogError($"[HoverSpriteText] Не найден TextMeshProUGUI на {gameObject.name}");
            enabled = false;
            return;
        }

        tmpText.richText = true; // Гарантируем поддержку тегов

        // Сохраняем исходную строку
        originalText = tmpText.text;

        // Подключаем кастомный ассет, если указан
        if (customSpriteAsset != null)
            tmpText.spriteAsset = customSpriteAsset;

        // Формируем строку для ховера. Атрибут scale меняет размер спрайта относительно высоты шрифта.
        hoverText = $"{originalText} <size={spriteScale}%> <sprite={spriteIndex}></size>";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        tmpText.text = hoverText;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tmpText.text = originalText;
    }
}
