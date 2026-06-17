// ============================================================
// IDroideContext.cs  |  Assets/Scripts/Player/
//
// PROPÓSITO (ISP — Interface Segregation Principle):
//   IPlayerState.Tick() antes recibía DroideController completo,
//   exponiendo 30+ miembros a los estados cuando solo necesitan
//   una fracción de ellos. IDroideContext es el subconjunto mínimo
//   que los estados necesitan para operar.
//
// CONTRATO:
//   Los estados concretos (NormalMovementState, FrictionMovementState)
//   dependen de IDroideContext, NO de DroideCore directamente.
//   DroideCore implementa esta interfaz.
//
// REGLA DE EXTENSIÓN:
//   Si un nuevo estado necesita algo que no está aquí,
//   añadirlo a esta interfaz — no romper la abstracción
//   haciendo que el estado dependa de DroideCore.
// ============================================================

namespace Celeris.Player
{
    /// <summary>
    /// Subconjunto mínimo del Droide que los estados de movimiento
    /// necesitan para operar. Implementado por DroideCore.
    /// </summary>
    public interface IDroideContext
    {
        // ── Parámetros de movimiento (read-only) ──────────────
        float NormalMoveDuration   { get; }
        float ChargeDrainRate      { get; }
        float ChargeMinSpeedToMove { get; }
        float ChargeTapImpulse     { get; }
        float ChargeFrictionDecay  { get; }
        float ChargeEscapeSpeed    { get; }

        // ── Estado observable ─────────────────────────────────
        int   Battery              { get; }
        int   MaxBattery           { get; }
        bool  IsInputHeld          { get; }

        // ── Mutadores que los estados necesitan ───────────────
        void SetShouldMove(bool value);
        void SetIsStuckInCharge(bool value);
        void SetMoveDurationOverride(float value);

        // ── Drenaje / daño ────────────────────────────────────
        void TakeBatteryHit(int amount);
        void ForceKill(Celeris.Data.DeathCause cause);

        // ── Transición de estado ──────────────────────────────
        void RequestStateTransition(PlayerStateType stateType);
    }

    /// <summary>
    /// Tipos de estado de movimiento conocidos por DroideStateMachine.
    /// Los estados piden transiciones por tipo, no por instancia,
    /// eliminando el 'new NormalMovementState()' en FrictionMovementState.Tick().
    /// </summary>
    public enum PlayerStateType
    {
        Normal,
        Friction
    }
}
