// ============================================================
// ProceduralGridGenerator.cs  |  Assets/Scripts/Grid/
//
// GENERACIÓN DE CAMINO MODULAR (Path Tracer)
// ──────────────────────────────────────────
// Genera un camino de bloques 1×1×1 conectados en el plano XZ.
// Todos los tiles comparten Y = 0. Sin huecos, sin solapamientos.
//
// Algoritmo:
//   1. Empieza en (0,0) moviéndose hacia el Norte.
//   2. En cada paso: seguir recto, girar izquierda o derecha.
//   3. No puede pisar posiciones ya ocupadas (anti-solapamiento).
//   4. Giros  → ArrowTile (redirige al Droide automáticamente).
//   5. Rectos → BaseTile / LaserTile / ChargeTile (pesos del config).
//   6. Último tile → siempre GoalTile.
//
// Colores (definidos en TileComponent.TypeColors, no tocar):
//   BaseTile(gris) ArrowTile(azul) LaserTile(naranja)
//   ChargeTile(verde) GoalTile(amarillo)
//
// INSPECTOR: Asignar ProceduralLevelConfig + prefab cubo 1×1×1.
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

        [Header("Prefab de Tile (cubo 1×1×1, escala fija)")]
        public GameObject tilePrefab;

        [Header("Forma del camino")]
        [Tooltip("Pasos mínimos en línea recta antes de permitir un giro")]
        [Range(1, 6)]
        public int minStraightSteps = 2;

        [Tooltip("Pasos máximos en línea recta antes de forzar un giro")]
        [Range(2, 10)]
        public int maxStraightSteps = 5;

        [Tooltip("Probabilidad de girar en cada paso elegible (0 = nunca, 1 = siempre)")]
        [Range(0f, 1f)]
        public float turnProbability = 0.40f;

        [Header("Animación de aparición")]
        [Tooltip("Metros/segundo al subir desde Y=-5 a Y=0")]
        public float waveSpeed = 8f;
        [Tooltip("Delay entre tiles consecutivos al subir (segundos)")]
        public float tileDelay = 0.05f;

        // ── Acceso público ────────────────────────────────────
        public IReadOnlyDictionary<Vector2Int, TileComponent> TileMap => _tileMap;
        public Vector3 StartWorldPos { get; private set; }
        public event Action OnGridReady;

        // ── Privado ───────────────────────────────────────────
        private readonly Dictionary<Vector2Int, TileComponent> _tileMap = new();

        // Nodo interno del path
        private readonly struct PathNode
        {
            public readonly Vector2Int    Coord;
            public readonly TileType      Type;
            public readonly MoveDirection ExitDir;
            public PathNode(Vector2Int c, TileType t, MoveDirection d)
            { Coord = c; Type = t; ExitDir = d; }
        }

        // ─────────────────────────────────────────────────────
        private void Start() => StartCoroutine(BuildGrid());

        // ── Pipeline ─────────────────────────────────────────
        private IEnumerator BuildGrid()
        {
            _tileMap.Clear();

            var rng = new System.Random(
                config.proceduralSeed != 0 ? config.proceduralSeed : Environment.TickCount);

            var path = GeneratePath(rng);
            SpawnPathTiles(path);

            yield return StartCoroutine(PlayWaveAnimation(path));

            OnGridReady?.Invoke();
        }

        // ── Generación del camino ─────────────────────────────
        //
        // INVARIANTE CLAVE:
        //   Cada tile se coloca en 'pos' con su 'exitDir' ya decidido.
        //   El Droide llega a pos siguiendo el exitDir del tile ANTERIOR,
        //   y al estar en pos usa su propio exitDir para saber a dónde ir.
        //   Por eso: exitDir del tile en pos ≠ dir del paso anterior.
        //   El tile se sitúa en pos, y el SIGUIENTE tile irá en pos + exitDir.
        //
        // Flujo correcto:
        //   1. Decidir exitDir para el tile en 'pos'.
        //   2. Colocar nodo en 'pos' con ese exitDir.
        //   3. Avanzar: pos = pos + exitDir, dir = exitDir.
        //
        private List<PathNode> GeneratePath(System.Random rng)
        {
            var nodes    = new List<PathNode>();
            var occupied = new HashSet<Vector2Int>();

            Vector2Int pos  = Vector2Int.zero;
            Vector2Int dir  = new(0, 1);  // Norte (coincide con DroideController._direction)
            int straightRun = 0;

            // total = Start + intermedios + Goal
            int total = config.mapLengthSegments + 2;

            occupied.Add(pos);

            for (int i = 0; i < total; i++)
            {
                bool isLastTile = (i == total - 1);

                // ── Decidir exitDir del tile actual ─────────────
                Vector2Int exitDir = dir;

                if (!isLastTile)
                {
                    bool straightBlocked = occupied.Contains(pos + dir);
                    bool mustTurn = (straightRun >= maxStraightSteps) || straightBlocked;
                    bool canTurn  = straightRun >= minStraightSteps;
                    bool wantTurn = rng.NextDouble() < turnProbability;

                    if (mustTurn || (canTurn && wantTurn))
                    {
                        var turn = TryPickTurnDir(pos, dir, occupied, rng);
                        if (turn != dir) exitDir = turn;
                    }

                    // Si la dirección elegida sigue bloqueada, buscar cualquier salida
                    if (occupied.Contains(pos + exitDir))
                    {
                        exitDir = FindFreeDir(pos, dir, occupied, rng);
                        if (exitDir == Vector2Int.zero) isLastTile = true;
                    }
                }

                // ── Colocar tile en 'pos' ───────────────────────
                bool turned = (exitDir != dir);
                TileType type;
                if (i == 0)          type = TileType.BaseTile;   // Spawn siempre BaseTile
                else if (isLastTile) type = TileType.GoalTile;
                else if (turned)     type = TileType.ArrowTile;  // Giro → flecha
                else                 type = PickStraightType(rng);

                nodes.Add(new PathNode(pos, type, VectorToDir(exitDir)));

                if (isLastTile) break;

                // ── Avanzar al siguiente tile ───────────────────
                Vector2Int nextPos = pos + exitDir;
                occupied.Add(nextPos);
                straightRun = turned ? 0 : straightRun + 1;
                dir = exitDir;
                pos = nextPos;
            }

            // Garantía final: el último nodo debe ser GoalTile
            if (nodes.Count > 0)
            {
                var last = nodes[nodes.Count - 1];
                nodes[nodes.Count - 1] = new PathNode(last.Coord, TileType.GoalTile, last.ExitDir);
            }

            return nodes;
        }

        // ── Spawn ─────────────────────────────────────────────
        private void SpawnPathTiles(List<PathNode> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                var node = path[i];

                // Spawn sumergido; la animación lo sube a Y=0
                var worldPos = new Vector3(node.Coord.x, -5f, node.Coord.y);
                var go = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                go.name = $"Tile_{node.Coord.x}_{node.Coord.y}";

                var tile = go.GetComponent<TileComponent>()
                           ?? go.AddComponent<TileComponent>();

                tile.gridCoord      = node.Coord;
                tile.tileType       = node.Type;
                tile.arrowDirection = node.ExitDir;
                tile.isActive       = true;   // LaserTiles activos por defecto

                tile.Refresh();   // Aplica color correcto (ver TypeColors en TileComponent)
                _tileMap[node.Coord] = tile;

                if (i == 0)
                    StartWorldPos = new Vector3(node.Coord.x, 0f, node.Coord.y);
            }
        }

        // ── Animación: tiles suben en orden del path ──────────
        private IEnumerator PlayWaveAnimation(List<PathNode> path)
        {
            foreach (var node in path)
            {
                if (_tileMap.TryGetValue(node.Coord, out var tile))
                    StartCoroutine(RiseTile(tile.transform));
                yield return new WaitForSeconds(tileDelay);
            }
            yield return new WaitForSeconds(5f / waveSpeed + 0.1f);
        }

        private IEnumerator RiseTile(Transform t)
        {
            float elapsed  = 0f;
            float duration = 5f / waveSpeed;

            while (elapsed < duration)
            {
                elapsed    += Time.deltaTime;
                var p       = t.position;
                p.y         = Mathf.Lerp(-5f, 0f, Mathf.Clamp01(elapsed / duration));
                t.position  = p;
                yield return null;
            }
            var fp = t.position; fp.y = 0f; t.position = fp;
        }

        // ── Helpers de dirección ──────────────────────────────

        // Elige giro izquierda o derecha si la posición está libre
        private Vector2Int TryPickTurnDir(Vector2Int pos, Vector2Int dir,
                                          HashSet<Vector2Int> occupied, System.Random rng)
        {
            var L = TurnLeft(dir);
            var R = TurnRight(dir);
            bool leftFirst = rng.Next(2) == 0;
            var first = leftFirst ? L : R;
            var second = leftFirst ? R : L;

            if (!occupied.Contains(pos + first))  return first;
            if (!occupied.Contains(pos + second)) return second;
            return dir;   // ambos bloqueados → seguir recto
        }

        // Busca cualquier dirección libre (excluye U-turn)
        private Vector2Int FindFreeDir(Vector2Int pos, Vector2Int dir,
                                       HashSet<Vector2Int> occupied, System.Random rng)
        {
            var candidates = new List<Vector2Int> { dir, TurnLeft(dir), TurnRight(dir) };

            // Shuffle para no sesgar
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            foreach (var d in candidates)
                if (!occupied.Contains(pos + d))
                    return d;

            return Vector2Int.zero;
        }

        // Norte(0,1)→Oeste(-1,0)→Sur(0,-1)→Este(1,0)→Norte
        private static Vector2Int TurnLeft(Vector2Int d)  => new(-d.y,  d.x);
        // Norte(0,1)→Este(1,0)→Sur(0,-1)→Oeste(-1,0)→Norte
        private static Vector2Int TurnRight(Vector2Int d) => new( d.y, -d.x);

        // weightArrow → tile sin obstáculo (BaseTile)
        // weightLaser → LaserTile   |   weightCharge → ChargeTile
        private TileType PickStraightType(System.Random rng)
        {
            int total = config.weightArrow + config.weightLaser + config.weightCharge;
            if (total <= 0) return TileType.BaseTile;

            int roll = rng.Next(total);
            if (roll < config.weightArrow)                              return TileType.BaseTile;
            if (roll < config.weightArrow + config.weightLaser)        return TileType.LaserTile;
            return TileType.ChargeTile;
        }

        private static MoveDirection VectorToDir(Vector2Int v)
        {
            if (v.y > 0) return MoveDirection.North;
            if (v.y < 0) return MoveDirection.South;
            if (v.x > 0) return MoveDirection.East;
            return MoveDirection.West;
        }

        // ── API de consulta ───────────────────────────────────

        /// <summary>TileComponent en la coordenada dada, o null si no existe.</summary>
        public TileComponent GetTile(Vector2Int coord) =>
            _tileMap.TryGetValue(coord, out var t) ? t : null;

        /// <summary>Posición mundial Y=0 de una coordenada lógica.</summary>
        public Vector3 CoordToWorld(Vector2Int coord) =>
            new(coord.x, 0f, coord.y);
    }
}
