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

        [Tooltip("Constante de suavidad de la curva sigmoide. " +
                 "A n=k el parámetro alcanza ~63% del máximo. " +
                 "A n=3k alcanza ~95%. Valor recomendado: 15.")]
        [Range(5f, 40f)] public float difficultyK = 15f;

        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Longitud total de este nivel: base + crecimiento por nivel + segmentos extra de dificultad.
        /// Los 2 tiles adicionales son el inicio y la meta.
        /// </summary>
        public int GetTotalTiles(int extraSegments = 0) =>
            baseMapLength + levelIndex * lengthGrowthPerLevel + extraSegments + 2;

        /// <summary>
        /// Devuelve los parámetros efectivos escalados por levelIndex.
        ///
        /// MODELO: función sigmoide f(n) = max * (1 - e^(-n/k))
        ///   • n = levelIndex (sin techo de niveles)
        ///   • k = difficultyK (15 por defecto)
        ///   • A n=k  → ~63% del máximo
        ///   • A n=3k → ~95% del máximo
        ///   • Asíntota finita: la dificultad nunca supera el valor máximo
        ///     configurado, el juego nunca cruza el umbral de imposibilidad.
        ///
        /// DIFERENCIA CON v5 (Lerp lineal con totalLevels=9):
        ///   La versión anterior bloqueaba la dificultad en su máximo desde el
        ///   nivel 9. Este modelo crece indefinidamente pero con velocidad
        ///   decreciente — adecuado para niveles infinitos.
        /// </summary>
        public RuntimeDifficulty GetScaledDifficulty()
        {
            float s = autoScaleDifficulty
                ? SigmoidT(levelIndex, difficultyK)
                : 0f;

            return new RuntimeDifficulty
            {
                Seed                   = ComputeSeed(levelIndex, proceduralSeed),
                // Laser se vuelve más frecuente con el nivel (1.0 → 3.5)
                LaserWeightMultiplier  = 1f + 2.5f * s,
                // Charge se vuelve menos frecuente con el nivel (1.0 → 0.2)
                ChargeWeightMultiplier = 1f - 0.8f * s,
                // Laser activo se acorta (más difícil de esquivar)
                LaserActiveDuration    = laserActiveDuration    * (1f - 0.72f * s),
                // Laser inactivo se acorta (menos tiempo de respiro)
                LaserInactiveDuration  = laserInactiveDuration  * (1f - 0.68f * s),
                // Segmentos extra (0 → 8)
                ExtraSegments          = Mathf.RoundToInt(8f * s)
            };
        }

        /// <summary>
        /// Curva sigmoide normalizada [0, 1).
        /// f(n, k) = 1 - e^(-n/k)
        /// Nunca llega exactamente a 1 — la dificultad tiene asíntota pero no techo absoluto.
        /// </summary>
        private static float SigmoidT(int n, float k)
        {
            if (!Mathf.Approximately(k, 0f))
                return 1f - Mathf.Exp(-n / k);
            return n > 0 ? 1f : 0f;
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

        // F3-T2 (Junio 2026): GetWeightedDangerSegment() y GetWeightedRandomSegment()
        // eliminados. Eran dead code del sistema de generación anterior.
        // El sistema activo usa PathSequencer + IObstacleDefinition[] (ver ProceduralGridGenerator).
    }
}
