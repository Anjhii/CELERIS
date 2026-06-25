// ============================================================
// FrictionMovementState.cs  |  Assets/Scripts/Player/
//
// Estado de movimiento con fricción: Droide atrapado en ChargeTile.
// CAMBIO v3 (Bloque 1 SOLID):
//   Recibe IDroideContext en lugar de DroideController (ISP).
//   El escape del ChargeTile usa ctx.RequestStateTransition(Normal)
//   en lugar de ctx.TransitionToState(new NormalMovementState()).
//   → CERO allocations en Update/Tick.

using Celeris.Data;
using Celeris.Player;
using UnityEngine;

namespace Celeris.Player
{
    public class FrictionMovementState : IPlayerState
    {
        private float _speed              = 1f;
        private float _batteryAccumulator = 0f;
        public void Enter(IDroideContext ctx)
        {
            _speed              = 0f;
            _batteryAccumulator = 1f;
            ctx.SetIsStuckInCharge(true);
            ApplySpeedToDuration(ctx);
            ctx.SetShouldMove(false);
            Debug.Log("[FrictionMovementState] Enter — Droide atrapado en ChargeTile.");
        }
        public void Exit(IDroideContext ctx)
        {
            ctx.SetIsStuckInCharge(false);
            ctx.SetMoveDurationOverride(-1f);
            Debug.Log("[FrictionMovementState] Exit — Droide liberado del ChargeTile.");
        }
        public void OnPressStart(IDroideContext ctx)
        {
            _speed = Mathf.Min(1f, _speed + ctx.ChargeTapImpulse);
            ctx.SetShouldMove(_speed > ctx.ChargeMinSpeedToMove);
        }
        public void OnPressEnd(IDroideContext ctx) { }
        public void Tick(IDroideContext ctx)
        {
            float dt = Time.deltaTime;
            // ── Drenaje de batería ────────────────────────────
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
            // ── Fricción ──────────────────────────────────────
            _speed = Mathf.Max(0f, _speed - ctx.ChargeFrictionDecay * dt);
            // ── Escape: sin allocation ────────────────────────
            if (_speed >= ctx.ChargeEscapeSpeed)
                ctx.RequestStateTransition(PlayerStateType.Normal);
        }
        private void ApplySpeedToDuration(IDroideContext ctx)
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
