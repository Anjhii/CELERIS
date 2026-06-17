// ============================================================
// IObstacleDefinition.cs  |  Assets/Scripts/Grid/
//
// Contrato de extensión para obstáculos mecánicos del camino.
//
// PRINCIPIO OCP (Open/Closed):
//   Añadir un nuevo tipo de obstáculo = crear una clase que
//   implemente esta interfaz + registrarla en ProceduralLevelConfig.
//   El PathSequencer y el ProceduralGridGenerator NO se modifican.
//
// IMPLEMENTACIONES ACTIVAS (Fase 1):
//   - LaserObstacleDefinition   → TileType.LaserTile,  cluster [1-5]
//   - ChargeObstacleDefinition  → TileType.ChargeTile, cluster [3-7]
//
// EXTENSIBILIDAD FUTURA:
//   Para añadir un nuevo obstáculo (ej. EMP, Spike, Shield):
//     1. Crear clase que implemente IObstacleDefinition.
//     2. Añadir su peso en ProceduralLevelConfig.
//     3. Registrar en la tabla de definiciones del generador.
//   El punto de entrada es PathSequencer.BuildObstacleBlock().
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    // ARQUITECTURA:
    //   Implementaciones activas: LaserObstacleDefinition (cluster 1-5), ChargeObstacleDefinition (cluster 3-7).
    //   Próximo caso esperado: EMPObstacleDefinition — desactiva láseres adyacentes por N tiles.
    //   Punto de entrada para extensión: TileFactory.Register(new EMPObstacleDefinition()) en ProceduralGridGenerator.BuildFactory().
    public interface IObstacleDefinition
    {
        /// <summary>
        /// Tipo de tile que este obstáculo representa en el enum global.
        /// Usado por TileDescriptor para identificar el tipo sin dependencia
        /// de la implementación concreta.
        /// </summary>
        TileType TileType { get; }

        /// <summary>
        /// Devuelve el tamaño del cluster para esta instancia.
        /// El PathSequencer llama esto una vez por bloque de obstáculo.
        /// Cada implementación aplica sus propios rangos [min, max].
        /// </summary>
        int GetClusterSize(System.Random rng);

        /// <summary>
        /// Configura los componentes específicos del obstáculo sobre el
        /// GameObject ya instanciado. Llamado por ProceduralGridGenerator
        /// después de instanciar el prefab base.
        ///
        /// FASE 3 completada: TileFactory llama este método para configurar
        /// el GameObject. La responsabilidad de instanciar componentes es de
        /// TileFactory; la responsabilidad de QUÉ componentes añadir es de
        /// cada IObstacleDefinition concreta.
        /// </summary>
        void ConfigureComponent(GameObject tileGo, float activeDuration, float inactiveDuration, int tileIndexInPath);
    }
}
