// ============================================================
// ProceduralGridGenerator.cs  |  Assets/Scripts/Grid/
//
// ESCENA: Crear un GameObject vacío "GridGenerator" en
//         GameplayScene y adjuntar este script.
// INSPECTOR: Asignar ProceduralLevelConfig + Tile Prefab
//            (cubo 1x0.2x1 con TileComponent adjunto).
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using Celeris.Config;
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    public class ProceduralGridGenerator : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Configuración")]
        public ProceduralLevelConfig config;

        [Header("Prefab de Tile (cubo placeholder)")]
        public GameObject tilePrefab;

        [Header("Animación ola")]
        [Tooltip("Metros por segundo al subir desde Y=-5 a Y=0")]
        public float waveSpeed       = 8f;
        [Tooltip("Delay entre columnas Z contiguas (segundos)")]
        public float waveZDelay      = 0.04f;

        // ── Acceso público ────────────────────────────────────
        /// <summary>Dictionary principal: coordenada lógica → TileComponent.</summary>
        public IReadOnlyDictionary<Vector2Int, TileComponent> TileMap => _tileMap;

        /// <summary>Posición mundial del tile Start (spawn del Droide).</summary>
        public Vector3 StartWorldPos { get; private set; }

        // ── Privado ───────────────────────────────────────────
        private readonly Dictionary<Vector2Int, TileComponent> _tileMap = new();
        private int   _totalSegments;
        private float _tileSize = 1f;   // unidad lógica por tile

        // ── Evento: avisa cuando el grid está listo ───────────
        public event Action OnGridReady;

        // ─────────────────────────────────────────────────────
        private void Start() => StartCoroutine(BuildGrid());

        // ── Pipeline de construcción ──────────────────────────
        private IEnumerator BuildGrid()
        {
            _tileMap.Clear();

            var rng = new System.Random(
                config.proceduralSeed != 0 ? config.proceduralSeed : Environment.TickCount);

            // Secuencia de segmentos: Start + N medios + Goal
            var sequence = BuildSegmentSequence(rng);

            // Generar todos los tiles con posición Y = -5 (sumergidos)
            int z = 0;
            foreach (SegmentType seg in sequence)
            {
                SpawnSegment(seg, z);
                z++;
            }

            // Animación ola: subir columna por columna
            yield return StartCoroutine(PlayWaveAnimation(z));

            OnGridReady?.Invoke();
        }

        // ── Secuencia de segmentos ────────────────────────────
        private List<SegmentType> BuildSegmentSequence(System.Random rng)
        {
            var seq = new List<SegmentType> { SegmentType.Start };
            for (int i = 0; i < config.mapLengthSegments; i++)
                seq.Add(config.GetWeightedRandomSegment(rng));
            seq.Add(SegmentType.Goal);
            _totalSegments = seq.Count;
            return seq;
        }

        // ── Spawn de un segmento (una "columna" de tiles en X) ─
        private void SpawnSegment(SegmentType seg, int zIndex)
        {
            for (int x = 0; x < config.gridWidth; x++)
            {
                var coord      = new Vector2Int(x, zIndex);
                var worldPos   = new Vector3(x * _tileSize, -5f, zIndex * _tileSize);
                var go         = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                go.name        = $"Tile_{x}_{zIndex}";

                var tile       = go.GetComponent<TileComponent>();
                if (tile == null) tile = go.AddComponent<TileComponent>();

                tile.gridCoord = coord;
                tile.tileType  = ResolveTileType(seg, x, config.gridWidth);

                // Arrow por defecto apunta al Norte (+Z)
                if (tile.tileType == TileType.ArrowTile)
                    tile.arrowDirection = MoveDirection.North;

                // IMPORTANTE: Awake ya ejecutó con BaseTile.
                // Refresh() actualiza el color al tipo real asignado.
                tile.Refresh();

                _tileMap[coord] = tile;

                // Guardar spawn del Droide (tile central del Start)
                if (seg == SegmentType.Start && x == config.gridWidth / 2)
                    StartWorldPos = new Vector3(x * _tileSize, 0f, zIndex * _tileSize);
            }
        }

        // ── Asigna TileType según segmento y columna X ────────
        // El tile especial siempre va en el carril central (gridWidth/2).
        private static TileType ResolveTileType(SegmentType seg, int x, int gridWidth)
        {
            bool isCenter = (x == gridWidth / 2);
            return seg switch
            {
                SegmentType.Start     => TileType.BaseTile,
                SegmentType.Goal      => TileType.GoalTile,
                SegmentType.Arrow     => isCenter ? TileType.ArrowTile     : TileType.BaseTile,
                SegmentType.Laser     => isCenter ? TileType.LaserTile     : TileType.BaseTile,
                SegmentType.Resonance => isCenter ? TileType.ResonanceTile : TileType.BaseTile,
                SegmentType.Charge    => isCenter ? TileType.ChargeTile    : TileType.BaseTile,
                _                     => TileType.BaseTile
            };
        }

        // ── Corrutina animación ola (Monument Valley style) ───
        private IEnumerator PlayWaveAnimation(int columnCount)
        {
            for (int z = 0; z < columnCount; z++)
            {
                // Lanza la subida de la columna Z sin esperar su fin
                StartCoroutine(RiseColumn(z));
                yield return new WaitForSeconds(waveZDelay);
            }
            // Esperar a que la última columna termine de subir
            yield return new WaitForSeconds(5f / waveSpeed + 0.1f);
        }

        private IEnumerator RiseColumn(int z)
        {
            float elapsed  = 0f;
            float duration = 5f / waveSpeed;   // distancia 5 unidades / vel

            // Recopilar tiles de esta columna
            var tiles = new List<Transform>();
            for (int x = 0; x < config.gridWidth; x++)
                if (_tileMap.TryGetValue(new Vector2Int(x, z), out var t))
                    tiles.Add(t.transform);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);
                float y  = Mathf.Lerp(-5f, 0f, t);
                foreach (var tr in tiles)
                {
                    var p = tr.position;
                    p.y   = y;
                    tr.position = p;
                }
                yield return null;
            }
            // Fijar en Y=0 exacto
            foreach (var tr in tiles)
            {
                var p = tr.position;
                p.y   = 0f;
                tr.position = p;
            }
        }

        // ── API de consulta (sin GetComponent en runtime) ─────

        /// <summary>Devuelve el TileComponent en la coordenada dada, o null.</summary>
        public TileComponent GetTile(Vector2Int coord) =>
            _tileMap.TryGetValue(coord, out var t) ? t : null;

        /// <summary>Convierte coordenada lógica a posición mundial Y=0.</summary>
        public Vector3 CoordToWorld(Vector2Int coord) =>
            new(coord.x * _tileSize, 0f, coord.y * _tileSize);
    }
}
