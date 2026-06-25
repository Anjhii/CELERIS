// ============================================================
// GameFlowManager.cs  |  Assets/Scripts/Core/  — v6 SOLID
//
// RESPONSABILIDAD ÚNICA:
//   Orquestar el flujo de juego: escucha eventos del Droide y del
//   minijuego, y delega en servicios especializados.
// LO QUE NO HACE (SRP):
//   No carga ni descarga escenas   → IMiniGameSceneService
//   No restaura el Droide          → IDroidRestoreService
//   No presenta Game Over UI       → GameOverTrigger + GameOverPresenter
//   No llama FindObjectOfType<>    → dependencias inyectadas o auto-detectadas en Awake
// FLUJO v6:
//   VICTORIA:
//     DroideCore.OnStateChanged(Victory)
//       → VictorySequence() → ScoreManager.RecordLevelResult()
//       → LevelManager.AdvanceLevel()
//   PORTAL (Carga Aditiva):
//     DroideCore.OnPortalEntered
//       → GameStateManager.SetPortalReturn() + Pause()
//       → _sceneService.Load()   [corrutina]
//     Retorno — dos caminos, un solo handler:
//       A) TerminalHackManager.OnTerminalExited  → OnPortalComplete()
//       B) MiniGameSimulator (desactivado)        → OnPortalComplete() [legacy]
//     OnPortalComplete():
//       → _sceneService.Unload() [corrutina]
//       → _restoreService.RestoreAfterPortal()
//       → GameStateManager.Resume()
//     Game Over del minijuego:
//       TerminalHackManager.OnHackGameOver
//       → DroideCore.ForceKill(Generic)
//         (GameOverTrigger escucha OnDied y muestra el panel)

using Celeris.Core;
using Celeris.Data;
using Celeris.Grid;
using Celeris.Leaderboard;
using Celeris.UI;
using System.Collections;
using UnityEngine;

namespace Celeris.Core
{
    public class GameFlowManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Referencias (auto-detectadas si están vacías)")]
        public ProceduralGridGenerator generator;
        public DroideCore              droide;
        [Header("Timing")]
        public float victoryDelay = 1.2f;
        [Header("Escenas")]
        [Tooltip("Nombre exacto de la escena de minijuego")]
        public string miniGameSceneName = "MiniGameScene";
        // ── Servicios (creados en Awake, interfaz DIP) ────────
        private IMiniGameSceneService _sceneService;
        private IDroidRestoreService  _restoreService;
        // ── F0-T3: guard contra doble suscripción a droide ────
        // OnEnable suscribe cuando droide != null.
        // Start solo suscribe si droide se encontró tarde (AutoConnect falló en Awake).
        // Sin este flag Start duplicaría las suscripciones de OnEnable.
        private bool _droideSubscribed = false;
        // ── Awake ─────────────────────────────────────────────
        private void Awake()
        {
            EnsureSingletons();
            AutoConnect();
            InjectLevelConfig();
            BuildServices();
            // Fallback: si venimos de una recarga de escena con retorno de portal
            // pendiente (raro, path de emergencia), esperar al grid.
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.HasPortalReturn)
                generator.OnGridReady += OnGridReadyFallback;
        }
        private void OnEnable()
        {
            if (droide != null && !_droideSubscribed)
            {
                droide.OnStateChanged  += HandleStateChanged;
                droide.OnPortalEntered += HandlePortalEntered;
                _droideSubscribed = true;
            }
            TerminalHackManager.OnTerminalExited += OnTerminalHackExited;
            TerminalHackManager.OnHackGameOver   += OnHackGameOverReceived;
        }
        private void OnDisable()
        {
            if (droide != null && _droideSubscribed)
            {
                droide.OnStateChanged  -= HandleStateChanged;
                droide.OnPortalEntered -= HandlePortalEntered;
                _droideSubscribed = false;
            }
            TerminalHackManager.OnTerminalExited -= OnTerminalHackExited;
            TerminalHackManager.OnHackGameOver   -= OnHackGameOverReceived;
        }
        private void Start()
        {
            // Fallback: si AutoConnect() falló en Awake (droide no estaba en escena aún),
            // intentar encontrarlo ahora. Solo suscribir si aún no está suscrito.
            if (droide == null)
            {
                droide = FindObjectOfType<DroideCore>();
                if (droide != null)
                {
                    if (!_droideSubscribed)
                    {
                        droide.OnStateChanged  += HandleStateChanged;
                        droide.OnPortalEntered += HandlePortalEntered;
                        _droideSubscribed = true;
                    }
                    RebuildRestoreService();
                }
                else
                    Debug.LogError("[GameFlowManager] DroideCore no encontrado en escena.");
            }
            ScoreManager.Instance?.ResetCurrentScore();
        }
        // ═════════════════════════════════════════════════════
        //  VICTORIA
        private void HandleStateChanged(DroideState state)
        {
            if (state == DroideState.Victory)
                StartCoroutine(VictorySequence());
        }
        private IEnumerator VictorySequence()
        {
            int levelIdx = LevelManager.CurrentLevelIndex;
            Debug.Log($"[GameFlowManager] Victoria en nivel {LevelManager.CurrentLevelNumber}.");
            var sm = ScoreManager.Instance;
            if (sm != null)
            {
                int finalScore = (int)sm.CurrentScore;
                int stars      = CalculateStars(finalScore);
                int battery    = droide != null ? droide.Battery : 0;
                sm.RecordLevelResult(new LevelResult
                {
                    levelIndex  = levelIdx,
                    score       = finalScore,
                    stars       = stars,
                    batteryLeft = battery,
                    isVictory   = true,
                });
                Debug.Log($"[GameFlowManager] Score nivel {levelIdx + 1}: {finalScore} pts, {stars}★");
            }
            yield return new WaitForSeconds(victoryDelay);
            LevelManager.EnsureExists().AdvanceLevel();
        }
        private static int CalculateStars(int score)
        {
            if (score >= 250) return 3;
            if (score >= 150) return 2;
            if (score >    0) return 1;
            return 0;
        }
        //  PORTAL — ENTRADA
        private void HandlePortalEntered(TileComponent portalTile, Vector3Int droideDirection)
        {
            if (droide == null || _sceneService.IsLoaded) return;
            Vector3Int coord = droide.GridCoord;
            Debug.Log($"[GameFlowManager] Portal activado en {coord}.");
            GameStateManager.Instance.SetPortalReturn(coord, droideDirection, coord);
            GameStateManager.Instance.Pause();
            StartCoroutine(_sceneService.Load());
        }
        //  PORTAL — RETORNO (éxito)
        /// <summary>
        /// Recibe OnTerminalExited de TerminalHackManager (hack exitoso).
        /// Guard: solo actúa si la escena del minijuego está cargada.
        /// </summary>
        private void OnTerminalHackExited()
        {
            if (!_sceneService.IsLoaded)
            {
                Debug.LogWarning("[GameFlowManager] OnTerminalHackExited recibido " +
                                 "sin portal activo. Ignorado.");
                return;
            }
            Debug.Log("[GameFlowManager] Hack exitoso → iniciando retorno de portal.");
            OnPortalComplete();
        }
        /// Punto de entrada para finalizar el flujo de portal con éxito.
        /// Llamable desde MiniGameSimulator (legacy, desactivado) si se reactiva.
        /// Guard delegado a _sceneService.IsLoaded.
        public void OnPortalComplete()
        {
            if (!_sceneService.IsLoaded)
            {
                Debug.LogWarning("[GameFlowManager] OnPortalComplete() sin portal activo. Ignorado.");
                return;
            }
            StartCoroutine(PortalCompleteSequence());
        }
        private IEnumerator PortalCompleteSequence()
        {
            yield return StartCoroutine(_sceneService.Unload());
            bool restored = _restoreService.RestoreAfterPortal();
            if (!restored)
                Debug.LogWarning("[GameFlowManager] RestoreAfterPortal() no restauró el Droide.");
            GameStateManager.Instance.Resume();
            Debug.Log("[GameFlowManager] Portal completado. Juego reanudado.");
        }
        //  PORTAL — RETORNO (fallo del minijuego)
        private void OnHackGameOverReceived()
        {
            StartCoroutine(HackGameOverSequence());
        }
        private IEnumerator HackGameOverSequence()
        {
            // Reanudar ANTES de ForceKill: DroideCore.Update() necesita
            // que IsPaused = false para procesar el estado Dead correctamente.
            droide?.ForceKill(DeathCause.Generic);
            // GameOverTrigger escucha DroideCore.OnDied y muestra el panel.
            Debug.Log("[GameFlowManager] Hack Game Over — ForceKill(Generic) disparado.");
            yield break;
        }
        //  FALLBACK: retorno por recarga de escena
        /// Path de emergencia: si la escena se recargó completamente
        /// (no carga aditiva) y había un retorno de portal pendiente,
        /// restaurar al Droide cuando el grid esté listo.
        private void OnGridReadyFallback()
        {
            generator.OnGridReady -= OnGridReadyFallback;
            bool restored = _restoreService.RestoreAfterPortal();
            if (restored)
                GameStateManager.Instance.Resume();
        }
        //  SETUP INTERNO
        private void BuildServices()
        {
            _sceneService   = new MiniGameSceneService(miniGameSceneName);
            _restoreService = new DroidRestoreService(droide, generator);
        }
        private void RebuildRestoreService()
        {
            // Llamado desde Start() si el Droide se encontró tarde (AutoConnect falló).
            _restoreService = new DroidRestoreService(droide, generator);
        }
        private void EnsureSingletons()
        {
            LevelManager.EnsureExists();
            if (GameStateManager.Instance == null)
            {
                var go = new GameObject("[GameStateManager-Auto]");
                go.AddComponent<GameStateManager>();
            }
            GameOverTrigger.EnsureExists();
        }
        private void AutoConnect()
        {
            if (generator == null) generator = FindObjectOfType<ProceduralGridGenerator>();
            if (droide    == null) droide    = FindObjectOfType<DroideCore>();
        }
        private void InjectLevelConfig()
        {
            if (generator == null) return;
            var config = LevelManager.EnsureExists().CurrentConfig;
            if (config != null)
            {
                generator.config = config;
                Debug.Log($"[GameFlowManager] Config '{config.name}' inyectada.");
            }
            else
            {
                Debug.LogError($"[GameFlowManager] Config para nivel " +
                               $"{LevelManager.CurrentLevelNumber} no encontrada.");
            }
        }
    }
}
