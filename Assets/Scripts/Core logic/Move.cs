using UnityEngine;

namespace Assets.Scripts.Core_logic
{
    public class Move
    {
        public Vector2Int Position { get; private set; }
        public int Rotation { get; private set; }

        // ID кусочка, куда бот хочет поставить мипла. (-1 означает "не ставить")
        public int MeepleRegionId { get; private set; }
        public int MeepleType { get; private set; }     // 0 = Обычный, 1 = Аббат
        public bool RetrieveAbbot { get; private set; } // Забираем ли мы аббата в этот ход?

        public Move(Vector2Int position, int rotation, int meepleRegionId = -1, int meepleType = 0, bool retrieveAbbot = false)
        {
            Position = position;
            Rotation = rotation;
            MeepleRegionId = meepleRegionId;
            MeepleType = meepleType;
            RetrieveAbbot = retrieveAbbot;
        }
    }
}