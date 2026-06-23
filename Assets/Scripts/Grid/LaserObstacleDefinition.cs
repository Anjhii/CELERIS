// ============================================================
// LaserObstacleDefinition.cs  |  Assets/Scripts/Grid/
//
// Implementación de IObstacleDefinition para LaserTile.
//
// REGLAS DE NEGOCIO (Fase 1):
//   Tamaño de cluster: aleatorio en [1, 5] tiles consecutivos.
//   Comportamiento: ciclo activo/inactivo gestionado por LaserController.
//   El estado inicial alterna por índice de tile para evitar que todos
//   los láseres de un cluster estén sincronizados (mejor jugabilidad).
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    public class LaserObstacleDefinition : IObstacleDefinition
    {
        // ── Rangos de cluster (Regla de Negocio — inamovibles) ──
        private const int ClusterMin = 1;
        private const int ClusterMax = 5;  // rng.Next(1,6) → [1,5] inclusivo

        public TileType TileType => TileType.LaserTile;

        public int GetClusterSize(System.Random rng)
        {
            // Next(min, max) en System.Random es [min, max-1] inclusivo.
            // Para [1,5] se usa Next(ClusterMin, ClusterMax + 1).
            return rng.Next(ClusterMin, ClusterMax + 1);
        }

        /// <summary>
        /// Añade LaserController al tile y lo configura con los intervalos
        /// de dificultad del nivel. El estado inicial alterna por índice
        /// para que los láseres de un mismo cluster no estén en fase.
        /// </summary>
        public void ConfigureComponent(GameObject tileGo, float activeDuration,
                                       float inactiveDuration, int tileIndexInPath)
        {
            var lc = tileGo.AddComponent<LaserController>();
            lc.Configure(
                active:           activeDuration,
                inactive:         inactiveDuration,
                startActiveState: (tileIndexInPath % 3 != 0)
            );
        }
    }
}
