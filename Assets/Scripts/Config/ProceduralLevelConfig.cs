// ============================================================
// ProceduralLevelConfig.cs  |  Assets/Scripts/Config/
// ScriptableObject: parámetros de nivel procedural.
// Crear via menú: Assets > Create > Celeris > LevelConfig
//
// v4:
//   • batteryLimit = 100 (el Droide siempre empieza al 100%)
//   • LaserController intervals configurables por nivel
//   • Portal interval (cada N tiles rectos un PortalTile)
//   • GetScaledDifficulty(): escalado automático por nivel
//   • Solo 6 tipos de tile (sin VoidTile/ResonanceTile)
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Config
{
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Celeris/LevelConfig")]
    public class ProceduralLevelConfig : ScriptableObject
    {
        [Header("Identificación")]
        public int levelIndex = 0;

        [Header("Mapa")]
        [Range(4, 30)] public int mapLengthSegments = 10;

        [Header("Batería")]
        [Tooltip("Batería máxima e inicial. Siempre 100 para consistencia visual.")]
        public int batteryLimit = 100;

        [Header("Semilla Procedural")]
        [Tooltip("0 = semilla automática determinista por levelIndex")]
        public int proceduralSeed = 0;

        [Header("Pesos de segmentos (base)")]
        [Range(1, 10)] public int weightBase   = 3;
        [Range(1, 10)] public int weightLaser  = 2;
        [Range(1, 10)] public int weightCharge = 1;

        [Header("Intervalos de Láser (valores base antes de escalar)")]
        [Range(0.3f, 6f)] public float laserActiveDuration   = 2.0f;
        [Range(0.3f, 6f)] public float laserInactiveDuration = 2.0f;

        [Header("Portal")]
        [Tooltip("Cada N tiles rectos se inserta un PortalTile. 0 = desactivado.")]
        [Range(0, 20)] public int portalInterval = 5;

        [Header("Escalado automático de dificultad")]
        public bool autoScaleDifficulty = true;

        // ─────────────────────────────────────────────────────

        /// <summary>Devuelve los parámetros efectivos escalados por levelIndex.</summary>
        public RuntimeDifficulty GetScaledDifficulty(int totalLevels = 9)
        {
            float t = autoScaleDifficulty
                ? Mathf.Clamp01((float)levelIndex / Mathf.Max(totalLevels - 1, 1))
                : 0f;

            return new RuntimeDifficulty
            {
                Seed                  = ComputeSeed(levelIndex, proceduralSeed),
                LaserWeightMultiplier  = Mathf.Lerp(1f,    3.5f,  t),
                ChargeWeightMultiplier = Mathf.Lerp(1f,    0.20f, t),
                LaserActiveDuration   = Mathf.Lerp(laserActiveDuration,   laserActiveDuration   * 0.28f, t),
                LaserInactiveDuration = Mathf.Lerp(laserInactiveDuration, laserInactiveDuration * 0.32f, t),
                ExtraSegments         = Mathf.RoundToInt(Mathf.Lerp(0f, 8f, t))
            };
        }

        /// <summary>Semilla determinista por nivel (si overrideSeed == 0).</summary>
        public static int ComputeSeed(int lvlIndex, int overrideSeed = 0)
        {
            if (overrideSeed != 0) return overrideSeed;
            unchecked
            {
                int h = lvlIndex + 1;
                h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                return h == 0 ? 1 : h;
            }
        }

        /// <summary>Tipo de segmento ponderado aplicando multiplicadores de dificultad.</summary>
        public SegmentType GetWeightedRandomSegment(System.Random rng, RuntimeDifficulty diff)
        {
            int wLaser  = Mathf.Max(1, Mathf.RoundToInt(weightLaser  * diff.LaserWeightMultiplier));
            int wCharge = Mathf.Max(0, Mathf.RoundToInt(weightCharge * diff.ChargeWeightMultiplier));
            int total   = weightBase + wLaser + wCharge;

            int roll = rng.Next(total);
            if (roll < weightBase)             return SegmentType.Arrow;
            if (roll < weightBase + wLaser)    return SegmentType.Laser;
            return SegmentType.Charge;
        }
    }
}
