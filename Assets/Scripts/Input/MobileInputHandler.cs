// ============================================================
// MobileInputHandler.cs  |  Assets/Scripts/Input/
//
// ESCENA: Adjuntar a un Button UI de pantalla completa
//         (Image transparente, Raycast Target = ON) en GameplayScene.
// INSPECTOR: Asignar DroideController + pulseButtonImage (opcional).
//
// ARQUITECTURA DE DOS TUBERÍAS
// ────────────────────────────
// • Tubería 1 (Carga)   : tap corto → RegisterChargeClick, siempre.
// • Tubería 2 (Pulso)   : tap corto → TryEnqueuePulse, ciego al estado
//                         (solo bloquea en Dead / Victory).
// Las dos tuberías son independientes: durante un ChargeTile el jugador
// puede seguir haciendo taps para subir la batería Y encolar un pulso
// para el siguiente LaserTile.
//
// COLA CON REEMPLAZO DE PRIORIDAD
// ───────────────────────────────
// _pulseQueue es List<bool>; el bool indica si el pulso tiene objetivo
// real (HasLaserInLookahead == true).  Cuando la cola está llena y llega
// un tap de alta prioridad, reemplaza el primer elemento de baja prioridad
// que encuentre.  Así los pulsos "ciegos" no bloquean a los urgentes.
//
// FEEDBACK VISUAL 3 ESTADOS
// ─────────────────────────
// colorReady    (azul)  : cola vacía y cooldown expirado.
// colorEnqueued (verde) : hay al menos un pulso esperando.
// colorFull     (gris)  : cola llena Y cooldown activo (no acepta más).
// ============================================================
using System.Collections.Generic;
using Celeris.Data;
using Celeris.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Celeris.Input
{
    public class MobileInputHandler : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Droide")]
        public DroideController droide;

        [Header("Umbrales de tiempo (segundos)")]
        public float shortTapMax = 0.30f;    // < 0.30s → Tap corto
        public float holdMin     = 0.50f;    // > 0.50s → Hold largo

        [Header("Pulso Eléctrico")]
        [Tooltip("Segundos de espera entre disparos del pulso")]
        public float pulseCooldown = 3f;
        [Tooltip("Máximo de pulsos encolados en simultáneo")]
        [Range(1, 5)]
        public int   maxQueueSize  = 2;

        [Header("Feedback visual del botón (opcional)")]
        [Tooltip("Image del botón de pulso. Su color refleja el estado de la cola.")]
        public Image  pulseButtonImage;
        [Tooltip("Cola vacía y cooldown expirado — pulso disponible inmediatamente")]
        public Color  colorReady    = new Color(0.20f, 0.70f, 1.00f, 0.85f);  // azul
        [Tooltip("Hay pulsos encolados esperando a dispararse")]
        public Color  colorEnqueued = new Color(0.30f, 0.90f, 0.30f, 0.85f);  // verde suave
        [Tooltip("Cola llena Y cooldown activo — no acepta más taps de pulso")]
        public Color  colorFull     = new Color(0.45f, 0.45f, 0.45f, 0.55f);  // gris

        // ── Privado ───────────────────────────────────────────
        private float      _pointerDownTime = 0f;
        private bool       _holdFired       = false;
        private bool       _isDown          = false;
        private float      _lastPulseTime   = -999f;

        // Cola de pulsos: bool = isHighPriority (láser detectado en lookahead al encolar)
        private readonly List<bool> _pulseQueue = new();

        // ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (droide != null) droide.OnStateChanged += HandleDroideStateChanged;
        }

        private void OnDisable()
        {
            if (droide != null) droide.OnStateChanged -= HandleDroideStateChanged;
        }

        // Vaciar la cola si el droide muere o gana — no tiene sentido
        // mantener pulsos pendientes cuando el juego terminó.
        private void HandleDroideStateChanged(DroideState state)
        {
            if (state == DroideState.Dead || state == DroideState.Victory)
                _pulseQueue.Clear();
        }

        // ─────────────────────────────────────────────────────
        public void OnPointerDown(PointerEventData _)
        {
            _pointerDownTime = Time.unscaledTime;
            _holdFired       = false;
            _isDown          = true;
        }

        public void OnPointerUp(PointerEventData _)
        {
            if (!_isDown) return;
            _isDown = false;

            float duration = Time.unscaledTime - _pointerDownTime;

            // Si el hold ya disparó el pulso estando en ReadyToAdvance,
            // confirmar avance al soltar (el pulso fue la "acción extra").
            if (_holdFired)
            {
                if (droide != null && droide.State == DroideState.ReadyToAdvance)
                    droide.ConfirmAdvance();
                return;
            }

            if (duration < shortTapMax)
                HandleShortTap();
            // zona muerta [shortTapMax, holdMin]: ignorar
        }

        // ── Update: hold + cola de pulsos + visual ───────────
        private void Update()
        {
            // ── Hold ──────────────────────────────────────────
            if (_isDown && !_holdFired &&
                (Time.unscaledTime - _pointerDownTime) >= holdMin)
            {
                _holdFired = true;
                HandleHold();
            }

            // ── Drenar cola de pulsos ─────────────────────────
            // Solo se bloquea en Dead / Victory (loop acabado).
            // En todos los demás estados (incluyendo Charging) los pulsos
            // encolados siguen drenándose con normalidad.
            if (_pulseQueue.Count > 0 && droide != null &&
                droide.State != DroideState.Dead &&
                droide.State != DroideState.Victory)
            {
                float elapsed = Time.unscaledTime - _lastPulseTime;
                if (elapsed >= pulseCooldown)
                {
                    _pulseQueue.RemoveAt(0);
                    _lastPulseTime = Time.unscaledTime;
                    droide.TriggerElectricPulse();
                }
            }

            // ── Feedback visual ───────────────────────────────
            UpdateButtonVisual();
        }

        // ── Tap corto — TRES CASOS ────────────────────────────
        // 1. ReadyToAdvance  : confirmar avance inmediato (sin pulso).
        // 2. Charging        : sumar batería (Tubería 1) + encolar pulso (Tubería 2).
        // 3. Resto           : solo encolar pulso (Tubería 2).
        private void HandleShortTap()
        {
            if (droide == null) return;

            // Caso 1: el jugador da la orden de avance tras la carga completa
            if (droide.State == DroideState.ReadyToAdvance)
            {
                droide.ConfirmAdvance();
                return;   // no encolar pulso ni booster de batería
            }

            // Tubería 1: carga — suma batería durante el Stress Test
            if (droide.State == DroideState.Charging)
                droide.RegisterChargeClick();

            // Tubería 2: pulso — solo se bloquea en Dead / Victory
            if (droide.State != DroideState.Dead &&
                droide.State != DroideState.Victory)
                TryEnqueuePulse();
        }

        // ── Encolar con reemplazo de prioridad ────────────────
        private void TryEnqueuePulse()
        {
            // Evaluar prioridad en el momento del tap
            bool isHighPriority = droide != null && droide.HasLaserInLookahead();

            // Cooldown expirado: disparar inmediatamente sin pasar por la cola
            if (Time.unscaledTime - _lastPulseTime >= pulseCooldown)
            {
                _lastPulseTime = Time.unscaledTime;
                droide.TriggerElectricPulse();
                return;
            }

            // Cooldown activo: intentar encolar
            if (_pulseQueue.Count < maxQueueSize)
            {
                _pulseQueue.Add(isHighPriority);
                return;
            }

            // Cola llena: si el nuevo tap tiene alta prioridad, busca el primer
            // elemento de baja prioridad y lo sustituye.
            if (isHighPriority)
            {
                int lowIdx = _pulseQueue.IndexOf(false);
                if (lowIdx >= 0)
                    _pulseQueue[lowIdx] = true;
                // Si todos ya son alta prioridad: este tap se descarta (ya están cubiertos)
            }
            // Baja prioridad + cola llena → descartar silenciosamente
        }

        // ── Hold largo ────────────────────────────────────────
        private void HandleHold()
        {
            if (droide == null) return;

            // En ReadyToAdvance: hold = pulso eléctrico (desactiva láseres en rango).
            // El avance se confirma al soltar el dedo, en OnPointerUp.
            if (droide.State == DroideState.ReadyToAdvance)
            {
                droide.TriggerElectricPulse();
                return;
            }

            droide.RotateCurrentArrow();
        }

        // ── Feedback visual 3 estados ─────────────────────────
        private void UpdateButtonVisual()
        {
            if (pulseButtonImage == null || droide == null) return;
            if (droide.State == DroideState.Dead ||
                droide.State == DroideState.Victory) return;

            bool onCooldown = (Time.unscaledTime - _lastPulseTime) < pulseCooldown;
            bool queueFull  = _pulseQueue.Count >= maxQueueSize;

            if (onCooldown && queueFull)
                pulseButtonImage.color = colorFull;         // gris: no acepta más taps
            else if (_pulseQueue.Count > 0)
                pulseButtonImage.color = colorEnqueued;     // verde: hay pulsos esperando
            else
                pulseButtonImage.color = colorReady;        // azul: disponible ahora mismo
        }
    }
}
