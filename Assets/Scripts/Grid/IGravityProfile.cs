// ============================================================
// IGravityProfile.cs  |  Assets/Scripts/Grid/
//
// STUB — Fase 4 (wall-walking). No tiene implementación activa.
//
// PROPÓSITO:
//   Define qué direcciones de avance y giro son válidas en cada
//   nodo del grafo de tiles. Hoy el único perfil es el plano XZ
//   (FlatGravityProfile). En Fase 4, perfiles adicionales
//   permitirán que el camino suba por paredes o techos.
//
// PUNTO DE CONEXIÓN:
//   PathGeometryTracer recibe un IGravityProfile en su constructor.
//   Hoy siempre recibe FlatGravityProfile (nuevo Vector3Int(0,-1,0)).
//   En Fase 4, ProceduralGridGenerator inyectará el perfil correcto
//   según el tipo de nivel.
//
// REGLA: No implementar lógica de wall-walking hasta Fase 4.
//   Solo el contrato y el perfil por defecto existen en esta fase.
// ============================================================
using UnityEngine;

namespace Celeris.Grid
{
    // ARQUITECTURA:
    //   Implementaciones activas: FlatGravityProfile — plano XZ, gravedad −Y, 4 direcciones (N/S/E/O).
    //   Próximo caso esperado: WallGravityProfile — el camino asciende por una pared (gravedad lateral, nuevas direcciones válidas).
    //   Punto de entrada para extensión: PathGeometryTracer(... gravityProfile: new WallGravityProfile()) en ProceduralGridGenerator.BuildGrid().
    /// <summary>
    /// Contrato para definir el plano de movimiento local de un nodo.
    /// Stub preparatorio para wall-walking (Fase 4).
    /// </summary>
    public interface IGravityProfile
    {
        /// <summary>
        /// Vector de gravedad local para el nodo en la coordenada dada.
        /// Hoy siempre Vector3.down. En Fase 4 puede ser cualquier dirección.
        /// </summary>
        Vector3 GetGravityAt(Vector3Int coord);

        /// <summary>
        /// Devuelve las direcciones de avance válidas desde la coordenada dada.
        /// Hoy solo las 4 direcciones del plano XZ.
        /// En Fase 4 incluirá direcciones para rampas/paredes.
        /// </summary>
        Vector3Int[] GetValidAdvanceDirections(Vector3Int coord);
    }

    /// <summary>
    /// Perfil por defecto: movimiento exclusivamente en plano XZ, gravedad hacia -Y.
    /// Única implementación activa hasta Fase 4.
    /// </summary>
    public class FlatGravityProfile : IGravityProfile
    {
        public static readonly FlatGravityProfile Instance = new();

        private static readonly Vector3Int[] _directions =
        {
            new( 0, 0,  1),  // Norte
            new( 0, 0, -1),  // Sur
            new( 1, 0,  0),  // Este
            new(-1, 0,  0),  // Oeste
        };

        public Vector3 GetGravityAt(Vector3Int coord) => Vector3.down;

        public Vector3Int[] GetValidAdvanceDirections(Vector3Int coord) => _directions;
    }
}
