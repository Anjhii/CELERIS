// ============================================================
// LevelManager.cs  |  Assets/Scripts/Core/
//
// PROGRESO POR USUARIO:
//   El nivel actual se guarda bajo la clave
//   "CELERIS_Level_{userId}" en PlayerPrefs, donde userId es
//   el UUID de Supabase del jugador autenticado.
//   Esto garantiza que cada cuenta tenga su propio progreso,
//   incluso en el mismo dispositivo.
//
//   Flujo de autenticación:
//     Login/Registro → AuthManager.ApplySession()
//       → LevelManager.BindToUser(userId)      (cambia la clave activa)
//       → LevelManager.ApplyServerProgress(n)  (aplica nivel del servidor)
//     Logout → AuthManager.ClearSession()
//       → LevelManager.UnbindUser()            (vuelve a clave anónima / 0)
//
// API PÚBLICA:
//   LevelManager.CurrentLevelIndex / CurrentLevelNumber
//   LevelManager.Instance.BindToUser(userId)
//   LevelManager.Instance.ApplyServerProgress(levelIndex)
//   LevelManager.Instance.UnbindUser()
//   LevelManager.Instance.AdvanceLevel()
//   LevelManager.Instance.RetryCurrentLevel()
//   LevelManager.Instance.ResetProgress()
//   LevelManager.Instance.GoToLevel(n)
//   LevelManager.EnsureExists()
// ============================================================
using Celeris.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Celeris.Core
{
    public class LevelManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static LevelManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────
        [Header("── Estado (solo lectura en Play) ──")]
        [SerializeField] private string _nivelActual   = "–";
        [SerializeField] private string _totalNiveles  = "–";
        [SerializeField] private string _configActiva  = "–";
        [SerializeField] private string _usuarioActivo = "anónimo";

        // ── Clave de progreso (cambia al autenticarse) ────────
        // Mientras no hay sesión se usa la clave anónima.
        // Al hacer BindToUser() se conmuta a la clave del usuario.
        private const string ANON_KEY    = "CELERIS_Level_anon";
        private const string KEY_PREFIX  = "CELERIS_Level_";
        private string       _activeKey  = ANON_KEY;

        // ── Propiedad de nivel actual ─────────────────────────
        public static int CurrentLevelIndex
        {
            get => PlayerPrefs.GetInt(
                       Instance != null ? Instance._activeKey : ANON_KEY, 0);
            private set
            {
                PlayerPrefs.SetInt(
                    Instance != null ? Instance._activeKey : ANON_KEY, value);
                PlayerPrefs.Save();
            }
        }

        public static int CurrentLevelNumber => CurrentLevelIndex + 1;

        // ── Total de niveles ──────────────────────────────────
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
            RefreshDebugFields();
            Debug.Log($"[LevelManager] Iniciado — clave='{_activeKey}' " +
                      $"Nivel {CurrentLevelNumber}");
        }

        private void RefreshDebugFields()
        {
            _nivelActual  = $"Nivel {CurrentLevelNumber} (idx {CurrentLevelIndex})";
            _totalNiveles = TotalLevels.ToString();
            var cfg = CurrentConfig;
            _configActiva = cfg != null ? cfg.name : "⚠ NO ENCONTRADA";
        }

        // ── API de usuario ────────────────────────────────────

        /// <summary>
        /// Enlaza el progreso al userId autenticado.
        /// Cambia la clave activa a "CELERIS_Level_{userId}".
        /// Si el usuario no tiene registro local, empieza en 0
        /// hasta que llegue el valor del servidor vía ApplyServerProgress().
        /// </summary>
        public void BindToUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning("[LevelManager] BindToUser: userId vacío.");
                return;
            }

            _activeKey    = KEY_PREFIX + userId;
            _usuarioActivo = userId.Substring(0, Mathf.Min(8, userId.Length)) + "…";
            RefreshDebugFields();
            Debug.Log($"[LevelManager] Progreso enlazado al usuario → " +
                      $"clave='{_activeKey}', nivel local={CurrentLevelNumber}");
        }

        /// <summary>
        /// Aplica el nivel que llegó del servidor (Supabase profiles.max_unlocked_level).
        /// Solo sobreescribe si el valor del servidor es mayor que el local,
        /// para no retroceder progreso ante fallos de sincronización.
        /// </summary>
        public void ApplyServerProgress(int serverLevelIndex)
        {
            int clamped = Mathf.Clamp(serverLevelIndex, 0, TotalLevels - 1);

            if (clamped > CurrentLevelIndex)
            {
                CurrentLevelIndex = clamped;
                Debug.Log($"[LevelManager] Progreso del servidor aplicado → " +
                          $"Nivel {CurrentLevelNumber}");
            }
            else
            {
                Debug.Log($"[LevelManager] Progreso local ({CurrentLevelNumber}) ≥ " +
                          $"servidor ({clamped + 1}). Se mantiene el local.");
            }

            RefreshDebugFields();
        }

        /// <summary>
        /// Desenlaza el usuario (al hacer logout).
        /// Vuelve a la clave anónima, que empieza en 0.
        /// </summary>
        public void UnbindUser()
        {
            _activeKey    = ANON_KEY;
            _usuarioActivo = "anónimo";

            // Limpiar la clave anónima para que el siguiente usuario empiece limpio
            PlayerPrefs.DeleteKey(ANON_KEY);
            PlayerPrefs.Save();

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

        // ── API de navegación ─────────────────────────────────

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
                Debug.Log($"[LevelManager] → Nivel {CurrentLevelNumber}");
                SceneManager.LoadScene("GameplayScene");
            }
        }

        public void RetryCurrentLevel()
        {
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
