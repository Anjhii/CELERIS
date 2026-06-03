// ============================================================
// FrictionMovementState.cs  |  Assets/Scripts/Player/
//
// Estado de movimiento con fricción: Droide atrapado en ChargeTile.
//
// COMPORTAMIENTO:
//   • Al entrar en el estado, el tile inicia el drenaje de batería.
//   • La velocidad del Droide disminuye continuamente (fricción).
//     Si el jugador no toca la pantalla, la velocidad llega a 0
//     y el Droide se detiene completamente.
//   • Cada tap (OnPressStart) inyecta un impulso de velocidad
//     (simula esfuerzo para superar la fricción).
//   • Cuanto más rápido y frecuente es el multi-tap, mayor es
//     la velocidad mantenida.
//   • Cuando la velocidad supera EscapeSpeed, el Droide sale del
//     tile y vuelve a NormalMovementState.
//   • Si la batería llega a 0 estando atrapado, el Droide muere.
//
// PARÁMETROS CONFIGURABLES (ver DroideController Inspector):
//   chargeEscapeSpeed    — velocidad mínima para escapar (0-1)
//   chargeFrictionDecay  — tasa de pérdida de velocidad por segundo
//   chargeTapImpulse     — impulso de velocidad por tap (0-1)
//   chargeDrainRate      — batería drenada por segundo
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Player
{
    public class FrictionMovementState : IPlayerState
    {
        // ── Estado interno ────────────────────────────────────
        /// <summary>
        /// Velocidad actual normalizada [0, 1].
        /// 1.0 = velocidad máxima (igual al movimiento normal).
        /// 0.0 = Droide completamente detenido.
        /// </summary>
        private float _speed = 1f;

        private float _batteryAccumulator = 0f;

        // ── IPlayerState ─────────────────────────────────────

        public void Enter(DroideController ctx)
        {
            _speed               = 1f;   // empieza a velocidad normal
            _batteryAccumulator  = 0f;

            // La velocidad actual determina la duración del paso
            ApplySpeedToDuration(ctx);

            // El Droide solo se mueve si la velocidad lo permite
            ctx.SetShouldMove(_speed > ctx.ChargeMinSpeedToMove);

            Debug.Log("[FrictionMovementState] Droide atrapado en ChargeTile.");
        }

        public void Exit(DroideController ctx)
        {
            ctx.SetMoveDurationOverride(-1f);
            ctx.SetShouldMove(false);
            Debug.Log("[FrictionMovementState] Droide escapó del ChargeTile.");
        }

        public void OnPressStart(DroideController ctx)
        {
            // Cada press inyecta impulso de velocidad (multi-tap)
            _speed = Mathf.Min(1f, _speed + ctx.ChargeTapImpulse);
            ApplySpeedToDuration(ctx);

            // Si hay velocidad suficiente, habilitar movimiento
            ctx.SetShouldMove(_speed > ctx.ChargeMinSpeedToMove);
        }

        public void OnPressEnd(DroideController ctx)
        {
            // No detener el movimiento al soltar en fricción:
            // el Droide sigue (lentamente) hasta que la velocidad caiga a 0.
        }

        public void Tick(DroideController ctx)
        {
            float dt = Time.deltaTime;

            // ── Drenaje de batería ────────────────────────────
            _batteryAccumulator += ctx.ChargeDrainRate * dt;
            if (_batteryAccumulator >= 1f)
            {
                int drain        = Mathf.FloorToInt(_batteryAccumulator);
                _batteryAccumulator -= drain;
                ctx.TakeBatteryHit(drain);

                if (ctx.Battery <= 0)
                {
                    ctx.ForceKill(DeathCause.Battery);
                    return;
                }
            }

            // ── Fricción: la velocidad decae sola ────────────
            _speed = Mathf.Max(0f, _speed - ctx.ChargeFrictionDecay * dt);
            ApplySpeedToDuration(ctx);

            bool canMove = _speed > ctx.ChargeMinSpeedToMove;
            ctx.SetShouldMove(canMove);

            // ── Condición de escape: velocidad alta sostenida ─
            if (_speed >= ctx.ChargeEscapeSpeed)
            {
                ctx.TransitionToState(new NormalMovementState());
            }
        }

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Convierte la velocidad normalizada en una duración de paso.
        /// Velocidad alta → paso rápido; velocidad baja → paso muy lento.
        /// </summary>
        private void ApplySpeedToDuration(DroideController ctx)
        {
            if (_speed <= 0f)
            {
                ctx.SetMoveDurationOverride(ctx.NormalMoveDuration * 4f);   // casi congelado
                return;
            }
            // duration = baseDuration / speed  (speed=1 → base, speed=0.25 → 4x más lento)
            float duration = ctx.NormalMoveDuration / Mathf.Max(_speed, 0.05f);
            ctx.SetMoveDurationOverride(Mathf.Clamp(duration, ctx.NormalMoveDuration, ctx.NormalMoveDuration * 8f));
        }
    }
}
