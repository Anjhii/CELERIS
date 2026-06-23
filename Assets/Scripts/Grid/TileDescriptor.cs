// ============================================================
// TileDescriptor.cs  |  Assets/Scripts/Grid/
//
// Resultado del Paso A (PathSequencer): descripción lógica de
// un tile sin posición ni dirección de salida.
//
// PROPÓSITO:
//   Representar "QUÉ hay en el camino" de forma independiente
//   de "POR DÓNDE va el camino". Esta separación es el cambio
//   arquitectural central de Fase 1.
//
// ESCALABILIDAD:
//   • obstacleDefinition (nullable): si es null, el tile no tiene
//     lógica de obstáculo especial (BaseTile, PortalTile, GoalTile).
//     Si no es null, el tipo real es obstacleDefinition.TileType.
//
//   • GravityVector (Vector3): reservado para Fase 4 (wall-walking).
//     Hoy siempre vale Vector3.down. El DroideController lo leerá
//     en Fase 4 para aplicar gravedad local al entrar al tile.
//     NO implementar lógica de gravedad hasta esa fase.
//
// NOTA ARQUITECTURAL — COORDENADAS:
//   TileDescriptor NO contiene coordenadas. Las coordenadas son
//   responsabilidad de PlacedTile (resultado del Paso B).
//   Esta clase opera en dominio lógico puro.
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    /// <summary>
    /// Descripción lógica de un tile en la secuencia del nivel.
    /// Producido por PathSequencer. Consumido por PathGeometryTracer.
    /// </summary>
    public readonly struct TileDescriptor
    {
        // ── Identidad ─────────────────────────────────────────

        /// <summary>
        /// Tipo de tile. Para obstáculos, coincide con
        /// obstacleDefinition.TileType. Para el resto,
        /// es BaseTile, PortalTile, ArrowTile o GoalTile.
        /// </summary>
        public readonly TileType TileType;

        // ── Obstáculo (nullable) ──────────────────────────────

        /// <summary>
        /// Definición del obstáculo. Null para tiles no-obstáculo.
        /// El PathGeometryTracer y el generador delegan en esta
        /// referencia para configurar el componente, sin conocer
        /// el tipo concreto de obstáculo (OCP).
        /// </summary>
        public readonly IObstacleDefinition ObstacleDefinition;

        // ── Metadatos de posición en pipeline ─────────────────

        /// <summary>
        /// Índice del portal al que pertenece este tile (0, 1 o 2).
        /// Solo relevante si TileType == PortalTile.
        /// -1 para tiles que no son portal.
        /// </summary>
        public readonly int PortalIndex;

        // ── Escalabilidad Futura — Gravedad Dinámica ──────────
        // FASE 4 (wall-walking): DroideController leerá GravityVector
        // al entrar a un tile para aplicar Physics.gravity correctamente.
        // Hoy todos los tiles tienen Vector3.down. NO usar hasta Fase 4.

        /// <summary>
        /// Vector de gravedad local para este tile.
        /// Valor actual: siempre Vector3.down (plano horizontal).
        /// Reservado para wall-walking en Fase 4.
        /// </summary>
        public readonly Vector3 GravityVector;

        // ── Constructores ─────────────────────────────────────

        /// <summary>Constructor para tiles básicos (Base, Goal, Portal).</summary>
        public TileDescriptor(TileType type, int portalIndex = -1)
        {
            TileType           = type;
            ObstacleDefinition = null;
            PortalIndex        = portalIndex;
            GravityVector      = Vector3.down;
        }

        /// <summary>Constructor para tiles de obstáculo.</summary>
        public TileDescriptor(IObstacleDefinition definition)
        {
            TileType           = definition.TileType;
            ObstacleDefinition = definition;
            PortalIndex        = -1;
            GravityVector      = Vector3.down;
        }

        // ── Helpers de consulta ───────────────────────────────

        public bool IsObstacle    => ObstacleDefinition != null;
        public bool IsPortal      => TileType == TileType.PortalTile;
        public bool IsGoal        => TileType == TileType.GoalTile;
        public bool IsBase        => TileType == TileType.BaseTile;
        public bool IsArrow       => TileType == TileType.ArrowTile;

        public override string ToString() =>
            IsObstacle
                ? $"{TileType}(obstacle)"
                : PortalIndex >= 0
                    ? $"Portal[{PortalIndex}]"
                    : TileType.ToString();
    }
}
