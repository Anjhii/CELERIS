// ============================================================
// TileComponent.cs  |  Assets/Scripts/Grid/
// Un componente por GameObject de tile.
//
// v4:
//   • Solo 6 tipos de tile: Base, Arrow, Laser, Charge, Goal, Portal.
//   • TypeColors indexado por (int)TileType directamente.
//   • GetExitDirection: sin VoidTile; continúa recto por defecto.
// ============================================================
using Celeris.Data;
using System.Collections;
using UnityEngine;

namespace Celeris.Grid
{
    public class TileComponent : MonoBehaviour
    {
        // ── Datos ─────────────────────────────────────────────
        [Header("Tipo")]
        public TileType tileType = TileType.BaseTile;

        [Header("Arrow")]
        public MoveDirection arrowDirection = MoveDirection.North;

        [Header("Estado interactivo")]
        /// <summary>
        /// Para LaserTile: true = activo (peligroso).
        /// Gestionado por LaserController en runtime.
        /// </summary>
        public bool isActive = true;

        [HideInInspector] public Vector2Int gridCoord;

        // ── Renderer ─────────────────────────────────────────
        private Renderer _rend;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Colores placeholder indexados por (int)TileType
        private static readonly Color[] TypeColors =
        {
            new Color(0.22f, 0.22f, 0.22f),   // 0 BaseTile   — gris oscuro
            new Color(0.20f, 0.55f, 1.00f),   // 1 ArrowTile  — azul
            new Color(1.00f, 0.38f, 0.00f),   // 2 LaserTile  — naranja
            new Color(0.15f, 0.85f, 0.35f),   // 3 ChargeTile — verde
            new Color(1.00f, 0.88f, 0.08f),   // 4 GoalTile   — amarillo
            new Color(0.58f, 0.08f, 1.00f)    // 5 PortalTile — violeta
        };

        // ── Init ─────────────────────────────────────────────
        private void Awake()
        {
            _rend = GetComponentInChildren<Renderer>();
            Refresh();
        }

        // ── Acciones públicas ─────────────────────────────────

        public void RotateArrow90Degrees()
        {
            if (tileType != TileType.ArrowTile) return;
            arrowDirection = (MoveDirection)(((int)arrowDirection + 1) % 4);
            transform.Rotate(Vector3.up, 90f, Space.World);
        }

        public void ToggleLaser()
        {
            if (tileType != TileType.LaserTile) return;
            isActive = !isActive;
            Refresh();
        }

        // ── Dirección de salida ───────────────────────────────
        public Vector2Int GetExitDirection(Vector2Int currentDir) =>
            tileType == TileType.ArrowTile
                ? DirectionToVector(arrowDirection)
                : currentDir;

        // ── Visual ───────────────────────────────────────────
        public void Refresh()
        {
            if (_rend == null) _rend = GetComponentInChildren<Renderer>();
            if (_rend == null) return;

            int idx = Mathf.Clamp((int)tileType, 0, TypeColors.Length - 1);
            Color c = TypeColors[idx];

            if (tileType == TileType.LaserTile && !isActive) c *= 0.30f;

            _rend.material.color = c;
        }

        public void PulseEmission(float duration = 0.3f)
        {
            if (_rend == null) return;
            StartCoroutine(EmissionPulseRoutine(duration));
        }

        private IEnumerator EmissionPulseRoutine(float duration)
        {
            var mat = _rend.material;

            Color baseEmission = mat.GetColor(EmissionColorID);
            Color peakEmission = baseEmission + new Color(0.4f, 0.5f, 0.6f) * 2f;

            float elapsed = 0f;
            float half    = duration * 0.5f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / half);
                mat.SetColor(EmissionColorID, Color.Lerp(baseEmission, peakEmission, t));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / half);
                mat.SetColor(EmissionColorID, Color.Lerp(peakEmission, baseEmission, t));
                yield return null;
            }

            mat.SetColor(EmissionColorID, baseEmission);
        }

        public static Vector2Int DirectionToVector(MoveDirection dir) => dir switch
        {
            MoveDirection.North => Vector2Int.up,
            MoveDirection.South => Vector2Int.down,
            MoveDirection.East  => Vector2Int.right,
            MoveDirection.West  => Vector2Int.left,
            _                   => Vector2Int.up
        };
    }
}
