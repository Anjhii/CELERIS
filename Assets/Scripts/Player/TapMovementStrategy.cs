// ============================================================
// TapMovementStrategy.cs  |  Assets/Scripts/Player/
//
// Estrategia de movimiento por Tap.
//
// Velocidad dinámica:
//   Mantiene una ventana deslizante de los últimos TAP_SAMPLES
//   intervalos entre taps y calcula el promedio.
//   - Taps muy frecuentes  → moveDuration cercano a minMoveDuration.
//   - Taps lentos/primero  → moveDuration cercano a maxMoveDuration.
//
// Cola de movimientos:
//   Los taps durante un movimiento en curso se encolan (hasta
//   maxQueuedMoves). Al finalizar el paso actual, se consume
//   el siguiente de la cola automáticamente.
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace Celeris.Player
{
    public class TapMovementStrategy : IMovementStrategy
    {
        // ── Parámetros públicos ───────────────────────────────
        /// <summary>Duración mínima de un paso (taps ultra-rápidos).</summary>
        public float MinMoveDuration = 0.10f;

        /// <summary>Duración máxima de un paso (primer tap o taps muy lentos).</summary>
        public float MaxMoveDuration = 0.45f;

        /// <summary>
        /// Intervalo de tap (segundos) considerado "tap rápido".
        /// Intervalos menores mapean a MinMoveDuration.
        /// </summary>
        public float FastTapThreshold = 0.18f;

        /// <summary>
        /// Intervalo de tap (segundos) considerado "tap lento".
        /// Intervalos mayores mapean a MaxMoveDuration.
        /// </summary>
        public float SlowTapThreshold = 0.60f;

        /// <summary>Máximo de movimientos encolados mientras el Droide está en tránsito.</summary>
        public int MaxQueuedMoves = 2;

        // ── Privado ───────────────────────────────────────────
        private const int TAP_SAMPLES = 4;

        private readonly float[]        _tapIntervals = new float[TAP_SAMPLES];
        private int                     _sampleCount  = 0;
        private int                     _writeIndex   = 0;
        private float                   _lastTapTime  = -1f;
        private readonly Queue<bool>    _moveQueue    = new();

        // ── IMovementStrategy ─────────────────────────────────

        public bool HasPendingMove => _moveQueue.Count > 0;

        public void RequestMove()
        {
            // Registrar intervalo entre taps para calcular velocidad
            float now = Time.unscaledTime;
            if (_lastTapTime > 0f)
            {
                float interval = now - _lastTapTime;
                _tapIntervals[_writeIndex % TAP_SAMPLES] = interval;
                _writeIndex++;
                _sampleCount = Mathf.Min(_sampleCount + 1, TAP_SAMPLES);
            }
            _lastTapTime = now;

            // Encolar movimiento si hay espacio
            if (_moveQueue.Count < MaxQueuedMoves)
                _moveQueue.Enqueue(true);
        }

        public void ConsumeMove()
        {
            if (_moveQueue.Count > 0)
                _moveQueue.Dequeue();
        }

        public float GetMoveDuration()
        {
            if (_sampleCount == 0)
                return MaxMoveDuration;

            // Promedio ponderado: muestras más recientes pesan más
            float weightedSum = 0f;
            float totalWeight = 0f;
            int count = Mathf.Min(_sampleCount, TAP_SAMPLES);

            for (int i = 0; i < count; i++)
            {
                // El sample más reciente tiene índice (_writeIndex - 1) % TAP_SAMPLES
                int idx    = (_writeIndex - 1 - i + TAP_SAMPLES * 2) % TAP_SAMPLES;
                float w    = 1f / (i + 1);   // 1, 1/2, 1/3, 1/4
                weightedSum += _tapIntervals[idx] * w;
                totalWeight += w;
            }

            float avgInterval = weightedSum / totalWeight;

            // Mapear intervalo → duración del paso
            float t = Mathf.InverseLerp(FastTapThreshold, SlowTapThreshold, avgInterval);
            return Mathf.Lerp(MinMoveDuration, MaxMoveDuration, t);
        }

        public void Reset()
        {
            for (int i = 0; i < TAP_SAMPLES; i++) _tapIntervals[i] = 0f;
            _sampleCount  = 0;
            _writeIndex   = 0;
            _lastTapTime  = -1f;
            _moveQueue.Clear();
        }
    }
}
