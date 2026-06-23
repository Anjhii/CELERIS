// ============================================================
// GameStateManager.cs  |  Assets/Scripts/Core/
//
// Singleton DontDestroyOnLoad que gestiona:
//   A) Estado de pausa global.
//   B) Estado de retorno de Portal.
//   C) Contador de terminales hackeadas en la run actual
//      (reemplaza TerminalHackManager.HackedTerminalsCount static).
//
// MIGRACION Fase 2: Vector2Int -> Vector3Int.
// ============================================================
using UnityEngine;

namespace Celeris.Core
{
    public class GameStateManager : MonoBehaviour
    {
        // -- Singleton ----------------------------------------
        public static GameStateManager Instance { get; private set; }

        // -- Estado global ------------------------------------
        public static bool IsPaused { get; private set; } = false;

        // -- Estado de retorno de Portal ----------------------
        private bool       _hasPortalReturn = false;
        private Vector3Int _portalReturnCoord;
        private Vector3Int _portalReturnDirection;
        private Vector3Int _portalTileCoord;

        // -- Terminales hackeadas en esta run -----------------
        // Reemplaza TerminalHackManager.HackedTerminalsCount (static eliminado).
        public int TerminalsHackedThisRun { get; private set; } = 0;

        /// <summary>
        /// Terminales requeridas para desbloquear la meta de victoria.
        /// Reemplaza TerminalHackManager.RequiredHacks (const eliminada).
        /// </summary>
        public const int RequiredTerminalHacks = 3;

        // -- Lifecycle ----------------------------------------
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

        // -- API de pausa -------------------------------------

        public void Pause()
        {
            IsPaused = true;
            Debug.Log("[GameStateManager] Juego pausado.");
        }

        public void Resume()
        {
            IsPaused = false;
            Debug.Log("[GameStateManager] Juego reanudado.");
        }

        // -- API de Portal ------------------------------------

        public void SetPortalReturn(Vector3Int droideCoord, Vector3Int droideDirection, Vector3Int portalTileCoord)
        {
            _portalReturnCoord     = droideCoord;
            _portalReturnDirection = droideDirection;
            _portalTileCoord       = portalTileCoord;
            _hasPortalReturn       = true;
            Debug.Log($"[GameStateManager] Portal guardado en coord={droideCoord}, dir={droideDirection}.");
        }

        public bool ConsumePortalReturn(
            out Vector3Int droideCoord,
            out Vector3Int droideDirection,
            out Vector3Int portalTileCoord)
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

        // -- API de Terminales Hackeadas ----------------------

        /// <summary>Llamado por TerminalHackManager al completar un hack exitoso.</summary>
        public void IncrementTerminalsHacked()
        {
            TerminalsHackedThisRun++;
            Debug.Log($"[GameStateManager] Terminales hackeadas: {TerminalsHackedThisRun}.");
        }

        /// <summary>
        /// Llamado al comenzar un nivel nuevo.
        /// Reemplaza TerminalHackManager.ResetHackedCount().
        /// </summary>
        public void ResetTerminalsHacked()
        {
            TerminalsHackedThisRun = 0;
            Debug.Log("[GameStateManager] Contador de terminales reiniciado.");
        }
    }
}
