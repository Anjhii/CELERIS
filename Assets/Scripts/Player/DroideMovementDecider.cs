// ============================================================
// DroideMovementDecider.cs  |  Assets/Scripts/Player/
//
// RESPONSABILIDAD ÚNICA:
//   Calcular la posición y rotación destino del Droide en el grid
//   y encolarlo en DroideCore para que FixedUpdate lo aplique.
//
// LO QUE NO HACE (SRP):
//   No gestiona física directamente (no llama MovePosition/MoveRotation).
//   No gestiona estados de movimiento.
//   No gestiona batería.
//   No gestiona portales.
//
// CONTRATO CON DroideCore:
//   ApplyArrowDirection() escribe via EnqueuePosition/EnqueueRotation.
//   DroideCore llama FixedUpdate que aplica los valores encolados.
//   Es el ÚNICO lugar del proyecto que lee _tileDetector.PreviousCoord.
//
// SNAP LATERAL:
//   SnapToTilePlane() es la ÚNICA función que asume el plano XZ.
//   Para wall-walking (Fase 5): reimplementar usando el plano local
//   del tile sin cambiar la firma ni el punto de llamada.
// ============================================================
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Player
{
    public class DroideMovementDecider
    {
        private const float SNAP_CORRECTION_THRESHOLD = 0.1f;

        // ── Dependencias ──────────────────────────────────────
        private readonly DroideCore _core;

        public DroideMovementDecider(DroideCore core)
        {
            _core = core;
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Aplica la dirección de un ArrowTile al Droide.
        /// Calcula snap lateral, actualiza dirección lógica y encola
        /// posición + rotación en DroideCore para FixedUpdate.
        /// </summary>
        /// <param name="tile">Tile flecha que se acaba de pisar.</param>
        /// <param name="previousCoord">Coordenada anterior (para anti-loop).</param>
        /// <param name="fromPlayerRotation">
        ///   true cuando el jugador rotó manualmente la flecha;
        ///   false cuando el Droide llegó al tile naturalmente.
        /// </param>
        public void ApplyArrowDirection(TileComponent tile, Vector3Int previousCoord,
                                        bool fromPlayerRotation = false)
        {
            Vector3Int d    = TileComponent.DirectionToVector(tile.arrowDirection);
            Vector3    dir3 = new Vector3(d.x, 0f, d.z).normalized;

            // ── Anti-loop (solo para flechas del camino) ──────
            if (!fromPlayerRotation && _core.GridCoord + d == previousCoord)
            {
                Debug.Log($"[ANTI-LOOP] Bloqueado: arrow en {_core.GridCoord} apunta " +
                          $"a {_core.GridCoord + d} = prevCoord={previousCoord}. " +
                          $"Se ignora la flecha, dir={_core.Direction}");
                return;
            }

            Debug.Log($"[ARROW] d={d} tile={tile.gridCoord} " +
                      $"prev={previousCoord} dir_old={_core.Direction} " +
                      $"playerRot={fromPlayerRotation}");

            // ── Snap al centro del tile ───────────────────────
            Vector3 snapped = SnapToTilePlane(tile, dir3);
            _core.EnqueuePosition(snapped);

            // ── Dirección lógica + rotación física ───────────
            _core.SetDirection(d);
            var targetRot = Quaternion.LookRotation(dir3, Vector3.up);
            _core.transform.rotation = targetRot;   // visual inmediato
            _core.EnqueueRotation(targetRot);

            // ── Redirigir velocidad sin perder inercia ────────
            var rb = _core.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float spd = rb.velocity.magnitude;
                if (spd > 0.01f)
                    rb.velocity = dir3 * spd;
            }
        }

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Calcula la posición con snap al centro del tile en el plano XZ.
        ///
        /// Movimiento Norte/Sur: fija X siempre, corrige Z si hay deriva.
        /// Movimiento Este/Oeste: fija Z siempre, corrige X si hay deriva.
        ///
        /// Esta es la ÚNICA función que asume el plano XZ.
        /// Fase 5 (wall-walking): reimplementar con normal local del tile.
        /// </summary>
        private Vector3 SnapToTilePlane(TileComponent tile, Vector3 direction)
        {
            Vector3 p         = _core.transform.position;
            float   tileX     = tile.gridCoord.x;
            float   tileZ     = tile.gridCoord.z;
            bool    movingOnZ = Mathf.Abs(direction.z) > Mathf.Abs(direction.x);

            if (movingOnZ)
            {
                p.x = tileX;
                if (Mathf.Abs(p.z - tileZ) > SNAP_CORRECTION_THRESHOLD)
                    p.z = tileZ;
            }
            else
            {
                p.z = tileZ;
                if (Mathf.Abs(p.x - tileX) > SNAP_CORRECTION_THRESHOLD)
                    p.x = tileX;
            }

            return p;
        }
    }
}
