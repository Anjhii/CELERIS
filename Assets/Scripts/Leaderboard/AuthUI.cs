using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Celeris.Leaderboard
{

    /// <summary>
    /// AuthUI — Controla la pantalla de Login y Registro en Unity.
    ///
    /// Estructura sugerida de Canvas:
    ///
    ///  AuthCanvas
    ///  ├── PanelLogin
    ///  │   ├── TxtTitle         (TextMeshProUGUI — "Iniciar sesión")
    ///  │   ├── InputEmail       (TMP_InputField)
    ///  │   ├── InputPassword    (TMP_InputField — ContentType: Password)
    ///  │   ├── BtnLogin         (Button)
    ///  │   ├── BtnGoRegister    (Button — "¿No tienes cuenta? Regístrate")
    ///  │   └── TxtError         (TextMeshProUGUI — oculto por defecto)
    ///  │
    ///  ├── PanelRegister
    ///  │   ├── TxtTitle         (TextMeshProUGUI — "Crear cuenta")
    ///  │   ├── InputUsername    (TMP_InputField)
    ///  │   ├── InputEmail       (TMP_InputField)
    ///  │   ├── InputPassword    (TMP_InputField — ContentType: Password)
    ///  │   ├── InputConfirm     (TMP_InputField — ContentType: Password)
    ///  │   ├── BtnRegister      (Button)
    ///  │   ├── BtnGoLogin       (Button — "¿Ya tienes cuenta? Inicia sesión")
    ///  │   └── TxtError         (TextMeshProUGUI — oculto por defecto)
    ///  │
    ///  └── PanelLoading         (Panel con spinner — cubre la UI durante requests)
    /// </summary>
    public class AuthUI : MonoBehaviour
    {
        // ─── Paneles ──────────────────────────────────────────────────────────────
        [Header("Paneles")]
        [SerializeField] private GameObject panelLogin;
        [SerializeField] private GameObject panelRegister;
        [SerializeField] private GameObject panelLoading;

        // ─── Login ────────────────────────────────────────────────────────────────
        [Header("Login")]
        [SerializeField] private TMP_InputField inputLoginEmail;
        [SerializeField] private TMP_InputField inputLoginPassword;
        [SerializeField] private Button         btnLogin;
        [SerializeField] private Button         btnGoRegister;
        [SerializeField] private TextMeshProUGUI txtLoginError;

        // ─── Registro ─────────────────────────────────────────────────────────────
        [Header("Registro")]
        [SerializeField] private TMP_InputField inputRegisterUsername;
        [SerializeField] private TMP_InputField inputRegisterEmail;
        [SerializeField] private TMP_InputField inputRegisterPassword;
        [SerializeField] private TMP_InputField inputRegisterConfirm;
        [SerializeField] private Button         btnRegister;
        [SerializeField] private Button         btnGoLogin;
        [SerializeField] private TextMeshProUGUI txtRegisterError;

        // ─── Loading ──────────────────────────────────────────────────────────────
        [Header("Loading")]
        [SerializeField] private TextMeshProUGUI txtLoadingMessage;

        // ─── Navegación post-auth ─────────────────────────────────────────────────
        [Header("Escena a cargar al autenticarse")]
        [SerializeField] private string sceneAfterAuth = "MainMenuScene";

        // ─────────────────────────────────────────────────────────────────────────
        private void Start()
        {
            // Si ya hay sesión activa, saltar directo al juego
            if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
            {
                GoToGame();
                return;
            }

            ShowLogin();
            WireButtons();
        }

        private void OnDestroy()
        {
            // Evitar leaks de listeners cuando la escena se descarga.
            btnLogin?.onClick.RemoveAllListeners();
            btnGoRegister?.onClick.RemoveAllListeners();
            btnRegister?.onClick.RemoveAllListeners();
            btnGoLogin?.onClick.RemoveAllListeners();
        }

        // ─── Conexión de botones ──────────────────────────────────────────────────
        private void WireButtons()
        {
            btnLogin?.onClick.AddListener(OnLoginClicked);
            btnGoRegister?.onClick.AddListener(ShowRegister);

            btnRegister?.onClick.AddListener(OnRegisterClicked);
            btnGoLogin?.onClick.AddListener(ShowLogin);
        }

        // ─── Acciones de botón ────────────────────────────────────────────────────
        private void OnLoginClicked()
        {
            string email    = inputLoginEmail?.text.Trim() ?? "";
            string password = inputLoginPassword?.text ?? "";

            SetError(txtLoginError, "");
            SetLoading(true, "Iniciando sesión...");

            AuthManager.Instance.SignIn(email, password, result =>
            {
                SetLoading(false);

                if (result.Success)
                {
                    GoToGame();
                }
                else
                {
                    SetError(txtLoginError, result.ErrorMessage);
                    ShakePanel(panelLogin);
                }
            });
        }

        private void OnRegisterClicked()
        {
            string username  = inputRegisterUsername?.text.Trim() ?? "";
            string email     = inputRegisterEmail?.text.Trim() ?? "";
            string password  = inputRegisterPassword?.text ?? "";
            string confirm   = inputRegisterConfirm?.text ?? "";

            SetError(txtRegisterError, "");

            // Validación local de contraseñas
            if (password != confirm)
            {
                SetError(txtRegisterError, "Las contraseñas no coinciden.");
                ShakePanel(panelRegister);
                return;
            }

            SetLoading(true, "Creando cuenta...");

            AuthManager.Instance.SignUp(email, password, username, result =>
            {
                SetLoading(false);

                if (result.Success)
                {
                    GoToGame();
                }
                else
                {
                    SetError(txtRegisterError, result.ErrorMessage);
                    ShakePanel(panelRegister);
                }
            });
        }

        // ─── Navegación de paneles ────────────────────────────────────────────────
        private void ShowLogin()
        {
            panelLogin?.SetActive(true);
            panelRegister?.SetActive(false);
            SetError(txtLoginError, "");
            inputLoginPassword?.SetTextWithoutNotify("");
        }

        private void ShowRegister()
        {
            panelRegister?.SetActive(true);
            panelLogin?.SetActive(false);
            SetError(txtRegisterError, "");
            inputRegisterPassword?.SetTextWithoutNotify("");
            inputRegisterConfirm?.SetTextWithoutNotify("");
        }

        // ─── Ir al juego ──────────────────────────────────────────────────────────
        private void GoToGame()
        {
            Debug.Log($"[AuthUI] Autenticado como {AuthManager.Instance?.Username}. Cargando {sceneAfterAuth}...");
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneAfterAuth);
        }

        // ─── Helpers de UI ────────────────────────────────────────────────────────
        private void SetLoading(bool show, string message = "")
        {
            if (panelLoading) panelLoading.SetActive(show);
            if (txtLoadingMessage) txtLoadingMessage.text = message;

            // Deshabilitar botones mientras carga para evitar doble click
            if (btnLogin)     btnLogin.interactable     = !show;
            if (btnRegister)  btnRegister.interactable  = !show;
            if (btnGoLogin)   btnGoLogin.interactable   = !show;
            if (btnGoRegister) btnGoRegister.interactable = !show;
        }

        private void SetError(TextMeshProUGUI label, string message)
        {
            if (label == null) return;
            label.text = message;
            label.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        // Sacude el panel con una pequeña animación para feedback de error
        private void ShakePanel(GameObject panel)
        {
            if (panel == null) return;
            StartCoroutine(ShakeRoutine(panel.GetComponent<RectTransform>()));
        }

        private IEnumerator ShakeRoutine(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector3 origin = rt.localPosition;
            float[] offsets = { -8f, 8f, -6f, 6f, -4f, 4f, 0f };

            foreach (float x in offsets)
            {
                rt.localPosition = origin + new Vector3(x, 0, 0);
                yield return new WaitForSeconds(0.04f);
            }

            rt.localPosition = origin;
        }
    }

}
