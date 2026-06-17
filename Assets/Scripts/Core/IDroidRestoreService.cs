// ============================================================
// IDroidRestoreService.cs  |  Assets/Scripts/Core/
//
// PROPÓSITO (DIP):
//   GameFlowManager depende de esta abstracción para restaurar
//   el Droide tras completar el minijuego de portal.
//   La implementación concreta (DroidRestoreService) encapsula
//   ConsumePortalReturn + MarkCompleted + RestoreFromPortal.
//
// CONTRATO:
//   RestoreAfterPortal() — restaura el Droide al punto exacto
//     donde entró al portal, marca el tile como completado
//     y reanuda el juego. Devuelve false si no había retorno
//     pendiente (guard contra doble llamada).
// ============================================================

namespace Celeris.Core
{
    public interface IDroidRestoreService
    {
        /// <summary>
        /// Consume el estado de retorno de portal guardado en GameStateManager,
        /// marca el PortalTile como completado y llama RestoreFromPortal en DroideCore.
        /// </summary>
        /// <returns>
        ///   true  — restauración ejecutada con éxito.
        ///   false — no había estado de retorno pendiente (no-op seguro).
        /// </returns>
        bool RestoreAfterPortal();
    }
}
