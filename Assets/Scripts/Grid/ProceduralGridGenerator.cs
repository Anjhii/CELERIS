// ============================================================
// ProceduralGridGenerator.cs  |  Assets/Scripts/Grid/
//
// COORDINADOR PURO — Fase 3
// ────────────────────────────────────────────────────────────
// RESPONSABILIDAD ÚNICA:
//   Crear las dependencias, conectarlas en orden y exponer eventos.
//   No contiene lógica de secuencia, geometría, spawn ni animación.
//
// PIPELINE (llamadas en orden estricto):
//   1. PathSequencer.BuildSequence()   → IReadOnlyList<TileDescriptor>
//   2. PathGeometryTracer.Trace()      → IReadOnlyList<PlacedTile>
//   3. TileFactory.CreateAll()         → puebla TileMap
//   4. TileWaveAnimator.PlayWave()     → animación de aparición
//
// EXTENSIBILIDAD:
//   Añadir un nuevo obstáculo:
//     1. Crear clase : IObstacleDefinition
//     2. Llamar factory.Register(new MiObstaculo()) aquí → done
//   Las otras 4 clases del pipeline no se tocan.
//
//   Añadir wall-walking (Fase 4):
//     Crear perfil : IGravityProfile
//     Inyectar en PathGeometryTracer → done
//   PathSequencer, TileFactory y TileWaveAnimator no se tocan.
//
// API PÚBLICA:
//   TileMap       : IReadOnlyDictionary<Vector3Int, TileComponent>
//   StartWorldPos : Vector3
//   OnGridStarted : event Action  (justo antes del spawn)
//   OnGridReady   : event Action  (después de la animación)
//   GetTile(Vector3Int)      : TileComponent
//   CoordToWorld(Vector3Int) : Vector3  (static)
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
        [Header("Configuración del nivel")]
        public ProceduralLevelConfig config;

        [Header("Prefab de Tile (cubo 1×1×1)")]
        public GameObject tilePrefab;

        [Header("Forma del camino")]
        [Range(1, 6)]   public int   minStraightSteps = 2;
        [Range(2, 12)]  public int   maxStraightSteps = 5;
        [Range(0f, 1f)] public float turnProbability  = 0.40f;

        [Header("Animación de aparición")]
        public float waveSpeed = 8f;
        public float tileDelay = 0.05f;

        // ── API pública ───────────────────────────────────────
        public IReadOnlyDictionary<Vector3Int, TileComponent> TileMap => _tileMap;
        public Vector3 StartWorldPos { get; private set; }

        public event Action OnGridStarted;
        public event Action OnGridReady;

        // ── Privado ───────────────────────────────────────────
        private readonly Dictionary<Vector3Int, TileComponent> _tileMap = new();
        private RuntimeDifficulty _diff;

        // ─────────────────────────────────────────────────────
        private void Start() => StartCoroutine(BuildGrid());

        private IEnumerator BuildGrid()
        {
            _tileMap.Clear();

            if (config == null)
            {
                Debug.LogError("[ProceduralGridGenerator] Config no asignada.");
                yield break;
            }

            _diff = config.GetScaledDifficulty();
            OnGridStarted?.Invoke();

            // ── Semilla determinista ──────────────────────────
            var rng = new System.Random(config.levelIndex);
            Debug.Log($"[Generator] nivel={config.levelIndex} seed={config.levelIndex}");

            // ── PASO 1 — Secuencia lógica (PathSequencer) ─────
            // F2-T3: obstaclePool se construye UNA sola vez y se pasa
            // tanto a PathSequencer como a TileFactory. DRY: cero instancias duplicadas.
            var obstaclePool = BuildObstaclePool();
            var weights      = BuildWeights(obstaclePool);
            var sequencer    = new PathSequencer(obstaclePool, weights, rng);
            var sequence     = sequencer.BuildSequence();
            Debug.Log($"[Generator] Secuencia: {sequence.Count} tiles");

            // ── PASO 2 — Trazado geométrico (PathGeometryTracer) ──
            // IGravityProfile: null → FlatGravityProfile por defecto.
            // En Fase 4: inyectar el perfil correcto según tipo de nivel.
            var tracer = new PathGeometryTracer(
                minStraightSteps, maxStraightSteps, turnProbability, rng
                // gravityProfile: null  ← Fase 4: pasar IGravityProfile aquí
            );
            var placed = tracer.Trace(sequence);
            Debug.Log($"[Generator] Tiles colocados: {placed.Count}");

            // ── PASO 3 — Spawn (TileFactory) ──────────────────
            // F2-T3: se pasa el mismo obstaclePool — no se reconstruye.
            var factory = BuildFactory(obstaclePool);
            factory.CreateAll(placed);
            StartWorldPos = factory.StartWorldPos;

            // ── PASO 4 — Animación (TileWaveAnimator) ─────────
            var animator = gameObject.GetComponent<TileWaveAnimator>()
                        ?? gameObject.AddComponent<TileWaveAnimator>();
            yield return StartCoroutine(
                animator.PlayWave(placed, _tileMap, tileDelay, waveSpeed));

            OnGridReady?.Invoke();
        }

        // ── Construcción de dependencias ──────────────────────

        /// <summary>
        /// Construye el pool de IObstacleDefinition para el nivel.
        /// OCP: añadir obstacle = crear clase + añadirla aquí.
        /// El resto del pipeline no cambia.
        /// </summary>
        private IObstacleDefinition[] BuildObstaclePool() =>
            new IObstacleDefinition[]
            {
                new LaserObstacleDefinition(),
                new ChargeObstacleDefinition(),
            };

        /// <summary>
        /// Calcula pesos de selección escalados por dificultad del nivel.
        /// LaserWeightMultiplier aumenta con el índice de nivel.
        /// </summary>
        private int[] BuildWeights(IObstacleDefinition[] pool)
        {
            int laserW  = Mathf.Max(1, Mathf.RoundToInt(10 * _diff.LaserWeightMultiplier));
            int chargeW = 10;

            var w = new int[pool.Length];
            for (int i = 0; i < pool.Length; i++)
            {
                w[i] = pool[i].TileType switch
                {
                    TileType.LaserTile  => laserW,
                    TileType.ChargeTile => chargeW,
                    _                   => 10
                };
            }
            return w;
        }

        /// <summary>
        /// Construye y configura TileFactory reutilizando el pool ya creado.
        /// F2-T3: recibe el mismo obstaclePool del PASO 1 — cero allocations duplicadas.
        /// DRY: LaserObstacleDefinition y ChargeObstacleDefinition se instancian una sola vez.
        /// </summary>
        private TileFactory BuildFactory(IObstacleDefinition[] pool)
        {
            var factory = new TileFactory(tilePrefab, transform, _diff, _tileMap);
            foreach (var obstacle in pool)
                factory.Register(obstacle);
            return factory;
        }

        // ── API de consulta ───────────────────────────────────

        /// <summary>Devuelve el TileComponent en la coordenada dada, o null.</summary>
        public TileComponent GetTile(Vector3Int coord) =>
            _tileMap.TryGetValue(coord, out var t) ? t : null;

        /// <summary>Convierte coordenada de grid a posición world (Y = 0).</summary>
        public static Vector3 CoordToWorld(Vector3Int coord) =>
            new(coord.x, 0f, coord.z);
    }
}
