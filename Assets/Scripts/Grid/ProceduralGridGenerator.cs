// ============================================================
// ProceduralGridGenerator.cs  |  Assets/Scripts/Grid/
//
// Generación procedural de camino (Path Tracer) — v4
// ────────────────────────────────────────────────────────────
// • Solo 6 tipos de tile: Base, Arrow, Laser, Charge, Goal, Portal.
// • LaserController añadido automáticamente a LaserTiles con
//   intervalos escalados por dificultad.
// • PortalTile cada portalInterval tiles rectos (configurable).
// • Semilla determinista calculada por ProceduralLevelConfig.
// • Dificultad escalada via RuntimeDifficulty.
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
        [Range(1, 6)]  public int   minStraightSteps = 2;
        [Range(2, 12)] public int   maxStraightSteps = 5;
        [Range(0f, 1f)] public float turnProbability = 0.40f;

        [Header("Animación de aparición")]
        public float waveSpeed = 8f;
        public float tileDelay = 0.05f;

        // ── API pública ───────────────────────────────────────
        public IReadOnlyDictionary<Vector2Int, TileComponent> TileMap => _tileMap;
        public Vector3 StartWorldPos { get; private set; }
        public event Action OnGridReady;

        // ── Privado ───────────────────────────────────────────
        private readonly Dictionary<Vector2Int, TileComponent> _tileMap = new();
        private RuntimeDifficulty _diff;

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

        private IEnumerator BuildGrid()
        {
            _tileMap.Clear();

            if (config == null)
            {
                Debug.LogError("[ProceduralGridGenerator] Config no asignada.");
                yield break;
            }

            _diff = config.GetScaledDifficulty();
            var rng = new System.Random(_diff.Seed);

            Debug.Log($"[Generator] Nivel={config.levelIndex} Seed={_diff.Seed} " +
                      $"LaserMult={_diff.LaserWeightMultiplier:F1} " +
                      $"LaserOn={_diff.LaserActiveDuration:F1}s");

            var path = GeneratePath(rng);
            SpawnPathTiles(path);
            yield return StartCoroutine(PlayWaveAnimation(path));
            OnGridReady?.Invoke();
        }

        // ── Generación del camino ─────────────────────────────
        private List<PathNode> GeneratePath(System.Random rng)
        {
            var nodes    = new List<PathNode>();
            var occupied = new HashSet<Vector2Int>();

            Vector2Int pos  = Vector2Int.zero;
            Vector2Int dir  = new(0, 1);
            int straightRun   = 0;
            int straightCount = 0;   // para insertar portales

            int total = config.mapLengthSegments + _diff.ExtraSegments + 2;
            occupied.Add(pos);

            for (int i = 0; i < total; i++)
            {
                bool isLast = (i == total - 1);
                Vector2Int exitDir = dir;

                if (!isLast)
                {
                    bool blocked  = occupied.Contains(pos + dir);
                    bool mustTurn = straightRun >= maxStraightSteps || blocked;
                    bool canTurn  = straightRun >= minStraightSteps;
                    bool wantTurn = rng.NextDouble() < turnProbability;

                    if (mustTurn || (canTurn && wantTurn))
                    {
                        var t = TryPickTurnDir(pos, dir, occupied, rng);
                        if (t != dir) exitDir = t;
                    }

                    if (occupied.Contains(pos + exitDir))
                    {
                        exitDir = FindFreeDir(pos, dir, occupied, rng);
                        if (exitDir == Vector2Int.zero) isLast = true;
                    }
                }

                bool turned = exitDir != dir;
                TileType type;

                if (i == 0)          type = TileType.BaseTile;
                else if (isLast)     type = TileType.GoalTile;
                else if (turned)     { type = TileType.ArrowTile; straightCount = 0; }
                else
                {
                    straightCount++;
                    type = (config.portalInterval > 0 &&
                            straightCount % config.portalInterval == 0)
                        ? TileType.PortalTile
                        : PickStraightType(rng);
                }

                nodes.Add(new PathNode(pos, type, VectorToDir(exitDir)));
                if (isLast) break;

                occupied.Add(pos + exitDir);
                straightRun = turned ? 0 : straightRun + 1;
                dir = exitDir;
                pos = pos + exitDir;
            }

            // Garantía: último = GoalTile
            if (nodes.Count > 0)
            {
                var last = nodes[nodes.Count - 1];
                nodes[nodes.Count - 1] = new PathNode(last.Coord, TileType.GoalTile, last.ExitDir);
            }

            return nodes;
        }

        // ── Spawn de tiles ────────────────────────────────────
        private void SpawnPathTiles(List<PathNode> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                var node = path[i];
                var worldPos = new Vector3(node.Coord.x, -5f, node.Coord.y);
                var go = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                go.name = $"Tile_{node.Coord.x}_{node.Coord.y}_{node.Type}";

                var tile = go.GetComponent<TileComponent>() ?? go.AddComponent<TileComponent>();
                tile.gridCoord      = node.Coord;
                tile.tileType       = node.Type;
                tile.arrowDirection = node.ExitDir;
                tile.isActive       = true;

                switch (node.Type)
                {
                    case TileType.LaserTile:
                        var lc = go.AddComponent<LaserController>();
                        // Láseres alternados: algunos empiezan activos, otros inactivos
                        // para dar variedad y no punish inmediato al entrar al nivel
                        lc.Configure(
                            active:           _diff.LaserActiveDuration,
                            inactive:         _diff.LaserInactiveDuration,
                            startActiveState: (i % 3 != 0)   // 2/3 empiezan activos
                        );
                        break;

                    case TileType.PortalTile:
                        go.AddComponent<PortalTileComponent>();
                        // PortalTileComponent.Awake() aplica el color violeta
                        break;

                    default:
                        tile.Refresh();
                        break;
                }

                _tileMap[node.Coord] = tile;

                if (i == 0)
                    StartWorldPos = new Vector3(node.Coord.x, 0f, node.Coord.y);
            }
        }

        // ── Animación de aparición (ola) ──────────────────────
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
            float dur     = 5f / waveSpeed;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed   += Time.deltaTime;
                var p      = t.position;
                p.y        = Mathf.Lerp(-5f, 0f, Mathf.Clamp01(elapsed / dur));
                t.position = p;
                yield return null;
            }
            var fp = t.position; fp.y = 0f; t.position = fp;
        }

        // ── Helpers de dirección ──────────────────────────────
        private Vector2Int TryPickTurnDir(Vector2Int pos, Vector2Int dir,
                                          HashSet<Vector2Int> occ, System.Random rng)
        {
            var L = TurnLeft(dir); var R = TurnRight(dir);
            bool lf = rng.Next(2) == 0;
            var a = lf ? L : R; var b = lf ? R : L;
            if (!occ.Contains(pos + a)) return a;
            if (!occ.Contains(pos + b)) return b;
            return dir;
        }

        private Vector2Int FindFreeDir(Vector2Int pos, Vector2Int dir,
                                       HashSet<Vector2Int> occ, System.Random rng)
        {
            var cands = new List<Vector2Int> { dir, TurnLeft(dir), TurnRight(dir) };
            for (int i = cands.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cands[i], cands[j]) = (cands[j], cands[i]);
            }
            foreach (var d in cands)
                if (!occ.Contains(pos + d)) return d;
            return Vector2Int.zero;
        }

        private static Vector2Int TurnLeft(Vector2Int d)  => new(-d.y,  d.x);
        private static Vector2Int TurnRight(Vector2Int d) => new( d.y, -d.x);

        private TileType PickStraightType(System.Random rng)
        {
            var seg = config.GetWeightedRandomSegment(rng, _diff);
            return seg switch
            {
                SegmentType.Laser  => TileType.LaserTile,
                SegmentType.Charge => TileType.ChargeTile,
                _                  => TileType.BaseTile
            };
        }

        private static MoveDirection VectorToDir(Vector2Int v)
        {
            if (v.y > 0) return MoveDirection.North;
            if (v.y < 0) return MoveDirection.South;
            if (v.x > 0) return MoveDirection.East;
            return MoveDirection.West;
        }

        // ── API de consulta ───────────────────────────────────
        public TileComponent GetTile(Vector2Int coord) =>
            _tileMap.TryGetValue(coord, out var t) ? t : null;

        public Vector3 CoordToWorld(Vector2Int coord) => new(coord.x, 0f, coord.y);
    }
}
