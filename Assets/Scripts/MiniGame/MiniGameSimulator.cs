// ============================================================
// MiniGameSimulator.cs  |  Assets/Scripts/MiniGame/
//
// ⚠️  ARCHIVO TEMPORAL — REEMPLAZAR CON MINIJUEGO REAL ⚠️
//
// SETUP EN MiniGameScene:
//   1. Crear un GameObject vacío "MiniGameSimulator".
//   2. Adjuntar este script.
//   3. (Opcional) Crear un Canvas con dos TextMeshPro y
//      arrastrarlos a levelCompleteText e instructionText.
//
// INTEGRACIÓN CON EL MINIJUEGO REAL (para tu compañero):
//   Cuando la lógica del minijuego detecte que el jugador pasó
//   el reto, llamar a:
//       GetComponent<MiniGameSimulator>().AdvanceToNextLevel();
//   o simplemente:
//       LevelManager.EnsureExists().AdvanceLevel();
// ============================================================
using Celeris.Core;
using TMPro;
using UnityEngine;

namespace Celeris.MiniGame
{
    public class MiniGameSimulator : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("UI (opcional)")]
        public TMP_Text levelCompleteText;
        public TMP_Text instructionText;

        [Header("Timing")]
        [Tooltip("Segundos antes de aceptar input (evita avance accidental)")]
        public float inputDelay = 0.8f;

        // ── Privado ───────────────────────────────────────────
        private bool _canAdvance = false;

        // ─────────────────────────────────────────────────────
        private void Start()
        {
            // EnsureExists garantiza que LevelManager exista incluso si
            // se llega a MiniGameScene sin haber pasado por LoginScene.
            var lm = LevelManager.EnsureExists();

            int completedLevel = LevelManager.CurrentLevelNumber;
            bool isLastLevel   = completedLevel >= lm.TotalLevels;

            if (levelCompleteText != null)
            {
                levelCompleteText.text = isLastLevel
                    ? "¡JUEGO COMPLETADO!\n¡Todos los niveles superados!"
                    : $"¡NIVEL {completedLevel} COMPLETADO!";
            }

            if (instructionText != null)
            {
                instructionText.text = isLastLevel
                    ? "Toca la pantalla para volver al menú"
                    : $"Toca la pantalla para jugar el nivel {completedLevel + 1}";
            }

            Debug.Log($"[MiniGameSimulator] Nivel {completedLevel} completado. " +
                      $"Activando input en {inputDelay}s...");

            Invoke(nameof(EnableAdvance), inputDelay);
        }

        private void EnableAdvance()
        {
            _canAdvance = true;
            Debug.Log("[MiniGameSimulator] Listo — toca la pantalla o presiona una tecla.");
        }

        private void Update()
        {
            if (!_canAdvance) return;

            bool keyPressed = UnityEngine.Input.anyKeyDown;
            bool touchBegan = UnityEngine.Input.touchCount > 0 &&
                              UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began;

            if (keyPressed || touchBegan)
                AdvanceToNextLevel();
        }

        // ── Punto de entrada público ──────────────────────────
        /// <summary>
        /// Llama a este método desde el minijuego real cuando el jugador
        /// supera el reto. Avanza al siguiente nivel automáticamente.
        /// </summary>
        public void AdvanceToNextLevel()
        {
            if (!_canAdvance) return;
            _canAdvance = false;

            Debug.Log("[MiniGameSimulator] Avanzando al siguiente nivel...");
            LevelManager.EnsureExists().AdvanceLevel();
        }
    }
}
