using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Network
{
    /// <summary>
    /// Структура для передачи одного поставленного тайла по сети
    /// </summary>
    public struct PlacedTileNetData
    {
        public string tileId;
        public int posX;
        public int posY;
        public int rotation;
    }
}
