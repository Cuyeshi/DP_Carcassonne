using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Core_logic.AI
{
    /// <summary>
    /// Вспомогательный класс для дерева UCB1
    /// </summary>
    public class MCTSNode
    {
        public Move NodeMove;
        public float Wins;
        public int Simulations;

        // Математическая формула UCB1 (Баланс между "Изученным" и "Неизведанным")
        public float GetUCB1Score(int totalBotSimulations, float explorationConstant = 1.41f)
        {
            if (Simulations == 0) return float.MaxValue; // Неизведанные ходы проверяем в первую очередь

            float winRate = Wins / Simulations;
            float exploration = explorationConstant * (float)Math.Sqrt(Math.Log(totalBotSimulations) / Simulations);
            return winRate + exploration;
        }
    }
}
