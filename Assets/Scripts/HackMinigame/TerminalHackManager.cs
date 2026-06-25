// ============================================================
// TerminalHackManager.cs  |  Assets/Scripts/HackMinigame/  — v5 SOLID
//
// RESPONSABILIDAD ÚNICA (SRP):
//   Orquestar el flujo del minijuego de hackeo. No genera secuencias
//   ni valida inputs directamente — delega en sub-componentes.
//
// LO QUE NO HACE:
//   No reproduce glifos          → HackSequenceController
//   No compara clicks del jugador → HackInputValidator
//   No carga/descarga escenas     → GameFlowManager (escucha OnTerminalExited)
//   No restaura el Droide         → DroidRestoreService
//
// CAMBIO v5 respecto a v4:
//   • HackedTerminalsCount (static) eliminado.
//     Reemplazado por GameStateManager.Instance.TerminalsHackedThisRun.
//   • ResetHackedCount() eliminado.
//     Reemplazado por GameStateManager.Instance.ResetTerminalsHacked().
//   • Lógica de secuencia extraída a HackSequenceController.
//   • Lógica de validación extraída a HackInputValidator.
//   • TerminalHackManager solo orquesta: Init → Play → WaitResult → Decide.
//
// FLUJO:
//   OnEnable  → sessionData.RuntimeReset()
//   Start     → BeginAttempt()
//               → sequenceController.Init() + StartCoroutine(Play())
//               → sequenceController.OnSequenceComplete → inputValidator.Enable()
//               → inputValidator.OnAttemptResult(success)
//                   success=true  → ResolutionRoutine(true)  → ExitScene()
//                   success=false → ResolutionRoutine(false)
//                     intentos restantes → BeginAttempt()
//                     sin intentos       → TriggerGameOver()
//
// MODO TEST AISLADO (sceneCount == 1):
//   ExitScene y TriggerGameOver recargan la misma escena para permitir
//   iterar el minijuego sin depender de GameFlowManager.
// ============================================================
using System.Collections;
using System.Collections.Generic;
using Celeris.Core;
using Celeris.Leaderboard;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Celeris.HackMinigame
{
    public class TerminalHackManager : MonoBehaviour
    {
        // ── Eventos globales ──────────────────────────────────
        /// <summary>
        /// Hack completado con éxito. GameFlowManager descarga la escena y restaura el Droide.
        /// </summary>
        public static event System.Action OnTerminalExited;

        /// <summary>
        /// Jugador agotó los 3 intentos. GameFlowManager descarga la escena y llama ForceKill.
        /// </summary>
        public static event System.Action OnHackGameOver;

        // ── Inspector ─────────────────────────────────────────
        [Header("Datos de sesión")]
        [SerializeField] private HackSessionData sessionData;

        [Header("Glifos y Audio")]
        [SerializeField] private List<AlienGlyph> glyphs;
        [SerializeField] private AudioSource       centralAudioSource;

        // ── Sub-componentes (SRP, DIP) ────────────────────────
        private HackSequenceController _sequenceController;
        private HackInputValidator     _inputValidator;

        // ── Lifecycle ─────────────────────────────────────────

        private void OnEnable()
        {
            // RuntimeReset garantiza que currentAttempt=1 aunque el ScriptableObject
            // haya persistido estado en el Editor entre sesiones de Play Mode.
            if (sessionData != null)
                sessionData.RuntimeReset();
        }

        private void Start()
        {
            _sequenceController = new HackSequenceController();
            _inputValidator     = new HackInputValidator();

            // Conectar el resultado del validador al orquestador
            _inputValidator.OnAttemptResult += HandleAttemptResult;

            SetGlyphsInteractable(false);
            BeginAttempt();
        }

        private void OnDisable()
        {
            if (_inputValidator != null)
            {
                _inputValidator.OnAttemptResult -= HandleAttemptResult;
                _inputValidator.Cleanup();
            }
        }

        // ── Flujo principal ───────────────────────────────────

        private void BeginAttempt()
        {
            // Guard: si ya agotamos intentos antes de llamar Init, ir a game over
            if (sessionData.currentAttempt > 3)
            {
                TriggerGameOver();
                return;
            }

            SetGlyphsInteractable(false);
            InitializeGlyphs();

            _sequenceController.Init(glyphs, sessionData, centralAudioSource);

            // Suscribir OnSequenceComplete justo antes de reproducir para
            // evitar doble-suscripción si BeginAttempt se llama múltiples veces.
            _sequenceController.OnSequenceComplete -= OnSequenceReady;
            _sequenceController.OnSequenceComplete += OnSequenceReady;

            StartCoroutine(_sequenceController.Play());
        }

        private void OnSequenceReady()
        {
            _sequenceController.OnSequenceComplete -= OnSequenceReady;

            _inputValidator.Init(
                glyphs,
                _sequenceController.CurrentSequence,
                centralAudioSource,
                this);

            SetGlyphsInteractable(true);
            _inputValidator.Enable();
        }

        private void HandleAttemptResult(bool success)
        {
            SetGlyphsInteractable(false);
            StartCoroutine(ResolutionRoutine(success));
        }

        private IEnumerator ResolutionRoutine(bool success)
        {
            yield return new WaitForSeconds(0.9f);

            if (success)
            {
                sessionData.wasHackSuccessful = true;
                sessionData.extractedDigit    = UnityEngine.Random.Range(0, 10);

                // Incrementar contador en GameStateManager (reemplaza static HackedTerminalsCount)
                var gsm = GameStateManager.Instance;
                if (gsm != null) gsm.IncrementTerminalsHacked();

                int reward = sessionData.CalculateScoreReward();
                Debug.Log($"<color=green>[HACK EXITOSO]</color> " +
                          $"Terminal {gsm?.TerminalsHackedThisRun}/3. " +
                          $"Recompensa: {reward} pts.");

                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.AddPoints(reward);
                else
                    Debug.LogWarning("[TerminalHackManager] ScoreManager no encontrado — puntos no registrados.");

                ExitScene();
            }
            else
            {
                sessionData.wasHackSuccessful = false;
                sessionData.RegisterFailedAttempt();

                if (sessionData.isGameOverDueToFailure)
                {
                    TriggerGameOver();
                }
                else
                {
                    Debug.Log($"<color=yellow>[INTRUSIÓN]</color> Intento {sessionData.currentAttempt} iniciado.");
                    BeginAttempt();
                }
            }
        }

        // ── Salida ────────────────────────────────────────────

        private void ExitScene()
        {
            // MODO TEST AISLADO
            if (SceneManager.sceneCount == 1)
            {
                Debug.Log("<color=cyan>[MODO PRUEBA]</color> Hack completado. Reiniciando escena...");
                sessionData.ResetForNewTerminal();
                OnTerminalExited?.Invoke();
                SceneManager.LoadScene(gameObject.scene.name);
                return;
            }

            // MODO JUEGO: ceder el control a GameFlowManager vía evento.
            Time.timeScale = 1f;
            OnTerminalExited?.Invoke();
        }

        private void TriggerGameOver()
        {
            Debug.Log("<color=red>[GAME OVER]</color> El robot falló los 3 intentos.");

            // MODO TEST AISLADO
            if (SceneManager.sceneCount == 1)
            {
                Debug.Log("<color=cyan>[MODO PRUEBA]</color> Reiniciando datos y escena...");
                sessionData.ResetForNewTerminal();
                SceneManager.LoadScene(gameObject.scene.name);
                return;
            }

            // Resetear sesión ANTES de notificar: si el jugador reintenta el nivel,
            // la próxima vez que entre a un portal la sesión estará limpia.
            sessionData.ResetForNewTerminal();

            Time.timeScale = 1f;
            OnHackGameOver?.Invoke();
        }

        // ── Helpers ───────────────────────────────────────────

        private void InitializeGlyphs()
        {
            for (int i = 0; i < glyphs.Count; i++)
                glyphs[i].Initialize(i);
        }

        private void SetGlyphsInteractable(bool state)
        {
            foreach (AlienGlyph g in glyphs)
                g.SetInteractable(state);
        }
    }
}
