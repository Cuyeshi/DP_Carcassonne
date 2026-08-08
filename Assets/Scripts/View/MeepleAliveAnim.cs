using UnityEngine;
using System.Collections;

namespace Assets.Scripts.View
{
    public class MeepleAliveAnim : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float minWaitTime = 5f;
        public float maxWaitTime = 15f;
        public float jumpHeight = 0.15f;
        public float jumpDuration = 0.3f;
        public float maxLookAngle = 30f; // На сколько градусов он может "повертеть головой"

        private Vector3 _startLocalPos;
        private Quaternion _startLocalRot;

        void Start()
        {
            // Мы не можем запускать анимацию сразу, так как сначала работает анимация падения с неба (MeepleView).
            // Поэтому ждем пару секунд перед тем, как "ожить".
            Invoke(nameof(InitializeAndStart), 2f);
        }

        private void InitializeAndStart()
        {
            // Не запускаем корутину, если объект выключен или уничтожается
            if (!gameObject.activeInHierarchy) return;

            _startLocalPos = transform.localPosition;
            _startLocalRot = transform.localRotation;
            StartCoroutine(AliveRoutine());
        }

        private IEnumerator AliveRoutine()
        {
            while (true)
            {
                // Ждем случайное время
                yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));

                // Начинаем прыжок
                float elapsedTime = 0f;

                // Случайный угол поворота головы (-30 до +30)
                float randomAngleY = Random.Range(-maxLookAngle, maxLookAngle);
                Quaternion targetRot = _startLocalRot * Quaternion.Euler(0, randomAngleY, 0);

                // Фаза 1: Прыжок вверх
                float halfDur = jumpDuration / 2f;
                while (elapsedTime < halfDur)
                {
                    float t = elapsedTime / halfDur;
                    // Плавный Ease-Out для прыжка
                    float easedT = Mathf.Sin(t * Mathf.PI * 0.5f);

                    transform.localPosition = _startLocalPos + new Vector3(0, jumpHeight * easedT, 0);
                    transform.localRotation = Quaternion.Slerp(_startLocalRot, targetRot, easedT);

                    elapsedTime += Time.deltaTime;
                    yield return null;
                }

                // Фаза 2: Падение вниз
                elapsedTime = 0f;
                while (elapsedTime < halfDur)
                {
                    float t = elapsedTime / halfDur;
                    // Плавный Ease-In для падения
                    float easedT = 1f - Mathf.Cos(t * Mathf.PI * 0.5f);

                    transform.localPosition = _startLocalPos + new Vector3(0, jumpHeight * (1f - easedT), 0);
                    // Возвращаемся в исходное положение (смотрим прямо) или оставляем повернутым - на твой вкус!
                    // Оставим его повернутым, чтобы он "наблюдал" за дорогой:
                    // transform.localRotation = Quaternion.Slerp(targetRot, _startLocalRot, easedT); 

                    elapsedTime += Time.deltaTime;
                    yield return null;
                }

                // Жесткая фиксация (чтобы не уплыл из-за погрешностей float)
                transform.localPosition = _startLocalPos;
            }
        }
    }
}