using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using DG.Tweening; // Используем DOTween для плавного затухания музыки!

namespace Assets.Scripts.Media
{
    

    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        public AudioSource musicSource; // Источник для фоновой музыки (Loop = true)
        public AudioSource sfxSource;   // Источник для коротких звуков (Loop = false)

        [Header("Background Music")]
        public AudioClip menuMusic;
        public AudioClip gameMusic;

        [Header("Sound Effects List")]
        public List<NamedClip> sfxClips; // Список всех звуков (клик, тайл, мипл)

        private Dictionary<string, AudioClip> _sfxDictionary = new Dictionary<string, AudioClip>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Звук не должен прерываться при смене сцен!

                // Заполняем словарь для быстрого поиска звуков по имени
                foreach (var item in sfxClips)
                {
                    _sfxDictionary[item.name] = item.clip;
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnEnable()
        {
            // Подписываемся на событие смены сцен в Unity
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Автоматически переключаем музыку при смене сцены
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MenuScene")
            {
                PlayMusicWithFade(menuMusic);
            }
            else if (scene.name == "GameScene")
            {
                PlayMusicWithFade(gameMusic);
            }
        }

        // Плавный переход музыки с помощью DOTween
        private void PlayMusicWithFade(AudioClip newClip)
        {
            if (musicSource.clip == newClip) return; // Если эта музыка уже играет - игнорируем

            if (musicSource.clip == null)
            {
                // Если музыка еще не играла, просто плавно включаем
                musicSource.clip = newClip;
                musicSource.volume = 0f;
                musicSource.Play();
                musicSource.DOFade(0.5f, 1.5f); // Плавно поднимаем громкость до 50% за 1.5 сек
            }
            else
            {
                // Если играет старый трек: плавно гасим его -> меняем -> плавно включаем новый!
                musicSource.DOFade(0f, 1f).OnComplete(() =>
                {
                    musicSource.clip = newClip;
                    musicSource.Play();
                    musicSource.DOFade(0.5f, 1.5f);
                });
            }
        }

        /// <summary>
        /// Воспроизвести короткий звук из любой точки кода по его имени
        /// </summary>
        public void PlaySFX(string soundName)
        {
            if (_sfxDictionary.TryGetValue(soundName, out AudioClip clip))
            {
                // PlayOneShot позволяет накладывать звуки друг на друга (не прерывая предыдущие)
                sfxSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] Звук с именем '{soundName}' не найден в списке!");
            }
        }
    }
}