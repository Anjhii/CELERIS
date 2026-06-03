using System;
using Celeris.Data;   // LevelResult
using UnityEngine;

/// <summary>
/// ScoreManager — Puntaje LOCAL + progreso de CELERIS.
/// Persiste con PlayerPrefs. Sincroniza con SupabaseManager cuando hay red.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────────────────────
    public static ScoreManager Instance { get; private set; }

    // ─── Eventos ────────────────────────────────────────────────────────────────
    public event Action<long> OnScoreChanged;
    public event Action<long> OnNewHighScore;

    // ─── Claves PlayerPrefs (score y sesión — sin cambios) ───────────────────────
    private const string KEY_HIGH_SCORE    = "LocalHighScore";
    private const string KEY_NEEDS_SYNC    = "ScoreNeedsSync";
    private const string KEY_PENDING_SCORE = "PendingScore";
    private const string KEY_DEVICE_ID     = "DeviceID";
    private const string KEY_USERNAME      = "Username";

    // ─── Claves PlayerPrefs (progreso CELERIS — nuevas) ─────────────────────────
    private const string KEY_MAX_LEVEL     = "MaxUnlockedLevel";
    private const string KEY_LEVELS_DONE   = "LevelsCompleted";
    private const string KEY_TIMES_PLAYED  = "TimesPlayed";
    private const string KEY_TOTAL_STARS   = "TotalStars";
    // bestScore por nivel: "LevelBestScore_N"
    // estrellas por nivel: "LevelStars_N"

    // ─── Estado de sesión ────────────────────────────────────────────────────────
    private long _currentScore = 0;

    // ─── Propiedades — score global (sin cambios) ────────────────────────────────
    public long   CurrentScore   => _currentScore;
    public long   LocalHighScore => PlayerPrefs.GetInt(KEY_HIGH_SCORE, 0);
    public string DeviceId       => PlayerPrefs.GetString(KEY_DEVICE_ID, "");
    public bool   HasPendingSync => PlayerPrefs.GetInt(KEY_NEEDS_SYNC, 0) == 1;
    public long   PendingScore   => PlayerPrefs.GetInt(KEY_PENDING_SCORE, 0);

    public string Username
    {
        get => PlayerPrefs.GetString(KEY_USERNAME, "Jugador");
        set { PlayerPrefs.SetString(KEY_USERNAME, value); PlayerPrefs.Save(); }
    }

    // ─── Propiedades — progreso CELERIS (nuevas) ────────────────────────────────
    public int MaxUnlockedLevel => PlayerPrefs.GetInt(KEY_MAX_LEVEL,    0);
    public int LevelsCompleted  => PlayerPrefs.GetInt(KEY_LEVELS_DONE,  0);
    public int TimesPlayed      => PlayerPrefs.GetInt(KEY_TIMES_PLAYED, 0);
    public int TotalStars       => PlayerPrefs.GetInt(KEY_TOTAL_STARS,  0);

    /// <summary>Mejor puntaje conseguido en un nivel concreto (índice 0-based).</summary>
    public int GetBestScoreForLevel(int levelIndex) =>
        PlayerPrefs.GetInt($"LevelBestScore_{levelIndex}", 0);

    /// <summary>Estrellas obtenidas en un nivel concreto (0-3).</summary>
    public int GetStarsForLevel(int levelIndex) =>
        PlayerPrefs.GetInt($"LevelStars_{levelIndex}", 0);

    /// <summary>
    /// Suma los mejores puntajes de todos los niveles completados.
    /// Este es el valor que se envía al leaderboard global.
    /// </summary>
    public long GetTotalCumulativeScore()
    {
        long total = 0;
        for (int i = 0; ; i++)
        {
            string key = $"LevelBestScore_{i}";
            if (!PlayerPrefs.HasKey(key)) break;
            total += PlayerPrefs.GetInt(key, 0);
        }
        return total;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureDeviceId();
    }

    private void EnsureDeviceId()
    {
        if (string.IsNullOrEmpty(PlayerPrefs.GetString(KEY_DEVICE_ID, "")))
        {
            PlayerPrefs.SetString(KEY_DEVICE_ID, Guid.NewGuid().ToString());
            PlayerPrefs.Save();
        }
    }

    // ─── API original (sin cambios) ──────────────────────────────────────────────

    public void ResetCurrentScore()
    {
        _currentScore = 0;
        OnScoreChanged?.Invoke(_currentScore);
    }

    public void AddPoints(long points)
    {
        if (points <= 0) return;
        _currentScore += points;
        OnScoreChanged?.Invoke(_currentScore);
        EvaluateHighScore(_currentScore);
    }

    /// <summary>Envía el high score global al leaderboard. Sigue funcionando igual.</summary>
    public void SubmitLevelScore(long finalScore)
    {
        _currentScore = finalScore;
        EvaluateHighScore(finalScore);

        if (SupabaseManager.Instance != null)
            SupabaseManager.Instance.SubmitScore(LocalHighScore, Username, DeviceId);
        else
            MarkScoreAsPending(LocalHighScore);
    }

    public void MarkScoreAsPending(long score)
    {
        PlayerPrefs.SetInt(KEY_NEEDS_SYNC, 1);
        PlayerPrefs.SetInt(KEY_PENDING_SCORE, (int)score);
        PlayerPrefs.Save();
    }

    public void ClearPendingSync()
    {
        PlayerPrefs.SetInt(KEY_NEEDS_SYNC, 0);
        PlayerPrefs.Save();
    }

    // ─── API CELERIS — método principal post-nivel (nueva) ──────────────────────

    /// <summary>
    /// Registra el resultado completo de un nivel:
    /// actualiza score global, progreso por nivel y sincroniza con Supabase.
    /// Llamar desde GameplayScene al terminar cada nivel (victoria o derrota).
    /// </summary>
    public void RecordLevelResult(LevelResult result)
    {
        _currentScore = result.score;
        OnScoreChanged?.Invoke(_currentScore);

        // Siempre incrementar partidas jugadas
        PlayerPrefs.SetInt(KEY_TIMES_PLAYED, TimesPlayed + 1);

        if (result.isVictory)
        {
            // ── Mejor score de este nivel ─────────────────────────────────────
            int prevBest = GetBestScoreForLevel(result.levelIndex);
            if (result.score > prevBest)
                PlayerPrefs.SetInt($"LevelBestScore_{result.levelIndex}", result.score);

            // ── Estrellas de este nivel (solo sube, nunca baja) ───────────────
            int prevStars = GetStarsForLevel(result.levelIndex);
            if (result.stars > prevStars)
            {
                int starDelta = result.stars - prevStars;
                PlayerPrefs.SetInt($"LevelStars_{result.levelIndex}", result.stars);
                PlayerPrefs.SetInt(KEY_TOTAL_STARS, TotalStars + starDelta);
            }

            // ── Niveles completados ───────────────────────────────────────────
            PlayerPrefs.SetInt(KEY_LEVELS_DONE, LevelsCompleted + 1);

            // ── Desbloqueo del siguiente nivel ────────────────────────────────
            int nextLevel = result.levelIndex + 1;
            if (nextLevel > MaxUnlockedLevel)
                PlayerPrefs.SetInt(KEY_MAX_LEVEL, nextLevel);
        }

        PlayerPrefs.Save();

        // ── Score global acumulado (suma de mejores puntajes de todos los niveles) ─
        // El leaderboard muestra el total acumulado, no el score de un único nivel.
        long cumulativeScore = GetTotalCumulativeScore();
        EvaluateHighScore(cumulativeScore);

        // ── Sincronizar todo con Supabase ─────────────────────────────────────
        if (SupabaseManager.Instance != null)
        {
            SupabaseManager.Instance.SubmitScore(LocalHighScore, Username, DeviceId);
            SupabaseManager.Instance.SyncPlayerProgress();
        }
        else
        {
            MarkScoreAsPending(LocalHighScore);
        }
    }

    // ─── Privados ────────────────────────────────────────────────────────────────
    private void EvaluateHighScore(long score)
    {
        long stored = PlayerPrefs.GetInt(KEY_HIGH_SCORE, 0);
        if (score > stored)
        {
            PlayerPrefs.SetInt(KEY_HIGH_SCORE, (int)score);
            PlayerPrefs.SetInt(KEY_NEEDS_SYNC, 1);
            PlayerPrefs.SetInt(KEY_PENDING_SCORE, (int)score);
            // NOTA: PlayerPrefs.Save() removido del hot-path.
            // Se persiste en OnApplicationPause / OnApplicationQuit.
            OnNewHighScore?.Invoke(score);
        }
    }

    // ─── Persistencia segura (solo al minimizar o cerrar) ────────────────────────
    // Elimina los tirones de disco durante el gameplay/minijuego.

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused) PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}



// B. Race Conditions (Condiciones de Carrera) en Singletons
// Archivos a editar: Entorno de Unity (Editor) y opcionalmente ScoreManager.cs

// Acción 1 (Recomendada - Editor): Ve a Edit > Project Settings > Script Execution Order. Añade los scripts AuthManager y SupabaseManager y asígnales un valor negativo (ej. -100) para que se ejecuten antes del Default Time. Asegúrate de que ScoreManager mantenga su tiempo por defecto o uno positivo para que siempre despierte después de la capa de red.

// Acción 2 (Arquitectura en Código): En ScoreManager.cs, si tienes lógica de sincronización en el Start() o en las primeras validaciones, asegúrate de envolverlas en una comprobación segura (if (SupabaseManager.Instance != null)). Para mayor robustez a futuro, puedes crear un evento OnNetworkReady en el SupabaseManager al que el ScoreManager se suscriba antes de enviar datos.


// Acción: Localiza el método EvaluateHighScore(...) y busca la línea donde llamas a PlayerPrefs.Save(). Bórrala de ese flujo de ejecución constante.

// Lógica: Agrega los métodos OnApplicationPause(bool isPaused) y OnApplicationQuit() en este mismo script. Traslada la llamada de PlayerPrefs.Save() dentro de estos métodos. De esta forma, el motor guardará los datos acumulados en la RAM física hacia el disco de almacenamiento únicamente cuando el jugador minimice la aplicación o la cierre, eliminando los tirones durante el minijuego.