using Mirror.BouncyCastle.Utilities.Encoders;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.Tutorial
{
    public class TutorialDragSource : MonoBehaviour, IPointerDownHandler
    {
        public int meepleType = 0; // 0 = Мипл, 1 = Аббат
        public GameObject dragPrefab;

        void Start()
        {
            string hex = "#3b699e";

            ColorUtility.TryParseHtmlString(hex, out Color parsedColor);

            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = parsedColor;
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            TutorialManager tm = FindFirstObjectByType<TutorialManager>();
            if (tm != null)
            {
                tm.StartDraggingMeeple(meepleType, dragPrefab);
            }
        }
    }
}