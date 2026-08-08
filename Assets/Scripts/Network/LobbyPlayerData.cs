using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Network
{
    public struct LobbyPlayerData
    {
        public string SessionToken; // Имя игрока
        public Color PlayerColor;   // Выданный цвет
        public bool IsReady;        // Нажал ли "Готов"
        public bool IsHost;         // Хост или обычный игрок
    }
}
