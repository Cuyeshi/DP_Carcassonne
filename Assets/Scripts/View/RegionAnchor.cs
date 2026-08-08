using UnityEngine;

namespace Assets.Scripts.View
{
    public class RegionAnchor : MonoBehaviour
    {
        [Tooltip("Local ID региона (0, 1, 2...) из метода создания в DeckManager")]
        public int regionId;
    }
}