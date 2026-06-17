// ============================================================
// PlacedTile.cs  |  Assets/Scripts/Grid/
//
// Resultado del Paso B (PathGeometryTracer): TileDescriptor
// con posición 3D y dirección de salida asignadas.
//
// ESCALABILIDAD — COORDENADAS 3D:
//   Coord es Vector3Int. Hoy todos los valores de Y son 0
//   (camino en plano horizontal). En Fase 3/4, cuando se
//   implemente wall-walking, PathGeometryTracer empezará a
//   producir valores Y != 0. El resto del sistema no cambia
//   porque ya opera sobre Vector3Int desde esta fase.
//
// NOTA:
//   ArrowTile es el único tipo que PathGeometryTracer puede
//   INSERTAR en la lista de PlacedTile sin que venga de un
//   TileDescriptor del PathSequencer. Representa un giro
//   geométrico y no forma parte de la secuencia lógica.
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    /// <summary>
    /// Tile con posición y dirección de salida asignadas.
    /// Producido por PathGeometryTracer. Consumido por ProceduralGridGenerator.
    /// </summary>
    public readonly struct PlacedTile
    {
        // ── Qué es ────────────────────────────────────────────
        public readonly TileDescriptor Descriptor;

        // ── Dónde está ────────────────────────────────────────

        /// <summary>
        /// Coordenada de grid en espacio 3D.
        /// Y = 0 en Fase 1. Y != 0 habilitado en Fase 3/4 (wall-walking).
        /// </summary>
        public readonly Vector3Int Coord;

        // ── Hacia dónde sale ──────────────────────────────────

        /// <summary>
        /// Dirección de salida de este tile hacia el siguiente.
        /// Para ArrowTile insertado por el trazador, esta es la
        /// nueva dirección después del giro.
        /// </summary>
        public readonly MoveDirection ExitDirection;

        // ── Shortcut de tipo ─────────────────────────────────
        public TileType TileType => Descriptor.TileType;

        // ── Constructores ─────────────────────────────────────

        /// <summary>Tile procedente de un TileDescriptor del PathSequencer.</summary>
        public PlacedTile(TileDescriptor descriptor, Vector3Int coord, MoveDirection exitDir)
        {
            Descriptor    = descriptor;
            Coord         = coord;
            ExitDirection = exitDir;
        }

        /// <summary>
        /// ArrowTile insertado por el PathGeometryTracer al detectar un giro.
        /// No tiene TileDescriptor del sequencer; se construye directamente.
        /// </summary>
        public static PlacedTile Arrow(Vector3Int coord, MoveDirection exitDir)
        {
            return new PlacedTile(
                new TileDescriptor(TileType.ArrowTile),
                coord,
                exitDir
            );
        }

        public override string ToString() =>
            $"{Descriptor} @ ({Coord.x},{Coord.y},{Coord.z}) →{ExitDirection}";
    }
}
