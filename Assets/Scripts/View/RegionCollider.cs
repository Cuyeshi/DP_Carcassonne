using UnityEngine;

namespace Assets.Scripts.View
{
    // Этот скрипт вешается НА меш города, дороги или поля
    // Обязательно требует наличия MeshCollider на этом же объекте
    [RequireComponent(typeof(Collider))]
    public class RegionCollider : MonoBehaviour
    {
        [Tooltip("Local ID региона (0, 1, 2...)")]
        public int regionId;
    }
}