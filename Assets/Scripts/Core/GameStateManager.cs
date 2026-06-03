// ============================================================
// GameStateManager.cs  |  Assets/Scripts/Core/
//
// Singleton DontDestroyOnLoad que gestiona:
//   A) Estado de pausa global (pausa el juego sin contaminar
//      a DroideController ni al generador).
//   B) Estado de retorno de Portal: guarda la coordenada y
//      dirección del Droide cuando entró al Portal, para que
//      GameFlowManager lo restaure al volver del minijuego.
//
// SETUP: Añadir a LoginScene junto a LevelManager.
//
// USO TÍPICO:
//   GameStateManager.IsPaused           → bool (chequear en Update)
//   GameStateManager.Instance.Pause()   → pausa
//   GameStateManager.Instance.Resume()  → reanuda
//   GameStateManager.Instance.SetPortalReturn(coord, dir)
//   GameStateManager.Instance.ConsumePortalReturn(out coord, out dir) → bool
// ============================================================
using UnityEngine;

namespace Celeris.Core
{
    public class GameStateManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static GameStateManager Instance { get; private set; }

        // ── Estado global ─────────────────────────────────────
        public static bool IsPaused { get; private set; } = false;

        // ── Estado de retorno de Portal ───────────────────────
        private bool              _hasPortalReturn = false;
        private Vector2Int        _portalReturnCoord;
        private Vector2Int        _portalReturnDirection;
        private Vector2Int        _portalTileCoord;   // para marcar el tile completado

        // ── Lifecycle ─────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            IsPaused = false;
        }

        // ── API de pausa ──────────────────────────────────────

        /// <summary>Pausa el movimiento del Droide y el estado del juego.</summary>
        public void Pause()
        {
            IsPaused = true;
            Debug.Log("[GameStateManager] Juego pausado.");
        }

        /// <summary>Reanuda el juego.</summary>
        public void Resume()
        {
            IsPaused = false;
            Debug.Log("[GameStateManager] Juego reanudado.");
        }

        // ── API de Portal ─────────────────────────────────────

        /// <summary>
        /// Guarda la información de retorno antes de cargar el minijuego.
        /// GameFlowManager llama esto cuando el Droide entra a un Portal.
        /// </summary>
        public void SetPortalReturn(Vector2Int droideCoord, Vector2Int droideDirection, Vector2Int portalTileCoord)
        {
            _portalReturnCoord     = droideCoord;
            _portalReturnDirection = droideDirection;
            _portalTileCoord       = portalTileCoord;
            _hasPortalReturn       = true;
            Debug.Log($"[GameStateManager] Portal guardado en coord={droideCoord}, dir={droideDirection}.");
        }

        /// <summary>
        /// Lee y limpia el estado de retorno de Portal.
        /// Devuelve true si había un retorno pendiente.
        /// </summary>
        public bool ConsumePortalReturn(
            out Vector2Int droideCoord,
            out Vector2Int droideDirection,
            out Vector2Int portalTileCoord)
        {
            droideCoord     = _portalReturnCoord;
            droideDirection = _portalReturnDirection;
            portalTileCoord = _portalTileCoord;

            if (!_hasPortalReturn)
                return false;

            _hasPortalReturn = false;
            return true;
        }

        public bool HasPortalReturn => _hasPortalReturn;
    }
}
