// ============================================================
// TapMovementStrategy.cs  |  Assets/Scripts/Player/
//
// ⚠ ARCHIVO ARCHIVADO — Fase 0, Acción 0.3
//
// ESTADO: INACTIVO. No compilar ni referenciar.
//
// MOTIVO DEL RETIRO:
//   Implementación concreta de IMovementStrategy (también archivada).
//   Nunca fue asignada ni referenciada por DroideController en el
//   sistema activo. Ver IMovementStrategy.cs para el contexto completo.
//
// CONTRATO ACTIVO:
//   Ver Assets/Scripts/Player/IPlayerState.cs
//
// ============================================================

// Código original conservado como referencia histórica:
/*
using System.Collections.Generic;
using UnityEngine;

namespace Celeris.Player
{
    public class TapMovementStrategy : IMovementStrategy
    {
        public float MinMoveDuration  = 0.10f;
        public float MaxMoveDuration  = 0.45f;
        public float FastTapThreshold = 0.18f;
        public float SlowTapThreshold = 0.60f;
        public int   MaxQueuedMoves   = 2;

        private const int TAP_SAMPLES = 4;
        private readonly float[]     _tapIntervals = new float[TAP_SAMPLES];
        private int                  _sampleCount  = 0;
        private int                  _writeIndex   = 0;
        private float                _lastTapTime  = -1f;
        private readonly Queue<bool> _moveQueue    = new();

        public bool HasPendingMove => _moveQueue.Count > 0;

        public void RequestMove()
        {
            float now = Time.unscaledTime;
            if (_lastTapTime > 0f)
            {
                float interval = now - _lastTapTime;
                _tapIntervals[_writeIndex % TAP_SAMPLES] = interval;
                _writeIndex++;
                _sampleCount = Mathf.Min(_sampleCount + 1, TAP_SAMPLES);
            }
            _lastTapTime = now;
            if (_moveQueue.Count < MaxQueuedMoves)
                _moveQueue.Enqueue(true);
        }

        public void ConsumeMove()
        {
            if (_moveQueue.Count > 0) _moveQueue.Dequeue();
        }

        public float GetMoveDuration()
        {
            if (_sampleCount == 0) return MaxMoveDuration;
            float weightedSum = 0f;
            float totalWeight = 0f;
            int count = Mathf.Min(_sampleCount, TAP_SAMPLES);
            for (int i = 0; i < count; i++)
            {
                int idx = (_writeIndex - 1 - i + TAP_SAMPLES * 2) % TAP_SAMPLES;
                float w = 1f / (i + 1);
                weightedSum += _tapIntervals[idx] * w;
                totalWeight += w;
            }
            float avgInterval = weightedSum / totalWeight;
            float t = Mathf.InverseLerp(FastTapThreshold, SlowTapThreshold, avgInterval);
            return Mathf.Lerp(MinMoveDuration, MaxMoveDuration, t);
        }

        public void Reset()
        {
            for (int i = 0; i < TAP_SAMPLES; i++) _tapIntervals[i] = 0f;
            _sampleCount = 0;
            _writeIndex  = 0;
            _lastTapTime = -1f;
            _moveQueue.Clear();
        }
    }
}
*/
