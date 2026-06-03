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
//
// MAPPING DE ESTADOS LÓGICOS:
//   Moving            → isMoving = true
//   IdleBetweenTiles  → isMoving = false  (posibles variantes idle)
//   RotatingArrow     → isMoving = false  (sin cambio extra)
//   Charging          → isReadyToAdvance = true  (Droide atrapado en ChargeTile)
//   AtPortal          → isReadyToAdvance = true  (Droide en portal, esperando)
//   ReadyToAdvance    → isReadyToAdvance = true  (feedback post-ExitPortal)
//   Dead              → isDead = true + deathType
//   Victory           → isVictory = true
//
// ⚠ NUNCA se llaman isCharging ni isAtPortal (no existen en el controller).
// ============================================================
using System.Collections;
using UnityEngine;
using Celeris.Data;

namespace Celeris.Player
{
    public class DroideAnimator : MonoBehaviour
    {
        [Header("Referencias")]
        public DroideController droide;
        public Animator         animator;

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
            if (droide != null) droide.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (droide != null) droide.OnStateChanged -= HandleStateChanged;
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

                // ── Rotando flecha (idle breve) ───────────────
                case DroideState.RotatingArrow:
                    animator.SetBool(P_MOVING, false);
                    break;

                // ── Atrapado en ChargeTile ────────────────────
                // Se mapea a isReadyToAdvance como feedback visual de "espera".
                case DroideState.Charging:
                    StopVariant();
                    ResetAllParams();
                    animator.SetBool(P_READY, true);
                    break;

                // ── En portal (esperando minijuego) ───────────
                // También mapea a isReadyToAdvance: el droide está "bloqueado".
                case DroideState.AtPortal:
                    StopVariant();
                    ResetAllParams();
                    animator.SetBool(P_READY, true);
                    break;

                // ── Feedback post-ExitPortal ──────────────────
                case DroideState.ReadyToAdvance:
                    animator.SetBool(P_MOVING, false);
                    animator.SetBool(P_READY,  true);
                    break;

                // ── Victoria ──────────────────────────────────
                case DroideState.Victory:
                    StopVariant();
                    ResetAllParams();
                    animator.SetBool(P_VICTORY, true);
                    break;

                // ── Muerte ────────────────────────────────────
                case DroideState.Dead:
                    StopVariant();
                    ResetAllParams();
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

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Pone todos los parámetros booleanos en false e int en 0.
        /// Solo toca los parámetros que EXISTEN en el controller.
        /// </summary>
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
