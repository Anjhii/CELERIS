// ============================================================
// IMovementStrategy.cs  |  Assets/Scripts/Player/
//
// ⚠ ARCHIVO ARCHIVADO — Fase 0, Acción 0.3
//
// ESTADO: INACTIVO. No compilar ni referenciar.
//
// MOTIVO DEL RETIRO:
//   Esta interfaz y su única implementación (TapMovementStrategy)
//   quedaron desconectadas del sistema activo cuando DroideController
//   migró al patrón de estado via IPlayerState. Ninguna clase del
//   proyecto referencia IMovementStrategy en tiempo de compilación.
//   Su presencia generaba confusión sobre cuál es el contrato real
//   del sistema de movimiento.
//
// CONTRATO ACTIVO:
//   El sistema de movimiento se gobierna por IPlayerState.
//   Ver: Assets/Scripts/Player/IPlayerState.cs
//   Implementaciones activas:
//     - NormalMovementState.cs  (movimiento continuo por hold)
//     - FrictionMovementState.cs (atrapado en ChargeTile)
//
// EXTENSIBILIDAD FUTURA:
//   Para añadir nuevas estrategias de movimiento (ej. auto-run,
//   ralentización por dificultad) implementar IPlayerState,
//   NO resucitar IMovementStrategy.
//
// HISTORIA:
//   Creado para un sistema de tap con cola de movimientos.
//   Reemplazado por IPlayerState + hold-to-move en v6 del controlador.
//   Archivado en Fase 0 del roadmap de refactorización.
// ============================================================

// Código original conservado como referencia histórica:
/*
namespace Celeris.Player
{
    public interface IMovementStrategy
    {
        void RequestMove();
        void ConsumeMove();
        bool HasPendingMove { get; }
        float GetMoveDuration();
        void Reset();
    }
}
*/
