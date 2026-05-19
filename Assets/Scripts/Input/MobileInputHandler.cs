// ============================================================
// MobileInputHandler.cs  |  Assets/Scripts/Input/
//
// ESCENA: Adjuntar a un Button UI de pantalla completa
//         (transparent, raycast habilitado) en GameplayScene.
// INSPECTOR: Asignar referencia a DroideController.
//
// El botón sirve como superficie de input unificada.
// No usamos EventSystem separado; Unity UI llama a los
// métodos OnPointerDown/Up/Click via EventTrigger o
// implementando las interfaces de pointer.
// ============================================================
using Celeris.Data;
using Celeris.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Celeris.Input
{
    public class MobileInputHandler : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Referencia al Droide")]
        public DroideController droide;

        [Header("Umbrales de tiempo (segundos)")]
        public float shortTapMax  = 0.30f;   // < 0.30s → Tap corto
        public float holdMin      = 0.50f;   // > 0.50s → Hold
        public float multiTapWindow = 0.40f; // ventana para multi-tap

        // ── Privado ───────────────────────────────────────────
        private float _pointerDownTime;
        private bool  _holdFired = false;
        private bool  _isDown    = false;

        // Multi-tap tracking
        private int   _tapCount       = 0;
        private float _lastTapTime    = -999f;
        private const int MultiTapThreshold = 3;   // 3 taps rápidos

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

            if (_holdFired) return;   // el hold ya fue procesado en Update

            if (duration < shortTapMax)
                HandleShortTap();
            // Entre shortTapMax y holdMin → ignorar (zona muerta intencional)
        }

        // ── Update: solo detecta Hold activo ─────────────────
        private void Update()
        {
            if (!_isDown || _holdFired) return;

            float duration = Time.unscaledTime - _pointerDownTime;
            if (duration >= holdMin)
            {
                _holdFired = true;
                HandleHold();
            }
        }

        // ── Acciones según tipo de interacción ───────────────

        /// <summary>Tap corto: pulso eléctrico en radio 1 Manhattan.</summary>
        private void HandleShortTap()
        {
            if (droide == null) return;

            // Comprobar si estamos en Charging para multi-tap
            if (droide.State == DroideState.Charging)
            {
                HandleMultiTap();
                return;
            }

            droide.TriggerElectricPulse();
        }

        /// <summary>Hold: rota flecha del tile actual del droide.</summary>
        private void HandleHold()
        {
            if (droide == null) return;
            droide.RotateCurrentArrow();
        }

        /// <summary>Multi-tap: registra clicks de recarga mientras el droide está en Charging.</summary>
        private void HandleMultiTap()
        {
            float now = Time.unscaledTime;
            if (now - _lastTapTime <= multiTapWindow)
            {
                _tapCount++;
            }
            else
            {
                _tapCount = 1;
            }
            _lastTapTime = now;

            droide.RegisterChargeClick();

            if (_tapCount >= MultiTapThreshold)
                _tapCount = 0;
        }
    }
}
