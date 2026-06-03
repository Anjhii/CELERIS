// ============================================================
// GameOverController.cs  |  Assets/Scripts/UI/
//
// Controlador de Game Over polimórfico y accesible desde
// cualquier escena (Gameplay o Minigame).
//
// ARQUITECTURA:
//   Singleton DontDestroyOnLoad que construye su propio Canvas
//   en runtime. No requiere prefabs ni escena dedicada.
//   Cualquier sistema llama a GameOverController.Show(reason)
//   y el panel aparece encima de la escena activa.
//
// SETUP:
//   Añadir a LoginScene junto a LevelManager y GameStateManager.
//   Requiere TextMeshPro (ya incluido en el proyecto).
//
// USO:
//   GameOverController.Show(GameOverReason.Battery)
//   GameOverController.Show(GameOverReason.Laser)
//   GameOverController.Show(GameOverReason.Custom, "Mensaje")
//   GameOverController.Hide()   ← solo si necesitas ocultarlo manualmente
//
// BOTONES:
//   Reintentar → LevelManager.RetryCurrentLevel()
//   Menú       → MainMenuScene
// ============================================================
using Celeris.Core;
using Celeris.Data;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Celeris.UI
{
    public enum GameOverReason
    {
        Battery,
        Laser,
        Fall,
        MinigameFail,
        Custom
    }

    public class GameOverController : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static GameOverController Instance { get; private set; }

        // ── Referencias UI (construidas en Awake) ─────────────
        private Canvas          _canvas;
        private GameObject      _panel;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _reasonText;
        private Button          _retryButton;
        private Button          _menuButton;

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

            BuildUI();
            HidePanel();

            // Escuchar cambios de escena para re-suscribir eventos
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Re-suscribir al DroideController de la nueva escena
            SubscribeToDroide();
        }

        private void Start()
        {
            SubscribeToDroide();
        }

        // ── Suscripción al Droide ─────────────────────────────
        private void SubscribeToDroide()
        {
            var droide = FindObjectOfType<Player.DroideController>();
            if (droide == null) return;

            droide.OnStateChanged -= HandleDroideStateChanged;   // evitar duplicados
            droide.OnStateChanged += HandleDroideStateChanged;
        }

        private void HandleDroideStateChanged(DroideState state)
        {
            if (state != DroideState.Dead) return;

            var droide = FindObjectOfType<Player.DroideController>();
            if (droide == null) return;

            GameOverReason reason = droide.LastDeathCause switch
            {
                DeathCause.Battery => GameOverReason.Battery,
                DeathCause.Laser   => GameOverReason.Laser,
                DeathCause.Fall    => GameOverReason.Fall,
                _                  => GameOverReason.Custom
            };

            Show(reason);
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>Muestra el panel de Game Over.</summary>
        public static void Show(GameOverReason reason, string customMessage = null)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[GameOverController] Instancia no encontrada.");
                return;
            }
            Instance.ShowPanel(reason, customMessage);
        }

        /// <summary>Oculta el panel sin navegar a ningún lado.</summary>
        public static void Hide()
        {
            Instance?.HidePanel();
        }

        // ── Lógica de panel ───────────────────────────────────
        private void ShowPanel(GameOverReason reason, string customMessage)
        {
            if (_reasonText != null)
                _reasonText.text = BuildReasonText(reason, customMessage);

            _panel?.SetActive(true);

            // Pausar el juego mientras el panel está visible
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.Pause();
        }

        private void HidePanel()
        {
            _panel?.SetActive(false);
        }

        private static string BuildReasonText(GameOverReason reason, string custom) =>
            reason switch
            {
                GameOverReason.Battery     => "Sin energía",
                GameOverReason.Laser       => "Alcanzado por el láser",
                GameOverReason.Fall        => "Caída al vacío",
                GameOverReason.MinigameFail => "Fallo en el minijuego",
                GameOverReason.Custom      => custom ?? "Error desconocido",
                _                          => ""
            };

        // ── Acciones de botón ─────────────────────────────────
        private void OnRetry()
        {
            HidePanel();
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.Resume();
            LevelManager.EnsureExists().RetryCurrentLevel();
        }

        private void OnMenu()
        {
            HidePanel();
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.Resume();
            SceneManager.LoadScene("MainMenuScene");
        }

        // ── Construcción de UI en runtime ─────────────────────
        private void BuildUI()
        {
            // Canvas raíz
            var canvasGo = new GameObject("GameOverCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;   // siempre encima

            canvasGo.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel semi-transparente
            _panel = CreatePanel(canvasGo.transform,
                new Color(0f, 0f, 0f, 0.82f));

            // Título
            _titleText = CreateText(_panel.transform, "GAME OVER",
                fontSize: 52, bold: true,
                anchorMin: new Vector2(0.1f, 0.62f), anchorMax: new Vector2(0.9f, 0.80f));
            _titleText.color = new Color(1f, 0.25f, 0.20f);

            // Razón
            _reasonText = CreateText(_panel.transform, "",
                fontSize: 28, bold: false,
                anchorMin: new Vector2(0.1f, 0.50f), anchorMax: new Vector2(0.9f, 0.62f));
            _reasonText.color = new Color(0.9f, 0.9f, 0.9f);

            // Botón Reintentar
            _retryButton = CreateButton(_panel.transform, "REINTENTAR",
                anchorMin: new Vector2(0.20f, 0.30f), anchorMax: new Vector2(0.48f, 0.46f),
                bgColor: new Color(0.15f, 0.60f, 1f),
                onClick: OnRetry);

            // Botón Menú
            _menuButton = CreateButton(_panel.transform, "MENÚ PRINCIPAL",
                anchorMin: new Vector2(0.52f, 0.30f), anchorMax: new Vector2(0.80f, 0.46f),
                bgColor: new Color(0.35f, 0.35f, 0.35f),
                onClick: OnMenu);
        }

        // ── Helpers de UI ─────────────────────────────────────
        private static GameObject CreatePanel(Transform parent, Color color)
        {
            var go  = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string text,
            int fontSize, bool bold, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp    = go.AddComponent<TextMeshProUGUI>();
            tmp.text   = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            var rt     = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return tmp;
        }

        private Button CreateButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax,
            Color bgColor, System.Action onClick)
        {
            var go  = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            var cb  = new Button.ButtonClickedEvent();
            cb.AddListener(() => onClick());
            btn.onClick = cb;

            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Texto del botón
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var tmp    = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text   = label;
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color  = Color.white;
            var trt    = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            return btn;
        }
    }
}
