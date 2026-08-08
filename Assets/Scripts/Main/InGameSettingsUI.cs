using Assets.Scripts.Main;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Main
{
    public class InGameSettingsUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private Toggle _fullScreenTggl;

        void Start()
        {
            SettingsManager sm = SettingsManager.Instance;
            if (sm == null) return;

            if (_volumeSlider != null) _volumeSlider.value = sm.MasterVolume;

            _volumeSlider?.onValueChanged.AddListener(val => sm.SetVolume(val));
            _fullScreenTggl?.onValueChanged.AddListener(val => sm.SetFullscreen(val));
        }
    }
}