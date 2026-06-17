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
    // ARQUITECTURA:
    //   Implementaciones activas: NormalMovementState (movimiento hold-to-run), FrictionMovementState (ChargeTile, escape por tap).
    //   Próximo caso esperado: GlidingMovementState — el droide desliza con inercia al cruzar un tile de hielo/viento.
    //   Punto de entrada para extensión: DroideBootstrapper.Awake() → droide.SetInitialState(new GlidingMovementState()) sin modificar DroideController.
    /// <summary>
    /// Contrato de estado de movimiento del Droide.
    /// Todos los métodos reciben el contexto (DroideController)
    /// para evitar que los estados guarden referencias externas.
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>Llamado al activarse este estado.</summary>
        void Enter(IDroideContext ctx);

        /// <summary>Llamado justo antes de cambiar a otro estado.</summary>
        void Exit(IDroideContext ctx);

        /// <summary>
        /// El jugador presionó la pantalla/botón.
        /// En NormalMovement inicia el movimiento continuo.
        /// En FrictionMovement cuenta como tap de impulso.
        /// </summary>
        void OnPressStart(IDroideContext ctx);

        /// <summary>El jugador soltó la pantalla/botón.</summary>
        void OnPressEnd(IDroideContext ctx);

        /// <summary>
        /// Llamado cada frame desde DroideCore.Update()
        /// para que el estado actualice su lógica interna.
        /// </summary>
        void Tick(IDroideContext ctx);
    }
}
