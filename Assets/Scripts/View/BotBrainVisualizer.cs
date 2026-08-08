using UnityEngine;
using System.Collections.Generic;
using Assets.Scripts.Core_logic.AI;

namespace Assets.Scripts.View
{
    public class BotBrainVisualizer : MonoBehaviour
    {
        public GameObject neuronPrefab;
        public Color idleColor = Color.gray;
        public Color goodColor = Color.green;
        public Color badColor = Color.red;
        public float glowIntensity = 3.5f;

        [Header("Настройки сетки")]
        [Tooltip("Количество цилиндров в ряду")]
        [SerializeField] int maxPerRow = 10; // Максимум цилиндров в одном ряду
        [Tooltip("Расстояние между цилиндрами по горизонтали")]
        [SerializeField] float spacingX = 1.5f; // Расстояние между цилиндрами по горизонтали
        [Tooltip("Расстояние между рядами (отрицательное, чтобы строить \"вниз\" к камере)")]
        [SerializeField] float spacingZ = -1.5f; // Расстояние между рядами (отрицательное, чтобы строить "вниз" к камере)

        private List<GameObject> _spawnedNeurons = new List<GameObject>();
        private List<Renderer> _neuronRenderers = new List<Renderer>();

        private AITelemetry _targetTelemetry; // Личный источник данных!
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        private bool _wasThinking = false;

        public void Initialize(AITelemetry telemetry)
        {
            _targetTelemetry = telemetry;
            _propBlock = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (_targetTelemetry == null) return;

            if (_targetTelemetry.IsThinking && !_wasThinking)
            {
                SpawnNeurons(_targetTelemetry.CurrentNodes.Count);
                _wasThinking = true;
            }
            else if (!_targetTelemetry.IsThinking && _wasThinking)
            {
                _wasThinking = false;
            }

            if (_targetTelemetry.IsThinking)
            {
                UpdateNeurons();
            }
        }

        private void SpawnNeurons(int count)
        {
            // Тот же самый код построения сетки из AIBrainVisualizer...
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
                ren.material.EnableKeyword("_EMISSION");

                _spawnedNeurons.Add(neuron);
                _neuronRenderers.Add(ren);
            }
        }

        private void UpdateNeurons()
        {
            if (_targetTelemetry.CurrentNodes.Count == 0 || _spawnedNeurons.Count == 0) return;

            for (int i = 0; i < _targetTelemetry.CurrentNodes.Count; i++)
            {
                if (i >= _spawnedNeurons.Count) break;

                var stat = _targetTelemetry.CurrentNodes[i];
                float winRate = stat.TotalSimulations > 0 ? (float)stat.Wins / stat.TotalSimulations : 0f;

                GameObject neuron = _spawnedNeurons[i];
                Renderer ren = _neuronRenderers[i];

                float targetHeight = 1f + (winRate * 4f);
                neuron.transform.localScale = Vector3.Lerp(neuron.transform.localScale, new Vector3(1, targetHeight, 1), Time.deltaTime * 5f);

                Color targetColor = winRate == 0f ? idleColor : Color.Lerp(badColor, goodColor, winRate);

                _propBlock.Clear();
                _propBlock.SetColor(BaseColorProperty, targetColor);
                _propBlock.SetColor(ColorProperty, targetColor);
                _propBlock.SetColor(EmissionColorProperty, targetColor * glowIntensity);
                ren.SetPropertyBlock(_propBlock);
            }
        }
    }
}