using Mirror;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Network
{
    /// <summary>
    /// Класс для хранения данных оффлайн игроков
    /// </summary>
    public class PlayerSessionData
    {
        public int PersistentId; // Постоянный ID, который не изменится при перезаходе
        public int Score;
        public int MeeplesAvailable;
        public bool HasAbbot = true; // У каждого игрока 1 аббат на всю игру
        public Color PlayerColor;
        public bool IsDisconnected;
        public float DisconnectTimer; // Таймер для отсчета времени
        public bool IsDead; // Игрок не успел вернуться
    }
}
