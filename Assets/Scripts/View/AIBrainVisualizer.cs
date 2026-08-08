using UnityEngine;
using System.Collections.Generic;
using Assets.Scripts.Main;
using Assets.Scripts.Core_logic.AI; // Для CarcaGameManager
using Mirror;

namespace Assets.Scripts.View
{
    public class AIBrainVisualizer : MonoBehaviour
    {
        AITelemetry _aITelemetry = new AITelemetry();

        // НАСТРОЙКИ СЕТКИ
        [Header("Настройки сетки")]
        [Tooltip("Количество цилиндров в ряду")]
        [SerializeField] int maxPerRow = 10; // Максимум цилиндров в одном ряду
        [Tooltip("Расстояние между цилиндрами по горизонтали")]
        [SerializeField] float spacingX = 1.5f; // Расстояние между цилиндрами по горизонтали
        [Tooltip("Расстояние между рядами (отрицательное, чтобы строить \"вниз\" к камере)")]
        [SerializeField] float spacingZ = -1.5f; // Расстояние между рядами (отрицательное, чтобы строить "вниз" к камере)

        [Header("Prefabs")]
        public GameObject neuronPrefab;

        [Header("Colors")]
        public Color idleColor = Color.gray;
        public Color goodColor = Color.green;
        public Color badColor = Color.red;

        [Header("Настройки свечения")]
        public float glowIntensity = 3.5f; // Сила свечения

        private List<GameObject> _spawnedNeurons = new List<GameObject>();
        private List<Renderer> _neuronRenderers = new List<Renderer>();

        private CarcaGameManager _gm;
        private bool _wasThinking = false;

        // Для оптимизации
        private MaterialPropertyBlock _propBlock;
        // В URP базовый цвет называется "_BaseColor", в Built-in - "_Color".
        // В URP свойства называются так:
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        //private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
        //private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        void Awake()
        {
            _propBlock = new MaterialPropertyBlock(); // Создаем один раз на всю игру
            this.enabled = false;
        }

        /// <summary>
        /// Инициализация скрипта визуализатора мыслей бота.
        /// </summary>
        /// <param name="managerInstance"></param>
        public void Initialize(CarcaGameManager managerInstance)
        {
            _gm = managerInstance;

            // Если мы НЕ СЕРВЕР (не Хост), навсегда отключаем этот скрипт и прячем комнату
            if (!NetworkServer.active)
            {
                gameObject.SetActive(false);
                return;
            }

            foreach (Transform child in transform) Destroy(child.gameObject);
            _spawnedNeurons.Clear();
            _neuronRenderers.Clear();
            _wasThinking = false;

            this.enabled = true;
            Debug.Log("[AIBRAIN] Визуализатор активирован (Режим Хоста).");
        }

        void Update()
        {
            // ЗАЩИТА: Проверяем, есть ли менеджер и бот
            if (_gm == null || _gm.AIBotInstance == null) return;

            // Читаем данные напрямую из телеметрии бота!
            bool isThinking = _gm.AIBotInstance.Telemetry.IsThinking;
            int nodesCount = _gm.AIBotInstance.Telemetry.CurrentNodes.Count;

            bool shouldSpawn = isThinking && nodesCount > 0 && _spawnedNeurons.Count != nodesCount;

            if (shouldSpawn)
            {
                SpawnNeurons(nodesCount);
                _wasThinking = true;
            }
            else if (!isThinking && _wasThinking)
            {
                _wasThinking = false;
                foreach (var n in _spawnedNeurons) Destroy(n);
                _spawnedNeurons.Clear();
                _neuronRenderers.Clear();
            }

            if (isThinking)
            {
                UpdateNeurons();
            }
        }

        private void SpawnNeurons(int count)
        {
            foreach (var n in _spawnedNeurons) Destroy(n);
            _spawnedNeurons.Clear();
            _neuronRenderers.Clear();

            int totalRows = Mathf.CeilToInt((float)count / maxPerRow);
            float startZ = (totalRows * -spacingZ) / 2f;

            for (int i = 0; i < count; i++)
            {
                int row = i / maxPerRow;
                int col = i % maxPerRow;
                int currentItemsInRow = (row == totalRows - 1 && count % maxPerRow != 0) ? count % maxPerRow : maxPerRow;
                float startX = -(currentItemsInRow * spacingX) / 2f + (spacingX / 2f);

                Vector3 pos = transform.position + new Vector3(startX + (col * spacingX), 0, startZ + (row * spacingZ));
                GameObject neuron = Instantiate(neuronPrefab, pos, Quaternion.identity, transform);

                Renderer ren = neuron.GetComponent<Renderer>();

                // Используем sharedMaterial (без дублирования в памяти)
                ren.sharedMaterial.EnableKeyword("_EMISSION");

                _spawnedNeurons.Add(neuron);
                _neuronRenderers.Add(ren);
            }
        }

        private void UpdateNeurons()
        {
            if (_gm.AIBotInstance.Telemetry.CurrentNodes.Count == 0 || _spawnedNeurons.Count == 0) return;

            for (int i = 0; i < _gm.AIBotInstance.Telemetry.CurrentNodes.Count; i++)
            {
                if (i >= _spawnedNeurons.Count) break;

                var stat = _gm.AIBotInstance.Telemetry.CurrentNodes[i];
                float winRate = stat.TotalSimulations > 0 ? (float)stat.Wins / stat.TotalSimulations : 0f;

                GameObject neuron = _spawnedNeurons[i];
                Renderer ren = _neuronRenderers[i];

                float targetHeight = 1f + (winRate * 4f);
                neuron.transform.localScale = Vector3.Lerp(neuron.transform.localScale, new Vector3(1, targetHeight, 1), Time.deltaTime * 5f);

                Color targetColor = winRate == 0f ? idleColor : Color.Lerp(badColor, goodColor, winRate);

                _propBlock.Clear();
                Color hdrColor = targetColor * glowIntensity;
                _propBlock.SetColor(BaseColorProperty, hdrColor);

                ren.SetPropertyBlock(_propBlock);
            }
        }

        public void NeuronsStats()
        {
            Debug.Log(_aITelemetry.CurrentNodes.Count);
            
        }
    }
}