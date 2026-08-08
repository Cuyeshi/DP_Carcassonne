using Assets.Scripts.Media;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.View
{
    public class PanelsToggle : MonoBehaviour
    {
        [Header("Панели")]
        [Tooltip("Порядок важен: 0 = Панель 1, 1 = Панель 2, 2 = Панель 3")]
        public Transform[] panels = new Transform[3];

        [Header("Настройки")]
        public float duration = 0.5f;
        public Ease ease = Ease.OutCubic;

        private enum PanelState { Closed = 0, Open = 1, Detail = 2 }
        private PanelState currentState = PanelState.Closed;
        private bool isAnimating = false;
        private int completedCount = 0;

        private Scene _currentScene;

        private void Awake()
        {
            _currentScene = SceneManager.GetActiveScene();

            DOTween.Init();
        }


        private void Update()
        {
            if (Assets.Scripts.View.LobbyUI.IsLobbyActive && _currentScene.name == "GameScene") return; // БЛОКИРОВКА ESC

            // Esc ВСЕГДА сбрасывает в начальное состояние (0), даже если анимация ещё идёт
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && 
                currentState != PanelState.Closed)
            {
                AnimateToState(PanelState.Closed);
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame &&
                currentState == PanelState.Closed)
            {
                AnimateToState(PanelState.Open);
            }
        }

        
        public void OnDetailClick()
        {
            if (currentState == PanelState.Open && !isAnimating)
            {
                AnimateToState(PanelState.Detail);
            }
        }

        
        public void OnBackClick()
        {
            if (currentState == PanelState.Detail && !isAnimating)
            {
                AnimateToState(PanelState.Open);
            }
        }

        public void OnOpenMenuClick()
        {
            if (currentState == PanelState.Closed && !isAnimating)
            {
                AnimateToState(PanelState.Open);
            }
        }

        public void OnContinueClick()
        {
            if (currentState == PanelState.Open && !isAnimating)
            {
                AnimateToState(PanelState.Closed);
            }
        }

        private void AnimateToState(PanelState targetState)
        {
            isAnimating = true;
            completedCount = 0;
            PanelState finalState = targetState; // Фиксируем состояние для корректной работы замыканий

            // Мгновенно прерываем все текущие анимации панелей
            DOTween.Kill("PanelTween");

            AudioManager.Instance.PlaySFX("SlowOpening"); // ОЗВУЧКА!

            float[] targets = targetState switch
            {
                PanelState.Closed => new float[] { 0f, 0f, 0f },
                PanelState.Open => new float[] { -16.43234f, -53.73235f, -37.43235f },
                PanelState.Detail => new float[] { -36.4f, -74.6f, -5.88f },
                _ => throw new ArgumentOutOfRangeException()
            };

            for (int i = 0; i < panels.Length; i++)
            {
                Vector3 targetRot = new Vector3(0f, 0f, targets[i]);

                // DOLocalRotate идеален для UI. RotateMode.Fast предотвращает "перекрут" через 360°
                panels[i].DOLocalRotate(targetRot, duration, RotateMode.Fast)
                    .SetId("PanelTween")
                    .SetEase(ease)
                    .OnComplete(() =>
                    {
                        completedCount++;
                        if (completedCount == panels.Length)
                        {
                            currentState = finalState;
                            isAnimating = false;
                        }
                    });
            }
        }
    }
}