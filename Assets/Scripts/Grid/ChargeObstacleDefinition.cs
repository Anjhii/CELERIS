// ============================================================
// ChargeObstacleDefinition.cs  |  Assets/Scripts/Grid/
//
// Implementación de IObstacleDefinition para ChargeTile.
//
// REGLAS DE NEGOCIO (Fase 1):
//   Tamaño de cluster: aleatorio en [3, 7] tiles consecutivos.
//   Prohibido: menos de 3 o más de 7 tiles consecutivos.
//   Comportamiento: FrictionMovementState en DroideController.
//   ChargeTile no necesita componente adicional en el GameObject;
//   la lógica de fricción la activa DroideController al detectar
//   el tipo del tile. ConfigureComponent es no-op en esta versión.
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    public class ChargeObstacleDefinition : IObstacleDefinition
    {
        // ── Rangos de cluster (Regla de Negocio — inamovibles) ──
        private const int ClusterMin = 3;
        private const int ClusterMax = 7;  // rng.Next(3,8) → [3,7] inclusivo

        public TileType TileType => TileType.ChargeTile;

        public int GetClusterSize(System.Random rng)
        {
            // Next(min, max) en System.Random es [min, max-1] inclusivo.
            // Para [3,7] se usa Next(ClusterMin, ClusterMax + 1).
            return rng.Next(ClusterMin, ClusterMax + 1);
        }

        /// <summary>
        /// ChargeTile no requiere componente extra: DroideController detecta
        /// TileType.ChargeTile y activa FrictionMovementState por sí solo.
        /// Este método es no-op intencionalmente.
        ///
        /// EXTENSIBILIDAD: si en el futuro ChargeTile necesita un componente
        /// (ej. efecto visual de campo magnético), se añade aquí sin tocar
        /// el generador ni el PathSequencer.
        /// </summary>
        public void ConfigureComponent(GameObject tileGo, float activeDuration,
                                       float inactiveDuration, int tileIndexInPath)
        {
            // No-op: ChargeTile no necesita configuración de componente adicional.
        }
    }
}
