// ============================================================
// TileDetector.cs  |  Assets/Scripts/Player/
//
// RESPONSABILIDAD ÚNICA:
//   Detectar cuándo el droide cruza a una nueva coordenada de grid
//   y emitir el evento OnTileEntered(TileComponent).
//
// PRINCIPIO SRP:
//   DroideController ya no lee transform.position para determinar
//   el tile actual. Esa responsabilidad pertenece aquí.
//   DroideController se suscribe a OnTileEntered y reacciona.
//
// DEPENDENCIAS:
//   - ProceduralGridGenerator: consultado para resolver coordenada → TileComponent.
//   - Transform del droide: leído cada frame para calcular la coordenada.
//     TileDetector se adjunta al mismo GameObject que DroideController.
//
// CICLO:
//   Tick() es llamado por DroideController en Update(), después de
//   ApplyVelocity(). No es un MonoBehaviour independiente con Update()
//   propio para garantizar el orden de ejecución sin depender de
//   Script Execution Order settings.
//
// ESCALABILIDAD — FASE 4 (wall-walking):
//   WorldToCoord() hoy fija Y = 0. Cuando se active wall-walking,
//   WorldToCoord() deberá considerar la Y real del tile.
//   El resto del contrato (OnTileEntered, Tick()) no cambia.
//
// EVENTO DE MUERTE POR CAÍDA:
//   Si la coordenada calculada no existe en el TileMap, TileDetector
//   emite OnTileMissed(). DroideController reacciona llamando
//   TriggerDeath(DeathCause.Fall). TileDetector no sabe qué es
//   "morir" — solo notifica que el tile no existe.
// ============================================================
using System;
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Player
{
    public class TileDetector
    {
        // ── Dependencias ──────────────────────────────────────
        private readonly Transform              _droideTransform;
        private readonly ProceduralGridGenerator _generator;

        // ── Estado interno ────────────────────────────────────
        private Vector3Int _lastProcessedCoord;
        private Vector3Int _previousCoord;

        // ── Eventos ───────────────────────────────────────────

        /// <summary>
        /// Emitido al cruzar a un tile existente en el TileMap.
        /// Argumentos: (tile actual, coordenada anterior).
        /// DroideController usa 'coordenada anterior' para anti-loop.
        /// </summary>
        public event Action<TileComponent, Vector3Int> OnTileEntered;

        /// <summary>
        /// Emitido cuando la coordenada calculada no existe en el TileMap.
        /// DroideController debe responder con TriggerDeath(DeathCause.Fall).
        /// </summary>
        public event Action OnTileMissed;

        // ── Propiedad de consulta ─────────────────────────────

        /// <summary>Última coordenada procesada con éxito (tile existente).</summary>
        public Vector3Int LastProcessedCoord => _lastProcessedCoord;

        /// <summary>Coordenada anterior al último tile procesado.</summary>
        public Vector3Int PreviousCoord => _previousCoord;

        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="droideTransform">Transform del droide (mismo GameObject).</param>
        /// <param name="generator">Generador del grid: fuente de GetTile().</param>
        /// <param name="initialCoord">Coordenada inicial del droide tras Init().</param>
        public TileDetector(Transform droideTransform,
                            ProceduralGridGenerator generator,
                            Vector3Int initialCoord)
        {
            _droideTransform    = droideTransform;
            _generator          = generator;
            _lastProcessedCoord = initialCoord;
            _previousCoord      = initialCoord - new Vector3Int(0, 0, 1);
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Reinicia el estado interno al inicializar un nuevo nivel.
        /// Llamar desde DroideController.Init() después de establecer GridCoord.
        /// </summary>
        /// <param name="initialCoord">Coordenada de partida del droide.</param>
        /// <param name="currentDirection">
        ///   Dirección actual del droide. Se usa para calcular _previousCoord
        ///   como coord - dir, garantizando que el anti-loop funcione correctamente
        ///   desde el primer frame (ej. al regresar de un portal en dirección Este).
        ///   Si no se pasa, se asume Norte (0,0,1) — correcto para nivel nuevo.
        /// </param>
        public void Reset(Vector3Int initialCoord, Vector3Int? currentDirection = null)
        {
            _lastProcessedCoord = initialCoord;
            var dir             = currentDirection ?? new Vector3Int(0, 0, 1);
            _previousCoord      = initialCoord - dir;
        }

        /// <summary>
        /// Evalúa la posición actual del droide.
        /// Llamar desde DroideController.Update() después de ApplyVelocity().
        ///
        /// Si la coordenada cambió:
        ///   - Si el tile existe → emite OnTileEntered(tile, previousCoord).
        ///   - Si el tile no existe → emite OnTileMissed().
        /// Si la coordenada no cambió → no hace nada.
        /// </summary>
        public void Tick()
        {
            Vector3Int current = WorldToCoord(_droideTransform.position);
            if (current == _lastProcessedCoord) return;

            var tile = _generator.GetTile(current);
            if (tile == null)
            {
                OnTileMissed?.Invoke();
                return;
            }

            var previousBeforeUpdate = _lastProcessedCoord;
            _previousCoord      = previousBeforeUpdate;
            _lastProcessedCoord = current;

            OnTileEntered?.Invoke(tile, previousBeforeUpdate);
        }

        // ── Conversión de coordenadas ─────────────────────────

        /// <summary>
        /// Convierte posición world a coordenada de grid.
        ///
        /// HOY: Y = 0 siempre (plano XZ).
        /// FASE 4 (wall-walking): considerar Y real cuando el droide
        /// pueda caminar en paredes o techos.
        /// </summary>
        private static Vector3Int WorldToCoord(Vector3 world) =>
            new(Mathf.RoundToInt(world.x), 0, Mathf.RoundToInt(world.z));
    }
}
