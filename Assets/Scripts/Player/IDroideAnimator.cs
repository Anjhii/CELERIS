// ============================================================
// IDroideAnimator.cs  |  Assets/Scripts/Player/
//
// PROPÓSITO (DIP — Dependency Inversion Principle):
//   DroideCore no debe depender de DroideAnimator concreto.
//   Depende de esta abstracción, inyectada por DroideBootstrapper.
//
// CONTRATO MÍNIMO (ISP):
//   Solo los métodos que DroideCore realmente necesita invocar.
//   DroideAnimator puede tener más métodos públicos — no importa.
//
// EXTENSIBILIDAD:
//   En Fase 5 (wall-walking u otro avatar):
//     Crear DroideAnimator3D : IDroideAnimator.
//     DroideBootstrapper inyecta la nueva implementación.
//     DroideCore no se toca.
// ============================================================

namespace Celeris.Player
{
    /// <summary>
    /// Contrato mínimo del animador del Droide.
    /// Implementado por DroideAnimator. Inyectado por DroideBootstrapper.
    /// </summary>
    public interface IDroideAnimator
    {
        /// <summary>
        /// Fuerza la animación de scan/forcejeo (idleVariant = 2).
        /// Llamado por DroideCore al entrar en un ChargeTile.
        /// </summary>
        void ForceScanAnimation();
    }
}
