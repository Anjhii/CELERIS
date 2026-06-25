// ============================================================
// DroideVFX.cs  |  Assets/Scripts/Player/
//
// RESPONSABILIDAD ÚNICA:
//   Reaccionar a eventos del ciclo de vida del Droide
//   disparando efectos visuales y de luz.
//
// LO QUE NO HACE (SRP):
//   No sabe nada de física, grid ni estados de movimiento.
//   No lee transform.position ni Rigidbody.
//   No llama FindObjectOfType<> — recibe referencias por [SerializeField].
//
// EVENTOS ESCUCHADOS (suscripción en OnEnable / desuscripción en OnDisable):
//   DroideCore.OnDied              → efecto de muerte (shockwave + apagar luz)
//   DroideCore.OnVictory           → efecto de victoria (light pulse)
//   DroideCore.OnVFXPulseRequested → pulso eléctrico (LightPulse + ShockwaveEffect)
//
// NOTA F2-T1: OnScanAnimationRequested eliminado de DroideCore.
//   DroideCore llama _droideAnimator.ForceScanAnimation() directamente (DIP).
//   HandleScanAnimation() era código muerto — eliminado.
//
// DISEÑO:
//   DroideAnimator ya escucha DroideCore.OnStateChanged desde su propio OnEnable.
//   DroideVFX NO duplica esa suscripción; solo gestiona luz y shockwave.
// ============================================================
using Celeris.Data;
using Celeris.Utils;
using UnityEngine;

namespace Celeris.Player
{
    public class DroideVFX : MonoBehaviour
    {
        [Header("Referencias (asignar en prefab)")]
        [SerializeField] private DroideCore     droideCore;
        [SerializeField] private LightPulse     lightPulse;
        [SerializeField] private ShockwaveEffect shockwave;

        // ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (droideCore == null) return;
            droideCore.OnDied                 += HandleDied;
            droideCore.OnVictory              += HandleVictory;
            droideCore.OnVFXPulseRequested    += HandleElectricPulse;
        }

        private void OnDisable()
        {
            if (droideCore == null) return;
            droideCore.OnDied                 -= HandleDied;
            droideCore.OnVictory              -= HandleVictory;
            droideCore.OnVFXPulseRequested    -= HandleElectricPulse;
        }

        // ── Manejadores ───────────────────────────────────────

        private void HandleDied(DeathCause cause)
        {
            // El shockwave marca visualmente el impacto final.
            shockwave?.Trigger();
        }

        private void HandleVictory()
        {
            lightPulse?.Pulse();
        }

        private void HandleElectricPulse()
        {
            lightPulse?.Pulse();
            shockwave?.Trigger();
            droideCore.PulseAdjacentTiles();
        }

    }
}
