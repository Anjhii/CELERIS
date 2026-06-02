// ============================================================
// MobileInputHandler.cs  |  Assets/Scripts/Input/
//
// ESCENA: Adjuntar a un Button UI de pantalla completa
//         (Image transparente, Raycast Target = ON) en GameplayScene.
// INSPECTOR: Asignar DroideController + pulseButtonImage (opcional).
//
// LÓGICA DE INPUT
// ───────────────
// • Tap corto en Charging       : RegisterChargeClick (sube batería).
// • Tap corto en movimiento     : dispara pulso rango-1 SOLO si hay un
//                                 LaserTile activo adyacente (rango 1).
//                                 Si no hay objetivo, el tap se descarta.
// • Tap corto en ReadyToAdvance : ignorado — el avance es siempre automático.
// • Hold 0.5s en ReadyToAdvance : TriggerElectricPulseExtended (rango 2)
//                                 + ConfirmAdvance inmediato (no espera soltar).
// • Hold 0.5s en movimiento     : RotateCurrentArrow.
//
// COLA CON REEMPLAZO DE PRIORIDAD
// ───────────────────────────────
// _pulseQueue es List<bool>; el bool indica si el pulso tenía objetivo real
// (HasLaserAtRangeOne) al encolarse.  Solo se encolan pulsos validados —
// nunca pulsos ciegos.  Si la cola está llena y llega un tap de alta
// prioridad, sustituye el primer elemento de baja prioridad.
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

        // Cola de pulsos: bool = isHighPriority (láser a rango 1 al encolar)
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

        // Vaciar la cola si el droide muere o gana.
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

            // Si el hold ya se procesó, no hay tap que interpretar.
            // El hold en ReadyToAdvance ya llamó ConfirmAdvance inmediatamente
            // desde HandleHold; no hace falta nada más al soltar.
            if (_holdFired) return;

            float duration = Time.unscaledTime - _pointerDownTime;
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
            // Los pulsos encolados se disparan con rango 1.
            // Se bloquea solo en Dead / Victory.
            if (_pulseQueue.Count > 0 && droide != null &&
                droide.State != DroideState.Dead &&
                droide.State != DroideState.Victory)
            {
                float elapsed = Time.unscaledTime - _lastPulseTime;
                if (elapsed >= pulseCooldown)
                {
                    _pulseQueue.RemoveAt(0);
                    _lastPulseTime = Time.unscaledTime;
                    droide.TriggerElectricPulse();   // rango 1
                }
            }

            // ── Feedback visual ───────────────────────────────
            UpdateButtonVisual();
        }

        // ── Tap corto ─────────────────────────────────────────
        private void HandleShortTap()
        {
            if (droide == null) return;

            // ReadyToAdvance: el tap no hace nada.
            // El avance es automático (timeout) o por Hold.
            if (droide.State == DroideState.ReadyToAdvance) return;

            // Charging: suma batería durante el Stress Test.
            if (droide.State == DroideState.Charging)
            {
                droide.RegisterChargeClick();
                return;   // durante la carga no se encolan pulsos de rango 1
            }

            // Movimiento normal: encolar pulso SOLO si hay láser adyacente (rango 1).
            // Si no hay objetivo, el tap se descarta silenciosamente.
            if (droide.State != DroideState.Dead &&
                droide.State != DroideState.Victory &&
                droide.HasLaserAtRangeOne())
            {
                TryEnqueuePulse();
            }
        }

        // ── Encolar con reemplazo de prioridad ────────────────
        // Solo se llega aquí cuando HasLaserAtRangeOne() es true,
        // así que todos los pulsos encolados tienen objetivo real.
        private void TryEnqueuePulse()
        {
            // El bool de prioridad sigue usando HasLaserAtRangeOne
            // para que si por alguna razón se llama sin objetivo, se marque
            // correctamente como baja prioridad y pueda ser reemplazado.
            bool isHighPriority = droide.HasLaserAtRangeOne();

            // Cooldown expirado: disparar inmediatamente sin pasar por la cola
            if (Time.unscaledTime - _lastPulseTime >= pulseCooldown)
            {
                _lastPulseTime = Time.unscaledTime;
                droide.TriggerElectricPulse();   // rango 1
                return;
            }

            // Cooldown activo: intentar encolar
            if (_pulseQueue.Count < maxQueueSize)
            {
                _pulseQueue.Add(isHighPriority);
                return;
            }

            // Cola llena: reemplazar primer elemento de baja prioridad
            // si el nuevo tap tiene objetivo real.
            if (isHighPriority)
            {
                int lowIdx = _pulseQueue.IndexOf(false);
                if (lowIdx >= 0)
                    _pulseQueue[lowIdx] = true;
            }
            // Baja prioridad + cola llena → descartar silenciosamente
        }

        // ── Hold largo ────────────────────────────────────────
        private void HandleHold()
        {
            if (droide == null) return;

            // ReadyToAdvance: pulso extendido (rango 2) + avance inmediato.
            // No se espera a soltar el dedo: el Droide reanuda al instante.
            if (droide.State == DroideState.ReadyToAdvance)
            {
                droide.TriggerElectricPulseExtended();
                droide.ConfirmAdvance();
                return;
            }

            // Movimiento normal: rotar la flecha del tile actual.
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