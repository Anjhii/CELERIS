// ============================================================
// MobileInputHandler.cs  |  Assets/Scripts/Input/
//
// v5 — Solo gestión de OnPressStart/End y pulso eléctrico.
//       El hold-to-rotate lo maneja exclusivamente
//       DroideController.HandleHoldAndPulse() para evitar
//       doble rotación.
//
// ESCENA: Adjuntar a un Button UI de pantalla completa
//         (Image transparente, Raycast Target = ON) en GameplayScene.
// INSPECTOR: Asignar DroideController.
//
// LÓGICA DE INPUT
// ───────────────
// • Presionar (PointerDown) → droide.OnPressStart()
//     – NormalMovementState: inicia movimiento continuo.
//     – FrictionMovementState: cada press cuenta como tap de impulso.
//
// • Soltar (PointerUp) → droide.OnPressEnd()
//     – NormalMovementState: detiene inmediatamente el Droide.
//     – FrictionMovementState: no interrumpe (el Droide sigue su inercia).
//
// • Pulso eléctrico: si hay láser adyacente + cooldown expirado,
//   se dispara automáticamente junto con OnPressStart.
// ============================================================
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

        [Header("Pulso Eléctrico")]
        [Tooltip("Cooldown en segundos entre pulsos eléctricos")]
        public float pulseCooldown = 3.0f;

        [Header("Feedback visual (opcional)")]
        public Image pulseButtonImage;
        public Color colorPulseReady    = new Color(0.20f, 0.70f, 1.00f, 0.85f);
        public Color colorPulseCooldown = new Color(0.45f, 0.45f, 0.45f, 0.55f);

        // ── Privado ───────────────────────────────────────────
        private float _pressDownTime = 0f;
        private bool  _isPressed     = false;
        private float _lastPulseTime = -999f;

        // ── Lifecycle ─────────────────────────────────────────
        private void OnEnable()
        {
            if (droide != null) droide.OnStateChanged += OnDroideStateChanged;
        }

        private void OnDisable()
        {
            if (droide != null) droide.OnStateChanged -= OnDroideStateChanged;
        }

        private void OnDroideStateChanged(DroideState state)
        {
            // Si el Droide muere o gana, limpiar estado de input
            if (state == DroideState.Dead    ||
                state == DroideState.Victory ||
                state == DroideState.AtPortal)
            {
                _isPressed = false;
            }
        }

        // ── Eventos de puntero ────────────────────────────────
        public void OnPointerDown(PointerEventData _)
        {
            _pressDownTime = Time.unscaledTime;
            _isPressed     = true;

            if (droide == null) return;
            if (droide.State == DroideState.Dead    ||
                droide.State == DroideState.Victory ||
                droide.State == DroideState.AtPortal) return;

            // Intentar pulso eléctrico si hay láser adyacente
            TryFireElectricPulse();

            // Notificar al estado activo
            droide.OnPressStart();
        }

        public void OnPointerUp(PointerEventData _)
        {
            if (!_isPressed) return;
            _isPressed = false;
            if (droide == null) return;

            if (droide.State == DroideState.Charging)
            {
                droide.RegisterChargeClick();
                droide.TriggerLightPulse();
            }
            else
            {
                droide.TriggerLightPulse();

                if (droide.State != DroideState.Dead &&
                    droide.State != DroideState.Victory &&
                    droide.HasLaserAtRangeOne())
                {
                    TryFireElectricPulse();
                }
            }

            droide.OnPressEnd();
        }

        // ── Update: solo feedback visual del pulso ───────────
        // NOTA: El hold-to-rotate lo gestiona exclusivamente
        // DroideController.HandleHoldAndPulse() para evitar
        // doble rotación (MobileInputHandler + HandleHoldAndPulse
        // se disparaban ambos en el mismo frame).
        private void Update()
        {
            UpdatePulseVisual();
        }

        // ── Pulso eléctrico ───────────────────────────────────
        private void TryFireElectricPulse()
        {
            if (droide == null) return;
            if (Time.unscaledTime - _lastPulseTime < pulseCooldown) return;
            if (!droide.HasLaserAtRangeOne()) return;

            _lastPulseTime = Time.unscaledTime;
            droide.TriggerElectricPulse();
        }

        // ── Feedback visual ───────────────────────────────────
        private void UpdatePulseVisual()
        {
            if (pulseButtonImage == null) return;
            bool onCooldown = (Time.unscaledTime - _lastPulseTime) < pulseCooldown;
            pulseButtonImage.color = onCooldown ? colorPulseCooldown : colorPulseReady;
        }
    }
}
