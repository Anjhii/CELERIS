// ============================================================
// MiniGameSimulator.cs  |  Assets/Scripts/MiniGame/
//
// ⚠️  ARCHIVO TEMPORAL — REEMPLAZAR CON MINIJUEGO REAL ⚠️
//
// v4 — Flujo dual con carga aditiva:
//   • PORTAL: la GameplayScene sigue cargada (carga aditiva).
//     CompleteMinigame() → GameFlowManager.OnPortalComplete()
//     (descarga el minijuego, restaura Droide, reanuda juego).
//   • VICTORIA: tap → LevelManager.AdvanceLevel().
//
// INTEGRACIÓN CON MINIJUEGO REAL:
//   Llama a CompleteMinigame() cuando el jugador supere el reto.
//   Para mostrar Game Over desde el minijuego:
//   GameOverController.Show(GameOverReason.MinigameFail)
// ============================================================
using Celeris.Core;
using Celeris.UI;
using TMPro;
using UnityEngine;

namespace Celeris.MiniGame
{
    public class MiniGameSimulator : MonoBehaviour
    {
        [Header("UI (opcional)")]
        public TMP_Text titleText;
        public TMP_Text instructionText;

        [Header("Timing")]
        public float inputDelay = 0.8f;

        private bool _canAdvance    = false;
        private bool _isPortalRoute = false;

        private void Start()
        {
            var lm = LevelManager.EnsureExists();
            _isPortalRoute = lm.IsPortalTransition;

            int lvl = LevelManager.CurrentLevelNumber;

            if (_isPortalRoute)
            {
                if (titleText != null)      titleText.text = $"EVENTO ESPECIAL — Nivel {lvl}";
                if (instructionText != null) instructionText.text = "Supera el reto para continuar";
            }
            else
            {
                bool isLast = lvl >= lm.TotalLevels;
                if (titleText != null)
                    titleText.text = isLast
                        ? "¡JUEGO COMPLETADO!"
                        : $"¡NIVEL {lvl} COMPLETADO!";
                if (instructionText != null)
                    instructionText.text = isLast
                        ? "Toca para volver al menú"
                        : $"Toca para el nivel {lvl + 1}";
            }

            Invoke(nameof(EnableAdvance), inputDelay);
        }

        private void EnableAdvance() => _canAdvance = true;

        private void Update()
        {
            if (!_canAdvance) return;

            bool pressed = UnityEngine.Input.anyKeyDown ||
                           (UnityEngine.Input.touchCount > 0 &&
                            UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began);
            if (pressed) CompleteMinigame();
        }

        /// <summary>Llamar cuando el jugador supera el reto del minijuego.</summary>
        public void CompleteMinigame()
        {
            if (!_canAdvance) return;
            _canAdvance = false;

            if (_isPortalRoute)
            {
                // Notificar al GameFlowManager que descargue el minijuego y restaure el Droide
                var gfm = FindObjectOfType<GameFlowManager>();
                if (gfm != null)
                {
                    gfm.OnPortalComplete();
                }
                else
                {
                    // Fallback: si GameFlowManager no se encuentra, usar retorno estándar
                    Debug.LogWarning("[MiniGameSimulator] GameFlowManager no encontrado. Usando fallback.");
                    LevelManager.EnsureExists().ReturnFromPortal();
                }
            }
            else
            {
                LevelManager.EnsureExists().AdvanceLevel();
            }
        }

        /// <summary>Forzar fallo del minijuego (llamar desde lógica del reto).</summary>
        public void FailMinigame()
        {
            _canAdvance = false;
            GameOverController.Show(GameOverReason.MinigameFail);
        }

        // Compatibilidad legacy
        public void AdvanceToNextLevel() => CompleteMinigame();
    }
}
