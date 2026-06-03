// ============================================================
// PortalTileComponent.cs  |  Assets/Scripts/Grid/
//
// Componente añadido automáticamente por ProceduralGridGenerator
// a los tiles de tipo PortalTile.
//
// Estados visuales:
//   Pending   — el portal está disponible (color brillante).
//   Completed — el portal ya fue visitado (color sombreado/gris).
//
// El TileComponent base gestiona el color genérico; este
// componente sobreescribe con colores específicos de portal
// y expone MarkCompleted() para que GameFlowManager lo llame
// al regresar del minijuego.
// ============================================================
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Grid
{
    [RequireComponent(typeof(TileComponent))]
    public class PortalTileComponent : MonoBehaviour
    {
        // ── Colores ───────────────────────────────────────────
        [Header("Colores del Portal")]
        [Tooltip("Color cuando el portal está disponible")]
        public Color colorPending   = new Color(0.60f, 0.10f, 1.00f);  // violeta brillante

        [Tooltip("Color cuando el portal ya fue completado")]
        public Color colorCompleted = new Color(0.25f, 0.25f, 0.35f);  // gris apagado

        // ── Estado ────────────────────────────────────────────
        public bool IsCompleted { get; private set; } = false;

        // ── Privado ───────────────────────────────────────────
        private TileComponent _tile;
        private Renderer      _rend;

        // ── Lifecycle ─────────────────────────────────────────
        private void Awake()
        {
            _tile = GetComponent<TileComponent>();
            _rend = GetComponentInChildren<Renderer>();
            ApplyColor();
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Marca el portal como completado y actualiza el visual.
        /// Llamar desde GameFlowManager al volver del minijuego.
        /// </summary>
        public void MarkCompleted()
        {
            IsCompleted = true;
            ApplyColor();
            Debug.Log($"[PortalTileComponent] Portal en {_tile?.gridCoord} completado.");
        }

        /// <summary>Resetea el portal (por ejemplo, al reiniciar nivel).</summary>
        public void ResetState()
        {
            IsCompleted = false;
            ApplyColor();
        }

        // ── Helpers ───────────────────────────────────────────
        private void ApplyColor()
        {
            if (_rend == null) return;
            _rend.material.color = IsCompleted ? colorCompleted : colorPending;
        }
    }
}
