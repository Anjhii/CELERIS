// ============================================================
// IMovementStrategy.cs  |  Assets/Scripts/Player/
//
// Contrato de estrategia de movimiento del Droide.
// La implementación activa es TapMovementStrategy (movimiento
// por Tap con velocidad proporcional a la frecuencia de taps).
//
// Para añadir nuevas estrategias (ej. auto-run, ralentización):
//   1. Implementar IMovementStrategy.
//   2. Asignarla en DroideController.SetMovementStrategy().
// ============================================================

namespace Celeris.Player
{
    public interface IMovementStrategy
    {
        /// <summary>
        /// Registra una intención de movimiento (generada por un tap del jugador).
        /// La estrategia decide si ejecutar inmediatamente o encolar.
        /// </summary>
        void RequestMove();

        /// <summary>
        /// Consume el movimiento pendiente. Llamar justo antes de ejecutar el paso.
        /// </summary>
        void ConsumeMove();

        /// <summary>True si hay al menos un movimiento pendiente de ejecutar.</summary>
        bool HasPendingMove { get; }

        /// <summary>
        /// Duración en segundos del próximo movimiento.
        /// Inversamente proporcional a la frecuencia de taps recientes.
        /// </summary>
        float GetMoveDuration();

        /// <summary>Reinicia el estado interno (al morir, iniciar nivel, etc.).</summary>
        void Reset();
    }
}
