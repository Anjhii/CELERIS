// ============================================================
// DroidePortalHandler.cs  |  Assets/Scripts/Player/
//
// RESPONSABILIDAD ÚNICA:
//   Escuchar TileDetector.OnTileEntered para detectar PortalTiles.
//   Delegar la restauración post-portal a DroideCore.
//   Resetear TileDetector cuando se reanuda el nivel.
//
// LO QUE NO HACE (SRP):
//   No gestiona física ni estado del Droide.
//   No carga ni descarga escenas (→ GameFlowManager).
//   No sabe qué es un minijuego.
//
// COMUNICACIÓN:
//   TileDetector.OnTileEntered → HandleTileEntered (privado)
//   → DroideCore.OnPortalEntered (si el tile es PortalTile no completado)
//   GameFlowManager llama RestoreFromPortal() cuando el minijuego termina
//   → DroideCore.RestoreFromPortal()
//   → TileDetector.Reset()
// ============================================================
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Player
{
    public class DroidePortalHandler
    {
        // ── Dependencias ──────────────────────────────────────
        private readonly DroideCore _core;
        private TileDetector        _tileDetector;

        public DroidePortalHandler(DroideCore core)
        {
            _core = core;
        }

        // ── API de inyección (llamada por DroideCore.Init) ───

        /// <summary>
        /// Inyecta el TileDetector y suscribe el manejador de tile.
        /// Solo se llama una vez: la primera vez que Init() crea el detector.
        /// </summary>
        public void SetTileDetector(TileDetector detector)
        {
            if (_tileDetector != null)
                _tileDetector.OnTileEntered -= HandleTileEntered;

            _tileDetector = detector;
            _tileDetector.OnTileEntered += HandleTileEntered;
        }

        // ── API pública — llamada por GameFlowManager ─────────

        /// <summary>
        /// Restaura la posición y estado del Droide tras volver del minijuego.
        /// GameFlowManager llama esto como único punto de restauración post-portal.
        /// </summary>
        public void RestoreFromPortal(Vector3Int coord, Vector3Int direction)
        {
            _core.RestoreFromPortal(coord, direction);
        }

        // ── Manejador de tile ─────────────────────────────────

        private void HandleTileEntered(TileComponent tile, Vector3Int previousCoord)
        {
            // Solo actuar sobre PortalTiles no completados.
            // La lógica de cambio de estado (AtPortal) y el evento OnPortalEntered
            // ya los dispara DroideCore en ProcessTileEffect. Este handler
            // solo necesita existir para el path de Reset() post-portal.
            // La lógica de portal en tiles ya está en DroideCore.ProcessTileEffect.
        }
    }
}
