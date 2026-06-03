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

            // DeathCause.Generic = fallo en el minijuego (3 intentos agotados).
            GameOverReason reason = droide.LastDeathCause switch
            {
                DeathCause.Battery => GameOverReason.Battery,
                DeathCause.Laser   => GameOverReason.Laser,
                DeathCause.Fall    => GameOverReason.Fall,
                DeathCause.Generic => GameOverReason.MinigameFail,
                _                  => GameOverReason.Custom
            };

            Show(reason);
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Garantiza que el singleton existe aunque LoginScene no se haya cargado.
        /// Llamado por GameFlowManager.EnsureSingletons().
        /// </summary>
        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("[GameOverController-Auto]");
            go.AddComponent<GameOverController>();
            Debug.Log("[GameOverController] Instancia auto-creada por GameFlowManager.");
        }

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
                GameOverReason.Battery     => "CONEXIÓN: <color=#9629A5>BATERÍA AGOTADA</color>", // Morado
                GameOverReason.Laser       => "NÚCLEO: <color=#FF001A>DESTRUIDO POR LÁSER</color>", // Rojo
                GameOverReason.Fall        => "ALERTA: <color=#FF001A>IMPACTO POR CAÍDA</color>", // Rojo
                GameOverReason.MinigameFail => "STATE: <color=#FF001A>ACCESS DENIED</color>", // El "Fallo en el juego" pedido en Rojo Puro
                GameOverReason.Custom      => custom ?? "ERROR: <color=#9629A5>CÓDIGO TERMINAL CORRUPTO</color>", // Morado
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
        // ── Construcción de UI en runtime (Estilo Ciberpunk Mejorado) ──
        // ── Construcción de UI: Edición Dark Ciberpunk (Negro, Morado y Rojo) ──

        // ── Construcción de UI Minimalista Sin Fondos Rojos ──
        private void BuildUI()
        {
            // 1. Canvas raíz
            var canvasGo = new GameObject("GameOverCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999; 

            canvasGo.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 2. Fondo Completo de la Pantalla (Negro absoluto muy elegante)
            var background = CreatePanel(canvasGo.transform, new Color(0.02f, 0.0f, 0.03f, 0.96f));

            // 3. Contenedor Central (Completamente transparente para evitar cualquier cuadrado de fondo)
            var frameGo = new GameObject("CentralFrame");
            frameGo.transform.SetParent(background.transform, false);
            var frameImg = frameGo.AddComponent<Image>();
            frameImg.color = Color.clear; // <-- TRANSPARENTE, sin bloques de fondo
            
            var frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.25f, 0.20f); 
            frameRt.anchorMax = new Vector2(0.75f, 0.80f);
            frameRt.offsetMin = frameRt.offsetMax = Vector2.zero;
            _panel = background;

            // 4. Borde Perimetral Sutil (Opcional: Morado muy oscuro para encuadrar, sin rojo)
            var borderGo = new GameObject("FrameBorder");
            borderGo.transform.SetParent(frameGo.transform, false);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.3f, 0.1f, 0.5f, 0.2f); // Morado translúcido muy suave
            var borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-1, -1); 
            borderRt.offsetMax = new Vector2(1, 1);
            borderGo.transform.SetAsFirstSibling();

            // 5. Título principal: GAME OVER (Solo el Texto en Rojo Neón, sin cajas de fondo)
            _titleText = CreateText(frameGo.transform, "GAME OVER",
                fontSize: 56, bold: true,
                anchorMin: new Vector2(0.05f, 0.72f), anchorMax: new Vector2(0.95f, 0.90f));
            _titleText.color = new Color(1.0f, 0.0f, 0.1f); // Letras en Rojo Puro de Alerta

            // 6. Mensaje de Fallo Dinámico (Texto en gris/morado suave)
            _reasonText = CreateText(frameGo.transform, "",
                fontSize: 24, bold: false,
                anchorMin: new Vector2(0.05f, 0.45f), anchorMax: new Vector2(0.95f, 0.65f));
            _reasonText.color = new Color(0.6f, 0.5f, 0.7f); 

            // 7. Botón REINTENTAR (Texto flotante Rojo Neón - Sin caja ni fondo)
            _retryButton = CreateButton(frameGo.transform, "RETRY",
                anchorMin: new Vector2(0.15f, 0.15f), anchorMax: new Vector2(0.45f, 0.32f),
                textColor: new Color(1.0f, 0.0f, 0.1f), // Texto Rojo
                onClick: OnRetry);

            // 8. Botón MENÚ PRINCIPAL (Texto flotante Morado Tecnológico - Sin caja ni fondo)
            _menuButton = CreateButton(frameGo.transform, "MAIN MENU",
                anchorMin: new Vector2(0.55f, 0.15f), anchorMax: new Vector2(0.85f, 0.32f),
                textColor: new Color(0.6f, 0.3f, 0.9f), // Texto Morado
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

        // Helper de botón corregido: Ya no pide 'bgColor' y el fondo es 100% transparente
        private Button CreateButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color textColor, System.Action onClick)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);

            // Añadimos Image pero completamente transparente (Color.clear)
            // Esto es obligatorio en Unity para que el botón mantenga su "zona de clic" activa
            var img = go.AddComponent<Image>();
            img.color = Color.clear;

            var btn = go.AddComponent<Button>();
            var cb  = new Button.ButtonClickedEvent();
            cb.AddListener(() => onClick());
            btn.onClick = cb;

            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Texto interno flotante del botón
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var tmp    = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text    = label;
            tmp.fontSize = 22; 
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color  = textColor; // Aquí se aplica tu Rojo o Morado Neón
            
            var trt    = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            return btn;
        }
    }
}
