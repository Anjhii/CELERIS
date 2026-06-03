// ============================================================
// GameFlowManager.cs  |  Assets/Scripts/Core/
//
// SETUP EN GameplayScene:
//   Adjuntar a cualquier GameObject (ej. "GameManager").
//   Las referencias se buscan automáticamente si se dejan vacías.
//
// FLUJO v5:
//
//   VICTORIA:
//     DroideState.Victory → espera victoryDelay → LevelManager.AdvanceLevel()
//
//   PORTAL (Carga Aditiva):
//     DroideController.OnPortalEntered
//       → GameStateManager.Pause() + SetPortalReturn()
//       → SceneManager.LoadScene("MiniGameScene", Additive)
//
//     Cuando el minijuego termina (dos caminos posibles):
//       A) TerminalHackManager.OnTerminalExited (minijuego real)
//            → GameFlowManager.OnTerminalHackExited()
//            → OnPortalComplete()
//       B) MiniGameSimulator.CompleteMinigame() (simulador temporal)
//            → GameFlowManager.OnPortalComplete() directamente
//
//     OnPortalComplete() (con guard _portalSceneLoaded):
//       → SceneManager.UnloadSceneAsync("MiniGameScene")
//       → PortalTileComponent.MarkCompleted()
//       → DroideController.RestoreFromPortal()   ← único punto de restauración
//       → GameStateManager.Resume()
//
// CAMBIOS v5:
//   • Suscripción a TerminalHackManager.OnTerminalExited para integrar
//     el minijuego real sin double-unload ni double-restore.
//   • Guard en OnPortalComplete(): _portalSceneLoaded previene doble llamada
//     tanto desde MiniGameSimulator como desde TerminalHackManager.
//   • DroideController ya NO suscribe a OnTerminalExited.
// ============================================================
using System.Collections;
using Celeris.Data;
using Celeris.Grid;
using Celeris.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Celeris.Core
{
    public class GameFlowManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Referencias (auto-detectadas si están vacías)")]
        public ProceduralGridGenerator generator;
        public DroideController        droide;

        [Header("Timing")]
        public float victoryDelay = 1.2f;

        [Header("Escenas")]
        [Tooltip("Nombre exacto de la escena de minijuego")]
        public string miniGameSceneName = "MiniGameScene";

        // ── Estado de portal ──────────────────────────────────
        // Guard central: previene double-unload y double-restore.
        // Se setea true al cargar MiniGameScene, false al descargarla.
        private bool _portalSceneLoaded = false;

        // ── Awake ─────────────────────────────────────────────
        private void Awake()
        {
            EnsureSingletons();
            AutoConnect();
            InjectLevelConfig();

            if (GameStateManager.Instance.HasPortalReturn)
                generator.OnGridReady += OnGridReadyAfterPortalReturn;
        }

        private void OnEnable()
        {
            if (droide != null) Subscribe();

            // Suscribir al evento de salida del minijuego real (TerminalHackManager).
            // Este es el punto central de integración: cuando TerminalHackManager
            // termina con éxito, notifica aquí y GameFlowManager orquesta todo.
            TerminalHackManager.OnTerminalExited += OnTerminalHackExited;

            // Suscribir al game over del minijuego: descarga la escena y muestra el panel.
            TerminalHackManager.OnHackGameOver += OnHackGameOverReceived;
        }

        private void OnDisable()
        {
            if (droide != null) Unsubscribe();
            TerminalHackManager.OnTerminalExited -= OnTerminalHackExited;
            TerminalHackManager.OnHackGameOver   -= OnHackGameOverReceived;
        }

        private void Start()
        {
            if (droide == null)
            {
                droide = FindObjectOfType<DroideController>();
                if (droide != null) Subscribe();
                else Debug.LogError("[GameFlowManager] DroideController no encontrado.");
            }
        }

        // ── Suscripciones al Droide ───────────────────────────
        private void Subscribe()
        {
            droide.OnStateChanged  += HandleStateChanged;
            droide.OnPortalEntered += HandlePortalEntered;
        }

        private void Unsubscribe()
        {
            droide.OnStateChanged  -= HandleStateChanged;
            droide.OnPortalEntered -= HandlePortalEntered;
        }

        // ── Victoria ──────────────────────────────────────────
        private void HandleStateChanged(DroideState state)
        {
            if (state == DroideState.Victory)
                StartCoroutine(VictorySequence());
        }

        private IEnumerator VictorySequence()
        {
            Debug.Log($"[GameFlowManager] Victoria en nivel {LevelManager.CurrentLevelNumber}.");
            yield return new WaitForSeconds(victoryDelay);
            LevelManager.EnsureExists().AdvanceLevel();
        }

        // ── Portal: entrada ───────────────────────────────────
        private void HandlePortalEntered(TileComponent portalTile, Vector2Int droideDirection)
        {
            if (droide == null || _portalSceneLoaded) return;

            Vector2Int coord = droide.GridCoord;
            Debug.Log($"[GameFlowManager] Portal activado en {coord}.");

            GameStateManager.Instance.SetPortalReturn(coord, droideDirection, coord);
            GameStateManager.Instance.Pause();

            _portalSceneLoaded = true;
            StartCoroutine(LoadMiniGameAdditive());
        }

        private IEnumerator LoadMiniGameAdditive()
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(miniGameSceneName, LoadSceneMode.Additive);
            yield return op;

            Scene miniScene = SceneManager.GetSceneByName(miniGameSceneName);
            if (miniScene.IsValid())
                SceneManager.SetActiveScene(miniScene);

            Debug.Log($"[GameFlowManager] {miniGameSceneName} cargada de forma aditiva.");
        }

        // ── Portal: retorno desde TerminalHackManager (minijuego real) ──
        /// <summary>
        /// Recibe el evento de TerminalHackManager cuando el hack termina con éxito.
        /// Guard: solo actúa si la escena del minijuego está efectivamente cargada.
        /// </summary>
        private void OnTerminalHackExited()
        {
            if (!_portalSceneLoaded)
            {
                Debug.LogWarning("[GameFlowManager] OnTerminalHackExited recibido pero " +
                                 "no hay escena de portal activa. Ignorado.");
                return;
            }
            Debug.Log("[GameFlowManager] TerminalHackManager completado → iniciando retorno de portal.");
            OnPortalComplete();
        }

        // ── Portal: retorno desde MiniGameSimulator (simulador temporal) ─
        /// <summary>
        /// Llamar desde MiniGameSimulator.CompleteMinigame() cuando la ruta es portal.
        /// Guard _portalSceneLoaded previene doble ejecución si ambos caminos disparan.
        /// </summary>
        public void OnPortalComplete()
        {
            // Guard: si ya se procesó (por doble llamada desde Simulator + TerminalHack),
            // ignorar la segunda.
            if (!_portalSceneLoaded)
            {
                Debug.LogWarning("[GameFlowManager] OnPortalComplete() llamado sin portal activo. Ignorado.");
                return;
            }
            StartCoroutine(UnloadMiniGameAndRestore());
        }

        private IEnumerator UnloadMiniGameAndRestore()
        {
            // Marcar como no cargado INMEDIATAMENTE para que cualquier llamada
            // concurrente sea descartada por el guard antes de que la corrutina termine.
            _portalSceneLoaded = false;

            // Restaurar escena de juego como activa
            Scene gameplayScene = SceneManager.GetSceneByName("GameplayScene");
            if (gameplayScene.IsValid())
                SceneManager.SetActiveScene(gameplayScene);

            // Descargar minijuego (único punto de descarga)
            AsyncOperation op = SceneManager.UnloadSceneAsync(miniGameSceneName);
            yield return op;

            // Consumir estado de retorno
            if (!GameStateManager.Instance.ConsumePortalReturn(
                    out Vector2Int coord,
                    out Vector2Int direction,
                    out Vector2Int portalTileCoord))
            {
                GameStateManager.Instance.Resume();
                yield break;
            }

            // Marcar portal como completado (visual sombreado)
            var portalTile = generator?.GetTile(portalTileCoord);
            if (portalTile != null)
            {
                var portalComp = portalTile.GetComponent<PortalTileComponent>();
                portalComp?.MarkCompleted();
            }

            // Restaurar Droide — ÚNICO punto de llamada a RestoreFromPortal().
            // DroideController ya no suscribe a OnTerminalExited, así que no
            // hay double-restore posible.
            if (droide == null) droide = FindObjectOfType<DroideController>();
            droide?.RestoreFromPortal(coord, direction);

            // Reanudar
            GameStateManager.Instance.Resume();
            Debug.Log($"[GameFlowManager] Portal completado. Droide en {coord}.");
        }

        // ── Restauración post-carga de escena (fallback) ──────
        private void OnGridReadyAfterPortalReturn()
        {
            generator.OnGridReady -= OnGridReadyAfterPortalReturn;

            if (!GameStateManager.Instance.ConsumePortalReturn(
                    out Vector2Int coord,
                    out Vector2Int direction,
                    out Vector2Int portalCoord)) return;

            var portalTile = generator.GetTile(portalCoord);
            portalTile?.GetComponent<PortalTileComponent>()?.MarkCompleted();

            if (droide == null) droide = FindObjectOfType<DroideController>();
            droide?.RestoreFromPortal(coord, direction);
            GameStateManager.Instance.Resume();
        }

        // ── Game Over del minijuego ───────────────────────────
        /// <summary>
        /// Recibe OnHackGameOver de TerminalHackManager (3 intentos agotados).
        /// Descarga MiniGameScene y muestra el panel de Game Over en-juego.
        /// </summary>
        private void OnHackGameOverReceived()
        {
            StartCoroutine(UnloadMiniGameAndShowGameOver());
        }

        private IEnumerator UnloadMiniGameAndShowGameOver()
        {
            // Limpiar estado de portal igual que en flujo normal
            _portalSceneLoaded = false;

            Scene gameplayScene = SceneManager.GetSceneByName("GameplayScene");
            if (gameplayScene.IsValid())
                SceneManager.SetActiveScene(gameplayScene);

            AsyncOperation op = SceneManager.UnloadSceneAsync(miniGameSceneName);
            if (op != null) yield return op;

            // Reanudar estado de pausa antes de delegar la muerte al droide
            GameStateManager.Instance.Resume();

            // Delegar el game over al DroideController: ForceKill dispara OnStateChanged(Dead),
            // que GameOverController escucha para mostrar el panel automáticamente.
            if (droide == null) droide = FindObjectOfType<DroideController>();
            droide?.ForceKill(Celeris.Data.DeathCause.Generic);

            Debug.Log("[GameFlowManager] Hack Game Over — ForceKill(Generic) disparado.");
        }

        // ── Helpers ───────────────────────────────────────────
        private void EnsureSingletons()
        {
            LevelManager.EnsureExists();
            if (GameStateManager.Instance == null)
            {
                var go = new GameObject("[GameStateManager-Auto]");
                go.AddComponent<GameStateManager>();
            }
        }

        private void AutoConnect()
        {
            if (generator == null) generator = FindObjectOfType<ProceduralGridGenerator>();
            if (droide    == null) droide    = FindObjectOfType<DroideController>();
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
