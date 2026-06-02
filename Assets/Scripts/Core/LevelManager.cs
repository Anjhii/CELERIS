// ============================================================
// LevelManager.cs  |  Assets/Scripts/Core/
//
// SETUP (hacer UNA sola vez):
//   1. En LoginScene (la primera escena del juego), crear un
//      GameObject vacío llamado "LevelManager".
//   2. Adjuntar este script.
//   3. Listo. DontDestroyOnLoad lo mantiene vivo en todas las
//      escenas siguientes (MainMenuScene, GameplayScene, etc.)
//
// ¿POR QUÉ LoginScene y no MainMenuScene?
//   Porque si el flujo va LoginScene → GameplayScene (omitiendo
//   MainMenuScene), LevelManager debe existir antes de llegar a
//   GameplayScene. LoginScene siempre es la primera.
//
// REQUISITO DE ASSETS:
//   Assets/Resources/LevelConfigs/Level01.asset … Level09.asset
//   (Todos los .asset deben estar en esa carpeta exacta)
//
// API PÚBLICA:
//   LevelManager.Instance.LoadMiniGame()       → carga MiniGameScene
//   LevelManager.Instance.AdvanceLevel()       → siguiente nivel + GameplayScene
//   LevelManager.Instance.RetryCurrentLevel()  → mismo nivel de nuevo
//   LevelManager.Instance.ResetProgress()      → vuelve al nivel 1
//   LevelManager.CurrentLevelIndex             → índice base-0
//   LevelManager.CurrentLevelNumber            → número base-1 (para UI)
//   LevelManager.Instance.CurrentConfig        → config del nivel activo
//   LevelManager.EnsureExists()                → devuelve la instancia;
//                                                si no existe la crea.
//                                                Usar solo como fallback.
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

        // ── Inspector: estado visible para el equipo ──────────
        [Header("── Estado del progreso (solo lectura en Play) ──")]
        [Tooltip("Nivel en el que está el jugador ahora mismo")]
        [SerializeField] private string _nivelActual   = "–";
        [Tooltip("Total de niveles detectados en Resources/LevelConfigs/")]
        [SerializeField] private string _totalNiveles  = "–";
        [Tooltip("Nombre del asset de config activo")]
        [SerializeField] private string _configActiva  = "–";

        // ── Persistencia ──────────────────────────────────────
        private const string PREFS_KEY = "CELERIS_CurrentLevel";

        public static int CurrentLevelIndex
        {
            get => PlayerPrefs.GetInt(PREFS_KEY, 0);
            private set { PlayerPrefs.SetInt(PREFS_KEY, value); PlayerPrefs.Save(); }
        }

        public static int CurrentLevelNumber => CurrentLevelIndex + 1;

        // ── Total de niveles (auto-detectado) ─────────────────
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
                    _totalNiveles = _totalLevelsCached.ToString();
                }
                return _totalLevelsCached;
            }
        }

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
            Debug.Log($"[LevelManager] Iniciado — Nivel {CurrentLevelNumber} / {TotalLevels}");
        }

        // Actualiza los campos visibles en Inspector durante Play Mode
        private void RefreshDebugFields()
        {
            _nivelActual  = $"Nivel {CurrentLevelNumber}  (índice {CurrentLevelIndex})";
            _totalNiveles = TotalLevels.ToString();
            var cfg = CurrentConfig;
            _configActiva = cfg != null ? cfg.name : "⚠ NO ENCONTRADA — mover assets a Resources/LevelConfigs/";
        }

        // ── EnsureExists — fallback de seguridad ──────────────
        /// <summary>
        /// Devuelve la instancia existente o crea una nueva de emergencia.
        /// Los scripts (GameFlowManager, MiniGameSimulator) deben usar ESTO
        /// en lugar de comprobar Instance == null, para que el flujo nunca
        /// se rompa silenciosamente al testear desde una escena media.
        /// </summary>
        public static LevelManager EnsureExists()
        {
            if (Instance != null) return Instance;

            Debug.LogWarning("[LevelManager] No se encontró instancia en escena. " +
                             "Creando una temporal. COLOCA LevelManager en LoginScene.");
            var go = new GameObject("[LevelManager — AUTO-CREADO]");
            go.AddComponent<LevelManager>();   // Awake asigna Instance
            return Instance;
        }

        // ── API pública ───────────────────────────────────────

        public ProceduralLevelConfig GetConfig(int levelIndex)
        {
            string name = $"Level{(levelIndex + 1):D2}";
            var cfg = Resources.Load<ProceduralLevelConfig>($"LevelConfigs/{name}");
            if (cfg == null)
                Debug.LogWarning($"[LevelManager] ⚠ No encontrado: Resources/LevelConfigs/{name}.asset");
            return cfg;
        }

        public ProceduralLevelConfig CurrentConfig => GetConfig(CurrentLevelIndex);

        public void LoadMiniGame()
        {
            Debug.Log($"[LevelManager] Nivel {CurrentLevelNumber} superado → MiniGameScene");
            SceneManager.LoadScene("MiniGameScene");
        }

        public void AdvanceLevel()
        {
            int next = CurrentLevelIndex + 1;
            if (next >= TotalLevels)
            {
                Debug.Log("[LevelManager] ¡Todos los niveles completados! → MainMenuScene");
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
            Debug.Log($"[LevelManager] Reintentando nivel {CurrentLevelNumber}");
            SceneManager.LoadScene("GameplayScene");
        }

        public void ResetProgress()
        {
            CurrentLevelIndex = 0;
            RefreshDebugFields();
            Debug.Log("[LevelManager] Progreso reiniciado al Nivel 1.");
        }

        /// <summary>Salta directamente a un nivel específico (base-1). Útil desde LevelSelectScene.</summary>
        public void GoToLevel(int levelNumber)
        {
            int idx = Mathf.Clamp(levelNumber - 1, 0, TotalLevels - 1);
            CurrentLevelIndex = idx;
            RefreshDebugFields();
            Debug.Log($"[LevelManager] Saltando al nivel {CurrentLevelNumber}");
            SceneManager.LoadScene("GameplayScene");
        }
    }
}
