// ============================================================
// DroideAnimator.cs  |  Assets/Scripts/Player/
//
// PARÁMETROS REALES DEL ANIMATOR CONTROLLER:
//   isMoving        (Bool)
//   idleVariant     (Int)   — 0 = idle normal, 1-2 = variantes
//   isDead          (Bool)
//   isVictory       (Bool)
//   isReadyToAdvance(Bool)
//   deathType       (Int)   — 0=genérico, 1=batería, 2=caída, 3=láser
// MAPPING DE ESTADOS LÓGICOS:
//   Moving            → isMoving = true
//   IdleBetweenTiles  → isMoving = false  (posibles variantes idle)
//   RotatingArrow     → isMoving = false  (sin cambio extra)
//   Charging          → isReadyToAdvance = true  (Droide atrapado en ChargeTile)
//   AtPortal          → isReadyToAdvance = true  (Droide en portal, esperando)
//   ReadyToAdvance    → isReadyToAdvance = true  (feedback post-ExitPortal)
//   Dead              → isDead = true + deathType
//   Victory           → isVictory = true
// ⚠ NUNCA se llaman isCharging ni isAtPortal (no existen en el controller).

using Celeris.Data;
using Celeris.Player;
using System.Collections;
using UnityEngine;

namespace Celeris.Player
{
    public class DroideAnimator : MonoBehaviour, IDroideAnimator
    {
        [Header("Referencias")]
        [Tooltip("Reemplaza DroideController — asignar DroideCore del mismo prefab")]
        public DroideCore droide;
        public Animator   animator;
        [Header("Idle Variants")]
        [Range(0f, 1f)]
        [Tooltip("Probabilidad de reproducir una variante al entrar en idle")]
        public float variantProbability = 0.25f;
        private Coroutine _variantCoroutine;
        // ── Nombre de parámetros como constantes ──────────────
        // Evita errores de tipeo y facilita renombrar en el futuro.
        private const string P_MOVING    = "isMoving";
        private const string P_IDLE_VAR  = "idleVariant";
        private const string P_DEAD      = "isDead";
        private const string P_VICTORY   = "isVictory";
        private const string P_READY     = "isReadyToAdvance";
        private const string P_DEATHTYPE = "deathType";
        // ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (droide == null) return;
            droide.OnStateChanged += HandleStateChanged;
        }
        private void OnDisable()
        {
            if (droide == null) return;
            droide.OnStateChanged -= HandleStateChanged;
        }
        private void HandleStateChanged(DroideState state)
        {
            if (animator == null) return;
            switch (state)
            {
                // ── Moviéndose ────────────────────────────────
                case DroideState.Moving:
                    StopVariant();
                    ResetAllParams();
                    animator.SetBool(P_MOVING, true);
                    break;
                // ── Idle entre tiles ──────────────────────────
                case DroideState.IdleBetweenTiles:
                    animator.SetBool(P_MOVING, false);
                    animator.SetBool(P_READY,  false);
                    TryPlayIdleVariant();
                    break;
                // ── Rotando flecha — sin cambio extra (el droide no detiene su animación) ─
                case DroideState.RotatingArrow:
                    break;
                // ── Atrapado en ChargeTile ────────────────────
                // isMoving = false + idleVariant = 2 (scan/forcejeo) rompe
                // la animación de caminata y comunica visualmente la fricción.
                case DroideState.Charging:
                    animator.SetBool(P_MOVING,         false);
                    animator.SetBool(P_READY,          true);
                    animator.SetInteger(P_IDLE_VAR,    2);    // variante scan/forcejeo
                    break;
                // ── En portal (esperando minijuego) ───────────
                // También mapea a isReadyToAdvance: el droide está "bloqueado".
                case DroideState.AtPortal:
                    animator.SetBool(P_READY, true);
                    break;
                // F3-T3: case ReadyToAdvance eliminado — estado removido del enum.
                // DroideCore nunca lo emitía; era dead code desde la migración.
                // ── Victoria ──────────────────────────────────
                case DroideState.Victory:
                    animator.SetBool(P_VICTORY, true);
                    break;
                // ── Muerte ────────────────────────────────────
                case DroideState.Dead:
                    animator.SetBool(P_DEAD, true);
                    animator.SetInteger(P_DEATHTYPE, droide.LastDeathCause switch
                    {
                        DeathCause.Battery => 1,
                        DeathCause.Fall    => 2,
                        DeathCause.Laser   => 3,
                        _                  => 0
                    });
                    break;
            }
        }
        // ── API pública ───────────────────────────────────────
        /// <summary>
        /// Fuerza de inmediato la animación de scan/forcejeo (idleVariant = 2, isMoving = false).
        /// Llamado por DroideController.SetScanAnimation() desde FrictionMovementState.Enter().
        /// </summary>
        public void ForceScanAnimation()
        {
            StopVariant();
            animator.SetBool(P_MOVING,      false);
            animator.SetInteger(P_IDLE_VAR, 2);
        }
        // ── Helpers ───────────────────────────────────────────
        /// Pone todos los parámetros booleanos en false e int en 0.
        /// Solo toca los parámetros que EXISTEN en el controller.
        private void ResetAllParams()
        {
            animator.SetBool(P_MOVING,    false);
            animator.SetBool(P_DEAD,      false);
            animator.SetBool(P_VICTORY,   false);
            animator.SetBool(P_READY,     false);
            animator.SetInteger(P_IDLE_VAR,  0);
            animator.SetInteger(P_DEATHTYPE, 0);
        }
        private void TryPlayIdleVariant()
        {
            if (_variantCoroutine != null || !(Random.value < variantProbability))
            {
                animator.SetInteger(P_IDLE_VAR, 0);
                return;
            }
            int v = Random.Range(1, 3);
            animator.SetInteger(P_IDLE_VAR, v);
            _variantCoroutine = StartCoroutine(ResetVariantAfter(v == 1 ? 3f : 2f));
        }
        private IEnumerator ResetVariantAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (animator != null) animator.SetInteger(P_IDLE_VAR, 0);
            _variantCoroutine = null;
        }
        private void StopVariant()
        {
            if (_variantCoroutine == null) return;
            StopCoroutine(_variantCoroutine);
            _variantCoroutine = null;
        }
    }
}
