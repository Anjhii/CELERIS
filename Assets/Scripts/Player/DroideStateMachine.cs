// ============================================================
// DroideStateMachine.cs  |  Assets/Scripts/Player/
//
// RESPONSABILIDAD ÚNICA:
//   Gestionar el estado de movimiento activo (IPlayerState).
//   Delegar Tick/OnPressStart/OnPressEnd al estado activo.
//   Llamar Enter/Exit en cada transición.
//
// LO QUE NO HACE (SRP):
//   No sabe nada de física ni de Rigidbody.
//   No sabe nada del grid ni de tiles.
//   No instancia estados concretos — los recibe de DroideBootstrapper.
//
// FIX CRÍTICO respecto al diseño anterior:
//   FrictionMovementState.Tick() hacía: ctx.TransitionToState(new NormalMovementState())
//   → 1 allocation por frame en el escape del ChargeTile.
//   Ahora usa RequestTransition(PlayerStateType.Normal) y DroideStateMachine
//   tiene las instancias pre-creadas. CERO allocations en Update.
// ============================================================

namespace Celeris.Player
{
    public class DroideStateMachine
    {
        // ── Estado activo ─────────────────────────────────────
        private IPlayerState _current;

        // ── Instancias pre-creadas (sin allocations en runtime) ─
        private readonly IPlayerState _normalState;
        private readonly IPlayerState _frictionState;

        // ─────────────────────────────────────────────────────
        public DroideStateMachine(IPlayerState normalState, IPlayerState frictionState)
        {
            _normalState   = normalState;
            _frictionState = frictionState;
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Activa el estado inicial y llama Enter().
        /// Llamado por DroideCore.Init() al reiniciar el nivel.
        /// </summary>
        public void ResetToInitialState(IDroideContext ctx)
        {
            Transition(_normalState, ctx);
        }

        /// <summary>
        /// Propaga el tick al estado activo.
        /// Llamado por DroideCore.Update().
        /// </summary>
        public void Tick(IDroideContext ctx)
        {
            _current?.Tick(ctx);
        }

        /// <summary>
        /// Propaga OnPressStart al estado activo.
        /// Llamado por DroideCore cuando recibe input de press.
        /// </summary>
        public void OnPressStart(IDroideContext ctx)
        {
            _current?.OnPressStart(ctx);
        }

        /// <summary>
        /// Propaga OnPressEnd al estado activo.
        /// Llamado por DroideCore cuando recibe input de release.
        /// </summary>
        public void OnPressEnd(IDroideContext ctx)
        {
            _current?.OnPressEnd(ctx);
        }

        /// <summary>
        /// Solicita una transición a un estado por tipo.
        /// Los estados llaman ctx.RequestStateTransition(type), que
        /// delega aquí. Las instancias son las pre-creadas — sin allocations.
        /// </summary>
        public void RequestTransition(PlayerStateType stateType, IDroideContext ctx)
        {
            var target = stateType switch
            {
                PlayerStateType.Normal  => _normalState,
                PlayerStateType.Friction => _frictionState,
                _                       => _normalState
            };
            Transition(target, ctx);
        }

        // ── Helper ────────────────────────────────────────────

        private void Transition(IPlayerState next, IDroideContext ctx)
        {
            if (_current == next) return;
            _current?.Exit(ctx);
            _current = next;
            _current?.Enter(ctx);
        }
    }
}
