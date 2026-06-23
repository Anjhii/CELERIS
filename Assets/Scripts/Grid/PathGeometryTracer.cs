// ============================================================
// PathGeometryTracer.cs  |  Assets/Scripts/Grid/
//
// PASO B de la generación: asigna posiciones y direcciones de
// salida a la lista de TileDescriptor producida por PathSequencer.
//
// ENTRADA:  IReadOnlyList<TileDescriptor>  (secuencia lógica)
// SALIDA:   List<PlacedTile>               (secuencia con geometría)
//
// RESPONSABILIDAD ÚNICA:
//   Solo decide POR DÓNDE va el camino. No decide QUÉ tiles
//   hay (eso es PathSequencer) ni instancia GameObjects (eso
//   es ProceduralGridGenerator).
//
// REGLAS GEOMÉTRICAS APLICADAS:
//   • Giros de exactamente 90° (TurnLeft / TurnRight).
//   • Sin carriles pegados: IsDirValid verifica que el tile
//     siguiente no tenga vecinos ortogonales ya ocupados.
//   • Los giros se insertan como ArrowTile SOLO sobre BaseTile.
//     Si el descriptor actual no es BaseTile, el trazador
//     no gira en ese paso (mantiene dirección).
//   • Sin colisión de tiles: HashSet<Vector3Int> occupied.
//   • Fallo de trazado: si no hay dirección libre, el trazador
//     detiene el camino y registra warning. El generador
//     detecta la lista incompleta y puede relanzar con otra semilla.
//
// ESCALABILIDAD — COORDENADAS 3D (Fase 3/4):
//   Toda la lógica opera sobre Vector3Int desde esta fase.
//   Hoy Y siempre es 0. Para wall-walking, PathGeometryTracer
//   recibirá un IGravityProfile que define las direcciones
//   válidas de avance por nodo. El stub del campo está reservado.
//
// ESCALABILIDAD — ANTI-SOLAPAMIENTO (Regla de Carriles Pegados):
//   IsDirValid verifica los 4 vecinos ortogonales del tile
//   siguiente. Esto previene que el camino corra paralelo y
//   pegado a un tramo ya existente.
// ============================================================
using System.Collections.Generic;
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    public class PathGeometryTracer
    {
        // ── Parámetros de forma del camino ────────────────────
        private readonly int   _minStraight;
        private readonly int   _maxStraight;
        private readonly float _turnProbability;
        private readonly System.Random _rng;

        // ── STUB Fase 4: perfil de gravedad ──────────────────
        // Hoy siempre FlatGravityProfile (plano XZ).
        // En Fase 4, ProceduralGridGenerator inyectará el perfil
        // correcto para cada tipo de nivel (wall, ramp, etc.).
        // GetValidAdvanceDirections() reemplazará las constantes
        // N/S/E/O de TurnLeft/TurnRight cuando se active.
        private readonly IGravityProfile _gravityProfile;

        // ── Estado interno del trazado ────────────────────────
        private HashSet<Vector3Int> _occupied;

        // ── Resultado de inicio para el generador ─────────────
        /// <summary>Coordenada world del primer tile (tile de inicio).</summary>
        public Vector3Int StartCoord { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="minStraight">Mínimo de pasos rectos antes de poder girar.</param>
        /// <param name="maxStraight">Máximo de pasos rectos antes de girar forzado.</param>
        /// <param name="turnProbability">Probabilidad [0,1] de girar cuando es posible.</param>
        /// <param name="rng">RNG con semilla del nivel.</param>
        /// <param name="gravityProfile">
        ///   Perfil de gravedad/plano de movimiento. Null → FlatGravityProfile (default).
        ///   Stub para Fase 4 (wall-walking). No usar hasta esa fase.
        /// </param>
        public PathGeometryTracer(int minStraight, int maxStraight,
                                   float turnProbability, System.Random rng,
                                   IGravityProfile gravityProfile = null)
        {
            _minStraight    = minStraight;
            _maxStraight    = maxStraight;
            _turnProbability = turnProbability;
            _rng            = rng;
            _gravityProfile = gravityProfile ?? FlatGravityProfile.Instance;
        }

        // ══════════════════════════════════════════════════════
        //  API PÚBLICA
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Asigna posición y dirección de salida a cada TileDescriptor.
        /// Los giros se marcan insertando un PlacedTile de tipo ArrowTile
        /// (solo posible sobre BaseTile — el trazador no gira en medio
        /// de un obstáculo o portal).
        ///
        /// INVARIANTE DE SALIDA:
        ///   La lista resultante contiene exactamente los mismos
        ///   TileDescriptor de entrada, en el mismo orden lógico,
        ///   más los ArrowTile insertados en los puntos de giro.
        /// </summary>
        // F3-T5: goto TileAdded / goto Done reemplazados con continue/break.
        // Lógica idéntica — solo cambia la estructura de control.
        // Flag 'arrowInserted' unifica los dos paths (giro y normal) en
        // el bloque de avance de posición al final de cada iteración.
        public List<PlacedTile> Trace(IReadOnlyList<TileDescriptor> sequence)
        {
            _occupied = new HashSet<Vector3Int>();

            var result       = new List<PlacedTile>(sequence.Count + 16);
            var pos          = Vector3Int.zero;
            var dir          = new Vector3Int(0, 0, 1);  // Norte (hacia +Z)
            int straightRun  = 0;

            StartCoord = pos;
            _occupied.Add(pos);

            for (int i = 0; i < sequence.Count; i++)
            {
                var descriptor = sequence[i];
                bool isLast    = (i == sequence.Count - 1);

                // ── Decidir dirección de salida para este tile ──
                Vector3Int exitDir     = dir;
                bool       arrowInserted = false;  // true si ya se añadió un ArrowTile

                if (!isLast)
                {
                    // Solo girar en BaseTile: no girar en medio de un
                    // obstáculo, portal ni goal (rompe la integridad física).
                    bool canInsertArrow = descriptor.IsBase;

                    // ── Decisión de giro ──────────────────────────
                    // Si es BaseTile, se puede insertar un ArrowTile
                    // en esta coordenada para marcar un giro de 90°.
                    // El ArrowTile REEMPLAZA al BaseTile en esa coord.
                    // El descriptor del sequencer se "consume" igualmente
                    // (el BaseTile lógico se convierte en ArrowTile geométrico).

                    if (canInsertArrow)
                    {
                        bool blocked  = _occupied.Contains(pos + dir);
                        bool mustTurn = straightRun >= _maxStraight || blocked;
                        bool wantTurn = straightRun >= _minStraight
                                        && _rng.NextDouble() < _turnProbability;

                        if (mustTurn || wantTurn)
                        {
                            var candidate = TryPickTurnDir(pos, dir);
                            if (candidate != dir)
                            {
                                // ArrowTile en la coord actual, salida = nueva dirección.
                                // El BaseTile descriptor se consume aquí (i avanzará al final del for).
                                result.Add(PlacedTile.Arrow(pos, VectorToDir(candidate)));
                                straightRun  = 0;
                                dir          = candidate;
                                exitDir      = candidate;
                                arrowInserted = true;
                            }
                        }
                    }

                    // ── Verificar dirección actual (si no se insertó arrow) ──
                    if (!arrowInserted && !IsDirValid(pos, dir))
                    {
                        var free = FindFreeDir(pos, dir);
                        if (free == Vector3Int.zero)
                        {
                            // Sin dirección libre: detener trazado (era goto Done)
                            Debug.LogWarning($"[PathGeometryTracer] Sin dirección libre en pos={pos}. " +
                                             $"Trazado detenido en índice {i}/{sequence.Count}.");
                            result.Add(new PlacedTile(descriptor, pos, VectorToDir(dir)));
                            break;
                        }

                        if (descriptor.IsBase)
                        {
                            // Giro forzado: el BaseTile se convierte en ArrowTile.
                            result.Add(PlacedTile.Arrow(pos, VectorToDir(free)));
                            straightRun  = 0;
                            dir          = free;
                            exitDir      = free;
                            arrowInserted = true;
                        }
                        else
                        {
                            // No se puede insertar flecha en tile no-base.
                            // Cambiar dirección internamente sin marcarlo visualmente.
                            dir     = free;
                            exitDir = free;
                        }
                    }

                    if (!arrowInserted) exitDir = dir;
                }

                // ── Tile normal (solo si no se insertó un ArrowTile) ──
                if (!arrowInserted)
                {
                    result.Add(new PlacedTile(descriptor, pos, VectorToDir(exitDir)));
                    straightRun++;
                }

                // ── Avanzar posición (unificado: arrow y normal llegan aquí) ──
                if (!isLast)
                {
                    pos = pos + exitDir;
                    _occupied.Add(pos);
                }
            }

            return result;
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS DE DIRECCIÓN
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Verifica que la dirección 'dir' desde 'pos' es válida:
        ///   1. El tile siguiente no está ocupado.
        ///   2. Ningún vecino ortogonal del tile siguiente está
        ///      ocupado (regla anti-carril-pegado).
        /// </summary>
        private bool IsDirValid(Vector3Int pos, Vector3Int dir)
        {
            Vector3Int next = pos + dir;
            if (_occupied.Contains(next)) return false;

            // Vecinos ortogonales en el plano XZ (hoy Y=0, preparado para Y!=0 en Fase 3)
            Vector3Int[] neighbors =
            {
                next + new Vector3Int( 1, 0,  0),
                next + new Vector3Int(-1, 0,  0),
                next + new Vector3Int( 0, 0,  1),
                next + new Vector3Int( 0, 0, -1),
            };

            foreach (var n in neighbors)
            {
                // El tile desde el que venimos es vecino válido (no bloquea)
                if (n == pos) continue;
                if (_occupied.Contains(n)) return false;
            }

            return true;
        }

        /// <summary>
        /// Intenta girar izquierda o derecha aleatoriamente.
        /// Devuelve la dirección original si ningún giro es válido.
        /// </summary>
        private Vector3Int TryPickTurnDir(Vector3Int pos, Vector3Int dir)
        {
            var L    = TurnLeft(dir);
            var R    = TurnRight(dir);
            bool lFirst = _rng.Next(2) == 0;
            var a    = lFirst ? L : R;
            var b    = lFirst ? R : L;

            if (IsDirValid(pos, a)) return a;
            if (IsDirValid(pos, b)) return b;
            return dir;
        }

        /// <summary>
        /// Busca cualquier dirección libre (incluida la actual) de forma aleatoria.
        /// Devuelve Vector3Int.zero si no hay ninguna.
        /// </summary>
        private Vector3Int FindFreeDir(Vector3Int pos, Vector3Int currentDir)
        {
            var candidates = new List<Vector3Int>
            {
                currentDir,
                TurnLeft(currentDir),
                TurnRight(currentDir)
            };

            // Shuffle
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            foreach (var d in candidates)
                if (IsDirValid(pos, d)) return d;

            return Vector3Int.zero;
        }

        // ── Operaciones de giro en plano XZ ───────────────────
        // TurnLeft / TurnRight operan en el plano XZ (Y=0 hoy).
        // En Fase 3/4 con wall-walking, estas operaciones dependerán
        // de un IGravityProfile que define el plano local de cada nodo.

        private static Vector3Int TurnLeft(Vector3Int d)  => new(-d.z,  0,  d.x);
        private static Vector3Int TurnRight(Vector3Int d) => new( d.z,  0, -d.x);

        // ── Conversión Dirección ↔ Vector ─────────────────────

        public static MoveDirection VectorToDir(Vector3Int v)
        {
            if (v.z > 0) return MoveDirection.North;
            if (v.z < 0) return MoveDirection.South;
            if (v.x > 0) return MoveDirection.East;
            return MoveDirection.West;
        }

        public static Vector3Int DirToVector(MoveDirection d) => d switch
        {
            MoveDirection.North => new Vector3Int( 0, 0,  1),
            MoveDirection.South => new Vector3Int( 0, 0, -1),
            MoveDirection.East  => new Vector3Int( 1, 0,  0),
            MoveDirection.West  => new Vector3Int(-1, 0,  0),
            _                   => new Vector3Int( 0, 0,  1)
        };
    }
}
