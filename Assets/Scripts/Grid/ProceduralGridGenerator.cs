// ============================================================
// ProceduralGridGenerator.cs  |  Assets/Scripts/Grid/
//
// Generación procedural de camino (Path Tracer) — v7
// ────────────────────────────────────────────────────────────
// ARQUITECTURA DE COMPUERTAS LÓGICAS ESTRICTAS
//
// Antes de instanciar cualquier tile, el bucle pasa por estos
// filtros en orden estricto (el primero que dispara, gana):
//
//   ■ COMPUERTA 0 — Especiales: Inicio, Meta, Epílogo.
//     Tienen prioridad absoluta y saltan todas las demás.
//
//   ■ COMPUERTA 1 — Candado de Clúster:
//     Si clusterLeft > 0  →  imprimir _currentActDangerType,
//     decrementar contador. Bloquea giros y portales mientras
//     el clúster no se haya completado.
//
//   ■ COMPUERTA 2 — Padding Universal:
//     if (lastPlacedType != BaseTile && desired != BaseTile)
//       → forzar BaseTile.
//     Garantiza ≥1 BaseTile entre CUALQUIER par de elementos.
//
//   ■ COMPUERTA 3 — Filtro de Acto:
//     Si desired es un peligro pero ≠ _currentActDangerType
//       → forzar BaseTile.
//     Cada acto solo permite UN tipo de peligro.
//
//   ■ COMPUERTA 4 — Generación normal:
//     Si las 3 compuertas anteriores pasan, instanciar el tile
//     solicitado. Si es peligro, arrancar nuevo clúster.
//
// Rangos de clúster (Regla 3 — ininterrumpibles):
//   ChargeTile : Random.Range(3, 7)  →  [3, 6] cubos
//   LaserTile  : Random.Range(1, 5)  →  [1, 4] cubos
//
// Aislamiento por Acto (Regla 1):
//   Al inicio de cada acto el generador elige _currentActDangerType
//   (LaserTile o ChargeTile). Cualquier peligro distinto → BaseTile.
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
        public IReadOnlyDictionary<Vector2Int, TileComponent> TileMap => _tileMap;
        public Vector3 StartWorldPos { get; private set; }
        public event Action OnGridStarted;
        public event Action OnGridReady;

        // ── Privado ───────────────────────────────────────────
        private readonly Dictionary<Vector2Int, TileComponent> _tileMap = new();
        private RuntimeDifficulty _diff;

        // ── Aislamiento por Acto (Regla 1) ───────────────────
        // Un único tipo de peligro por acto. Se sortea al inicio
        // de cada acto y se mantiene hasta el siguiente portal.
        private TileType _currentActDangerType;

        // ── PathNode ──────────────────────────────────────────
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

            OnGridStarted?.Invoke();

            // Semilla aleatoria — cada intento produce un mapa distinto.
            var rng = new System.Random();

            int totalTiles = config.GetTotalTiles(_diff.ExtraSegments);

            Debug.Log($"[Generator] v7 | Nivel={config.levelIndex} " +
                      $"TotalTiles={totalTiles} " +
                      $"LaserMult={_diff.LaserWeightMultiplier:F1} (seed=random)");

            var path = GeneratePath(rng, totalTiles);
            SpawnPathTiles(path);
            yield return StartCoroutine(PlayWaveAnimation(path));
            OnGridReady?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        //  GENERACIÓN DEL CAMINO — bucle principal
        // ══════════════════════════════════════════════════════
        private List<PathNode> GeneratePath(System.Random rng, int totalTiles)
        {
            var nodes    = new List<PathNode>();
            var occupied = new HashSet<Vector2Int>();

            Vector2Int pos = Vector2Int.zero;
            Vector2Int dir = new(0, 1);
            int straightRun = 0;

            // ── Posiciones de los 3 portales (≈25 / 50 / 75 %) ───
            int portalInterval = Mathf.Max(3, totalTiles / 4);
            int[] portalTargets = {
                portalInterval,
                portalInterval * 2,
                portalInterval * 3
            };
            int nextPortalIdx = 0;

            // ── Distancia segura máxima escalada por nivel ────────
            float scaledSafe = config.maxSafeDistanceStart - (float)config.levelIndex / 5f;
            if (config.levelIndex >= 1) scaledSafe *= 0.80f;
            int maxSafeDist = Mathf.Max(
                config.minSafeDistanceEnd,
                Mathf.Min(Mathf.FloorToInt(scaledSafe), portalInterval - 2));

            // ══ Estado del generador ══════════════════════════════
            //
            // lastPlacedType  — tipo del tile colocado en la iteración anterior.
            //                   Usado exclusivamente por Compuerta 2 (Padding).
            //
            // clusterLeft     — tiles de peligro que aún quedan por emitir en el
            //                   clúster actual (0 = sin clúster activo).
            //                   Cuando > 0, actúa como CANDADO: bloquea giros,
            //                   portales y cualquier cambio de tipo.
            //
            // safeCounter     — BaseTiles emitidos desde el último evento.
            //                   Cuando alcanza maxSafeDist, el generador puede
            //                   intentar arrancar un nuevo clúster de peligro.
            //
            // inEpilogue      — true tras el 3er portal: solo BaseTile / ArrowTile.
            // ──────────────────────────────────────────────────────
            TileType lastPlacedType = TileType.BaseTile;
            int      clusterLeft    = 0;
            int      safeCounter    = 0;
            bool     inEpilogue     = false;

            // Seleccionar tipo de peligro para el Acto 1.
            _currentActDangerType = PickActDangerType(rng);
            Debug.Log($"[Generator] Acto 1 → peligro: {_currentActDangerType}");

            occupied.Add(pos);

            for (int i = 0; i < totalTiles; i++)
            {
                bool isLast    = (i == totalTiles - 1);
                Vector2Int exitDir = dir;

                // ── Geometría del camino (dirección de salida) ────
                if (!isLast)
                {
                    bool blocked  = occupied.Contains(pos + dir);
                    bool mustTurn = straightRun >= maxStraightSteps || blocked;
                    bool canTurn  = straightRun >= minStraightSteps;
                    bool wantTurn = rng.NextDouble() < turnProbability;

                    // CANDADO (Regla 3): mid-clúster → sin giros de ningún tipo.
                    if (clusterLeft > 0)
                    { mustTurn = false; wantTurn = false; }

                    // PADDING (Regla 2): no girar si el tile anterior no es BaseTile
                    // (eso pondría un ArrowTile sin separación).
                    if (clusterLeft == 0 && lastPlacedType != TileType.BaseTile)
                    { wantTurn = false; if (!blocked) mustTurn = false; }

                    // Sin giros alrededor de los portales (±1 índice).
                    if (clusterLeft == 0)
                    {
                        for (int pi = nextPortalIdx; pi < 3; pi++)
                        {
                            if (Mathf.Abs(i - portalTargets[pi]) <= 1)
                            { mustTurn = false; wantTurn = false; break; }
                        }
                    }

                    if (mustTurn || (canTurn && wantTurn))
                    {
                        var t = TryPickTurnDir(pos, dir, occupied, rng);
                        if (t != dir) exitDir = t;
                    }

                    if (!IsDirValid(pos, exitDir, occupied))
                    {
                        exitDir = FindFreeDir(pos, dir, occupied, rng);
                        if (exitDir == Vector2Int.zero) isLast = true;
                    }
                }

                bool turned = exitDir != dir;
                TileType type;

                // ════════════════════════════════════════════════
                //  COMPUERTA 0 — Tiles especiales (prioridad abs.)
                // ════════════════════════════════════════════════

                if (i == 0)
                {
                    // Inicio: BaseTile de arranque. La Regla 2 exige que el
                    // tile inmediatamente después del inicio sea también BaseTile,
                    // lo cual se garantiza porque safeCounter=0 < maxSafeDist.
                    type = TileType.BaseTile;
                }
                else if (isLast)
                {
                    // Meta: siempre GoalTile.
                    type = TileType.GoalTile;
                }

                // ════════════════════════════════════════════════
                //  Epílogo (después del 3er portal): sin peligros.
                // ════════════════════════════════════════════════

                else if (inEpilogue)
                {
                    if (turned && lastPlacedType == TileType.BaseTile)
                        type = TileType.ArrowTile;
                    else
                        type = TileType.BaseTile;   // Padding o recto → BaseTile
                }

                // ════════════════════════════════════════════════
                //  COMPUERTA 1 — Candado de Clúster
                //
                //  Si clusterLeft > 0, el generador DEBE emitir el
                //  tipo de peligro del acto sin excepción.
                //  Ninguna otra compuerta puede interrumpirlo.
                // ════════════════════════════════════════════════

                else if (clusterLeft > 0)
                {
                    type = _currentActDangerType;
                    clusterLeft--;

                    // Al terminar el clúster, resetear safeCounter para que
                    // la Compuerta 4 emita un BaseTile de buffer en la próxima
                    // iteración (safeCounter=0 < maxSafeDist → desired=BaseTile).
                    if (clusterLeft == 0)
                        safeCounter = 0;
                }

                // ════════════════════════════════════════════════
                //  Portal (posición absoluta, solo en segmentos rectos)
                //
                //  Se evalúa DESPUÉS del candado de clúster para que
                //  un clúster activo no sea interrumpido por un portal.
                // ════════════════════════════════════════════════

                else if (!turned && nextPortalIdx < 3 && i >= portalTargets[nextPortalIdx])
                {
                    // COMPUERTA 2 aplicada al portal:
                    // Si el tile anterior no es BaseTile, insertar buffer primero.
                    if (lastPlacedType != TileType.BaseTile)
                    {
                        type = TileType.BaseTile;
                        safeCounter = 0;
                        // nextPortalIdx NO se incrementa — el portal se reintenta en i+1.
                    }
                    else
                    {
                        type = TileType.PortalTile;
                        nextPortalIdx++;

                        // Reset completo para el siguiente acto.
                        clusterLeft  = 0;
                        safeCounter  = 0;

                        if (nextPortalIdx < 3)
                        {
                            // Seleccionar nuevo tipo de peligro para el siguiente acto.
                            _currentActDangerType = PickActDangerType(rng);
                            Debug.Log($"[Generator] Acto {nextPortalIdx + 1} " +
                                      $"→ peligro: {_currentActDangerType}");
                        }
                        else
                        {
                            inEpilogue = true;
                            Debug.Log("[Generator] Epílogo activado.");
                        }
                    }
                }

                // ════════════════════════════════════════════════
                //  Giro → ArrowTile
                //
                //  COMPUERTA 2 aplicada a giros: si lastPlaced no es
                //  BaseTile, se coloca BaseTile en su lugar (el giro
                //  físico se mantiene, pero sin el ArrowTile visual).
                //  Esto solo ocurre en bloqueos físicos extremos.
                // ════════════════════════════════════════════════

                else if (turned)
                {
                    if (lastPlacedType != TileType.BaseTile)
                    {
                        // Padding: no se puede colocar ArrowTile aquí.
                        // Colocar BaseTile en la dirección del giro.
                        type = TileType.BaseTile;
                        safeCounter = 0;
                        Debug.LogWarning($"[Generator] i={i}: Padding forzó BaseTile en giro " +
                                         $"(lastPlaced={lastPlacedType}). ArrowTile diferido.");
                    }
                    else
                    {
                        type = TileType.ArrowTile;
                        // El post-arrow es responsabilidad de la Compuerta 2 en la
                        // siguiente iteración: lastPlaced=ArrowTile + desired≠Base → Base.
                        // No hace falta forcedBaseTilesLeft.
                    }
                }

                // ════════════════════════════════════════════════
                //  COMPUERTAS 2, 3 y 4 — Generación normal
                // ════════════════════════════════════════════════

                else
                {
                    // ── Tile deseado según el contador seguro ────
                    TileType desired;
                    if (safeCounter < maxSafeDist)
                    {
                        // Zona segura: seguir emitiendo BaseTiles.
                        desired = TileType.BaseTile;
                    }
                    else
                    {
                        // Zona segura agotada: intentar iniciar clúster.
                        desired = _currentActDangerType;
                    }

                    // ── COMPUERTA 2 — Padding Universal ──────────
                    // Si el tile anterior no es BaseTile y el deseado
                    // tampoco lo es → forzar BaseTile de separación.
                    if (lastPlacedType != TileType.BaseTile && desired != TileType.BaseTile)
                    {
                        type = TileType.BaseTile;
                        safeCounter = 0;
                    }

                    // ── COMPUERTA 3 — Filtro de Acto ─────────────
                    // Si el desired es peligro pero no coincide con el
                    // tipo del acto → forzar BaseTile.
                    // (Con _currentActDangerType esto nunca debería
                    //  dispararse, pero actúa como red de seguridad.)
                    else if (desired != TileType.BaseTile &&
                             desired != _currentActDangerType)
                    {
                        type = TileType.BaseTile;
                        safeCounter = 0;
                        Debug.LogWarning($"[Generator] i={i}: Filtro de Acto bloqueó " +
                                         $"{desired} (acto permite {_currentActDangerType}).");
                    }

                    // ── COMPUERTA 4 — Instanciar tile solicitado ──
                    else
                    {
                        type = desired;

                        if (type == TileType.BaseTile)
                        {
                            safeCounter++;
                        }
                        else
                        {
                            // Arrancar clúster de peligro.
                            // El primer tile se emite ahora; clusterLeft = tamaño - 1.
                            int clusterSize = (type == TileType.ChargeTile)
                                ? rng.Next(3, 7)   // ChargeTile: [3, 6]
                                : rng.Next(1, 5);  // LaserTile:  [1, 4]

                            clusterLeft = clusterSize - 1;
                            safeCounter = 0;

                            Debug.Log($"[Generator] i={i}: Clúster {type} ×{clusterSize} " +
                                      $"(acto {nextPortalIdx + 1})");
                        }
                    }
                }

                // ── Registrar tile ────────────────────────────────
                nodes.Add(new PathNode(pos, type, VectorToDir(exitDir)));
                lastPlacedType = type;
                if (isLast) break;

                occupied.Add(pos + exitDir);
                straightRun = turned ? 0 : straightRun + 1;
                dir = exitDir;
                pos = pos + exitDir;
            }

            // Garantía absoluta: el último nodo siempre es GoalTile.
            if (nodes.Count > 0)
            {
                var last = nodes[nodes.Count - 1];
                nodes[nodes.Count - 1] = new PathNode(
                    last.Coord, TileType.GoalTile, last.ExitDir);
            }

            return nodes;
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS DE GENERACIÓN
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Sortea el tipo de peligro para un acto.
        /// Usa el peso de dificultad del config para favorecer Láser en niveles altos.
        /// </summary>
        private TileType PickActDangerType(System.Random rng)
        {
            var seg = config.GetWeightedDangerSegment(rng, _diff);
            return seg == SegmentType.Laser ? TileType.LaserTile : TileType.ChargeTile;
        }

        // ══════════════════════════════════════════════════════
        //  SPAWN DE TILES
        // ══════════════════════════════════════════════════════
        private void SpawnPathTiles(List<PathNode> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                var node     = path[i];
                var worldPos = new Vector3(node.Coord.x, -5f, node.Coord.y);
                var go       = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                go.name      = $"Tile_{node.Coord.x}_{node.Coord.y}_{node.Type}";

                var tile = go.GetComponent<TileComponent>() ?? go.AddComponent<TileComponent>();
                tile.gridCoord      = node.Coord;
                tile.tileType       = node.Type;
                tile.arrowDirection = node.ExitDir;
                tile.isActive       = true;

                switch (node.Type)
                {
                    case TileType.LaserTile:
                        var lc = go.AddComponent<LaserController>();
                        // 2/3 de los láseres empiezan activos → no punish inmediato.
                        lc.Configure(
                            active:           _diff.LaserActiveDuration,
                            inactive:         _diff.LaserInactiveDuration,
                            startActiveState: (i % 3 != 0)
                        );
                        break;

                    case TileType.PortalTile:
                        go.AddComponent<PortalTileComponent>();
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

        // ══════════════════════════════════════════════════════
        //  ANIMACIÓN DE APARICIÓN (OLA)
        // ══════════════════════════════════════════════════════
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

        // ══════════════════════════════════════════════════════
        //  HELPERS DE DIRECCIÓN
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Valida que el destino (pos+dir) no esté ocupado y no sea adyacente
        /// lateralmente a otro tile del camino (garantía anti-Snake).
        /// </summary>
        private static bool IsDirValid(Vector2Int pos, Vector2Int dir, HashSet<Vector2Int> occ)
        {
            Vector2Int next = pos + dir;
            if (occ.Contains(next)) return false;

            Vector2Int[] neighbors = {
                next + new Vector2Int( 1, 0),
                next + new Vector2Int(-1, 0),
                next + new Vector2Int( 0, 1),
                next + new Vector2Int( 0,-1)
            };
            foreach (var n in neighbors)
            {
                if (n == pos) continue;   // predecesor inmediato — permitido
                if (occ.Contains(n)) return false;
            }
            return true;
        }

        private Vector2Int TryPickTurnDir(Vector2Int pos, Vector2Int dir,
                                          HashSet<Vector2Int> occ, System.Random rng)
        {
            var L = TurnLeft(dir); var R = TurnRight(dir);
            bool lf = rng.Next(2) == 0;
            var a = lf ? L : R; var b = lf ? R : L;
            if (IsDirValid(pos, a, occ)) return a;
            if (IsDirValid(pos, b, occ)) return b;
            return dir;   // fallback: seguir recto
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
                if (IsDirValid(pos, d, occ)) return d;
            return Vector2Int.zero;
        }

        private static Vector2Int TurnLeft(Vector2Int d)  => new(-d.y,  d.x);
        private static Vector2Int TurnRight(Vector2Int d) => new( d.y, -d.x);

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
