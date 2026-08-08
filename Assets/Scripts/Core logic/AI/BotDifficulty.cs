using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Core_logic.AI
{
    public enum BotDifficulty
    {
        Easy,   // Случайные симуляции (Как было раньше)
        Medium, // UCB1 + Базовая интуиция
        Hard    // UCB1 + Интуиция + Умный противник в симуляциях + Подсчет карт
    }
}
