using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ─── Modelo de respuesta de Auth ─────────────────────────────────────────────
[Serializable]
public class AuthUser
{
    public string id;           // UUID del usuario en Supabase
    public string email;
    public string created_at;
}

[Serializable]
public class AuthSession
{
    public string access_token;
    public string refresh_token;
    public string token_type;
    public int    expires_in;
    public AuthUser user;
}

[Serializable]
public class AuthError
{
    public string error;
    public string error_description;
    public string msg;          // Supabase a veces usa "msg"
    public string message;      // ...y a veces "message"

    public string GetMessage() =>
        !string.IsNullOrEmpty(error_description) ? error_description :
        !string.IsNullOrEmpty(msg)               ? msg               :
        !string.IsNullOrEmpty(message)           ? message           :
        !string.IsNullOrEmpty(error)             ? error             : "Error desconocido";
}

// ─── Resultado unificado ──────────────────────────────────────────────────────
public class AuthResult
{
    public bool        Success;
    public AuthSession Session;     // Solo en login/register exitosos
    public string      ErrorMessage;
}

/// <summary>
/// AuthManager — Registro e inicio de sesión con Supabase Auth (email + contraseña).
/// Sin confirmación de correo. Persiste la sesión entre sesiones del juego.
///
/// Uso:
///   AuthManager.Instance.SignUp(email, password, username, callback)
///   AuthManager.Instance.SignIn(email, password, callback)
///   AuthManager.Instance.SignOut()
///   AuthManager.Instance.IsLoggedIn  →  bool
///   AuthManager.Instance.CurrentUser →  AuthUser
/// </summary>
public class AuthManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static AuthManager Instance { get; private set; }

    // ─── Config (llenar en el Inspector) ─────────────────────────────────────
    [Header("Supabase Config (mismo que SupabaseManager)")]
    [SerializeField] private string supabaseUrl     = "https://TU_PROYECTO.supabase.co";
    [SerializeField] private string supabaseAnonKey = "TU_ANON_KEY";

    // ─── Claves PlayerPrefs ───────────────────────────────────────────────────
    private const string KEY_ACCESS_TOKEN   = "Auth_AccessToken";
    private const string KEY_REFRESH_TOKEN  = "Auth_RefreshToken";
    private const string KEY_USER_ID        = "Auth_UserId";
    private const string KEY_USER_EMAIL     = "Auth_UserEmail";
    private const string KEY_USERNAME       = "Auth_Username";

    // ─── Estado público ───────────────────────────────────────────────────────
    public bool     IsLoggedIn    { get; private set; }
    public AuthUser CurrentUser   { get; private set; }
    public string   AccessToken   { get; private set; }
    public string   Username      { get; private set; }

    // ─── Eventos ──────────────────────────────────────────────────────────────
    public event Action<AuthUser> OnLoginSuccess;
    public event Action           OnLogout;

    // ─── Endpoints ───────────────────────────────────────────────────────────
    private string UrlSignUp     => $"{supabaseUrl}/auth/v1/signup";
    private string UrlSignIn     => $"{supabaseUrl}/auth/v1/token?grant_type=password";
    private string UrlSignOut    => $"{supabaseUrl}/auth/v1/logout";
    private string UrlGetUser    => $"{supabaseUrl}/auth/v1/user";
    private string UrlRefresh    => $"{supabaseUrl}/auth/v1/token?grant_type=refresh_token";
    private string UrlProfiles   => $"{supabaseUrl}/rest/v1/profiles";

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        TryRestoreSession();
    }

    // ─── REGISTRO ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Crea una cuenta nueva. Verifica que el email no esté registrado.
    /// El username se guarda en la tabla `profiles`.
    /// </summary>
    public void SignUp(string email, string password, string username, Action<AuthResult> callback)
    {
        if (!ValidateInputs(email, password, username, callback)) return;
        StartCoroutine(SignUpRoutine(email.Trim().ToLower(), password, username.Trim(), callback));
    }

    private IEnumerator SignUpRoutine(string email, string password, string username, Action<AuthResult> callback)
    {
        // 1) Registrar en Supabase Auth
        string json = $"{{\"email\":\"{EscapeJson(email)}\",\"password\":\"{EscapeJson(password)}\"}}";

        using (UnityWebRequest req = CreateAuthRequest(UrlSignUp, json))
        {
            yield return req.SendWebRequest();

            string body = req.downloadHandler.text;

            if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 400)
            {
                string msg = ParseErrorMessage(body);

                // Detectar usuario duplicado (email ya registrado)
                if (msg.ToLower().Contains("already registered") ||
                    msg.ToLower().Contains("user already") ||
                    req.responseCode == 422)
                {
                    callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "Este correo ya está registrado." });
                }
                else
                {
                    callback?.Invoke(new AuthResult { Success = false, ErrorMessage = msg });
                }
                yield break;
            }

            AuthSession session = JsonUtility.FromJson<AuthSession>(body);

            if (session?.user == null || string.IsNullOrEmpty(session.access_token))
            {
                // Supabase crea el usuario pero no devuelve token cuando la
                // confirmación de correo está activa. Detectarlo para dar
                // un mensaje útil en lugar de "Respuesta inválida".
                bool pendingConfirmation = body.Contains("confirmation_sent_at") ||
                                           body.Contains("\"confirmed_at\":null");
                string errMsg = pendingConfirmation
                    ? "Cuenta creada. Confirma tu correo antes de iniciar sesión."
                    : "Respuesta inválida del servidor.";

                callback?.Invoke(new AuthResult { Success = false, ErrorMessage = errMsg });
                yield break;
            }

            // 2) Guardar perfil con username + campos de progreso en tabla `profiles`
            yield return StartCoroutine(UpsertProfileRoutine(session.user.id, username, session.access_token));

            // 3) Persistir sesión y notificar
            ApplySession(session, username);
            callback?.Invoke(new AuthResult { Success = true, Session = session });
            Debug.Log($"[AuthManager] ✓ Cuenta creada: {email} | username: {username}");
        }
    }

    // ─── LOGIN ────────────────────────────────────────────────────────────────
    /// <summary>Inicia sesión con email y contraseña.</summary>
    public void SignIn(string email, string password, Action<AuthResult> callback)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "Completa todos los campos." });
            return;
        }
        StartCoroutine(SignInRoutine(email.Trim().ToLower(), password, callback));
    }

    private IEnumerator SignInRoutine(string email, string password, Action<AuthResult> callback)
    {
        string json = $"{{\"email\":\"{EscapeJson(email)}\",\"password\":\"{EscapeJson(password)}\"}}";

        using (UnityWebRequest req = CreateAuthRequest(UrlSignIn, json))
        {
            yield return req.SendWebRequest();

            string body = req.downloadHandler.text;

            if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 400)
            {
                string msg = ParseErrorMessage(body);

                if (msg.ToLower().Contains("invalid") || req.responseCode == 400)
                    callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "Correo o contraseña incorrectos." });
                else
                    callback?.Invoke(new AuthResult { Success = false, ErrorMessage = msg });

                yield break;
            }

            AuthSession session = JsonUtility.FromJson<AuthSession>(body);

            if (session?.user == null || string.IsNullOrEmpty(session.access_token))
            {
                callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "Respuesta inválida del servidor." });
                yield break;
            }

            // Obtener username del perfil guardado
            yield return StartCoroutine(FetchUsernameRoutine(session.user.id, session.access_token,
                fetchedUsername =>
                {
                    ApplySession(session, fetchedUsername ?? session.user.email);
                    callback?.Invoke(new AuthResult { Success = true, Session = session });
                    Debug.Log($"[AuthManager] ✓ Sesión iniciada: {email}");
                }
            ));
        }
    }

    // ─── LOGOUT ───────────────────────────────────────────────────────────────
    public void SignOut()
    {
        StartCoroutine(SignOutRoutine());
    }

    private IEnumerator SignOutRoutine()
    {
        if (!string.IsNullOrEmpty(AccessToken))
        {
            UnityWebRequest req = new UnityWebRequest(UrlSignOut, "POST");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("apikey",        supabaseAnonKey);
            req.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            yield return req.SendWebRequest();
        }

        ClearSession();
        OnLogout?.Invoke();
        Debug.Log("[AuthManager] Sesión cerrada.");
    }

    // ─── REFRESH DE TOKEN ─────────────────────────────────────────────────────
    /// <summary>Renueva el token de acceso usando el refresh token guardado.</summary>
    public void RefreshSession(Action<bool> callback = null)
    {
        string refreshToken = PlayerPrefs.GetString(KEY_REFRESH_TOKEN, "");
        if (string.IsNullOrEmpty(refreshToken)) { callback?.Invoke(false); return; }
        StartCoroutine(RefreshRoutine(refreshToken, callback));
    }

    private IEnumerator RefreshRoutine(string refreshToken, Action<bool> callback)
    {
        string json = $"{{\"refresh_token\":\"{refreshToken}\"}}";

        using (UnityWebRequest req = CreateAuthRequest(UrlRefresh, json))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && req.responseCode < 400)
            {
                AuthSession session = JsonUtility.FromJson<AuthSession>(req.downloadHandler.text);
                if (session?.access_token != null)
                {
                    string savedUsername = PlayerPrefs.GetString(KEY_USERNAME, "");
                    ApplySession(session, savedUsername);
                    callback?.Invoke(true);
                    Debug.Log("[AuthManager] ✓ Token renovado.");
                    yield break;
                }
            }

            // Refresh falló: limpiar sesión
            ClearSession();
            callback?.Invoke(false);
        }
    }

    // ─── PERFIL en tabla `profiles` ───────────────────────────────────────────
    private IEnumerator UpsertProfileRoutine(string userId, string username, string token)
    {
        // Perfil inicial: incluye todos los campos de progreso de CELERIS.
        // Supabase ignorará columnas que no existan; añadirlas con el SQL de FASE 2.
        string json = $"[{{" +
            $"\"id\":\"{userId}\"," +
            $"\"username\":\"{EscapeJson(username)}\"," +
            $"\"high_score\":0," +
            $"\"max_unlocked_level\":0," +
            $"\"levels_completed\":0," +
            $"\"times_played\":0," +
            $"\"total_stars\":0" +
            $"}}]";

        UnityWebRequest req = new UnityWebRequest(UrlProfiles, "POST");
        byte[] raw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();

        req.SetRequestHeader("Content-Type",  "application/json");
        req.SetRequestHeader("apikey",        supabaseAnonKey);
        req.SetRequestHeader("Authorization", $"Bearer {token}");
        req.SetRequestHeader("Prefer",        "resolution=merge-duplicates"); // UPSERT

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[AuthManager] ✓ Perfil guardado para {username}");
        else
            Debug.LogWarning($"[AuthManager] Perfil no guardado: {req.error}");
    }

    private IEnumerator FetchUsernameRoutine(string userId, string token, Action<string> callback)
    {
        string url = $"{UrlProfiles}?id=eq.{userId}&select=username";

        UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("apikey",        supabaseAnonKey);
        req.SetRequestHeader("Authorization", $"Bearer {token}");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            // Respuesta: [{"username":"Valor"}]
            string raw = req.downloadHandler.text;
            string parsed = ExtractJsonField(raw, "username");
            callback?.Invoke(parsed);
        }
        else
        {
            callback?.Invoke(null);
        }
    }

    // ─── Restaurar sesión guardada ────────────────────────────────────────────
    private void TryRestoreSession()
    {
        string token   = PlayerPrefs.GetString(KEY_ACCESS_TOKEN, "");
        string userId  = PlayerPrefs.GetString(KEY_USER_ID, "");
        string email   = PlayerPrefs.GetString(KEY_USER_EMAIL, "");
        string uname   = PlayerPrefs.GetString(KEY_USERNAME, "");

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId)) return;

        // Restaurar estado en memoria (el token puede estar expirado; se renueva con RefreshSession si falla)
        AccessToken = token;
        Username    = uname;
        IsLoggedIn  = true;
        CurrentUser = new AuthUser { id = userId, email = email };

        // Notificar al SupabaseManager y ScoreManager si ya están listos
        OnLoginSuccess?.Invoke(CurrentUser);

        // Intentar refrescar token en segundo plano
        RefreshSession(ok =>
        {
            if (!ok) Debug.Log("[AuthManager] Sesión expirada, se requiere nuevo login.");
        });

        Debug.Log($"[AuthManager] Sesión restaurada: {email}");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private void ApplySession(AuthSession session, string username)
    {
        AccessToken = session.access_token;
        Username    = username;
        IsLoggedIn  = true;
        CurrentUser = session.user;

        // Persistir
        PlayerPrefs.SetString(KEY_ACCESS_TOKEN,  session.access_token);
        PlayerPrefs.SetString(KEY_REFRESH_TOKEN, session.refresh_token ?? "");
        PlayerPrefs.SetString(KEY_USER_ID,       session.user.id);
        PlayerPrefs.SetString(KEY_USER_EMAIL,    session.user.email);
        PlayerPrefs.SetString(KEY_USERNAME,      username);
        PlayerPrefs.Save();

        // Sincronizar con ScoreManager
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.Username = username;

        OnLoginSuccess?.Invoke(session.user);
    }

    private void ClearSession()
    {
        AccessToken = null;
        Username    = null;
        IsLoggedIn  = false;
        CurrentUser = null;

        PlayerPrefs.DeleteKey(KEY_ACCESS_TOKEN);
        PlayerPrefs.DeleteKey(KEY_REFRESH_TOKEN);
        PlayerPrefs.DeleteKey(KEY_USER_ID);
        PlayerPrefs.DeleteKey(KEY_USER_EMAIL);
        PlayerPrefs.DeleteKey(KEY_USERNAME);
        PlayerPrefs.Save();
    }

    private bool ValidateInputs(string email, string password, string username, Action<AuthResult> callback)
    {
        if (string.IsNullOrWhiteSpace(email))    { callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "El correo es requerido." });      return false; }
        if (!email.Contains("@"))                { callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "Correo inválido." });             return false; }
        if (string.IsNullOrWhiteSpace(password)) { callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "La contraseña es requerida." }); return false; }
        if (password.Length < 6)                 { callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "La contraseña debe tener al menos 6 caracteres." }); return false; }
        if (string.IsNullOrWhiteSpace(username)) { callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "El nombre de jugador es requerido." }); return false; }
        return true;
    }

    private UnityWebRequest CreateAuthRequest(string url, string jsonBody)
    {
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("apikey",       supabaseAnonKey);
        return req;
    }

    private string ParseErrorMessage(string json)
    {
        try
        {
            AuthError err = JsonUtility.FromJson<AuthError>(json);
            return err?.GetMessage() ?? "Error desconocido";
        }
        catch { return "Error al procesar respuesta."; }
    }

    // Mini-parser sin dependencias: extrae "campo":"valor" del primer objeto del array JSON
    private string ExtractJsonField(string json, string field)
    {
        string search = $"\"{field}\":\"";
        int start = json.IndexOf(search, StringComparison.Ordinal);
        if (start < 0) return null;
        start += search.Length;
        int end = json.IndexOf('"', start);
        return end < 0 ? null : json.Substring(start, end - start);
    }

    private string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "");
}
