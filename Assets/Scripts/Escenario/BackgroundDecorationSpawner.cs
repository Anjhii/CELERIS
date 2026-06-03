using Celeris.Grid;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Celeris.Core
{
    public class BackgroundDecorationSpawner : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private ProceduralGridGenerator gridGenerator;

        [Header("Prefabs decorativos")]
        [SerializeField] private GameObject torre;
        [SerializeField] private GameObject rombo;
        [SerializeField] private GameObject fragmento01;
        [SerializeField] private GameObject fragmento02;
        [SerializeField] private GameObject fragmento03;

        [Header("Configuración de spawn")]
        [SerializeField] private int totalPieces = 8;
        [SerializeField] private float marginAroundGrid = 3f;
        [SerializeField] private float maxExtraDistance = 10f;
        [SerializeField] private float minY = 0f;
        [SerializeField] private float maxY = 8f;
        [SerializeField] private float minScale = 3f;
        [SerializeField] private float maxScale = 8f;

        [Header("Paleta de colores")]
        [SerializeField] private Material[] colorPalette;

        private System.Random _rng;
        private int _totalTiles;
        private float _gridTotalDuration;

        private void OnEnable()
        {
            if (gridGenerator != null)
            {
                gridGenerator.OnGridStarted += OnGridStarted;
                gridGenerator.OnGridReady += OnGridReady;
            }
        }

        private void OnDisable()
        {
            if (gridGenerator != null)
            {
                gridGenerator.OnGridStarted -= OnGridStarted;
                gridGenerator.OnGridReady -= OnGridReady;
            }
        }

        private void OnGridStarted()
        {
            int seed = gridGenerator.config != null ? gridGenerator.config.proceduralSeed : 0;
            _rng = new System.Random(seed != 0 ? seed + 99 : System.Environment.TickCount);

            _totalTiles = gridGenerator.TileMap.Count;
            float singleRiseDuration = 5f / gridGenerator.waveSpeed;
            _gridTotalDuration = (_totalTiles * gridGenerator.tileDelay) + singleRiseDuration;

            StartCoroutine(SpawnAndRise(_gridTotalDuration));
        }

        private void OnGridReady() { }

        private IEnumerator SpawnAndRise(float totalDuration)
        {
            var prefabs = BuildPrefabList();
            if (prefabs.Count == 0) yield break;

            GetGridBounds(out float minX, out float maxX, out float minZ, out float maxZ);

            var instances = new List<(Transform t, float targetY)>();

            for (int i = 0; i < totalPieces; i++)
            {
                Vector3 spawnPos = GetPositionOutsideGrid(minX, maxX, minZ, maxZ);
                float scale = Lerp(_rng, minScale, maxScale);
                Quaternion rotation = Quaternion.Euler(0f, (float)(_rng.NextDouble() * 360f), 0f);
                GameObject prefab = prefabs[_rng.Next(prefabs.Count)];

                Vector3 submergedPos = new Vector3(spawnPos.x, -5f, spawnPos.z);
                GameObject instance = Instantiate(prefab, submergedPos, rotation, transform);
                instance.transform.localScale = Vector3.one * scale;
                ApplyRandomMaterial(instance);

                instances.Add((instance.transform, spawnPos.y));
            }

            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / totalDuration);

                foreach (var (t, targetY) in instances)
                {
                    if (t == null) continue;
                    float currentY = Mathf.Lerp(-5f, targetY, progress);
                    t.position = new Vector3(t.position.x, currentY, t.position.z);
                }

                yield return null;
            }

            foreach (var (t, targetY) in instances)
            {
                if (t == null) continue;
                t.position = new Vector3(t.position.x, targetY, t.position.z);
            }
        }

        private Vector3 GetPositionOutsideGrid(float minX, float maxX, float minZ, float maxZ)
        {
            int side = _rng.Next(4);

            float x, z;

            switch (side)
            {
                case 0:
                    x = Lerp(_rng, minX - marginAroundGrid, maxX + marginAroundGrid);
                    z = maxZ + marginAroundGrid + Lerp(_rng, 0f, maxExtraDistance);
                    break;
                case 1:
                    x = Lerp(_rng, minX - marginAroundGrid, maxX + marginAroundGrid);
                    z = minZ - marginAroundGrid - Lerp(_rng, 0f, maxExtraDistance);
                    break;
                case 2:
                    x = maxX + marginAroundGrid + Lerp(_rng, 0f, maxExtraDistance);
                    z = Lerp(_rng, minZ - marginAroundGrid, maxZ + marginAroundGrid);
                    break;
                default:
                    x = minX - marginAroundGrid - Lerp(_rng, 0f, maxExtraDistance);
                    z = Lerp(_rng, minZ - marginAroundGrid, maxZ + marginAroundGrid);
                    break;
            }

            float y = Lerp(_rng, minY, maxY);
            return new Vector3(x, y, z);
        }

        private void GetGridBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.MaxValue; maxX = float.MinValue;
            minZ = float.MaxValue; maxZ = float.MinValue;

            foreach (var kvp in gridGenerator.TileMap)
            {
                if (kvp.Key.x < minX) minX = kvp.Key.x;
                if (kvp.Key.x > maxX) maxX = kvp.Key.x;
                if (kvp.Key.y < minZ) minZ = kvp.Key.y;
                if (kvp.Key.y > maxZ) maxZ = kvp.Key.y;
            }

            if (minX == float.MaxValue) { minX = 0; maxX = 0; minZ = 0; maxZ = 0; }
        }

        private void ApplyRandomMaterial(GameObject instance)
        {
            if (colorPalette == null || colorPalette.Length == 0) return;

            Material mat = colorPalette[_rng.Next(colorPalette.Length)];
            if (mat == null) return;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                r.material = mat;
        }

        private List<GameObject> BuildPrefabList()
        {
            var list = new List<GameObject>();
            if (torre != null)       list.Add(torre);
            if (rombo != null)       list.Add(rombo);
            if (fragmento01 != null) list.Add(fragmento01);
            if (fragmento02 != null) list.Add(fragmento02);
            if (fragmento03 != null) list.Add(fragmento03);
            return list;
        }

        private float Lerp(System.Random rng, float min, float max) =>
            min + (float)(rng.NextDouble() * (max - min));
    }
}
