using UnityEngine;

public class SmoothFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, 5f);

    [Header("Spring Physics (Пружина)")]
    [Tooltip("Время реакции (отставание). Меньше = резче, Больше = плавнее.")]
    [Range(0.01f, 1f)]
    public float positionSmoothTime = 0.05f; // Сделаем по умолчанию чуть резче

    [Tooltip("ОГРАНИЧИТЕЛЬ: Максимальная скорость объекта. Если камера движется быстрее, объект 'натянет' пружину, но не улетит в космос.")]
    public float maxSpeed = 50f;

    [Tooltip("ЖЕСТКИЙ ПОВОДОК: Максимально допустимое отклонение от идеальной точки (в меттах).")]
    public float maxDriftRadius = 1.0f;

    [Header("Rotation Settings")]
    public bool followRotation = true;
    [Range(0.1f, 30f)]
    public float rotationLerpSpeed = 15f;

    private Vector3 _currentVelocity = Vector3.zero;

    void Start()
    {
        if (offset == Vector3.zero && target != null)
        {
            offset = target.InverseTransformPoint(transform.position);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Идеальная точка, где ДОЛЖЕН быть объект
        Vector3 targetPosition = target.TransformPoint(offset);

        // 1. ВЫЧИСЛЯЕМ ПЛАВНОЕ ДВИЖЕНИЕ С ОГРАНИЧЕНИЕМ СКОРОСТИ
        Vector3 smoothPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref _currentVelocity,
            positionSmoothTime,
            maxSpeed // Передаем максимальную скорость
        );

        // 2. ЖЕСТКИЙ ПОВОДОК (Clamp Distance)
        // Если из-за резкого рывка камеры smoothPosition оказался слишком далеко от targetPosition...
        float currentDistance = Vector3.Distance(targetPosition, smoothPosition);

        if (currentDistance > maxDriftRadius)
        {
            // ...мы жестко притягиваем объект к границе нашего "радиуса отклонения"
            Vector3 directionToTarget = (smoothPosition - targetPosition).normalized;
            smoothPosition = targetPosition + (directionToTarget * maxDriftRadius);

            // Сбрасываем скорость, чтобы пружина не начала "прыгать"
            _currentVelocity = Vector3.zero;
        }

        // Применяем финальную позицию
        transform.position = smoothPosition;

        // === ПЛАВНЫЙ ПОВОРОТ ===
        if (followRotation)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target.rotation,
                Time.deltaTime * rotationLerpSpeed
            );
        }
    }
}