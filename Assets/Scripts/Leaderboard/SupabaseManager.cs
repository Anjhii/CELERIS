using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ─── Estructuras de datos ────────────────────────────────────────────────────
[Serializable]
public class PlayerScore
{
    public int    posicion;
    public string username;
    public long   high_score;
}

[Serializable]
public class LeaderboardWrapper
{
    public PlayerScore[] players;
}

/// <summary>
/// SupabaseManager — Capa de red. Maneja toda la comunicación con Supabase.
/// Cubre:
///   A) Wake-up al arrancar (evita cold-start lento).
///   B) UPSERT de puntaje vía RPC (solo guarda si es récord más alto).
///   C) Cola offline: reintenta al detectar conexión.
///   D) Obtención del ranking Top-N.
/// </summary>
public class SupabaseManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────────────────────
    public static SupabaseManager Instance { get; private set; }

    // ─── Configuración (llenar en el Inspector) ───────────────────────────────
    [Header("Supabase Config")]
    [SerializeField] private string supabaseUrl    = "https://TU_PROYECTO.supabase.co";
    [SerializeField] private string supabaseAnonKey = "TU_ANON_KEY";

    [Header("Comportamiento")]
    [SerializeField] private int  topRankingsCount = 10;       // Cuántos puestos traer
    [SerializeField] private float retryInterval   = 30f;      // Segundos entre reintentos offline
    [SerializeField] private int  maxRetries       = 5;        // Reintentos máximos por sesión

    // ─── Eventos públicos ────────────────────────────────────────────────────
    public event Action                      OnDatabaseReady;          // Supabase despertó
    public event Action<bool>                OnScoreSubmitted;         // true = éxito
    public event Action<LeaderboardWrapper>  OnLeaderboardReceived;    // datos del ranking

    // ─── Estado interno ──────────────────────────────────────────────────────
    private bool   _isDatabaseReady = false;
    private bool   _isRetrying      = false;
    private int    _retryCount      = 0;

    // ─── Endpoints ───────────────────────────────────────────────────────────
    private string UrlSubmitScore  => $"{supabaseUrl}/rest/v1/rpc/submit_score";
    private string UrlGetTopScores => $"{supabaseUrl}/rest/v1/rpc/get_top_scores";
    private string UrlProfiles     => $"{supabaseUrl}/rest/v1/profiles";

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // CASO A: Despertar Supabase en segundo plano inmediatamente
        StartCoroutine(WakeUpRoutine());
    }

    // ─── CASO A: Wake-up ─────────────────────────────────────────────────────
    /// <summary>
    /// Envía una petición liviana al arrancar para que el servidor de Supabase
    /// salga del estado "dormido" (cold-start de plan gratuito).
    /// </summary>
    private IEnumerator WakeUpRoutine()
    {
        Debug.Log("[SupabaseManager] Despertando base de datos...");
        string jsonPayload = "{\"limit_num\": 1}";

        using (UnityWebRequest req = CreatePostRequest(UrlGetTopScores, jsonPayload))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                _isDatabaseReady = true;
                Debug.Log("[SupabaseManager] ✓ Supabase despierto y listo.");
                OnDatabaseReady?.Invoke();

                // CASO C: Si había puntaje pendiente de sesión anterior, enviarlo ahora
                CheckAndSyncPendingScore();
            }
            else
            {
                Debug.LogWarning($"[SupabaseManager] Wake-up falló ({req.error}). Iniciando reintentos...");
                if (!_isRetrying) StartCoroutine(RetryConnectionRoutine());
            }
        }
    }

    // ─── CASO C: Cola offline y reintentos ───────────────────────────────────
    private IEnumerator RetryConnectionRoutine()
    {
        _isRetrying = true;
        while (!_isDatabaseReady && _retryCount < maxRetries)
        {
            _retryCount++;
            Debug.Log($"[SupabaseManager] Reintento #{_retryCount} en {retryInterval}s...");
            yield return new WaitForSeconds(retryInterval);

            // Ping de reconexión
            using (UnityWebRequest req = CreatePostRequest(UrlGetTopScores, "{\"limit_num\": 1}"))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    _isDatabaseReady = true;
                    _isRetrying = false;
                    Debug.Log("[SupabaseManager] ✓ Reconexión exitosa.");
                    OnDatabaseReady?.Invoke();
                    CheckAndSyncPendingScore();
                    yield break;
                }
            }
        }

        if (!_isDatabaseReady)
            Debug.LogWarning("[SupabaseManager] Se agotaron los reintentos. El puntaje permanece en cola offline.");

        _isRetrying = false;
    }

    private void CheckAndSyncPendingScore()
    {
        if (ScoreManager.Instance != null && ScoreManager.Instance.HasPendingSync)
        {
            long pending   = ScoreManager.Instance.PendingScore;
            string uid     = ScoreManager.Instance.DeviceId;
            string uname   = ScoreManager.Instance.Username;

            Debug.Log($"[SupabaseManager] Puntaje offline pendiente detectado: {pending}. Sincronizando...");
            StartCoroutine(SubmitScoreRoutine(uid, uname, pending));
        }
    }

    // ─── CASO B: Envío de puntaje ─────────────────────────────────────────────
    /// <summary>
    /// Envía el puntaje a Supabase. Usa el userId del usuario autenticado automáticamente.
    /// Si no hay conexión, lo deja marcado en PlayerPrefs para sincronizar más tarde (Caso C).
    /// </summary>
    public void SubmitScore(long score, string username = null, string userId = null)
    {
        string uid   = userId   ?? GetUserId();
        string uname = username ?? GetUsername();

        if (_isDatabaseReady)
        {
            StartCoroutine(SubmitScoreRoutine(uid, uname, score));
        }
        else
        {
            // Sin conexión: persiste localmente
            ScoreManager.Instance?.MarkScoreAsPending(score);
            Debug.LogWarning("[SupabaseManager] Sin conexión. Puntaje guardado offline.");

            if (!_isRetrying) StartCoroutine(RetryConnectionRoutine());
        }
    }

    private IEnumerator SubmitScoreRoutine(string userId, string username, long score)
    {
        string json = $"{{\"p_user_id\":\"{userId}\",\"p_username\":\"{EscapeJson(username)}\",\"p_score\":{score}}}";

        using (UnityWebRequest req = CreatePostRequest(UrlSubmitScore, json))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[SupabaseManager] ✓ Puntaje {score} sincronizado correctamente.");
                ScoreManager.Instance?.ClearPendingSync();
                OnScoreSubmitted?.Invoke(true);
            }
            else
            {
                Debug.LogWarning($"[SupabaseManager] Error al enviar puntaje: {req.error}. Queda en cola offline.");
                ScoreManager.Instance?.MarkScoreAsPending(score);
                OnScoreSubmitted?.Invoke(false);

                if (!_isRetrying) StartCoroutine(RetryConnectionRoutine());
            }
        }
    }

    // ─── CASO E: Sincronizar progreso CELERIS ────────────────────────────────
    /// <summary>
    /// Actualiza los campos de progreso del jugador autenticado en la tabla profiles.
    /// Llama automáticamente a este método desde ScoreManager.RecordLevelResult().
    /// Solo ejecuta si el usuario está logueado y la DB está lista.
    /// </summary>
    public void SyncPlayerProgress()
    {
        if (!_isDatabaseReady)
        {
            Debug.Log("[SupabaseManager] DB no lista, progreso queda en PlayerPrefs.");
            return;
        }
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) return;
        if (ScoreManager.Instance == null) return;

        StartCoroutine(SyncProgressRoutine());
    }

    private IEnumerator SyncProgressRoutine()
    {
        string userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) yield break;

        var sm = ScoreManager.Instance;

        // PATCH sobre la fila cuyo id coincide con el UUID del usuario autenticado.
        // No toca high_score: eso lo maneja el RPC submit_score con lógica "solo sube".
        string url  = $"{UrlProfiles}?id=eq.{userId}";
        string json = $"{{" +
            $"\"max_unlocked_level\":{sm.MaxUnlockedLevel}," +
            $"\"levels_completed\":{sm.LevelsCompleted}," +
            $"\"times_played\":{sm.TimesPlayed}," +
            $"\"total_stars\":{sm.TotalStars}" +
            $"}}";

        UnityWebRequest req = new UnityWebRequest(url, "PATCH");
        byte[] raw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type",  "application/json");
        req.SetRequestHeader("apikey",        supabaseAnonKey);
        req.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.AccessToken}");
        req.SetRequestHeader("Prefer",        "return=minimal");   // no necesitamos la respuesta

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("[SupabaseManager] ✓ Progreso del jugador sincronizado.");
        else
            Debug.LogWarning($"[SupabaseManager] Error sincronizando progreso: {req.error}");
    }

    // ─── CASO D: Obtener ranking ──────────────────────────────────────────────
    /// <summary>Solicita el Top-N al servidor y lo entrega vía callback y evento.</summary>
    public void FetchLeaderboard(Action<LeaderboardWrapper> callback = null, int limit = -1)
    {
        int count = limit > 0 ? limit : topRankingsCount;
        StartCoroutine(FetchLeaderboardRoutine(callback, count));
    }

    private IEnumerator FetchLeaderboardRoutine(Action<LeaderboardWrapper> callback, int limit)
    {
        string json = $"{{\"limit_num\":{limit}}}";

        using (UnityWebRequest req = CreatePostRequest(UrlGetTopScores, json))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string raw     = req.downloadHandler.text;
                string wrapped = "{\"players\":" + raw + "}";

                LeaderboardWrapper data = JsonUtility.FromJson<LeaderboardWrapper>(wrapped);
                Debug.Log($"[SupabaseManager] ✓ Ranking recibido: {data?.players?.Length ?? 0} entradas.");

                OnLeaderboardReceived?.Invoke(data);
                callback?.Invoke(data);
            }
            else
            {
                Debug.LogError($"[SupabaseManager] Error al obtener ranking: {req.error}");
                callback?.Invoke(null);
            }
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private UnityWebRequest CreatePostRequest(string url, string jsonBody)
    {
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        byte[] raw = Encoding.UTF8.GetBytes(jsonBody);

        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("apikey",       supabaseAnonKey);

        // Usar JWT del usuario autenticado si está disponible; si no, anon key
        string token = (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
            ? AuthManager.Instance.AccessToken
            : supabaseAnonKey;
        req.SetRequestHeader("Authorization", $"Bearer {token}");

        return req;
    }

    // ─── Obtener userId correcto (Auth UUID tiene prioridad sobre DeviceID) ───
    private string GetUserId()
    {
        if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn &&
            AuthManager.Instance.CurrentUser != null)
            return AuthManager.Instance.CurrentUser.id;

        return ScoreManager.Instance?.DeviceId ?? System.Guid.NewGuid().ToString();
    }

    private string GetUsername()
    {
        if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn &&
            !string.IsNullOrEmpty(AuthManager.Instance.Username))
            return AuthManager.Instance.Username;

        return ScoreManager.Instance?.Username ?? "Jugador";
    }

    /// <summary>Escapa caracteres especiales en strings para JSON seguro.</summary>
    private string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "");
}
