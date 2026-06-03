// ============================================================
// GameFlowManager.cs  |  Assets/Scripts/Core/
//
// SETUP EN GameplayScene:
//   Adjuntar a cualquier GameObject (ej. "GameManager").
//   Las referencias se buscan automáticamente si se dejan vacías.
//
// FLUJO v4:
//
//   VICTORIA:
//     DroideState.Victory → espera victoryDelay → LevelManager.AdvanceLevel()
//
//   PORTAL (Carga Aditiva):
//     DroideController.OnPortalEntered
//       → GameStateManager.Pause() + SetPortalReturn()
//       → SceneManager.LoadScene("MiniGameScene", Additive)
//         (GameplayScene permanece cargada: tiles visibles)
//       → MiniGameSimulator llama CompleteMinigame()
//       → GameFlowManager.OnPortalComplete()
//           → SceneManager.UnloadSceneAsync("MiniGameScene")
//           → PortalTileComponent.MarkCompleted() (tile sombreado)
//           → DroideController.RestoreFromPortal()
//           → GameStateManager.Resume()
//
//   GAME OVER:
//     GameOverController escucha DroideState.Dead directamente.
//     No necesita intervención de GameFlowManager.
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
        private bool _portalSceneLoaded = false;

        // ── Awake ─────────────────────────────────────────────
        private void Awake()
        {
            EnsureSingletons();
            AutoConnect();
            InjectLevelConfig();

            // ¿Estamos restaurando desde portal? (por si el nivel se recargó)
            if (GameStateManager.Instance.HasPortalReturn)
                generator.OnGridReady += OnGridReadyAfterPortalReturn;
        }

        private void OnEnable()
        {
            if (droide != null) Subscribe();
        }

        private void OnDisable()
        {
            if (droide != null) Unsubscribe();
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

        // ── Suscripciones ─────────────────────────────────────
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

            // Carga ADITIVA: GameplayScene permanece cargada con todos los tiles
            _portalSceneLoaded = true;
            StartCoroutine(LoadMiniGameAdditive());
        }

        private IEnumerator LoadMiniGameAdditive()
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(miniGameSceneName, LoadSceneMode.Additive);
            yield return op;

            // Hacer activa la escena del minijuego para que su UI quede al frente
            Scene miniScene = SceneManager.GetSceneByName(miniGameSceneName);
            if (miniScene.IsValid())
                SceneManager.SetActiveScene(miniScene);

            Debug.Log($"[GameFlowManager] {miniGameSceneName} cargada de forma aditiva.");
        }

        // ── Portal: retorno (llamado por MiniGameSimulator) ───

        /// <summary>
        /// Llamar desde MiniGameSimulator.CompleteMinigame() cuando la ruta es portal.
        /// Descarga el minijuego, marca el portal y restaura el Droide.
        /// </summary>
        public void OnPortalComplete()
        {
            StartCoroutine(UnloadMiniGameAndRestore());
        }

        private IEnumerator UnloadMiniGameAndRestore()
        {
            // Restaurar escena de juego como activa
            Scene gameplayScene = SceneManager.GetSceneByName("GameplayScene");
            if (gameplayScene.IsValid())
                SceneManager.SetActiveScene(gameplayScene);

            // Descargar minijuego
            AsyncOperation op = SceneManager.UnloadSceneAsync(miniGameSceneName);
            yield return op;
            _portalSceneLoaded = false;

            // Consumir estado de retorno
            if (!GameStateManager.Instance.ConsumePortalReturn(
                    out Vector2Int coord,
                    out Vector2Int direction,
                    out Vector2Int portalTileCoord))
            {
                GameStateManager.Instance.Resume();
                yield break;
            }

            // Marcar portal como completado (color sombreado)
            var portalTile = generator?.GetTile(portalTileCoord);
            if (portalTile != null)
            {
                var portalComp = portalTile.GetComponent<PortalTileComponent>();
                portalComp?.MarkCompleted();
            }

            // Restaurar Droide en la misma posición
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
