// ============================================================
// NormalMovementState.cs  |  Assets/Scripts/Player/
//
// Estado de movimiento normal del Droide.
// COMPORTAMIENTO:
//   • Mantener presionado → Droide avanza tile a tile de forma
//     continua hasta soltar o encontrar un obstáculo.
//   • Soltar → detención inmediata (no inicia el siguiente paso).
//   • La velocidad es fija según DroideController.NormalMoveDuration.
// TRANSICIONES:
//   → FrictionMovementState  cuando el Droide pisa un ChargeTile.
//   (gestionado por DroideController al detectar el tipo de tile)

using Celeris.Player;

namespace Celeris.Player
{
    public class NormalMovementState : IPlayerState
    {
        public void Enter(IDroideContext ctx)
        {
            ctx.SetMoveDurationOverride(-1f);
            ctx.SetShouldMove(ctx.IsInputHeld);
        }
        public void Exit(IDroideContext ctx)
        {
            ctx.SetShouldMove(false);
        }
        public void OnPressStart(IDroideContext ctx)
        {
            ctx.SetShouldMove(true);
        }
        public void OnPressEnd(IDroideContext ctx)
        {
            ctx.SetShouldMove(false);
        }
        public void Tick(IDroideContext ctx)
        {
            // Sin logica adicional por frame.
        }
    }
}
