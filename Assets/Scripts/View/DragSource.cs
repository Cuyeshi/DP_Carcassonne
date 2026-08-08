using Assets.Scripts.Network;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.View
{
    // Обязательно нужен EventSystem на сцене и PhysicsRaycaster на камере, 
    // чтобы OnMouseDown срабатывал четко
    public class DragSource : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("0 = Мипл, 1 = Аббат")]
        public int meepleType = 0;

        public GameObject dragPrefab; // Префаб, который появится под мышкой

        private CarcaPlayer _localPlayer;

        // Этот метод вызывается автоматически НОВОЙ системой ввода при клике на коллайдер
        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log($"[DragSource] Современный клик зафиксирован! Тип: {(meepleType == 0 ? "Мипл" : "Аббат")}");


            // Находим своего игрока
            if (_localPlayer == null)
            {
                var playerObj = Mirror.NetworkClient.localPlayer;
                if (playerObj != null)
                {
                    _localPlayer = playerObj.GetComponent<CarcaPlayer>();
                    Debug.Log("[DragSource] Локальный игрок найден!");
                }
                else
                    Debug.LogWarning("[DragSource] Локальный игрок НЕ НАЙДЕН! Mirror еще не заспавнил его?");
            }

            if (_localPlayer != null)
            {
                // Говорим игроку начать фазу перетаскивания
                Debug.Log("[DragSource] Передаем команду StartDraggingMeeple в CarcaPlayer");
                _localPlayer.StartDraggingMeeple(meepleType, dragPrefab);
            }
        }
    }
}