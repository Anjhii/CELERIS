// ============================================================
// ProceduralLevelConfig.cs  |  Assets/Scripts/Config/
// ScriptableObject: parámetros de nivel procedural.
// Crear via menú: Assets > Create > Celeris > LevelConfig
//
// v5:
//   • baseMapLength + lengthGrowthPerLevel reemplazan mapLengthSegments.
//   • Restricciones de clústeres: laser (1-4), charge (3-6).
//   • Escalado de zonas seguras: el "respiro" se reduce con el nivel.
//   • batteryLimit = 100 constante.
//   • Portal interval se calcula dinámicamente en el generador.
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

        // ── Longitud del mapa (dinámica) ──────────────────────
        [Header("Escalado de Longitud")]
        [Tooltip("Tiles base en nivel 0")]
        [Range(10, 40)] public int baseMapLength       = 20;
        [Tooltip("Tiles extra que se añaden por cada nivel")]
        [Range(0, 6)]   public int lengthGrowthPerLevel = 2;

        // ── Batería ───────────────────────────────────────────
        [Header("Batería")]
        [Tooltip("Batería máxima e inicial. Siempre 100 para consistencia visual.")]
        public int batteryLimit = 100;

        // ── Semilla ───────────────────────────────────────────
        [Header("Semilla Procedural")]
        [Tooltip("0 = semilla automática determinista por levelIndex")]
        public int proceduralSeed = 0;

        // ── Pesos de segmentos (base) ─────────────────────────
        [Header("Pesos de segmentos (base)")]
        [Range(1, 10)] public int weightBase   = 3;
        [Range(1, 10)] public int weightLaser  = 2;
        [Range(1, 10)] public int weightCharge = 1;

        // ── Intervalos de láser ───────────────────────────────
        [Header("Intervalos de Láser (valores base antes de escalar)")]
        [Range(0.3f, 6f)] public float laserActiveDuration   = 2.0f;
        [Range(0.3f, 6f)] public float laserInactiveDuration = 2.0f;

        // ── Portal (obsoleto como intervalo manual; el generador calcula) ─
        [Header("Portal")]
        [Tooltip("Legado. El generador v5 usa posicionamiento absoluto (totalTiles/4). " +
                 "Mantener a 0 para usar el nuevo sistema.")]
        [Range(0, 20)] public int portalInterval = 0;

        // ── Restricciones de Clústeres ────────────────────────
        [Header("Restricciones de Clústeres (Tamaños)")]
        [Tooltip("Mínimo de tiles láser consecutivos en un clúster")]
        [Range(1, 4)] public int minLaserCluster  = 1;
        [Tooltip("Máximo de tiles láser consecutivos en un clúster")]
        [Range(1, 8)] public int maxLaserCluster  = 4;
        [Tooltip("Mínimo de tiles charge consecutivos en un clúster")]
        [Range(1, 6)] public int minChargeCluster = 3;
        [Tooltip("Máximo de tiles charge consecutivos en un clúster")]
        [Range(1, 10)] public int maxChargeCluster = 6;

        // ── Escalado de Zonas Seguras ─────────────────────────
        [Header("Escalado de Zonas Seguras (Respiro)")]
        [Tooltip("Máxima distancia segura entre peligros en nivel 1 (tiles base)")]
        [Range(2, 12)] public int maxSafeDistanceStart = 6;
        [Tooltip("Mínima distancia segura en niveles avanzados (nunca cero)")]
        [Range(1, 4)]  public int minSafeDistanceEnd   = 1;

        // ── Escalado automático ───────────────────────────────
        [Header("Escalado automático de dificultad")]
        public bool autoScaleDifficulty = true;

        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Longitud total de este nivel: base + crecimiento por nivel + segmentos extra de dificultad.
        /// Los 2 tiles adicionales son el inicio y la meta.
        /// </summary>
        public int GetTotalTiles(int extraSegments = 0) =>
            baseMapLength + levelIndex * lengthGrowthPerLevel + extraSegments + 2;

        /// <summary>Devuelve los parámetros efectivos escalados por levelIndex.</summary>
        public RuntimeDifficulty GetScaledDifficulty(int totalLevels = 9)
        {
            float t = autoScaleDifficulty
                ? Mathf.Clamp01((float)levelIndex / Mathf.Max(totalLevels - 1, 1))
                : 0f;

            return new RuntimeDifficulty
            {
                Seed                   = ComputeSeed(levelIndex, proceduralSeed),
                LaserWeightMultiplier  = Mathf.Lerp(1f,    3.5f,  t),
                ChargeWeightMultiplier = Mathf.Lerp(1f,    0.20f, t),
                LaserActiveDuration    = Mathf.Lerp(laserActiveDuration,   laserActiveDuration   * 0.28f, t),
                LaserInactiveDuration  = Mathf.Lerp(laserInactiveDuration, laserInactiveDuration * 0.32f, t),
                ExtraSegments          = Mathf.RoundToInt(Mathf.Lerp(0f, 8f, t))
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

        /// <summary>
        /// Tipo de segmento ponderado para la máquina de estados del generador.
        /// Solo devuelve Laser o Charge (nunca Arrow/Base — eso lo decide GenState).
        /// </summary>
        public SegmentType GetWeightedDangerSegment(System.Random rng, RuntimeDifficulty diff)
        {
            int wLaser  = Mathf.Max(1, Mathf.RoundToInt(weightLaser  * diff.LaserWeightMultiplier));
            int wCharge = Mathf.Max(1, Mathf.RoundToInt(weightCharge * diff.ChargeWeightMultiplier));
            int total   = wLaser + wCharge;
            return rng.Next(total) < wLaser ? SegmentType.Laser : SegmentType.Charge;
        }

        /// <summary>Legado: selector de segmento con peso base incluido.</summary>
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
