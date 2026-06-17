// ============================================================
// DroidRestoreService.cs  |  Assets/Scripts/Core/
//
// RESPONSABILIDAD ÚNICA:
//   Restaurar el estado del Droide después de que el jugador
//   completa (o falla) el minijuego de portal.
//
// LO QUE NO HACE (SRP):
//   No carga ni descarga escenas.
//   No pausa ni reanuda el juego — eso lo hace GameFlowManager.
//   No sabe cuándo llamarse — GameFlowManager lo ordena.
//
// ELIMINA los FindObjectOfType<DroideCore>() que existían en:
//   - GameFlowManager.UnloadMiniGameAndRestore()
//   - GameFlowManager.OnGridReadyAfterPortalReturn()
//   - GameFlowManager.UnloadMiniGameAndShowGameOver()
//
// RECIBE DroideCore e ProceduralGridGenerator por constructor
// (inyectados por GameFlowManager desde el Inspector o AutoConnect).
// Si DroideCore es null en construcción (raro pero posible en
// escenas sin droide), RestoreAfterPortal() lo detecta y loguea.
//
// IMPLEMENTA IDroidRestoreService (DIP).
// ============================================================
using Celeris.Grid;
using Celeris.Player;
using UnityEngine;

namespace Celeris.Core
{
    public class DroidRestoreService : IDroidRestoreService
    {
        // ── Dependencias ──────────────────────────────────────
        private readonly DroideCore              _droide;
        private readonly ProceduralGridGenerator _generator;

        public DroidRestoreService(DroideCore droide, ProceduralGridGenerator generator)
        {
            _droide    = droide;
            _generator = generator;
        }

        // ── IDroidRestoreService ──────────────────────────────

        /// <summary>
        /// 1. Consume el estado de retorno de portal de GameStateManager.
        /// 2. Marca el PortalTile como completado (visual sombreado).
        /// 3. Llama DroideCore.RestoreFromPortal() con coord y dirección.
        ///
        /// Devuelve false si no había estado pendiente — no-op seguro,
        /// GameFlowManager puede llamarlo sin guard adicional.
        /// </summary>
        public bool RestoreAfterPortal()
        {
            if (GameStateManager.Instance == null)
            {
                Debug.LogError("[DroidRestoreService] GameStateManager.Instance es null. " +
                               "¿Falta el singleton en la escena?");
                return false;
            }

            if (!GameStateManager.Instance.ConsumePortalReturn(
                    out Vector3Int coord,
                    out Vector3Int direction,
                    out Vector3Int portalTileCoord))
            {
                Debug.LogWarning("[DroidRestoreService] RestoreAfterPortal() llamado " +
                                 "pero no hay estado de portal pendiente. No-op.");
                return false;
            }

            // ── Marcar portal completado ──────────────────────
            if (_generator != null)
            {
                var portalTile = _generator.GetTile(portalTileCoord);
                if (portalTile != null)
                    portalTile.GetComponent<PortalTileComponent>()?.MarkCompleted();
                else
                    Debug.LogWarning($"[DroidRestoreService] No se encontró tile en " +
                                     $"portalTileCoord={portalTileCoord}.");
            }

            // ── Restaurar Droide ──────────────────────────────
            if (_droide == null)
            {
                Debug.LogError("[DroidRestoreService] DroideCore es null. " +
                               "El Droide no será restaurado.");
                return false;
            }

            _droide.RestoreFromPortal(coord, direction);
            Debug.Log($"[DroidRestoreService] Droide restaurado en coord={coord} dir={direction}.");
            return true;
        }
    }
}
