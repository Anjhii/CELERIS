// ============================================================
// LevelManager.cs  |  Assets/Scripts/Core/  — v3 SOLID
//
// PROGRESO POR USUARIO:
//   El nivel actual se guarda bajo la clave
//   "CELERIS_Level_{userId}" via IPlayerProgressStore.
//   Mientras no hay sesion se usa la clave anonima.
//   Al hacer BindToUser() se conmuta a la clave del usuario.
//   Al hacer UnbindUser() se vuelve a la clave anonima.
//
// CAMBIOS v2 (Bloque 5):
//   Toda lectura/escritura de PlayerPrefs eliminada de este archivo.
//   Reemplazada por IPlayerProgressStore (inyectado en Awake).
//
// CAMBIOS v3 (F4-T5 — Game Director):
//   Integración de IDifficultyDirector (DIP).
//   Apply() se llama antes de cargar GameplayScene.
//   RecordLevelResult() se llama tras cada resultado.
//   debugForceD >= 0 inyecta NullDifficultyDirector (QA).
//
// API PUBLICA (sin cambios de firma):
//   LevelManager.CurrentLevelIndex / CurrentLevelNumber
//   LevelManager.Instance.BindToUser(userId)
//   LevelManager.Instance.ApplyServerProgress(levelIndex)
//   LevelManager.Instance.UnbindUser()
//   LevelManager.Instance.NotifyLevelResult(won, time, battery, cause)
//   LevelManager.Instance.AdvanceLevel()
//   LevelManager.Instance.RetryCurrentLevel()
//   LevelManager.Instance.ResetProgress()
//   LevelManager.Instance.GoToLevel(n)
//   LevelManager.EnsureExists()
// ============================================================
using Celeris.Config;
using Celeris.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Celeris.Core
{
    public class LevelManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static LevelManager Instance { get; private set; }

        // ── Inspector (solo lectura en Play) ──────────────────
        [Header("Estado (solo lectura en Play)")]
        [SerializeField] private string _nivelActual   = "-";
        [SerializeField] private string _totalNiveles  = "-";
        [SerializeField] private string _configActiva  = "-";
        [SerializeField] private string _usuarioActivo = "anonimo";

        // ── Clave de progreso (cambia al autenticarse) ────────
        private const string ANON_KEY   = "CELERIS_Level_anon";
        private const string KEY_PREFIX = "CELERIS_Level_";
        private string       _activeKey = ANON_KEY;

        // ── Dependencias inyectadas (DIP) ────────────────────
        private IPlayerProgressStore _store;
        private IDifficultyDirector  _director;

        // ── Debug: forzar D constante (QA / diseño de niveles) ─
        [Header("Game Director (Debug)")]
        [Tooltip("-1 = director real. 0-1 = NullDifficultyDirector con ese D fijo.")]
        [Range(-1f, 1f)]
        public float debugForceD = -1f;

        // ── Propiedad de nivel actual ─────────────────────────
        public static int CurrentLevelIndex
        {
            get
            {
                if (Instance == null || Instance._store == null)
                    return PlayerPrefs.GetInt(ANON_KEY, 0); // fallback de emergencia
                return Instance._store.GetLevelIndex(Instance._activeKey);
            }
            private set
            {
                if (Instance == null || Instance._store == null)
                {
                    PlayerPrefs.SetInt(ANON_KEY, value);
                    PlayerPrefs.Save();
                    return;
                }
                Instance._store.SetLevelIndex(Instance._activeKey, value);
                // Persistencia diferida — el store la ejecuta en pause/quit.
            }
        }

        public static int CurrentLevelNumber => CurrentLevelIndex + 1;

        // ── Total de niveles (cacheado) ───────────────────────
        private int _totalLevelsCached = -1;
        public int TotalLevels
        {
            get
            {
                if (_totalLevelsCached < 0)
                {
                    int count = 0;
                    while (Resources.Load<ProceduralLevelConfig>(
                               $"LevelConfigs/Level{(count + 1):D2}") != null)
                        count++;
                    _totalLevelsCached = Mathf.Max(count, 1);
                }
                return _totalLevelsCached;
            }
        }

        // ── Flag: venimos de portal ───────────────────────────
        private bool _isPortalTransition = false;

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

            _store = PlayerProgressStore.EnsureExists();

            // F4-T5: inyectar director según modo debug o producción
            if (debugForceD >= 0f)
            {
                _director = new NullDifficultyDirector(debugForceD);
                Debug.Log($"[LevelManager] Game Director en modo DEBUG — D fijo={debugForceD:F2}");
            }
            else
            {
                _director = new DifficultyDirectorImpl(_store);
            }

            RefreshDebugFields();
            Debug.Log($"[LevelManager] Iniciado — clave='{_activeKey}' " +
                      $"Nivel {CurrentLevelNumber}");
        }

        private void RefreshDebugFields()
        {
            _nivelActual  = $"Nivel {CurrentLevelNumber} (idx {CurrentLevelIndex})";
            _totalNiveles = TotalLevels.ToString();
            var cfg = CurrentConfig;
            _configActiva = cfg != null ? cfg.name : "NO ENCONTRADA";
        }

        // ── API de usuario ────────────────────────────────────

        public void BindToUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning("[LevelManager] BindToUser: userId vacio.");
                return;
            }

            _activeKey     = KEY_PREFIX + userId;
            _usuarioActivo = userId.Substring(0, Mathf.Min(8, userId.Length)) + "...";
            RefreshDebugFields();
            Debug.Log($"[LevelManager] Progreso enlazado al usuario -> " +
                      $"clave='{_activeKey}', nivel local={CurrentLevelNumber}");
        }

        public void ApplyServerProgress(int serverLevelIndex)
        {
            int clamped = Mathf.Clamp(serverLevelIndex, 0, TotalLevels - 1);

            if (clamped > CurrentLevelIndex)
            {
                CurrentLevelIndex = clamped;
                Debug.Log($"[LevelManager] Progreso del servidor aplicado -> " +
                          $"Nivel {CurrentLevelNumber}");
            }
            else
            {
                Debug.Log($"[LevelManager] Progreso local ({CurrentLevelNumber}) >= " +
                          $"servidor ({clamped + 1}). Se mantiene el local.");
            }

            RefreshDebugFields();
        }

        public void UnbindUser()
        {
            // Borrar la clave anonima para que el siguiente usuario empiece limpio
            _store.DeleteKey(ANON_KEY);
            _store.FlushIfDirty();

            _activeKey     = ANON_KEY;
            _usuarioActivo = "anonimo";
            RefreshDebugFields();
            Debug.Log("[LevelManager] Usuario desenlazado. Progreso en 0.");
        }

        // ── EnsureExists ──────────────────────────────────────
        public static LevelManager EnsureExists()
        {
            if (Instance != null) return Instance;
            Debug.LogWarning("[LevelManager] Instancia no encontrada. Creando temporal.");
            var go = new GameObject("[LevelManager-Auto]");
            go.AddComponent<LevelManager>();
            return Instance;
        }

        // ── API de navegacion ─────────────────────────────────

        public ProceduralLevelConfig GetConfig(int levelIndex)
        {
            string name = $"Level{(levelIndex + 1):D2}";
            var cfg = Resources.Load<ProceduralLevelConfig>($"LevelConfigs/{name}");
            if (cfg == null)
                Debug.LogWarning($"[LevelManager] No encontrado: LevelConfigs/{name}.asset");
            return cfg;
        }

        public ProceduralLevelConfig CurrentConfig => GetConfig(CurrentLevelIndex);

        public bool IsPortalTransition => _isPortalTransition;

        public void LoadMiniGame(bool asPortalTransition = false)
        {
            _isPortalTransition = asPortalTransition;
            SceneManager.LoadScene("MiniGameScene");
        }

        public void ReturnFromPortal()
        {
            _isPortalTransition = false;
            SceneManager.LoadScene("GameplayScene");
        }

        // ── F4-T5: Notificar resultado de nivel al director ───
        /// <summary>
        /// Llamar desde GameOverTrigger o GameFlowManager tras cada victoria/muerte.
        /// El director actualizará D y lo persistirá en IPlayerProgressStore.
        /// </summary>
        public void NotifyLevelResult(bool won, float completionTime,
                                      float batteryConsumed, DeathCause lastDeath)
        {
            _director?.RecordLevelResult(won, completionTime, batteryConsumed, lastDeath);
            Debug.Log($"[LevelManager] Resultado registrado — won={won} " +
                      $"D={_director?.CurrentDifficulty:F2}");
        }

        public void AdvanceLevel()
        {
            int next = CurrentLevelIndex + 1;
            if (next >= TotalLevels)
            {
                CurrentLevelIndex = 0;
                RefreshDebugFields();
                SceneManager.LoadScene("MainMenuScene");
            }
            else
            {
                CurrentLevelIndex = next;
                RefreshDebugFields();

                // F4-T5: aplicar dificultad al config del PRÓXIMO nivel antes de cargar.
                var nextConfig = CurrentConfig;
                if (nextConfig != null)
                    _director?.Apply(nextConfig);

                Debug.Log($"[LevelManager] -> Nivel {CurrentLevelNumber} " +
                          $"D={_director?.CurrentDifficulty:F2}");
                SceneManager.LoadScene("GameplayScene");
            }
        }

        public void RetryCurrentLevel()
        {
            // F4-T5: re-aplicar dificultad al config actual (D no cambia en retry).
            var cfg = CurrentConfig;
            if (cfg != null)
                _director?.Apply(cfg);

            SceneManager.LoadScene("GameplayScene");
        }

        public void ResetProgress()
        {
            CurrentLevelIndex = 0;
            RefreshDebugFields();
            Debug.Log("[LevelManager] Progreso reiniciado al Nivel 1.");
        }

        public void GoToLevel(int levelNumber)
        {
            CurrentLevelIndex = Mathf.Clamp(levelNumber - 1, 0, TotalLevels - 1);
            RefreshDebugFields();
            SceneManager.LoadScene("GameplayScene");
        }
    }
}
