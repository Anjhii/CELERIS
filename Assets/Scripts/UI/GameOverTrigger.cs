// ============================================================
// GameOverTrigger.cs  |  Assets/Scripts/UI/
//
// RESPONSABILIDAD ÚNICA:
//   Detectar la muerte del Droide y ordenar a GameOverPresenter
//   que muestre el panel.
//
// LO QUE NO HACE (SRP):
//   No presenta UI — eso es responsabilidad de GameOverPresenter.
//   No gestiona lógica de reintentar ni navegar al menú.
//
// FIX DEL BUG sceneLoaded (GameOverController original):
//   El problema raíz era que GameOverController se suscribía a
//   SceneManager.sceneLoaded para hacer FindObjectOfType<DroideController>()
//   global después de cada carga, creando un ciclo frágil que fallaba
//   en cargas aditivas (MiniGameScene) porque el Droide seguía en la
//   escena anterior.
//
//   SOLUCIÓN:
//     GameOverTrigger escucha DroideCore.OnDied directamente.
//     DroideCore es DontDestroyOnLoad (junto al prefab del jugador),
//     por lo que la suscripción persiste entre recargas de escena.
//     GameOverPresenter se localiza en la escena activa SOLO cuando
//     ocurre una muerte — no en cada carga.
//     El FindObjectsOfType se limita a la escena activa, no global.
//
// SETUP:
//   Instanciar en LoginScene junto a LevelManager y GameStateManager.
//   El prefab de GameOverPresenter debe existir en GameplayScene.
// ============================================================
using Celeris.Data;
using Celeris.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Celeris.UI
{
    public class GameOverTrigger : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static GameOverTrigger Instance { get; private set; }

        // ── Referencia al Droide (se actualiza en cada suscripción) ─
        private DroideCore _subscribedDroide;

        // ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            // Suscribir al Droide que ya esté en la escena.
            // Si el Droide también es DDOL (mismo prefab persistente),
            // esta suscripción persiste entre recargas sin necesidad
            // de re-suscribirse en sceneLoaded.
            SubscribeToDroide();

            // Escuchar cargas de escena SOLO para re-suscribir si el Droide
            // es destruido y recreado (escena no-DDOL).
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnsubscribeFromDroide();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ── Suscripción al Droide ─────────────────────────────

        private void SubscribeToDroide()
        {
            // Si el Droide actual ya está suscrito, no hacer nada.
            var droide = FindDroideInActiveScene();
            if (droide == null || droide == _subscribedDroide) return;

            UnsubscribeFromDroide();
            _subscribedDroide        = droide;
            _subscribedDroide.OnDied += HandleDroideDied;

            Debug.Log($"[GameOverTrigger] Suscrito a DroideCore.OnDied " +
                      $"en escena '{droide.gameObject.scene.name}'.");
        }

        private void UnsubscribeFromDroide()
        {
            if (_subscribedDroide == null) return;
            _subscribedDroide.OnDied -= HandleDroideDied;
            _subscribedDroide        = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Solo re-suscribir en cargas no-aditivas (recarga de GameplayScene).
            // En cargas aditivas (MiniGameScene) el Droide no cambia.
            if (mode == LoadSceneMode.Additive) return;
            SubscribeToDroide();
        }

        // ── Manejador de muerte ───────────────────────────────

        private void HandleDroideDied(DeathCause cause)
        {
            var data = new GameOverData(cause);

            // Localizar GameOverPresenter en la escena activa.
            // FindObjectsOfType<T>(includeInactive: false) limita la búsqueda
            // a la escena activa visible — no rastrea escenas aditivas descargadas.
            // Se llama SOLO cuando ocurre una muerte, no en cada frame.
            var presenter = FindPresenterInActiveScene();
            if (presenter == null)
            {
                Debug.LogWarning("[GameOverTrigger] No se encontró GameOverPresenter " +
                                 $"en la escena activa '{SceneManager.GetActiveScene().name}'. " +
                                 "¿Falta el componente en la escena?");
                return;
            }

            presenter.Show(data);
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Garantiza que el singleton existe aunque LoginScene no se haya cargado.
        /// Llamado por GameFlowManager.EnsureSingletons().
        /// </summary>
        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("[GameOverTrigger-Auto]");
            go.AddComponent<GameOverTrigger>();
            Debug.Log("[GameOverTrigger] Instancia auto-creada por EnsureSingletons.");
        }

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Busca DroideCore en la escena activa actual.
        /// No rastrea escenas aditivas para evitar el bug original.
        /// </summary>
        private static DroideCore FindDroideInActiveScene()
        {
            // FindObjectOfType sin parámetros busca en TODAS las escenas cargadas.
            // Iteramos manualmente para limitarnos a la escena activa.
            var activeScene = SceneManager.GetActiveScene();
            foreach (var root in activeScene.GetRootGameObjects())
            {
                var droide = root.GetComponentInChildren<DroideCore>(includeInactive: false);
                if (droide != null) return droide;
            }

            // Fallback: si el Droide es DDOL (no pertenece a ninguna escena cargada normal),
            // buscamos globalmente pero solo si la escena activa es GameplayScene.
            if (activeScene.name == "GameplayScene")
                return Object.FindObjectOfType<DroideCore>();

            return null;
        }

        private static GameOverPresenter FindPresenterInActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var root in activeScene.GetRootGameObjects())
            {
                var presenter = root.GetComponentInChildren<GameOverPresenter>(includeInactive: true);
                if (presenter != null) return presenter;
            }
            return null;
        }
    }
}
