// ============================================================
// FrictionMovementState.cs  |  Assets/Scripts/Player/
//
// Estado de movimiento con fricción: Droide atrapado en ChargeTile.
//
// COMPORTAMIENTO:
//   • Al entrar: SetIsStuckInCharge(true) → ApplyVelocity aplica
//     chargeSpeedMultiplier; DroideController.TickChargeDrain() NO
//     se llama (este estado gestiona el drenaje en Tick()).
//   • La velocidad normalizada [0,1] decae cada frame (fricción).
//     Si el jugador no toca, llega a 0 y el Droide se detiene.
//   • Cada OnPressStart inyecta ChargeTapImpulse de velocidad.
//   • Si _speed >= ChargeEscapeSpeed → TransitionToState(Normal).
//   • Al salir: SetIsStuckInCharge(false) → ApplyVelocity vuelve
//     a velocidad normal.
//
// CAMBIOS v2:
//   • Enter() llama ctx.SetIsStuckInCharge(true).
//   • Exit() llama ctx.SetIsStuckInCharge(false).
//   Esto corrige el bug donde _isStuckInCharge quedaba false y
//   ApplyVelocity() nunca aplicaba el chargeSpeedMultiplier.
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

        // NOTA: Ya no se usa un acumulador de batería aquí porque
        // DroideController.TickChargeDrain() fue eliminado del Update()
        // para evitar doble drenaje. Este estado ES el responsable
        // único del drenaje mientras el Droide está atrapado.
        private float _batteryAccumulator = 0f;

        // ── IPlayerState ─────────────────────────────────────

        public void Enter(DroideController ctx)
        {
            _speed = 0f;   // droide parte desde quieto — requiere tap para arrancar

            // Drenaje inmediato: acumulador pre-cargado para que el primer
            // Tick() descuente energía sin esperar el primer segundo completo.
            _batteryAccumulator = 1f;

            // CRÍTICO: marcar al droide como atrapado.
            // Esto permite que ApplyVelocity() aplique chargeSpeedMultiplier.
            ctx.SetIsStuckInCharge(true);

            // La velocidad actual determina la duración del paso
            ApplySpeedToDuration(ctx);

            // Droide detenido al entrar; el primer tap inyecta el impulso inicial
            ctx.SetShouldMove(false);

            // NOTA: SetScanAnimation() ya NO se llama aquí.
            // DroideController.ProcessTileEffect(ChargeTile) lo invoca, pero solo
            // al pisar el PRIMER tile del clúster (_lastProcessedTileType != ChargeTile).
            // Esto evita el tartamudeo visual al cruzar tiles consecutivos de fricción.

            Debug.Log("[FrictionMovementState] Enter — Droide atrapado en ChargeTile.");
        }

        public void Exit(DroideController ctx)
        {
            // CRÍTICO: liberar al droide antes de cambiar de estado.
            // Garantiza que ApplyVelocity() vuelva a velocidad normal.
            ctx.SetIsStuckInCharge(false);

            ctx.SetMoveDurationOverride(-1f);
            ctx.SetShouldMove(false);
            Debug.Log("[FrictionMovementState] Exit — Droide liberado del ChargeTile.");
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
            // Este estado es el ÚNICO responsable del drenaje en ChargeTile.
            // DroideController.Update() NO llama TickChargeDrain() mientras
            // este estado está activo (para evitar doble drenaje).
            _batteryAccumulator += ctx.ChargeDrainRate * dt;
            if (_batteryAccumulator >= 1f)
            {
                int drain           = Mathf.FloorToInt(_batteryAccumulator);
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
            // Exit() limpia SetIsStuckInCharge(false) automáticamente.
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
                ctx.SetMoveDurationOverride(ctx.NormalMoveDuration * 4f);
                return;
            }
            float duration = ctx.NormalMoveDuration / Mathf.Max(_speed, 0.05f);
            ctx.SetMoveDurationOverride(
                Mathf.Clamp(duration, ctx.NormalMoveDuration, ctx.NormalMoveDuration * 8f));
        }
    }
}
