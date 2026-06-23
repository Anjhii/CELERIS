// ============================================================
// GameOverPresenter.cs  |  Assets/Scripts/UI/
//
// RESPONSABILIDAD ÚNICA:
//   Presentar el panel de Game Over: mostrar/ocultar textos,
//   ejecutar los botones Retry y Menu.
//
// LO QUE NO HACE (SRP):
//   No sabe cuándo morir — eso es responsabilidad de GameOverTrigger.
//   No construye UI en runtime con new GameObject() — todo viene
//   del prefab asignado en el Inspector.
//   No usa FindObjectOfType ni SceneManager.sceneLoaded.
//
// SETUP EN PREFAB:
//   Crear un prefab "GameOverPresenter" con:
//     - Canvas (ScreenSpaceOverlay, SortOrder 999)
//     - Panel raíz (Image oscura, setActive false por defecto)
//     - TextMeshProUGUI para título
//     - TextMeshProUGUI para motivo
//     - Button para Retry
//     - Button para Menu
//   Asignar todas las referencias en el Inspector.
//   Instanciar en LoginScene (DontDestroyOnLoad se gestiona desde GameOverTrigger).
//
// USO:
//   GameOverTrigger llama Show(data) / Hide().
//   Los botones llaman OnRetry / OnMenu internamente.
// ============================================================
using Celeris.Core;
using Celeris.Data;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Celeris.UI
{
    /// <summary>
    /// Datos inmutables que describen el motivo del Game Over.
    /// GameOverTrigger construye este struct y lo pasa a Show().
    /// </summary>
    public readonly struct GameOverData
    {
        public readonly DeathCause  Cause;
        public readonly string      CustomMessage;

        public GameOverData(DeathCause cause, string customMessage = null)
        {
            Cause         = cause;
            CustomMessage = customMessage;
        }
    }

    public class GameOverPresenter : MonoBehaviour
    {
        // ── Referencias (asignar en prefab) ───────────────────
        [Header("Panel raíz")]
        [SerializeField] private GameObject panel;

        [Header("Textos")]
        [SerializeField] private TextMeshProUGUI reasonText;

        [Header("Botones")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        // ─────────────────────────────────────────────────────
        private void Awake()
        {
            // Suscribir botones una sola vez — sin RemoveAllListeners previo
            // porque Awake solo corre una vez por instancia.
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (menuButton  != null) menuButton.onClick.AddListener(OnMenu);

            HideImmediate();
        }

        private void OnDestroy()
        {
            // Limpiar listeners para evitar referencias colgantes.
            if (retryButton != null) retryButton.onClick.RemoveAllListeners();
            if (menuButton  != null) menuButton.onClick.RemoveAllListeners();
        }

        // ── API pública — llamada por GameOverTrigger ─────────

        /// <summary>Muestra el panel con el motivo de muerte correspondiente.</summary>
        public void Show(GameOverData data)
        {
            if (reasonText != null)
                reasonText.text = BuildReasonText(data);

            panel?.SetActive(true);

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.Pause();
        }

        /// <summary>Oculta el panel sin navegar a ningún lado.</summary>
        public void Hide()
        {
            panel?.SetActive(false);
        }

        // ── Acciones de botón ─────────────────────────────────

        private void OnRetry()
        {
            Hide();
            GameStateManager.Instance?.Resume();
            LevelManager.EnsureExists().RetryCurrentLevel();
        }

        private void OnMenu()
        {
            Hide();
            GameStateManager.Instance?.Resume();
            SceneManager.LoadScene("MainMenuScene");
        }

        // ── Helpers ───────────────────────────────────────────

        private void HideImmediate()
        {
            panel?.SetActive(false);
        }

        private static string BuildReasonText(GameOverData data) =>
            data.Cause switch
            {
                DeathCause.Battery => "CONEXIÓN: <color=#9629A5>BATERÍA AGOTADA</color>",
                DeathCause.Laser   => "NÚCLEO: <color=#FF001A>DESTRUIDO POR LÁSER</color>",
                DeathCause.Fall    => "ALERTA: <color=#FF001A>IMPACTO POR CAÍDA</color>",
                DeathCause.Generic => "STATE: <color=#FF001A>ACCESS DENIED</color>",
                _                  => data.CustomMessage ?? "ERROR: <color=#9629A5>CÓDIGO TERMINAL CORRUPTO</color>"
            };
    }
}
