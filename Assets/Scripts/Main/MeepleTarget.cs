using UnityEngine;
using Assets.Scripts.Core_logic;

namespace Assets.Scripts.Main
{
    public class MeepleTarget : MonoBehaviour
    {
        // Ссылка на кусочек тайла в C# логике
        public TileRegion Region { get; set; }
    }
}