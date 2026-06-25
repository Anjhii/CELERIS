// ============================================================
// PlayerProgressStore.cs  |  Assets/Scripts/Core/
//
// RESPONSABILIDAD UNICA (SRP):
//   Ser el UNICO lugar del proyecto que llama PlayerPrefs.
//   Carga todos los valores en Awake() a campos privados en memoria.
//   Expone propiedades: getters leen memoria, setters actualizan
//   memoria y marcan _isDirty = true.
//   PlayerPrefs.Save() solo ocurre en FlushIfDirty() (llamado
//   desde OnApplicationPause / OnApplicationQuit).
//
// LO QUE NO HACE:
//   No conoce la logica de negocio (que score gana estrellas, etc.).
//   No decide cuando guardar — eso lo ordenan los consumidores.
//   No gestiona tokens de autenticacion — scope de AuthManager.
//
// IMPLEMENTA IPlayerProgressStore (DIP).
//   ScoreManager y LevelManager reciben la interfaz por inyeccion.
//
// SINGLETON DDOL:
//   Es un MonoBehaviour para poder capturar OnApplicationPause /
//   OnApplicationQuit y existir entre escenas como los demas
//   managers. GameFlowManager.EnsureSingletons() lo crea si no existe.
//
// CLAVES DINAMICAS:
//   GetLevelIndex / SetLevelIndex reciben la clave como parametro
//   porque LevelManager la cambia al autenticarse. El store escribe
//   esa clave en disco pero no la cachea en memoria (no forma parte
//   del snapshot de Awake porque su valor depende del userId, que
//   se conoce despues del login).
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace Celeris.Core
{
    public class PlayerProgressStore : MonoBehaviour, IPlayerProgressStore
    {
        // ── Singleton DDOL ────────────────────────────────────
        public static PlayerProgressStore Instance { get; private set; }

        // ── Claves PlayerPrefs (fuente unica de verdad) ───────
        private const string KEY_HIGH_SCORE    = "LocalHighScore";
        private const string KEY_NEEDS_SYNC    = "ScoreNeedsSync";
        private const string KEY_PENDING_SCORE = "PendingScore";
        private const string KEY_DEVICE_ID     = "DeviceID";
        private const string KEY_USERNAME      = "Username";
        private const string KEY_MAX_LEVEL     = "MaxUnlockedLevel";
        private const string KEY_LEVELS_DONE   = "LevelsCompleted";
        private const string KEY_TIMES_PLAYED  = "TimesPlayed";
        private const string KEY_TOTAL_STARS   = "TotalStars";
        private const string KEY_PREFIX_BEST   = "LevelBestScore_";
        private const string KEY_PREFIX_STARS  = "LevelStars_";

        // ── Cache en memoria (cargado una vez en Awake) ───────
        private long   _highScore;
        private bool   _needsSync;
        private long   _pendingScore;
        private string _deviceId;
        private string _username;
        private int    _maxUnlockedLevel;
        private int    _levelsCompleted;
        private int    _timesPlayed;
        private int    _totalStars;

        // Cache dinamico para scores/estrellas por nivel
        private readonly Dictionary<int, int> _bestScores = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _levelStars = new Dictionary<int, int>();

        private bool _isDirty = false;

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
            LoadAll();
        }

        private void LoadAll()
        {
            _highScore        = long.TryParse(PlayerPrefs.GetString(KEY_HIGH_SCORE,    "0"), out long hs) ? hs : 0L;
            _needsSync        = PlayerPrefs.GetInt(KEY_NEEDS_SYNC,    0) == 1;
            _pendingScore     = long.TryParse(PlayerPrefs.GetString(KEY_PENDING_SCORE, "0"), out long ps) ? ps : 0L;
            _deviceId         = PlayerPrefs.GetString(KEY_DEVICE_ID,  "");
            _username         = PlayerPrefs.GetString(KEY_USERNAME,   "Jugador");
            _maxUnlockedLevel = PlayerPrefs.GetInt(KEY_MAX_LEVEL,     0);
            _levelsCompleted  = PlayerPrefs.GetInt(KEY_LEVELS_DONE,   0);
            _timesPlayed      = PlayerPrefs.GetInt(KEY_TIMES_PLAYED,  0);
            _totalStars       = PlayerPrefs.GetInt(KEY_TOTAL_STARS,   0);

            // Los scores/estrellas por nivel se cargan bajo demanda (lazy)
            // para no iterar un numero indeterminado de niveles en Awake.
            _bestScores.Clear();
            _levelStars.Clear();

            Debug.Log("[PlayerProgressStore] Cache cargado desde disco.");
        }

        // ── IPlayerProgressStore — Score global ───────────────

        public long HighScore
        {
            get => _highScore;
            set { _highScore = value; PlayerPrefs.SetString(KEY_HIGH_SCORE, value.ToString()); _isDirty = true; }
        }

        public bool NeedsSync
        {
            get => _needsSync;
            set { _needsSync = value; PlayerPrefs.SetInt(KEY_NEEDS_SYNC, value ? 1 : 0); _isDirty = true; }
        }

        public long PendingScore
        {
            get => _pendingScore;
            set { _pendingScore = value; PlayerPrefs.SetString(KEY_PENDING_SCORE, value.ToString()); _isDirty = true; }
        }

        public string DeviceId
        {
            get => _deviceId;
            set { _deviceId = value; PlayerPrefs.SetString(KEY_DEVICE_ID, value); _isDirty = true; }
        }

        public string Username
        {
            get => _username;
            set { _username = value; PlayerPrefs.SetString(KEY_USERNAME, value); _isDirty = true; }
        }

        // ── IPlayerProgressStore — Progreso de niveles ────────

        public int MaxUnlockedLevel
        {
            get => _maxUnlockedLevel;
            set { _maxUnlockedLevel = value; PlayerPrefs.SetInt(KEY_MAX_LEVEL, value); _isDirty = true; }
        }

        public int LevelsCompleted
        {
            get => _levelsCompleted;
            set { _levelsCompleted = value; PlayerPrefs.SetInt(KEY_LEVELS_DONE, value); _isDirty = true; }
        }

        public int TimesPlayed
        {
            get => _timesPlayed;
            set { _timesPlayed = value; PlayerPrefs.SetInt(KEY_TIMES_PLAYED, value); _isDirty = true; }
        }

        public int TotalStars
        {
            get => _totalStars;
            set { _totalStars = value; PlayerPrefs.SetInt(KEY_TOTAL_STARS, value); _isDirty = true; }
        }

        // ── IPlayerProgressStore — Claves dinamicas por nivel ─

        public int GetBestScoreForLevel(int levelIndex)
        {
            if (!_bestScores.TryGetValue(levelIndex, out int cached))
            {
                cached = PlayerPrefs.GetInt(KEY_PREFIX_BEST + levelIndex, 0);
                _bestScores[levelIndex] = cached;
            }
            return cached;
        }

        public void SetBestScoreForLevel(int levelIndex, int score)
        {
            _bestScores[levelIndex] = score;
            PlayerPrefs.SetInt(KEY_PREFIX_BEST + levelIndex, score);
            _isDirty = true;
        }

        public int GetStarsForLevel(int levelIndex)
        {
            if (!_levelStars.TryGetValue(levelIndex, out int cached))
            {
                cached = PlayerPrefs.GetInt(KEY_PREFIX_STARS + levelIndex, 0);
                _levelStars[levelIndex] = cached;
            }
            return cached;
        }

        public void SetStarsForLevel(int levelIndex, int stars)
        {
            _levelStars[levelIndex] = stars;
            PlayerPrefs.SetInt(KEY_PREFIX_STARS + levelIndex, stars);
            _isDirty = true;
        }

        // ── IPlayerProgressStore — Clave dinamica de nivel ────
        // LevelManager cambia la clave activa al autenticarse;
        // estos metodos NO cachean en memoria porque el valor
        // depende del userId conocido solo en runtime.

        public int  GetLevelIndex(string key)         => PlayerPrefs.GetInt(key, 0);
        public void SetLevelIndex(string key, int val) { PlayerPrefs.SetInt(key, val); _isDirty = true; }
        public void DeleteKey(string key)              { PlayerPrefs.DeleteKey(key);   _isDirty = true; }
        public bool HasKey(string key)                 => PlayerPrefs.HasKey(key);

        // ── IPlayerProgressStore — Flotantes genéricos (F4) ───
        // No se cachean en memoria: son valores infrecuentes (ej: D de dificultad).
        public float GetFloat(string key, float defaultValue = 0f)
            => PlayerPrefs.GetFloat(key, defaultValue);

        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            _isDirty = true;
        }

        // ── IPlayerProgressStore — Persistencia ───────────────

        public void FlushIfDirty()
        {
            if (!_isDirty) return;
            PlayerPrefs.Save();
            _isDirty = false;
            Debug.Log("[PlayerProgressStore] Guardado en disco (dirty flush).");
        }

        public void ForceFlush()
        {
            PlayerPrefs.Save();
            _isDirty = false;
            Debug.Log("[PlayerProgressStore] Guardado en disco (force flush).");
        }

        // ── Persistencia segura al minimizar / cerrar ─────────

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) FlushIfDirty();
        }

        private void OnApplicationQuit() => FlushIfDirty();

        // ── EnsureExists ──────────────────────────────────────
        public static PlayerProgressStore EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[PlayerProgressStore-Auto]");
            go.AddComponent<PlayerProgressStore>();
            return Instance;
        }
    }
}
