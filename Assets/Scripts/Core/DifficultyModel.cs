// ============================================================
// DifficultyModel.cs  |  Assets/Scripts/Core/
//
// RESPONSABILIDAD ÚNICA (SRP):
//   Transformar métricas de rendimiento en un escalar D ∈ [0.05, 0.95].
//   NO sabe nada de Unity, ni de cómo se aplica D al juego.
//   Es la clase más testeable del sistema — solo matemática pura.
//
// FÓRMULA:
//   D(n, k) = 1 - e^(-n / k)
//   Donde:
//     n = victorias consecutivas recientes (desde PerformanceTracker)
//     k = constante de saturación (default = 5, configurable)
//
//   Recuperación por muertes:
//     D -= LOSS_PENALTY * muertes_consecutivas (lineal — recuperación rápida)
//
//   D siempre se clampea a [D_MIN, D_MAX]:
//     D_MIN = 0.05 → nunca dificultad cero (el juego siempre tiene reto mínimo)
//     D_MAX = 0.95 → nunca imposible (siempre hay algo de margen)
//
// NOTA SOBRE DEPENDENCIAS:
//   Esta clase NO referencia UnityEngine en producción.
//   Usa System.Math (no Mathf) para ser testeable con NUnit puro.
// ============================================================

namespace Celeris.Core
{
    /// <summary>
    /// Modelo matemático de dificultad dinámica.
    /// Clase pura — no MonoBehaviour, sin dependencias de Unity.
    /// </summary>
    public sealed class DifficultyModel
    {
        // ── Constantes del modelo ──────────────────────────────
        public const float D_MIN        = 0.05f;
        public const float D_MAX        = 0.95f;
        private const float LOSS_PENALTY = 0.10f;  // reducción de D por muerte consecutiva

        // ── Estado ────────────────────────────────────────────
        private float _d;
        private readonly float _k;

        /// <summary>Dificultad actual en [D_MIN, D_MAX].</summary>
        public float D => _d;

        /// <summary>
        /// Crea el modelo con D inicial y constante de saturación k.
        /// </summary>
        /// <param name="initialD">D de partida (se clampea a [D_MIN, D_MAX]).</param>
        /// <param name="k">Constante de saturación. A n=k el modelo alcanza ~63% del máximo.
        /// A n=3k alcanza ~95%. Recomendado: 5 (reactivo) a 10 (suave).</param>
        public DifficultyModel(float initialD = D_MIN, float k = 5f)
        {
            _k = k > 0f ? k : 5f;
            _d = Clamp(initialD);
        }

        // ══════════════════════════════════════════════════════
        //  API PÚBLICA
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Recalcula D en base a las métricas del PerformanceTracker.
        /// Llamar después de cada nivel (victoria o muerte).
        /// </summary>
        public void Update(PerformanceTracker tracker)
        {
            if (tracker == null) return;

            if (tracker.ConsecutiveWins > 0)
            {
                // Subida por sigmoid: más victorias consecutivas → D sube
                _d = Sigmoid(tracker.ConsecutiveWins, _k);
            }
            else if (tracker.ConsecutiveLosses > 0)
            {
                // Bajada lineal: cada muerte consecutiva reduce D
                _d -= LOSS_PENALTY * tracker.ConsecutiveLosses;
            }

            _d = Clamp(_d);
        }

        /// <summary>
        /// Fuerza D a un valor concreto (para debug / QA / restaurar sesión).
        /// </summary>
        public void ForceSet(float value)
        {
            _d = Clamp(value);
        }

        // ══════════════════════════════════════════════════════
        //  MATEMÁTICA PURA (ESTÁTICA Y TESTEABLE)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// D(n, k) = 1 - e^(-n / k), clampeo incluido.
        /// Pública para permitir tests unitarios directos.
        /// </summary>
        public static float Sigmoid(int n, float k)
        {
            if (k <= 0f) return n > 0 ? D_MAX : D_MIN;
            float raw = 1f - (float)System.Math.Exp(-n / (double)k);
            return Clamp(raw);
        }

        /// <summary>Clampea D al rango [D_MIN, D_MAX].</summary>
        public static float Clamp(float d)
        {
            if (d < D_MIN) return D_MIN;
            if (d > D_MAX) return D_MAX;
            return d;
        }
    }
}
