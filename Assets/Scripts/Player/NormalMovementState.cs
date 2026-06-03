// ============================================================
// NormalMovementState.cs  |  Assets/Scripts/Player/
//
// Estado de movimiento normal del Droide.
//
// COMPORTAMIENTO:
//   • Mantener presionado → Droide avanza tile a tile de forma
//     continua hasta soltar o encontrar un obstáculo.
//   • Soltar → detención inmediata (no inicia el siguiente paso).
//   • La velocidad es fija según DroideController.NormalMoveDuration.
//
// TRANSICIONES:
//   → FrictionMovementState  cuando el Droide pisa un ChargeTile.
//   (gestionado por DroideController al detectar el tipo de tile)
// ============================================================

namespace Celeris.Player
{
    public class NormalMovementState : IPlayerState
    {
        public void Enter(DroideController ctx)
        {
            // Restaurar duración de paso normal
            ctx.SetMoveDurationOverride(-1f);   // -1 = usar valor base
        }

        public void Exit(DroideController ctx)
        {
            // Garantizar que el movimiento continuo se detiene al salir
            ctx.SetShouldMove(false);
        }

        public void OnPressStart(DroideController ctx)
        {
            ctx.SetShouldMove(true);
        }

        public void OnPressEnd(DroideController ctx)
        {
            ctx.SetShouldMove(false);
        }

        public void Tick(DroideController ctx)
        {
            // Sin lógica adicional por frame en movimiento normal.
            // El MovementLoop de DroideController gestiona los pasos.
        }
    }
}
