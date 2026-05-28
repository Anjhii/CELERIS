// ============================================================
// TileComponent.cs  |  Assets/Scripts/Grid/
// Un componente por GameObject de tile. Sin GetComponent
// en runtime: el generador cachea referencias al nacer.
//
// ESCENA: Se añade automáticamente a cada tile instanciado
//         por ProceduralGridGenerator. No crear a mano.
// ============================================================
using Celeris.Data;
using UnityEngine;

namespace Celeris.Grid
{
    public class TileComponent : MonoBehaviour
    {
        // ── Datos ────────────────────────────────────────────
        [Header("Tipo")]
        public TileType tileType = TileType.BaseTile;

        [Header("Arrow")]
        public MoveDirection arrowDirection = MoveDirection.North;

        [Header("Estado interactivo")]
        public bool isActive = true;   // Laser ON-OFF (Resonance eliminada en v2)

        // Coordenada lógica en el grid (asignada por el generador)
        [HideInInspector] public Vector2Int gridCoord;

        // Referencia al renderer para feedback visual placeholder
        private Renderer _rend;

        // Colores placeholder por tipo
        private static readonly Color[] TypeColors =
        {
            new Color(0.25f, 0.25f, 0.25f), // BaseTile      — gris oscuro
            new Color(0.20f, 0.60f, 1.00f), // ArrowTile     — azul
            new Color(0.80f, 0.20f, 0.80f), // ResonanceTile — magenta
            new Color(1.00f, 0.40f, 0.00f), // LaserTile     — naranja
            new Color(0.20f, 0.90f, 0.40f), // ChargeTile    — verde
            Color.black,                    // VoidTile      — negro
            new Color(1.00f, 0.90f, 0.10f)  // GoalTile      — amarillo
        };

        // ── Init ─────────────────────────────────────────────
        private void Awake()
        {
            _rend = GetComponentInChildren<Renderer>();
            ApplyVisual();
        }

        // ── Acciones públicas ─────────────────────────────────

        /// <summary>Rota la dirección de la flecha 90° horario.</summary>
        public void RotateArrow90Degrees()
        {
            if (tileType != TileType.ArrowTile) return;
            arrowDirection = (MoveDirection)(((int)arrowDirection + 1) % 4);
            // Rota el modelo visualmente (eje Y, espacio local)
            transform.Rotate(Vector3.up, 90f, Space.World);
        }

        /// <summary>Alterna estado ON/OFF del Laser.</summary>
        public void ToggleLaser()
        {
            if (tileType != TileType.LaserTile) return;
            isActive = !isActive;
            ApplyVisual();
        }

        // ── Dirección de salida para el Droide ───────────────
        /// <summary>Devuelve el vector de movimiento según el tipo de tile.</summary>
        public Vector2Int GetExitDirection(Vector2Int currentDir)
        {
            return tileType switch
            {
                TileType.ArrowTile => DirectionToVector(arrowDirection),
                TileType.VoidTile  => Vector2Int.zero,
                _                  => currentDir   // continúa recto
            };
        }

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Aplica el color del tipo actual al Renderer.
        /// Llamar desde el generador tras asignar tileType.
        /// </summary>
        public void Refresh()
        {
            if (_rend == null)
                _rend = GetComponentInChildren<Renderer>();
            if (_rend == null) return;
            int idx = Mathf.Clamp((int)tileType, 0, TypeColors.Length - 1);
            Color c = TypeColors[idx];
            if (!isActive) c *= 0.4f;
            _rend.material.color = c;
        }

        // Mantener privado para Awake (evita duplicar lógica)
        private void ApplyVisual() => Refresh();

        public static Vector2Int DirectionToVector(MoveDirection dir) =>
            dir switch
            {
                MoveDirection.North => Vector2Int.up,
                MoveDirection.South => Vector2Int.down,
                MoveDirection.East  => Vector2Int.right,
                MoveDirection.West  => Vector2Int.left,
                _                   => Vector2Int.up
            };
    }
}
