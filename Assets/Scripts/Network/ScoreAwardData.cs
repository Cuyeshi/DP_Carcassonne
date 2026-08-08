using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Network
{
    public struct ScoreAwardData
    {
        public int PlayerId; // netId игрока (или 999 для бота)
        public int Points;   // Сколько очков получил
    }
}
