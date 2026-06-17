// ============================================================
// DifficultyDirectorImpl.cs  |  Assets/Scripts/Core/
//
// IMPLEMENTACIÓN PRINCIPAL de IDifficultyDirector.
//
// Coordina los tres subsistemas del Game Director:
//   PerformanceTracker → observa eventos del Droide
//   DifficultyModel    → calcula D con curva sigmoid
//   DifficultyActuator → aplica D al ProceduralLevelConfig
//
// SRP: esta clase es un coordinador puro.
//   No observa, no calcula, no modifica el config por sí misma.
//   Delega cada responsabilidad al subsistema correspondiente.
//
// DIP: LevelManager depende de IDifficultyDirector, no de esta clase.
//
// PERSISTENCIA:
//   D se guarda en IPlayerProgressStore con clave PREF_KEY_D.
//   Al arrancar, se restaura el D de la sesión anterior para
//   continuidad entre sesiones.
// ============================================================

using Celeris.Config;
using Celeris.Data;
using Celeris.Player;
using UnityEngine;

namespace Celeris.Core
{
    /// <summary>
    /// Implementación de producción del Game Director.
    /// Coordina PerformanceTracker, DifficultyModel y DifficultyActuator.
    /// </summary>
    public sealed class DifficultyDirectorImpl : IDifficultyDirector
    {
        // ── Clave de persistencia ──────────────────────────────
        private const string PREF_KEY_D = "CELERIS_DifficultyD";

        // ── Subsistemas ───────────────────────────────────────
        private readonly PerformanceTracker _tracker;
        private readonly DifficultyModel    _model;
        private readonly DifficultyActuator _actuator;
        private readonly IPlayerProgressStore _store;

        // ── IDifficultyDirector ───────────────────────────────
        public float CurrentDifficulty => _model.D;

        // ══════════════════════════════════════════════════════
        //  CONSTRUCCIÓN
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Crea el director y restaura D de la sesión anterior.
        /// </summary>
        /// <param name="store">Store de persistencia (PlayerProgressStore).</param>
        /// <param name="k">Constante de saturación del modelo sigmoid (default 5).</param>
        public DifficultyDirectorImpl(IPlayerProgressStore store, float k = 5f)
        {
            _store    = store;
            _tracker  = new PerformanceTracker();
            _actuator = new DifficultyActuator();

            // Restaurar D persistido de la sesión anterior
            float savedD = LoadD();
            _model = new DifficultyModel(savedD, k);

            Debug.Log($"[DifficultyDirector] Iniciado — D restaurado={savedD:F2}  k={k}");
        }

        // ══════════════════════════════════════════════════════
        //  IDifficultyDirector
        // ══════════════════════════════════════════════════════

        public void RecordLevelResult(bool won, float completionTime,
                                      float batteryConsumed, DeathCause lastDeath)
        {
            // El tracker ya registró el resultado vía eventos de DroideCore.
            // Aquí actualizamos el modelo con las métricas calculadas por el tracker.
            _model.Update(_tracker);
            SaveD(_model.D);
            Debug.Log($"[DifficultyDirector] RecordResult won={won} " +
                      $"D={_model.D:F2} " +
                      $"consWins={_tracker.ConsecutiveWins} " +
                      $"consLoss={_tracker.ConsecutiveLosses}");
        }

        public void Apply(ProceduralLevelConfig config)
        {
            _actuator.ResetBaseline();
            _actuator.Apply(config, _model.D);
        }

        // ══════════════════════════════════════════════════════
        //  GESTIÓN DEL DROIDE (ATTACH / DETACH)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Suscribe el tracker al DroideCore de la escena actual.
        /// Llamar desde LevelManager al inicio de cada nivel
        /// (cuando el grid esté listo y el droide inicializado).
        /// </summary>
        public void AttachToDroide(DroideCore droide, float unscaledTimeNow, int batteryAtStart)
        {
            _tracker.Attach(droide);
            _tracker.NotifyGridReady(unscaledTimeNow, batteryAtStart);
        }

        /// <summary>
        /// Desuscribe el tracker antes de cambiar de escena.
        /// Llamar desde LevelManager antes de SceneManager.LoadScene().
        /// </summary>
        public void DetachFromDroide()
        {
            _tracker.Detach();
        }

        // ══════════════════════════════════════════════════════
        //  PERSISTENCIA
        // ══════════════════════════════════════════════════════

        private float LoadD()
        {
            if (_store == null) return DifficultyModel.D_MIN;
            float saved = _store.GetFloat(PREF_KEY_D, DifficultyModel.D_MIN);
            return DifficultyModel.Clamp(saved);
        }

        private void SaveD(float d)
        {
            _store?.SetFloat(PREF_KEY_D, d);
            _store?.FlushIfDirty();
        }
    }
}
