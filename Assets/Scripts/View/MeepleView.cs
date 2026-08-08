using Assets.Scripts.Core_logic;
using Assets.Scripts.Main;
using Assets.Scripts.Media;
using DG.Tweening;
using Mirror;
using System.Collections;
using UnityEngine;

namespace Assets.Scripts.View
{
    public class MeepleView : NetworkBehaviour
    {
        public TileRegion AssignedRegion { get; set; }

        // Синхронизируемый цвет. Hook вызывается автоматически у всех клиентов при изменении.
        [SyncVar(hook = nameof(OnColorChanged))]
        public Color MeepleColor = Color.white;

        // Высота, с которой падает мипл
        [Tooltip("Высота, с которой падает мипл в анимации")]
        public float dropHeight = 3.0f;
        // Время падения
        [Tooltip("Время падения мипла в анимации")]
        public float dropDuration = 0.4f;

        // Оптимизация памяти
        private MaterialPropertyBlock _propBlock;
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        public override void OnStartClient()
        {
            // Запускаем DOTween анимацию
            AnimateDrop();
        }

        private void OnColorChanged(Color oldColor, Color newColor)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null)
            {
                // Читаем блок свойств, меняем цвет без дублирования материала в памяти
                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(ColorProperty, newColor);
                _propBlock.SetColor(BaseColorProperty, newColor);
                r.SetPropertyBlock(_propBlock);
            }
        }

        public void AnimateDrop()
        {
            Vector3 targetPos = transform.position;
            Vector3 startPos = targetPos + Vector3.up * dropHeight;

            transform.position = startPos;

            // Плавное падение (Ease.InCubic дает эффект гравитационного ускорения)
            transform.DOMove(targetPos, dropDuration).SetEase(Ease.InCubic).OnComplete(() =>
            {
                // Этот код выполнится ТОЧНО в момент касания стола

                CarcaGameManager gm = FindFirstObjectByType<CarcaGameManager>();
                if (gm != null && gm.meepleDustVFX != null)
                {
                    GameObject vfx = Instantiate(gm.meepleDustVFX, targetPos, Quaternion.identity);
                    Destroy(vfx, 2f);
                }

                AudioManager.Instance.PlaySFX("MeepleDrop"); // ОЗВУЧКА!

                // Легкое сплющивание при ударе (Squash & Stretch)
                transform.DOPunchScale(new Vector3(0.3f, -0.3f, 0.3f), 0.2f, 1);
            });
        }
    }
}