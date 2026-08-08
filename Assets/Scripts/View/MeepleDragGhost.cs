using UnityEngine;
using TMPro;

namespace Assets.Scripts.View
{
    public class MeepleDragGhost : MonoBehaviour
    {
        [Header("References")]
        public Renderer meepleRenderer;
        public TextMeshProUGUI statusText;

        [Header("Physics Settings")]
        public float swingIntensity = 15f; // Насколько сильно он раскачивается
        public float returnSpeed = 10f;    // Как быстро возвращается в ровное положение

        private Vector3 _lastPosition;
        private Vector3 _currentVelocity;

        void Start()
        {
            _lastPosition = transform.position;

            // Ищем компонент Canvas в себе или в дочерних объектах
            Canvas canvas = GetComponentInChildren<Canvas>();

            if (canvas != null)
            {
                // Принудительно назначаем Главную Камеру как Event Camera!
                canvas.worldCamera = Camera.main;
                Debug.Log($"[GHOST] Камера {Camera.main.name} успешно привязана к Canvas голограммы!");
            }
            else
            {
                Debug.LogWarning("[GHOST] Предупреждение: На префабе голограммы не найден компонент Canvas!");
            }

        }

        void Update()
        {
            // === АНИМАЦИЯ КАЧАНИЯ (МАЯТНИК) ===
            // 1. Вычисляем скорость перемещения мышки (разницу позиций)
            Vector3 velocity = (transform.position - _lastPosition) / Time.deltaTime;

            // 2. Сглаживаем скорость, чтобы не было дерганий
            _currentVelocity = Vector3.Lerp(_currentVelocity, velocity, Time.deltaTime * 10f);
            _lastPosition = transform.position;

            // 3. Вычисляем угол наклона (наклоняем В СТОРОНУ, противоположную движению)
            // Движемся по X -> наклоняемся по Z. Движемся по Z -> наклоняемся по X.
            float targetTiltX = _currentVelocity.z * swingIntensity;
            float targetTiltZ = -_currentVelocity.x * swingIntensity;

            // Ограничиваем максимальный наклон (например, 45 градусов)
            targetTiltX = Mathf.Clamp(targetTiltX, -45f, 45f);
            targetTiltZ = Mathf.Clamp(targetTiltZ, -45f, 45f);

            // 4. Плавно применяем вращение (возвращаясь к (0,0,0) когда мышь стоит)
            Quaternion targetRotation = Quaternion.Euler(targetTiltX, 0f, targetTiltZ);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * returnSpeed);

            // 5. Текст всегда должен смотреть ровно в камеру (иначе он наклонится вместе с миплом)
            if (statusText != null)
            {
                statusText.transform.rotation = Camera.main.transform.rotation;
            }
        }

        public void SetStatus(bool isValid, string message, Color baseColor)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = isValid ? Color.green : Color.red;
            }

            if (meepleRenderer != null)
            {
                Color matColor = isValid ? Color.green : Color.red;
                matColor.a = 0.6f; // Делаем прозрачным
                meepleRenderer.material.color = matColor;
            }
        }

        public void ResetToReject(Color originalColor)
        {
            // Метод вызывается перед броском (отскоком), чтобы мипл снова стал своего цвета
            if (statusText != null) statusText.text = "";
            if (meepleRenderer != null) meepleRenderer.material.color = originalColor;
        }
    }
}