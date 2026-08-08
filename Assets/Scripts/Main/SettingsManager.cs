using Assets.Scripts.Network;
using UnityEngine;
using UnityEngine.Audio;

namespace Assets.Scripts.Main
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("Audio")]
        public AudioMixer audioMixer;

        // Текущие значения
        public float MasterVolume { get; private set; }
        public int QualityLevel { get; private set; }
        public bool IsFullscreen { get; private set; }
        public float TurnTimeout { get; private set; } // В секундах

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.75f);
            QualityLevel = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
            IsFullscreen = PlayerPrefs.GetInt("IsFullscreen", Screen.fullScreen ? 1 : 0) == 1;
            TurnTimeout = PlayerPrefs.GetFloat("TurnTimeout", 60f); // По умолчанию минута на перезаход

            // Если в реестре компьютера осталось старое значение 9999 от предыдущих тестов
            if (TurnTimeout != 30f && TurnTimeout != 60f && TurnTimeout != 120f)
            {
                Debug.Log($"[НАСТРОЙКИ] Обнаружено устаревшее значение таймаута ({TurnTimeout}с). Сбрасываю на 120с.");
                TurnTimeout = 120f; // Сбрасываем на дефолтные 2 минуты
                PlayerPrefs.SetFloat("TurnTimeout", 120f);
                PlayerPrefs.Save();
            }

            ApplyAllSettings();
        }

        public void SetVolume(float value)
        {
            MasterVolume = value;
            PlayerPrefs.SetFloat("MasterVolume", value);
            // Чтобы звук менялся линейно, используется логарифмическая шкала децибел
            if (audioMixer != null) audioMixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f);
        }

        public void SetQuality(int levelIndex)
        {
            QualityLevel = levelIndex;
            PlayerPrefs.SetInt("QualityLevel", levelIndex);

            // Передаем 'true', чтобы принудительно обновить весь графический конвейер
            QualitySettings.SetQualityLevel(levelIndex, true);

            Debug.Log($"[НАСТРОЙКИ] Уровень качества изменен на: {levelIndex}");
        }

        public void SetFullscreen(bool isFull)
        {
            IsFullscreen = isFull;
            PlayerPrefs.SetInt("IsFullscreen", isFull ? 1 : 0);
            Screen.fullScreen = isFull;
        }

        public void SetTurnTimeout(float seconds)
        {
            TurnTimeout = seconds;
            PlayerPrefs.SetFloat("TurnTimeout", seconds);
            CarcaNetworkManager cnm = FindFirstObjectByType<CarcaNetworkManager>();
            cnm.UpdateTurnTimeOut(seconds);
        }

        private void ApplyAllSettings()
        {
            SetVolume(MasterVolume);
            SetQuality(QualityLevel);
            SetFullscreen(IsFullscreen);
            // Timeout и Speed просто лежат в переменных, игровые скрипты сами их прочитают
        }
    }
}