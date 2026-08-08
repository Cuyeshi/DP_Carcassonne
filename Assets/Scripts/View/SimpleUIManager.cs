using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace Assets.Scripts.View
{
    public class SimpleUIManager: MonoBehaviour
    {        
        [Header("Перекрывающая плашка")]
        public GameObject visualShield3D;      // Перекрывающая плашка позади лобби
        public float shieldDropDepth = -2f;    // Насколько уходит вниз
        public float shieldFlyBackDist = -20f; // Насколько улетает назад
        public float animationDuration = 1.5f; // Длительность анимации

        private void Awake()
        {
            OpenVisualShield3D();
        }

        /// <summary>
        /// Простой метод для перехода на сцену главного экрана.
        /// </summary>
        public void OnExitBtnClicked()
        {
            // Запоминаем на жестком диске, что мы совершили выход из игры
            PlayerPrefs.SetInt("ReturnedFromGame", 1);
            PlayerPrefs.Save();

            SceneManager.LoadScene("MenuScene");
        }

        public void OpenVisualShield3D()
        {
            if (visualShield3D != null)
            {

                Vector3 startPos = visualShield3D.transform.position;

                // Создаем массив точек-ориентиров для траектории полета
                // Точка 1 (Промежуточная): щит опускается наполовину и уже начинает лететь назад
                Vector3 midPoint = startPos + new Vector3(0, shieldDropDepth * 0.5f, shieldFlyBackDist * 0.2f);

                // Точка 2 (Конечная): щит полностью опустился и улетел назад
                Vector3 endPoint = startPos + new Vector3(0, shieldDropDepth, shieldFlyBackDist);

                Vector3[] pathWaypoints = new Vector3[] { midPoint, endPoint };

                // Запускаем принудительную очистку предыдущих анимаций
                visualShield3D.transform.DOKill();

                // DOPath строит плавную кривую CatmullRom между всеми точками!
                visualShield3D.transform.DOPath(pathWaypoints, animationDuration, PathType.CatmullRom)
                    .SetEase(Ease.InOutQuad) // Плавный разгон в начале и плавное торможение в конце
                    .SetDelay(0.5f)
                    .OnComplete(() => visualShield3D.SetActive(false)); // Выключаем в конце

            }
        }
    }
}
