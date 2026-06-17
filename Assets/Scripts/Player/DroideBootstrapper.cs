// ============================================================
// DroideBootstrapper.cs  |  Assets/Scripts/Player/
//
// RESPONSABILIDAD ÚNICA:
//   Instanciar y conectar todos los sub-componentes del sistema Droide.
//   Es el único lugar donde se crean objetos concretos con 'new'.
//
// PRINCIPIO DIP:
//   DroideCore depende de abstracciones (IDroideContext, IPlayerState).
//   DroideBootstrapper conoce las implementaciones concretas.
//   El resto del sistema no sabe qué implementación se usa.
//
// ORDEN DE EJECUCIÓN:
//   [DefaultExecutionOrder(-200)] garantiza que este Awake corre
//   ANTES que DroideCore.OnEnable (que suscribe a generator.OnGridReady).
//   Si el orden cambia, DroideCore iniciaría sin sub-componentes inyectados.
//
// EXTENSIBILIDAD:
//   Para tests: crear TestDroideBootstrapper con estados mock.
//   Para nuevos modos: crear BootstrapperVX con estados distintos
//   sin modificar DroideCore.
// ============================================================
using Celeris.Input;
using UnityEngine;

namespace Celeris.Player
{
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(DroideCore))]
    public class DroideBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            var core = GetComponent<DroideCore>();

            if (core == null)
            {
                Debug.LogError("[DroideBootstrapper] No se encontró DroideCore " +
                               "en el mismo GameObject. Sub-componentes no inyectados.");
                return;
            }

            // ── 1. Estados de movimiento (pre-creados, sin allocations en runtime) ─
            var normalState   = new NormalMovementState();
            var frictionState = new FrictionMovementState();

            // ── 2. State machine ──────────────────────────────
            var stateMachine = new DroideStateMachine(normalState, frictionState);
            core.InjectStateMachine(stateMachine);

            // ── 3. Batería ────────────────────────────────────
            var battery = new DroideBatteryController();
            core.InjectBattery(battery);

            // ── 4. Movement decider ───────────────────────────
            var movementDecider = new DroideMovementDecider(core);
            core.InjectMovementDecider(movementDecider);

            // ── 5. Portal handler ─────────────────────────────
            var portalHandler = new DroidePortalHandler(core);
            core.InjectPortalHandler(portalHandler);

            // ── 6. Animador (DIP: IDroideAnimator, no DroideAnimator concreto) ──
            // F2-T1: GetComponentInChildren evita FindObjectOfType global.
            // La búsqueda se limita al mismo prefab del Droide.
            var droideAnimator = GetComponentInChildren<DroideAnimator>();
            if (droideAnimator != null)
            {
                core.InjectAnimator(droideAnimator);
                Debug.Log("[DroideBootstrapper] IDroideAnimator inyectado: " + droideAnimator.name);
            }
            else
                Debug.LogWarning("[DroideBootstrapper] No se encontró DroideAnimator " +
                                 "en hijos del prefab. Animación de scan desactivada.");

            // ── 7. Input handler (suscripción vía evento, sin FindObjectOfType) ──
            // MobileInputHandler se suscribe a OnStateChanged desde su propio OnEnable.
            // Solo inyectamos la referencia para que DroideCore no caiga en raw input.
            var mobileHandler = FindObjectOfType<MobileInputHandler>();
            core.InjectMobileHandler(mobileHandler);
            if (mobileHandler == null)
                Debug.LogWarning("[DroideBootstrapper] No hay MobileInputHandler. " +
                                 "DroideCore usará raw input (modo test).");

            Debug.Log("[DroideBootstrapper] Todos los sub-componentes inyectados en DroideCore.");
        }
    }
}
