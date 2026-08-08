using UnityEngine;
using System.Collections;

namespace Assets.Scripts.View
{
    public class MenuFader : MonoBehaviour
    {
        [Header("Настройки анимации")]
        public float fadeDuration = 0.25f; // Длительность перехода в секундах

        private CanvasGroup _currentActivePanel;
        private bool _isFading = false; // Блокировка от двойных кликов

        // Вспомогательный метод: мгновенно выключить панель (без анимации)
        public void SetPanelInstant(CanvasGroup panel, bool isActive)
        {
            panel.alpha = isActive ? 1f : 0f;
            panel.interactable = isActive;
            panel.blocksRaycasts = isActive;
        }

        // ГЛАВНЫЙ МЕТОД: Плавный переход от одной панели к другой
        public void SwitchToPanel(CanvasGroup newPanel, CanvasGroup oldPanel)
        {
            if (_isFading) return; // Защита от спама кликами
            StartCoroutine(FadeRoutine(newPanel, oldPanel));
        }

        private IEnumerator FadeRoutine(CanvasGroup newPanel, CanvasGroup oldPanel)
        {
            _isFading = true;

            // 1. Отключаем клики на старой панели СРАЗУ (чтобы игрок не нажал ничего в процессе)
            if (oldPanel != null)
            {
                oldPanel.interactable = false;
                oldPanel.blocksRaycasts = false;
            }

            // 2. Гасим старую панель (Fade Out)
            float elapsedTime = 0f;
            while (oldPanel != null && elapsedTime < fadeDuration)
            {
                oldPanel.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            if (oldPanel != null) oldPanel.alpha = 0f;

            // 3. Включаем новую панель (Fade In)
            elapsedTime = 0f;
            if (newPanel != null)
            {
                // Готовим новую панель (пока прозрачная)
                newPanel.alpha = 0f;
                // НО пока идет анимация, кликать на нее еще нельзя!
                newPanel.interactable = false;
                newPanel.blocksRaycasts = false;

                while (elapsedTime < fadeDuration)
                {
                    newPanel.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
                newPanel.alpha = 1f;

                // 4. Как только панель появилась - разрешаем кликать!
                newPanel.interactable = true;
                newPanel.blocksRaycasts = true;

                _currentActivePanel = newPanel;
            }

            _isFading = false;
        }
    }
}