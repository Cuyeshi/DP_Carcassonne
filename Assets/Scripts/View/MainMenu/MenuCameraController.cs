using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.View.MainMenu
{
    public class MenuCameraController : MonoBehaviour
    {
        [Header("Points")]
        public Transform pointIdle;  // Точка в небе
        public Transform pointTable; // Точка у стола

        [Header("FOV Settings")]
        public float fovIdle = 60f;
        public float fovTable = 45f; 

        [Header("Animation")]
        public float duration = 1.5f;
        public Ease easeType = Ease.InOutSine; // Плавное начало и плавный конец

        private Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();

            // НОВОЕ: Проверка на "бесшовный возврат из игры"
            // Читаем PlayerPrefs, как мы обсуждали ранее
            bool returnedFromGame = PlayerPrefs.GetInt("ReturnedFromGame", 0) == 1;

            if (returnedFromGame)
            {
                // Если мы только что вышли из игры, мгновенно ставим камеру к столу
                SetToTableInstant();

                // И тут же запускаем анимацию отлета в небо!
                FlyToIdle();

                // Сбрасываем флаг
                PlayerPrefs.SetInt("ReturnedFromGame", 0);
                PlayerPrefs.Save();
            }
            else
            {
                // Обычный старт (стоим в небе)
                SetToIdleInstant();
            }
        }

        // =====================================
        // МЕТОДЫ ДЛЯ МГНОВЕННОЙ ТЕЛЕПОРТАЦИИ
        // =====================================

        private void SetToIdleInstant()
        {
            transform.DOKill();
            transform.position = pointIdle.position;
            transform.rotation = pointIdle.rotation;
            _cam.fieldOfView = fovIdle;
        }

        public void SetToTableInstant()
        {
            transform.DOKill();
            transform.position = pointTable.position;
            transform.rotation = pointTable.rotation;
            _cam.fieldOfView = fovTable;
        }

        // =====================================
        // МЕТОДЫ ПЛАВНЫХ ПЕРЕЛЕТОВ (DOTween)
        // =====================================

        // Летим к столу (когда нажимаем "Начать игру" или "Подключиться")
        public void FlyToTable()
        {
            transform.DOKill(); // Убиваем старые анимации, если они были

            // Одновременно запускаем 3 анимации (Позиция, Вращение, FOV)
            transform.DOMove(pointTable.position, duration).SetEase(easeType);
            transform.DORotateQuaternion(pointTable.rotation, duration).SetEase(easeType);
            _cam.DOFieldOfView(fovTable, duration).SetEase(easeType);
        }

        // Летим обратно в небо (при ошибке сети или при возврате из игры)
        public void FlyToIdle()
        {
            transform.DOKill();

            transform.DOMove(pointIdle.position, duration).SetEase(easeType);
            transform.DORotateQuaternion(pointIdle.rotation, duration).SetEase(easeType);
            _cam.DOFieldOfView(fovIdle, duration).SetEase(easeType);
        }
    }
}
