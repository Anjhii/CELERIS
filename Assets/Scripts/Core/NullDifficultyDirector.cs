// ============================================================
// NullDifficultyDirector.cs  |  Assets/Scripts/Core/
//
// PATRÓN: Null Object — implementación de IDifficultyDirector
//   que no hace nada. D se fija en el valor pasado al constructor.
//
// USOS:
//   • QA / diseño de niveles: forzar D constante sin tener que
//     completar niveles para alcanzar la dificultad deseada.
//   • Tests unitarios: inyectar como stub sin efectos secundarios.
//   • Flag de debug en LevelManager: si debugForceD >= 0,
//     LevelManager inyecta NullDifficultyDirector(debugForceD).
//
// OCP: LevelManager no necesita saber si está usando el director
//   real o el null — ambos implementan IDifficultyDirector.
// ============================================================

using Celeris.Config;
using Celeris.Data;

namespace Celeris.Core
{
    /// <summary>
    /// Director de dificultad nulo: no observa, no calcula.
    /// Aplica un D constante configurado en el constructor.
    /// </summary>
    public sealed class NullDifficultyDirector : IDifficultyDirector
    {
        public float CurrentDifficulty { get; }

        private readonly DifficultyActuator _actuator = new();

        /// <summary>
        /// Crea el director nulo con D fijo.
        /// </summary>
        /// <param name="fixedD">D constante a aplicar. Default = 0.5 (dificultad media).</param>
        public NullDifficultyDirector(float fixedD = 0.5f)
        {
            CurrentDifficulty = DifficultyModel.Clamp(fixedD);
        }

        public void RecordLevelResult(bool won, float completionTime,
                                      float batteryConsumed, DeathCause lastDeath)
        {
            // Null Object: no hace nada. D no cambia.
        }

        public void Apply(ProceduralLevelConfig config)
        {
            if (config == null) return;
            _actuator.ResetBaseline();
            _actuator.Apply(config, CurrentDifficulty);
        }
    }
}
