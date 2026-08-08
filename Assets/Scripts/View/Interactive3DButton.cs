using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.View
{
    /// <summary>
    /// Вспомогательный класс для управления 3D-кнопками.
    /// </summary>
    [Serializable]
    public class Interactive3DButton
    {
        public GameObject buttonObject; // Сама 3D моделька (должен быть коллайдер!)
        public bool colorizeWithPlayer = false; // Красить ли эту модельку в цвет игрока?

        [HideInInspector] public Vector3 baseLocalPos;
        [HideInInspector] public Renderer[] renderers;
        [HideInInspector] public bool isInteractable = true;

        // Запоминаем оригинальный цвет (чтобы вернуть его обратно)
        [HideInInspector] public Color originalColor = Color.white;

        public void Initialize()
        {
            if (buttonObject != null)
            {
                baseLocalPos = buttonObject.transform.localPosition;
                renderers = buttonObject.GetComponentsInChildren<Renderer>();

                // Запоминаем базовый цвет первой попавшейся детальки
                if (renderers != null && renderers.Length > 0)
                {
                    originalColor = renderers[0].material.color;
                }
            }
        }

        public void SetColor(Color color)
        {
            if (!colorizeWithPlayer) return;

            // Если красим в цвет игрока - это становится нашим новым "оригинальным" цветом
            originalColor = color;

            // Применяем цвет только если кнопка активна. 
            // Если она ВЫКЛЮЧЕНА, метод SetInteractable сам покрасит ее в серый.
            if (isInteractable)
            {
                ApplyColorToRenderers(originalColor);
            }
        }

        public void SetInteractable(bool state)
        {
            isInteractable = state;

            if (renderers != null)
            {
                if (state)
                {
                    // Кнопка АКТИВНА: Возвращаем яркий оригинальный цвет
                    ApplyColorToRenderers(originalColor);
                }
                else
                {
                    // Кнопка ВЫКЛЮЧЕНА: Делаем цвет темно-серым (смешиваем оригинальный с черным)
                    Color disabledColor = Color.Lerp(originalColor, Color.black, 0.7f);
                    ApplyColorToRenderers(disabledColor);
                }
            }
        }

        // Вспомогательный метод покраски всех деталек
        private void ApplyColorToRenderers(Color c)
        {
            foreach (var r in renderers)
            {
                r.material.color = c;
            }
        }
    }
}
