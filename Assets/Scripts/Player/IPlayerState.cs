// ============================================================
// IPlayerState.cs  |  Assets/Scripts/Player/
//
// Interfaz del patrón de estado para el movimiento del Droide.
//
// Estados concretos:
//   NormalMovementState  — movimiento por mantener presionado
//   FrictionMovementState — atrapado en ChargeTile, escape por multi-tap
//
// El DroideController actúa como contexto: delega cada evento
// de input al estado activo y expone métodos internos que los
// estados usan para manipular el Droide sin duplicar lógica.
// ============================================================

namespace Celeris.Player
{
    /// <summary>
    /// Contrato de estado de movimiento del Droide.
    /// Todos los métodos reciben el contexto (DroideController)
    /// para evitar que los estados guarden referencias externas.
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>Llamado al activarse este estado.</summary>
        void Enter(DroideController ctx);

        /// <summary>Llamado justo antes de cambiar a otro estado.</summary>
        void Exit(DroideController ctx);

        /// <summary>
        /// El jugador presionó la pantalla/botón.
        /// En NormalMovement inicia el movimiento continuo.
        /// En FrictionMovement cuenta como tap de impulso.
        /// </summary>
        void OnPressStart(DroideController ctx);

        /// <summary>El jugador soltó la pantalla/botón.</summary>
        void OnPressEnd(DroideController ctx);

        /// <summary>
        /// Llamado cada frame desde DroideController.Update()
        /// para que el estado actualice su lógica interna.
        /// </summary>
        void Tick(DroideController ctx);
    }
}
