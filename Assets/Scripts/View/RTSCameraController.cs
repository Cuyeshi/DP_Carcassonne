using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.View
{
    /// <summary>
    /// Контроллер камеры в стиле RTS для нового Input System.
    /// Перемещение зажатием средней кнопки мыши (колёсика).
    /// </summary>
    public class RTSCameraController : MonoBehaviour
    {
        [Header("Настройки перемещения")]
        [Tooltip("Включить перемещение камеры")]
        public bool canMoveCamera = true;
        [Tooltip("Скорость перемещения камеры")]
        public float panSpeed = 20f;

        [Header("Настройки приближения")]
        [Tooltip("Включить приближение камеры")]
        public bool canZoomCamera = true;
        [Tooltip("Множитель чувствительности мыши")]
        public float mouseSensitivity = 1f;

        [Header("Ограничения карты (Bounds)")]
        [Tooltip("Ограничивать ли движение камеры границами карты")]
        public bool clampToBounds = true;

        [Tooltip("Минимальные координаты границы (X, Z)")]
        public Vector2 boundsMin = new Vector2(-50f, -50f);

        [Tooltip("Максимальные координаты границы (X, Z)")]
        public Vector2 boundsMax = new Vector2(50f, 50f);



        [Header("Диапазон зума")]
        [SerializeField, Tooltip("Минимальное значение (приближение)")] private float minZoom = 2f;
        [SerializeField, Tooltip("Максимальное значение (отдаление)")] private float maxZoom = 10f;

        [Header("Поведение зума")]
        [SerializeField, Tooltip("Скорость реакции на колёсико")] private float sensitivity = 5f;
        [SerializeField, Tooltip("Плавность перехода (чем выше, тем резче)")] private float smoothSpeed = 10f;

        private Camera cam;
        private float currentZoom;
        private float targetZoom;
        private Scene _currentScene;

        private void Awake()
        {
            _currentScene = SceneManager.GetActiveScene();

            cam = GetComponent<Camera>();
            currentZoom = cam.fieldOfView;
            targetZoom = currentZoom;
        }

        void LateUpdate()
        {
            if (Assets.Scripts.View.LobbyUI.IsLobbyActive && _currentScene.name == "GameScene") return; // БЛОКИРОВКА КАМЕРЫ

            if (Mouse.current != null && Mouse.current.middleButton.isPressed && canMoveCamera)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();

                Vector3 moveDirection = new Vector3(-mouseDelta.x, 0f, -mouseDelta.y);

                float zoomMultiplier = currentZoom / 57f;

                Vector3 moveOffset = moveDirection * panSpeed * mouseSensitivity * zoomMultiplier;

                transform.Translate(moveOffset, Space.World);

                if (clampToBounds)
                {
                    Vector3 pos = transform.position;
                    pos.x = Mathf.Clamp(pos.x, boundsMin.x, boundsMax.x);
                    pos.z = Mathf.Clamp(pos.z, boundsMin.y, boundsMax.y);
                    transform.position = pos;
                }
            }

            if (canZoomCamera)
            {
                // Зум (оставляем deltaTime, так как scroll - это не пиксели, а дискретные шаги колесика)
                float scroll = GetScrollDelta();

                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // Для колесика лучше использовать нормализованное значение (-1, 0, 1), 
                    // иначе на разных мышках зум будет работать по-разному
                    float normalizedScroll = Mathf.Sign(scroll);
                    targetZoom -= normalizedScroll * sensitivity;
                    targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
                }

                currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * smoothSpeed);
                cam.fieldOfView = currentZoom;
            }
        }

        private float GetScrollDelta()
        {
            if (Mouse.current == null) return 0f;
            return Mouse.current.scroll.ReadValue().y;
        }
    }
}