using System.Collections;
using UnityEngine;
using Celeris.Player;
using Celeris.Data;

namespace Celeris.Player
{
    public class DroideAnimator : MonoBehaviour
    {
        [Header("Referencias")]
        public DroideController droide;
        public Animator animator;

        [Header("Idle Variants")]
        [Tooltip("Probabilidad de lanzar una variante al entrar en idle (0-1)")]
        public float variantProbability = 0.3f;

        private Coroutine _resetVariantCoroutine;

        private void OnEnable()
        {
            droide.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            droide.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(DroideState state)
        {
            switch (state)
            {
                case DroideState.Moving:
                    StopResetCoroutine();
                    animator.SetBool("isMoving", true);
                    animator.SetBool("isReadyToAdvance", false);
                    animator.SetBool("isDead", false);
                    animator.SetBool("isVictory", false);
                    animator.SetInteger("deathType", 0);
                    animator.SetInteger("idleVariant", 0);
                    break;

                case DroideState.IdleBetweenTiles:
                    animator.SetBool("isMoving", false);
                    animator.SetBool("isReadyToAdvance", false);
                    if (_resetVariantCoroutine == null && Random.value < variantProbability)
                    {
                        int variant = Random.Range(1, 3);
                        animator.SetInteger("idleVariant", variant);
                        _resetVariantCoroutine = StartCoroutine(ResetVariantAfterClip(variant));
                    }
                    else
                    {
                        animator.SetInteger("idleVariant", 0);
                    }
                    break;

                case DroideState.Charging:
                    StopResetCoroutine();
                    animator.SetBool("isMoving", false);
                    animator.SetBool("isReadyToAdvance", true);
                    animator.SetBool("isDead", false);
                    animator.SetBool("isVictory", false);
                    animator.SetInteger("idleVariant", 0);
                    break;

                case DroideState.ReadyToAdvance:
                    animator.SetBool("isMoving", false);
                    animator.SetBool("isReadyToAdvance", true);
                    break;

                case DroideState.Victory:
                    StopResetCoroutine();
                    animator.SetBool("isMoving", false);
                    animator.SetBool("isReadyToAdvance", false);
                    animator.SetBool("isDead", false);
                    animator.SetInteger("idleVariant", 0);
                    animator.SetInteger("deathType", 0);
                    animator.SetBool("isVictory", true);
                    break;

                case DroideState.Dead:
                    StopResetCoroutine();
                    Debug.Log($"Dead — Causa: {droide.LastDeathCause} | Batería: {droide.Battery}");
                    animator.SetBool("isMoving", false);
                    animator.SetBool("isReadyToAdvance", false);
                    animator.SetBool("isVictory", false);
                    animator.SetInteger("idleVariant", 0);
                    animator.SetBool("isDead", true);
                    animator.SetInteger("deathType", droide.LastDeathCause switch {
                        DeathCause.Fall    => 2,
                        _                  => droide.Battery <= 0 ? 1 : 0
                    });
                    break;

                default:
                    animator.SetBool("isMoving", false);
                    animator.SetBool("isReadyToAdvance", false);
                    animator.SetBool("isDead", false);
                    animator.SetBool("isVictory", false);
                    animator.SetInteger("deathType", 0);
                    animator.SetInteger("idleVariant", 0);
                    break;
            }
        }

        private IEnumerator ResetVariantAfterClip(int variant)
        {
            yield return new WaitForSeconds(GetClipLength(variant));
            animator.SetInteger("idleVariant", 0);
        }

        private float GetClipLength(int variant)
        {
            return variant switch
            {
                1 => 3f,  // Mirar_Alrededor
                2 => 2f,  // Adelante_Atras
                _ => 2f
            };
        }

        private void StopResetCoroutine()
        {
            if (_resetVariantCoroutine != null)
            {
                StopCoroutine(_resetVariantCoroutine);
                _resetVariantCoroutine = null;
            }
        }

        
    }
}