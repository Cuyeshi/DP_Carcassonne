using Assets.Scripts.Main;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Main
{
    public class FullSettingsUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private TMP_Dropdown _qualityDropdown;
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private TMP_Dropdown _timeoutDropdown; // 30с, 60с, 120с

        void Start()
        {
            // Подгружаем сохраненные данные в UI элементы
            SettingsManager sm = SettingsManager.Instance;
            if (sm == null) return;

            if (_volumeSlider != null) _volumeSlider.value = sm.MasterVolume;
            if (_qualityDropdown != null) _qualityDropdown.value = sm.QualityLevel;
            if (_fullscreenToggle != null) _fullscreenToggle.isOn = sm.IsFullscreen;

            // Логика таймаута: 0 = 30с, 1 = 60с, 2 = 120с
            if (_timeoutDropdown != null)
            {
                if (sm.TurnTimeout == 30f) _timeoutDropdown.value = 0;
                else if (sm.TurnTimeout == 60f) _timeoutDropdown.value = 1;
                else _timeoutDropdown.value = 2;
            }

            // Добавляем слушателей (Listeners), чтобы при сдвиге ползунка настройка сразу применялась
            _volumeSlider?.onValueChanged.AddListener(val => sm.SetVolume(val));
            _qualityDropdown?.onValueChanged.AddListener(val => sm.SetQuality(val));
            _fullscreenToggle?.onValueChanged.AddListener(val => sm.SetFullscreen(val));

            _timeoutDropdown?.onValueChanged.AddListener(val =>
            {
                float t = 60f;
                if (val == 0) t = 30f;
                else if (val == 2) t = 120f;
                sm.SetTurnTimeout(t);
            });
        }
    }
}