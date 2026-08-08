using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core_logic.AI
{
    // Глобальный контейнер телеметрии
    public class AITelemetry
    {
        // volatile гарантирует, что переменная не кэшируется процессором, 
        // и Главный поток всегда видит свежие данные Фонового потока
        public volatile bool IsThinking = false;

        // Список всех рассматриваемых ходов (нейронов)
        public List<MoveNodeStat> CurrentNodes = new List<MoveNodeStat>();

        // НОВЫЕ ПОЛЯ ДЛЯ СТАТИСТИКИ:
        public int TotalSimulationsCompleted = 0;
        public string BestMoveCoord = "-";
        public float BestMoveWinRate = 0f;

        // Очистка перед новым ходом
        public void Clear()
        {
            CurrentNodes = new List<MoveNodeStat>();
            TotalSimulationsCompleted = 0;
            BestMoveCoord = "-";
            BestMoveWinRate = 0f;
        }
    }
}