// ============================================================
// DifficultyActuator.cs  |  Assets/Scripts/Core/
//
// RESPONSABILIDAD ÚNICA (SRP):
//   Tomar el escalar D y modificar ProceduralLevelConfig in-place.
//   No calcula D — eso es DifficultyModel.
//   No observa eventos — eso es PerformanceTracker.
//
// PRINCIPIO OCP:
//   ProceduralGridGenerator NO se modifica. Lee el config normalmente.
//   El actuador es el único punto de escritura sobre el config
//   antes de que el generador lo consuma. Añadir un nuevo parámetro
//   controlado por D = añadir una línea aquí, zero cambios fuera.
//
// PARÁMETROS CONTROLADOS POR D:
//   ─────────────────────────────────────────────────────────────
//   Campo en ProceduralLevelConfig      D=0 (min)     D=1 (max)
//   ─────────────────────────────────────────────────────────────
//   weightLaser (relativo a weightBase) 1             4
//   weightCharge (relativo a weightBase)2             0
//   batteryLimit                        base * 1.20f  base * 0.70f
//   ─────────────────────────────────────────────────────────────
//
// NOTA IMPORTANTE — batteryLimit:
//   ProceduralLevelConfig.batteryLimit se usa como "límite del nivel".
//   El valor base lo leemos ANTES de modificar para no acumular
//   modificaciones entre niveles. Por eso el actuador GUARDA
//   el batteryLimit original del ScriptableObject en su primer Apply.
//
// INVARIANTE:
//   Apply() es idempotente: llamarlo N veces con el mismo D
//   produce siempre el mismo resultado (usa _baseBatteryLimit).
// ============================================================

using Celeris.Config;
using UnityEngine;

namespace Celeris.Core
{
    /// <summary>
    /// Aplica el escalar D de dificultad a un ProceduralLevelConfig in-place.
    /// </summary>
    public sealed class DifficultyActuator
    {
        // ── Pesos base (se leen del config la primera vez) ─────
        private int   _baseWeightLaser  = -1;   // -1 = no inicializado
        private int   _baseWeightCharge = -1;
        private int   _baseBatteryLimit = -1;

        // ── Rangos de pesos de obstáculos ─────────────────────
        private const int LASER_WEIGHT_MIN  = 1;
        private const int LASER_WEIGHT_MAX  = 8;
        private const int CHARGE_WEIGHT_MIN = 0;
        private const int CHARGE_WEIGHT_MAX = 5;

        // ── Multiplicadores de batería ─────────────────────────
        private const float BATTERY_MULT_MIN = 1.20f;  // D=0: 20% de bonus
        private const float BATTERY_MULT_MAX = 0.70f;  // D=1: 30% de penalización

        // ══════════════════════════════════════════════════════
        //  API PÚBLICA
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Modifica config in-place según D ∈ [0, 1].
        /// Llamar ANTES de ProceduralGridGenerator.BuildGrid().
        /// </summary>
        public void Apply(ProceduralLevelConfig config, float d)
        {
            if (config == null) return;
            d = Mathf.Clamp01(d);

            // Leer valores base del ScriptableObject la primera vez.
            // Los valores base son los diseñados por el Game Designer en el Inspector —
            // el actuador los interpola, nunca los reemplaza permanentemente.
            if (_baseWeightLaser < 0)
            {
                _baseWeightLaser  = config.weightLaser;
                _baseWeightCharge = config.weightCharge;
                _baseBatteryLimit = config.batteryLimit;
            }

            // ── Peso láser: sube con D ────────────────────────
            // D=0 → LASER_WEIGHT_MIN, D=1 → LASER_WEIGHT_MAX
            config.weightLaser = Mathf.RoundToInt(
                Mathf.Lerp(LASER_WEIGHT_MIN, LASER_WEIGHT_MAX, d));

            // ── Peso charge: baja con D ───────────────────────
            // D=0 → CHARGE_WEIGHT_MAX, D=1 → CHARGE_WEIGHT_MIN
            config.weightCharge = Mathf.RoundToInt(
                Mathf.Lerp(CHARGE_WEIGHT_MAX, CHARGE_WEIGHT_MIN, d));

            // ── Batería inicial: penaliza con D ───────────────
            // D=0 → base * 1.20 (bono), D=1 → base * 0.70 (penalización)
            float batteryMult   = Mathf.Lerp(BATTERY_MULT_MIN, BATTERY_MULT_MAX, d);
            config.batteryLimit = Mathf.Max(10,
                Mathf.RoundToInt(_baseBatteryLimit * batteryMult));

            Debug.Log($"[DifficultyActuator] D={d:F2} → " +
                      $"wLaser={config.weightLaser} " +
                      $"wCharge={config.weightCharge} " +
                      $"battery={config.batteryLimit}");
        }

        /// <summary>
        /// Resetea los valores base cacheados.
        /// Llamar al cambiar de nivel (el nuevo config tiene su propio base).
        /// </summary>
        public void ResetBaseline()
        {
            _baseWeightLaser  = -1;
            _baseWeightCharge = -1;
            _baseBatteryLimit = -1;
        }
    }
}
