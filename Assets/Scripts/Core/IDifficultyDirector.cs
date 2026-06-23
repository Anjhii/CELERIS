// ============================================================
// IDifficultyDirector.cs  |  Assets/Scripts/Core/
//
// CONTRATO del sistema de dificultad dinámica (F4-T1).
//
// PRINCIPIOS:
//   OCP — LevelManager depende de esta abstracción, no de
//         DifficultyDirectorImpl. Añadir una variante (debug,
//         QA, A/B test) = nueva clase, cero cambios en LevelManager.
//   DIP — La interfaz vive en el namespace Core, sin dependencias
//         de Unity ni de ninguna implementación concreta.
//   SRP — Este contrato solo define QUÉ puede hacer un director.
//         Cómo lo hace es responsabilidad de cada implementación.
//
// IMPLEMENTACIONES INCLUIDAS:
//   DifficultyDirectorImpl  — producción (sigmoid + actuator)
//   NullDifficultyDirector  — QA / bypass (D fijo en 0 o constante)
//
// CICLO DE VIDA EN LEVELMANAGER:
//   1. OnLevelResult()          → RecordLevelResult(...)
//   2. Antes de BuildGrid()     → Apply(config)
//   3. En debug overlay/tests   → CurrentDifficulty
// ============================================================

using Celeris.Config;
using Celeris.Data;

namespace Celeris.Core
{
    /// <summary>
    /// Contrato mínimo del Game Director de dificultad dinámica.
    /// Inyectado en LevelManager por DependencyInjection en Awake.
    /// </summary>
    public interface IDifficultyDirector
    {
        /// <summary>
        /// Registra el resultado de un nivel completado.
        /// Llamar DESPUÉS de que el nivel termina (victoria o muerte).
        /// </summary>
        /// <param name="won">True = victoria, false = muerte.</param>
        /// <param name="completionTime">
        ///   Segundos transcurridos desde OnGridReady hasta DroideState.Victory.
        ///   Pasar 0 si el jugador murió.
        /// </param>
        /// <param name="batteryConsumed">
        ///   Batería gastada en [0,1]. (MaxBattery - Battery_final) / MaxBattery.
        ///   Pasar 1 si el jugador murió por batería.
        /// </param>
        /// <param name="lastDeath">
        ///   Causa de muerte. DeathCause.Generic si el jugador ganó.
        /// </param>
        void RecordLevelResult(bool won, float completionTime,
                               float batteryConsumed, DeathCause lastDeath);

        /// <summary>
        /// Aplica la dificultad actual al config IN-PLACE antes de BuildGrid().
        /// OCP: ProceduralGridGenerator no sabe que la config fue modificada.
        /// </summary>
        void Apply(ProceduralLevelConfig config);

        /// <summary>
        /// Dificultad actual en [0, 1]. Para debug overlay y tests.
        /// 0 = mínimo, 1 = máximo teórico (asíntota).
        /// </summary>
        float CurrentDifficulty { get; }
    }
}
