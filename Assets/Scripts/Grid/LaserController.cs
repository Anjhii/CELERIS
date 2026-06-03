// ============================================================
// LaserController.cs  |  Assets/Scripts/Grid/
//
// Componente que gestiona el ciclo activo/inactivo de un
// LaserTile mediante una Corrutina.
//
// SETUP:
//   ProceduralGridGenerator lo añade automáticamente a cada
//   tile de tipo LaserTile al generar el grid. No añadir a mano.
//
// PARÁMETROS:
//   activeDuration   — segundos que el láser permanece encendido
//   inactiveDuration — segundos que permanece apagado
//   startActive      — si true, empieza activo; si false, empieza apagado
//
// El controlador escribe en TileComponent.isActive y llama a
// TileComponent.Refresh() para actualizar el color del tile.
// ============================================================
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Grid
{
    [RequireComponent(typeof(TileComponent))]
    public class LaserController : MonoBehaviour
    {
        // ── Inspector / API de configuración ─────────────────
        [Header("Intervalos")]
        [Tooltip("Segundos que el láser permanece activo (peligroso)")]
        public float activeDuration   = 2.0f;

        [Tooltip("Segundos que el láser permanece inactivo (seguro)")]
        public float inactiveDuration = 2.0f;

        [Tooltip("Estado inicial del láser al generarse")]
        public bool startActive = true;

        // ── Privado ───────────────────────────────────────────
        private TileComponent _tile;

        // ── Lifecycle ─────────────────────────────────────────
        private void Awake()
        {
            _tile = GetComponent<TileComponent>();
        }

        private void Start()
        {
            if (_tile == null) return;

            _tile.isActive = startActive;
            _tile.Refresh();
            StartCoroutine(CycleRoutine());
        }

        // ── Ciclo principal ───────────────────────────────────
        private System.Collections.IEnumerator CycleRoutine()
        {
            while (true)
            {
                // Fase activa
                _tile.isActive = true;
                _tile.Refresh();
                yield return new WaitForSeconds(Mathf.Max(0.1f, activeDuration));

                // Fase inactiva
                _tile.isActive = false;
                _tile.Refresh();
                yield return new WaitForSeconds(Mathf.Max(0.1f, inactiveDuration));
            }
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Reconfigura los intervalos en runtime (por ejemplo, al escalar dificultad).
        /// Reinicia el ciclo desde el estado activo.
        /// </summary>
        public void Configure(float active, float inactive, bool startActiveState = true)
        {
            activeDuration   = active;
            inactiveDuration = inactive;
            startActive      = startActiveState;

            StopAllCoroutines();
            if (_tile != null)
            {
                _tile.isActive = startActiveState;
                _tile.Refresh();
            }
            StartCoroutine(CycleRoutine());
        }
    }
}
