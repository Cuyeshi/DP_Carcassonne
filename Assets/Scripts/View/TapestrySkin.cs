using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.View
{
    // Специальная структура для привязки Цвета к Спрайту
    [System.Serializable]
    public struct TapestrySkin
    {
        [Tooltip("Hex цвет игрока (например, #3B699E для синего)")]
        public string hexColor;
        [Tooltip("Спрайт самого гобелена (флага)")]
        public Sprite tapestrySprite;
        [Tooltip("Суффикс цвета для поиска иконок в TMP (например, 'blue', 'green', 'red')")]
        public string spriteColorSuffix;
    }
}
