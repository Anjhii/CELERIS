// ============================================================
// PerformanceTracker.cs  |  Assets/Scripts/Core/
//
// RESPONSABILIDAD ÚNICA (SRP):
//   Observar eventos del Droide y calcular métricas de sesión.
//   NO decide nada — solo mide. La decisión de ajustar dificultad
//   es responsabilidad exclusiva de DifficultyModel.
//
// PATRÓN: Observador pasivo (no MonoBehaviour).
//   Recibe DroideCore en el constructor y suscribe a sus eventos.
//   Al cambiar de nivel, LevelManager llama Detach() para
//   desuscribir y luego Attach(nuevoCore) en la siguiente escena.
//
// VENTANA DESLIZANTE:
//   Solo se conservan los últimos WINDOW_SIZE resultados.
//   Esto permite que la dificultad reaccione al rendimiento
//   reciente, no al histórico completo.
//
// NOTA DE UNIDAD:
//   Esta clase es pura C# — no hereda de MonoBehaviour.
//   Ningún método de Unity puede llamarse desde aquí.
//   Para tiempo de escena, usa el valor inyectado vía NotifyGridReady().
// ============================================================

using System.Collections.Generic;
using Celeris.Data;
using Celeris.Player;

namespace Celeris.Core
{
    /// <summary>
    /// Observador pasivo de eventos del Droide.
    /// Calcula métricas de sesión para DifficultyModel.
    /// </summary>
    public class PerformanceTracker
    {
        // ── Ventana deslizante ────────────────────────────────
        private const int WINDOW_SIZE = 5;

        // ── Registro de resultados recientes ──────────────────
        private readonly Queue<LevelResult> _results = new(WINDOW_SIZE + 1);

        // ── Estado de la sesión actual ─────────────────────────
        private DroideCore _droide;
        private float      _gridReadyTime   = 0f;   // tiempo (unscaled) en que el grid estuvo listo
        private int        _batteryAtStart  = 100;

        // ── Snapshot de métricas (último nivel completado) ─────
        public float WinRate            { get; private set; } = 0.5f;
        public float AvgCompletionTime  { get; private set; } = 0f;
        public float AvgBatteryConsumed { get; private set; } = 0f;
        public DeathCause DominantDeath { get; private set; } = DeathCause.Generic;
        public int   ConsecutiveWins    { get; private set; } = 0;
        public int   ConsecutiveLosses  { get; private set; } = 0;

        // ── Struct interno ─────────────────────────────────────
        private readonly struct LevelResult
        {
            public readonly bool       Won;
            public readonly float      CompletionTime;
            public readonly float      BatteryConsumed;
            public readonly DeathCause Death;

            public LevelResult(bool won, float time, float battery, DeathCause death)
            {
                Won             = won;
                CompletionTime  = time;
                BatteryConsumed = battery;
                Death           = death;
            }
        }

        // ══════════════════════════════════════════════════════
        //  CICLO DE VIDA
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Suscribe al DroideCore de la escena actual.
        /// Llamar desde LevelManager al inicio de cada nivel.
        /// </summary>
        public void Attach(DroideCore droide)
        {
            Detach(); // garantía de no doble suscripción
            _droide = droide;
            if (_droide == null) return;

            _droide.OnDied    += HandleDied;
            _droide.OnVictory += HandleVictory;
        }

        /// <summary>
        /// Desuscribe del DroideCore anterior.
        /// Llamar desde LevelManager antes de cargar la siguiente escena.
        /// </summary>
        public void Detach()
        {
            if (_droide == null) return;
            _droide.OnDied    -= HandleDied;
            _droide.OnVictory -= HandleVictory;
            _droide = null;
        }

        /// <summary>
        /// Notifica cuándo el grid estuvo listo (para calcular tiempo de completación).
        /// Llamar desde LevelManager al recibir el evento OnGridReady del generador.
        /// </summary>
        public void NotifyGridReady(float unscaledTimeNow, int batteryAtStart)
        {
            _gridReadyTime  = unscaledTimeNow;
            _batteryAtStart = batteryAtStart;
        }

        // ══════════════════════════════════════════════════════
        //  HANDLERS DE EVENTOS
        // ══════════════════════════════════════════════════════

        private void HandleVictory()
        {
            float time    = UnityEngine.Time.unscaledTime - _gridReadyTime;
            float battery = _droide != null
                ? (_batteryAtStart - _droide.Battery) / (float)UnityEngine.Mathf.Max(1, _batteryAtStart)
                : 1f;

            RecordResult(new LevelResult(true, time, battery, DeathCause.Generic));
        }

        private void HandleDied(DeathCause cause)
        {
            RecordResult(new LevelResult(false, 0f, 1f, cause));
        }

        // ══════════════════════════════════════════════════════
        //  CÁLCULO DE MÉTRICAS
        // ══════════════════════════════════════════════════════

        private void RecordResult(LevelResult r)
        {
            _results.Enqueue(r);
            if (_results.Count > WINDOW_SIZE)
                _results.Dequeue();

            RecalculateMetrics();
        }

        private void RecalculateMetrics()
        {
            if (_results.Count == 0) return;

            int   wins         = 0;
            float totalTime    = 0f;
            float totalBattery = 0f;
            var   deathCounts  = new System.Collections.Generic.Dictionary<DeathCause, int>();
            int   consWin      = 0;
            int   consLoss     = 0;
            bool  countingWins = true;

            // Iterar de más reciente a más antiguo para racha consecutiva
            var list = new List<LevelResult>(_results);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var result = list[i];

                if (result.Won)
                {
                    wins++;
                    totalTime    += result.CompletionTime;
                    totalBattery += result.BatteryConsumed;
                    if (countingWins) consWin++;
                    else              consLoss = 0; // reset si alternamos
                }
                else
                {
                    if (!deathCounts.ContainsKey(result.Death))
                        deathCounts[result.Death] = 0;
                    deathCounts[result.Death]++;
                    totalBattery += result.BatteryConsumed;
                    if (!countingWins) consLoss++;
                    else
                    {
                        countingWins = false;
                        consLoss     = 1;
                    }
                }
            }

            int total = _results.Count;
            WinRate            = wins / (float)total;
            AvgCompletionTime  = wins > 0 ? totalTime / wins : 0f;
            AvgBatteryConsumed = totalBattery / total;
            ConsecutiveWins    = consWin;
            ConsecutiveLosses  = consLoss;

            // Causa de muerte dominante
            DeathCause dominant = DeathCause.Generic;
            int        maxCount = 0;
            foreach (var kv in deathCounts)
            {
                if (kv.Value > maxCount)
                {
                    maxCount = kv.Value;
                    dominant = kv.Key;
                }
            }
            DominantDeath = dominant;
        }
    }
}
