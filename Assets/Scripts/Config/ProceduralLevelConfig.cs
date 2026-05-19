// ============================================================
// ProceduralLevelConfig.cs  |  Assets/Scripts/Config/
// ScriptableObject: parámetros de nivel procedural.
// Crear via menú: Assets > Create > Celeris > LevelConfig
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Config
{
    [CreateAssetMenu(
        fileName = "LevelConfig",
        menuName  = "Celeris/LevelConfig")]
    public class ProceduralLevelConfig : ScriptableObject
    {
        [Header("Identificación")]
        public int levelIndex = 0;

        [Header("Mapa")]
        [Tooltip("Número de segmentos que forman el mapa (excluye Start y Goal)")]
        [Range(3, 30)]
        public int mapLengthSegments = 8;

        [Tooltip("Ancho del grid en tiles (eje X)")]
        [Range(1, 7)]
        public int gridWidth = 3;

        [Header("Balance")]
        [Tooltip("Batería inicial del Droide")]
        [Range(5, 100)]
        public int batteryLimit = 20;

        [Header("Procedural")]
        [Tooltip("Semilla determinista. 0 = aleatoria en runtime")]
        public int proceduralSeed = 0;

        [Header("Pesos de segmentos (probabilidad relativa)")]
        public int weightArrow     = 3;
        public int weightLaser     = 2;
        public int weightResonance = 2;
        public int weightCharge    = 1;

        // ── Helper: devuelve un tipo de segmento aleatorio
        //    según pesos, usando la semilla del config ────────
        public SegmentType GetWeightedRandomSegment(System.Random rng)
        {
            int total = weightArrow + weightLaser + weightResonance + weightCharge;
            int roll  = rng.Next(0, total);

            if (roll < weightArrow)                              return SegmentType.Arrow;
            if (roll < weightArrow + weightLaser)               return SegmentType.Laser;
            if (roll < weightArrow + weightLaser + weightResonance) return SegmentType.Resonance;
            return SegmentType.Charge;
        }
    }
}
