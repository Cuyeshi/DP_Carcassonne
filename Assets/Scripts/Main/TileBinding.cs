using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Main
{
    [System.Serializable]
    public struct TileBinding
    {
        [Tooltip("Имя тайла из DeckManager (например: StraightRoad)")]
        public string tileName;
        public GameObject prefab;
    }
}
