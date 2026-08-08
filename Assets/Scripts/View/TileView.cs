using UnityEngine;
using Assets.Scripts.Core_logic;

namespace Assets.Scripts.View
{
    public class TileView : MonoBehaviour
    {
        private TileData _data;

        /// <summary>
        /// Инициализация визуального тайла на основе серверной логики
        /// </summary>
        public void Initialize(TileData data)
        {
            _data = data;

            float yRotation = _data.Rotation * 90f;

            // Берем изначальные углы префаба (чтобы сохранить твой наклон по X и Z)
            Vector3 currentEuler = transform.rotation.eulerAngles;

            // Меняем только ось Y для поворота по правилам игры
            transform.rotation = Quaternion.Euler(-90f, currentEuler.y + yRotation, currentEuler.z);
        }
    }
}