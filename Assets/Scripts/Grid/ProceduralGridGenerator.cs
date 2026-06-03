// ============================================================
// ProceduralGridGenerator.cs  |  Assets/Scripts/Grid/
//
// Generación procedural de camino (Path Tracer) — v8
// ────────────────────────────────────────────────────────────
// ARQUITECTURA DE COMPUERTAS LÓGICAS ESTRICTAS
//
// Antes de instanciar cualquier tile, el bucle pasa por estos
// filtros en orden estricto (el primero que dispara, gana):
//
//   ■ COMPUERTA 0 — Especiales: Inicio, Meta, Epílogo.
//     Tienen prioridad absoluta y saltan todas las demás.
//     La Meta exige un BaseTile previo como buffer (Regla 2).
//
//   ■ COMPUERTA 1 — Candado de Clúster:
//     Si clusterLeft > 0  →  imprimir currentClusterType,
//     decrementar contador. Bloquea giros y portales mientras
//     el clúster no se haya completado.
//
//   ■ COMPUERTA 2 — Padding Universal:
//     if (lastPlacedType != BaseTile && desired != BaseTile)
//       → forzar BaseTile. NO resetea safeCounter.
//     Garantiza ≥1 BaseTile entre CUALQUIER par de elementos.
//
//   ■ COMPUERTA 4 — Generación normal:
//     Si las compuertas anteriores pasan, instanciar el tile
//     solicitado. Si es peligro, arrancar nuevo clúster.
//     Cada clúster elige su tipo libremente (Regla 1).
//
// COMPUERTA 3 eliminada (v8): ambos tipos de peligro están
// permitidos en el mismo acto. El tipo se sortea por clúster,
// no por acto. _currentActDangerType se conserva solo para log.
//
// Rangos de clúster (Regla 3 — ininterrumpibles):
//   ChargeTile : Random.Range(3, 7)  →  [3, 6] cubos
//   LaserTile  : Random.Range(1, 5)  →  [1, 4] cubos
//
// Garantías adicionales (v8):
//   • Siempre existen exactamente 3 portales.
//   • Cada acto tiene al menos un peligro (VerifyActQuota).
//   • Los clústeres no desbordan al acto siguiente.
//   • GoalTile siempre precedida de BaseTile.
//   • ArrowTile nunca se oculta por padding (exitDir revertido).
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

        // ── Tipo de peligro del acto actual (solo para logging) ───
        // v8: ya no filtra la generación. Cada clúster elige su
        // tipo libremente mediante currentClusterType (local).
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

            var rng = new System.Random();

            int totalTiles = config.GetTotalTiles(_diff.ExtraSegments);

            Debug.Log($"[Generator] v8 | Nivel={config.levelIndex} " +
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
            // lastPlacedType    — tipo del tile colocado en la iteración anterior.
            //                     Usado por Compuerta 2 (Padding).
            //
            // clusterLeft       — tiles de peligro restantes del clúster activo.
            //                     Cuando > 0, bloquea giros y portales.
            //
            // currentClusterType — tipo del clúster activo (LaserTile/ChargeTile).
            //                     [v8] independiente del acto, se sortea por clúster.
            //
            // safeCounter       — BaseTiles emitidos desde el último evento.
            //                     [v8] NO se resetea al hacer padding.
            //
            // actHasDanger      — true si el acto actual ya tiene al menos un peligro.
            //                     [v8] VerifyActQuota lo verifica al llegar al portal.
            //
            // inEpilogue        — true tras el 3er portal: solo BaseTile / ArrowTile.
            // ──────────────────────────────────────────────────────
            TileType lastPlacedType    = TileType.BaseTile;
            int      clusterLeft       = 0;
            TileType currentClusterType = TileType.BaseTile;  // v8: per-cluster
            int      safeCounter       = 0;
            bool     inEpilogue        = false;
            bool     actHasDanger      = false;               // v8: Bug 4

            // Tipo sugerido para el Acto 1 (solo log).
            _currentActDangerType = PickActDangerType(rng);
            Debug.Log($"[Generator] Acto 1 → peligro sugerido: {_currentActDangerType}");

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

                    // CANDADO (Regla 3): mid-clúster → sin giros.
                    if (clusterLeft > 0)
                    { mustTurn = false; wantTurn = false; }

                    // PADDING (Regla 2): no girar si el tile anterior no es BaseTile.
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
                    type = TileType.BaseTile;
                }

                // BUG 3 (v8): GoalTile requiere BaseTile inmediatamente anterior.
                // Si lastPlacedType no es BaseTile, emitir BaseTile aquí como buffer;
                // el post-procesamiento añadirá GoalTile como nodo extra.
                else if (isLast)
                {
                    type = (lastPlacedType != TileType.BaseTile)
                        ? TileType.BaseTile
                        : TileType.GoalTile;
                }

                // ════════════════════════════════════════════════
                //  Epílogo (después del 3er portal): sin peligros.
                // ════════════════════════════════════════════════

                else if (inEpilogue)
                {
                    if (turned && lastPlacedType == TileType.BaseTile)
                        type = TileType.ArrowTile;
                    else
                        type = TileType.BaseTile;
                }

                // ════════════════════════════════════════════════
                //  COMPUERTA 1 — Candado de Clúster
                //
                //  v8: usa currentClusterType (por clúster),
                //  no _currentActDangerType (por acto).
                // ════════════════════════════════════════════════

                else if (clusterLeft > 0)
                {
                    type = currentClusterType;   // BUG 1 fix
                    clusterLeft--;

                    if (clusterLeft == 0)
                        safeCounter = 0;
                }

                // ════════════════════════════════════════════════
                //  Portal (posición absoluta, solo en segmentos rectos)
                //
                //  BUG 4 (v8): VerifyActQuota — si el acto no tiene
                //  ningún peligro, convertir el último BaseTile del
                //  acto a danger antes de colocar el portal.
                //
                //  BUG 2 (v8): NO resetear safeCounter en padding.
                // ════════════════════════════════════════════════

                else if (!turned && nextPortalIdx < 3 && i >= portalTargets[nextPortalIdx])
                {
                    // BUG 4: verificar cuota ANTES del padding
                    if (!actHasDanger && lastPlacedType == TileType.BaseTile)
                    {
                        for (int ni = nodes.Count - 1; ni >= 0; ni--)
                        {
                            if (nodes[ni].Type == TileType.BaseTile)
                            {
                                TileType quota = PickActDangerType(rng);
                                var n = nodes[ni];
                                nodes[ni]    = new PathNode(n.Coord, quota, n.ExitDir);
                                actHasDanger = true;
                                // Actualizar lastPlacedType si era el nodo más reciente
                                if (ni == nodes.Count - 1) lastPlacedType = quota;
                                Debug.Log($"[Generator] VerifyActQuota: nodo {ni} → {quota}");
                                break;
                            }
                        }
                    }

                    // Padding antes del portal
                    if (lastPlacedType != TileType.BaseTile)
                    {
                        type = TileType.BaseTile;
                        // BUG 2: NO resetear safeCounter
                        // nextPortalIdx NO se incrementa — el portal se reintenta en i+1.
                    }
                    else
                    {
                        type = TileType.PortalTile;
                        nextPortalIdx++;

                        clusterLeft  = 0;
                        safeCounter  = 0;
                        actHasDanger = false;   // BUG 4: reset para el siguiente acto

                        if (nextPortalIdx < 3)
                        {
                            _currentActDangerType = PickActDangerType(rng);
                            Debug.Log($"[Generator] Acto {nextPortalIdx + 1} " +
                                      $"→ peligro sugerido: {_currentActDangerType}");
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
                //  BUG 6 (v8): si el padding impide colocar ArrowTile,
                //  revertir exitDir (seguir recto) para que la flecha
                //  aparezca en la siguiente iteración con BaseTile previo.
                //  BUG 2 (v8): NO resetear safeCounter.
                // ════════════════════════════════════════════════

                else if (turned)
                {
                    if (lastPlacedType != TileType.BaseTile)
                    {
                        // BUG 6: revertir giro — la flecha aparecerá en la iteración siguiente
                        exitDir = dir;
                        turned  = false;
                        type    = TileType.BaseTile;
                        // BUG 2: NO resetear safeCounter
                        Debug.LogWarning($"[Generator] i={i}: Padding → BaseTile, giro diferido " +
                                         $"(lastPlaced={lastPlacedType}).");
                    }
                    else
                    {
                        type = TileType.ArrowTile;
                    }
                }

                // ════════════════════════════════════════════════
                //  COMPUERTAS 2 y 4 — Generación normal
                //
                //  COMPUERTA 3 eliminada (v8 — BUG 1):
                //  el generador elige el tipo de peligro libremente
                //  en cada clúster; no hay restricción por acto.
                // ════════════════════════════════════════════════

                else
                {
                    // ── Tile deseado según el contador seguro ────
                    TileType desired;
                    if (safeCounter < maxSafeDist)
                    {
                        desired = TileType.BaseTile;
                    }
                    else
                    {
                        // BUG 1 (v8): elegir tipo libremente (no forzado por acto)
                        desired = PickActDangerType(rng);
                    }

                    // ── COMPUERTA 2 — Padding Universal ──────────
                    // BUG 2 (v8): NO resetear safeCounter al forzar padding.
                    if (lastPlacedType != TileType.BaseTile && desired != TileType.BaseTile)
                    {
                        type = TileType.BaseTile;
                        // safeCounter se mantiene — el clúster arranca en la iteración siguiente
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
                            // Calcular tamaño del clúster
                            int clusterSize = (type == TileType.ChargeTile)
                                ? rng.Next(3, 7)   // ChargeTile: [3, 6]
                                : rng.Next(1, 5);  // LaserTile:  [1, 4]

                            // BUG 5 (v8): no iniciar clúster si desborda al siguiente acto.
                            // Necesitamos: clúster + 1 buffer BaseTile antes del portal.
                            bool overflow = (nextPortalIdx < 3) &&
                                            (clusterSize >= portalTargets[nextPortalIdx] - i - 1);

                            if (overflow)
                            {
                                // Diferir el clúster; emitir BaseTile y esperar al siguiente acto
                                type = TileType.BaseTile;
                                safeCounter++;
                                Debug.Log($"[Generator] i={i}: Clúster diferido por overflow " +
                                          $"(tilesHastaPortal={portalTargets[nextPortalIdx] - i}, " +
                                          $"clusterSize={clusterSize})");
                            }
                            else
                            {
                                // BUG 1 (v8): guardar tipo del clúster (independiente del acto)
                                currentClusterType = type;
                                clusterLeft        = clusterSize - 1;
                                safeCounter        = 0;
                                actHasDanger       = true;   // BUG 4

                                Debug.Log($"[Generator] i={i}: Clúster {type} ×{clusterSize} " +
                                          $"(acto {nextPortalIdx + 1})");
                            }
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

            // ── Post-procesamiento: garantizar [BaseTile] → [GoalTile] ──
            //
            // BUG 3 (v8): Si el último nodo es BaseTile (buffer insertado porque
            // lastPlacedType era peligro en isLast), añadir GoalTile como nodo extra.
            // Si ya es GoalTile, no hace falta nada.
            if (nodes.Count > 0)
            {
                var last = nodes[nodes.Count - 1];

                if (last.Type == TileType.GoalTile)
                {
                    // Correcto — GoalTile ya está en su lugar.
                }
                else if (last.Type == TileType.BaseTile)
                {
                    // El buffer ya está; añadir GoalTile un paso adelante.
                    Vector2Int goalCoord = last.Coord + DirToVector(last.ExitDir);
                    nodes.Add(new PathNode(goalCoord, TileType.GoalTile, last.ExitDir));
                    Debug.Log($"[Generator] Post: GoalTile añadido en {goalCoord} tras buffer.");
                }
                else
                {
                    // Caso extremo: forzar GoalTile en el último nodo.
                    nodes[nodes.Count - 1] = new PathNode(
                        last.Coord, TileType.GoalTile, last.ExitDir);
                    Debug.LogWarning($"[Generator] Post: GoalTile forzado en {last.Coord} " +
                                     $"(lastType={last.Type}).");
                }
            }

            return nodes;
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS DE GENERACIÓN
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Sortea el tipo de peligro para un acto o un clúster.
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
                if (n == pos) continue;
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

        /// <summary>
        /// Convierte MoveDirection a Vector2Int. Inverso de VectorToDir.
        /// Necesario para el post-procesamiento de GoalTile (Bug 3).
        /// </summary>
        private static Vector2Int DirToVector(MoveDirection d) => d switch
        {
            MoveDirection.North => new Vector2Int( 0,  1),
            MoveDirection.South => new Vector2Int( 0, -1),
            MoveDirection.East  => new Vector2Int( 1,  0),
            MoveDirection.West  => new Vector2Int(-1,  0),
            _                   => Vector2Int.zero
        };

        // ── API de consulta ───────────────────────────────────
        public TileComponent GetTile(Vector2Int coord) =>
            _tileMap.TryGetValue(coord, out var t) ? t : null;

        public Vector3 CoordToWorld(Vector2Int coord) => new(coord.x, 0f, coord.y);
    }
}
