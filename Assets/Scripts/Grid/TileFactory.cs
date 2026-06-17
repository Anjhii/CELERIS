// ============================================================
// TileFactory.cs  |  Assets/Scripts/Grid/
//
// RESPONSABILIDAD ÚNICA: instanciar y configurar GameObjects de tiles.
//
// PRINCIPIO OCP:
//   TileFactory no contiene switches sobre TileType para obstacle-logic.
//   Toda la configuración específica de un obstáculo vive en su
//   IObstacleDefinition.ConfigureComponent(). Para añadir un nuevo
//   tipo de obstáculo, solo se llama Register() — esta clase no cambia.
//
// TILES NO-OBSTÁCULO (PortalTile, BaseTile, ArrowTile, GoalTile):
//   Se configuran directamente aquí porque son tipos fijos del sistema,
//   no extensibles vía IObstacleDefinition. Si en el futuro un
//   tipo no-obstáculo necesita lógica configurable, se introduce
//   ITileDecorator (no planificado hasta Fase 5).
//
// PATRÓN DE USO:
//   var factory = new TileFactory(tilePrefab, transform, diff);
//   factory.Register(new LaserObstacleDefinition());
//   factory.Register(new ChargeObstacleDefinition());
//   TileComponent tile = factory.Create(placedTile, indexInPath);
//
// OUTPUT:
//   Create() devuelve el TileComponent configurado y listo.
//   Popula el tileMap y registra StartWorldPos para el primer tile.
// ============================================================
using System.Collections.Generic;
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    public class TileFactory
    {
        // ── Dependencias inyectadas ───────────────────────────
        private readonly GameObject     _tilePrefab;
        private readonly Transform      _parent;
        private readonly RuntimeDifficulty _diff;

        // ── Registro OCP de obstáculos ────────────────────────
        // Clave: TileType. Valor: definición de obstáculo.
        // Poblado vía Register() antes de comenzar el spawn.
        private readonly Dictionary<TileType, IObstacleDefinition> _obstacleRegistry = new();

        // ── Output: mapa de tiles y posición de inicio ────────
        private readonly Dictionary<Vector3Int, TileComponent> _tileMap;

        public Vector3 StartWorldPos { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="tilePrefab">Prefab base de todos los tiles.</param>
        /// <param name="parent">Transform padre en la jerarquía de escena.</param>
        /// <param name="diff">Dificultad escalada del nivel (intervalos de laser, etc.).</param>
        /// <param name="tileMap">Diccionario compartido con ProceduralGridGenerator.</param>
        public TileFactory(GameObject tilePrefab, Transform parent,
                           RuntimeDifficulty diff,
                           Dictionary<Vector3Int, TileComponent> tileMap)
        {
            _tilePrefab = tilePrefab;
            _parent     = parent;
            _diff       = diff;
            _tileMap    = tileMap;
        }

        // ── Registro de obstáculos (OCP) ──────────────────────

        /// <summary>
        /// Registra la definición de un obstáculo.
        /// Para añadir un nuevo tipo de obstáculo a TileFactory:
        ///   1. Crear clase que implemente IObstacleDefinition.
        ///   2. Llamar factory.Register(new MiNuevoObstaculo()).
        ///   TileFactory NO se modifica.
        /// </summary>
        public void Register(IObstacleDefinition definition)
        {
            _obstacleRegistry[definition.TileType] = definition;
        }

        // ── Creación de tiles ─────────────────────────────────

        /// <summary>
        /// Instancia y configura el GameObject para un PlacedTile.
        /// Devuelve el TileComponent configurado.
        /// El tile se crea con Y = -5f para la animación de subida.
        /// </summary>
        /// <param name="pt">Tile con descriptor y coordenada 3D.</param>
        /// <param name="indexInPath">Índice global en la lista de tiles (usado por LaserController).</param>
        public TileComponent Create(PlacedTile pt, int indexInPath)
        {
            var worldPos = new Vector3(pt.Coord.x, -5f, pt.Coord.z);
            var go       = Object.Instantiate(_tilePrefab, worldPos, Quaternion.identity, _parent);
            go.name      = $"Tile_{pt.Coord.x}_{pt.Coord.z}_{pt.TileType}";

            var tile = go.GetComponent<TileComponent>() ?? go.AddComponent<TileComponent>();
            tile.gridCoord      = pt.Coord;
            tile.tileType       = pt.TileType;
            tile.arrowDirection = pt.ExitDirection;
            tile.isActive       = true;

            // ── Configuración por tipo ────────────────────────
            ConfigureTile(go, pt, indexInPath);

            _tileMap[pt.Coord] = tile;

            if (indexInPath == 0)
                StartWorldPos = new Vector3(pt.Coord.x, 0f, pt.Coord.z);

            return tile;
        }

        // ── Lógica de configuración ───────────────────────────

        /// <summary>
        /// Delega la configuración del tile:
        ///   - Obstáculos: al IObstacleDefinition registrado (OCP).
        ///   - Tipos fijos: lógica directa aquí (son tipos cerrados del sistema).
        ///
        /// OCP invariante: agregar un nuevo obstáculo nunca modifica este método.
        /// Solo los tipos no-obstáculo (Portal, Base, Arrow, Goal) tienen ramas aquí.
        /// </summary>
        private void ConfigureTile(GameObject go, PlacedTile pt, int indexInPath)
        {
            // ── Obstáculos: delegar a IObstacleDefinition ─────
            if (pt.Descriptor.IsObstacle)
            {
                var def = pt.Descriptor.ObstacleDefinition;
                def.ConfigureComponent(go, _diff.LaserActiveDuration,
                                           _diff.LaserInactiveDuration,
                                           indexInPath);
                return;
            }

            // ── Tipos fijos del sistema ───────────────────────
            switch (pt.TileType)
            {
                case TileType.PortalTile:
                    go.AddComponent<PortalTileComponent>();
                    break;

                // BaseTile, ArrowTile, GoalTile: solo necesitan Refresh visual.
                default:
                    go.GetComponent<TileComponent>()?.Refresh();
                    break;
            }
        }

        /// <summary>
        /// Crea todos los tiles de la lista en orden y devuelve el mapa poblado.
        /// Shortcut para que ProceduralGridGenerator haga una sola llamada.
        /// </summary>
        public void CreateAll(IReadOnlyList<PlacedTile> placed)
        {
            for (int i = 0; i < placed.Count; i++)
                Create(placed[i], i);
        }
    }
}
