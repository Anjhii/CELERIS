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
using System;
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Grid
{
    [RequireComponent(typeof(TileComponent))]
    public class LaserController : MonoBehaviour
    {
        // ── Evento estático ───────────────────────────────────
        /// <summary>
        /// Disparado cuando un láser transiciona de inactivo a activo.
        /// Envía la coordenada de grid del tile para comparación eficiente.
        /// DroideController suscribe para detectar láseres que se encienden
        /// mientras el droide está parado sobre ellos.
        /// MIGRACIÓN Fase 2: Vector2Int → Vector3Int (Y siempre 0 hasta Fase 3/4).
        /// </summary>
        public static event Action<Vector3Int> OnLaserActivated;

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

        private void OnDisable()
        {
            // Detener CycleRoutine al desactivarse para evitar corrutinas zombie.
            // Configure() ya llama StopAllCoroutines() antes de reiniciar,
            // pero OnDisable cubre el path de destruccion de tile en mid-cycle.
            StopAllCoroutines();
        }

        // ── Ciclo principal ───────────────────────────────────
        private System.Collections.IEnumerator CycleRoutine()
        {
            while (true)
            {
                // Fase activa
                bool wasInactive = !_tile.isActive;
                _tile.isActive = true;
                _tile.Refresh();
                // Notificar solo cuando el láser pasa de inactivo a activo
                if (wasInactive) OnLaserActivated?.Invoke(_tile.gridCoord);
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
