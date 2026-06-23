// ============================================================
// ScoreManager.cs  |  Assets/Scripts/Leaderboard/  — v2 SOLID
//
// CAMBIOS v2 (Bloque 5):
//   Toda lectura/escritura de PlayerPrefs eliminada de este archivo.
//   Reemplazada por IPlayerProgressStore (inyectado en Awake via
//   PlayerProgressStore.EnsureExists()).
//   Las constantes KEY_* eliminadas — son responsabilidad del store.
//
// RESPONSABILIDAD:
//   Gestionar el score de la sesion actual (_currentScore) y delegar
//   persistencia al store. Sincronizar con Supabase cuando hay red.
// ============================================================
using System;
using Celeris.Core;
using Celeris.Data;
using UnityEngine;

namespace Celeris.Leaderboard
{
    public class ScoreManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────
        public static ScoreManager Instance { get; private set; }

        // ── Eventos ───────────────────────────────────────────
        public event Action<long> OnScoreChanged;
        public event Action<long> OnNewHighScore;

        // ── Dependencia inyectada (DIP) ───────────────────────
        private IPlayerProgressStore _store;

        // ── Estado de sesion (en memoria, no persiste) ────────
        private long _currentScore = 0;

        // ── Propiedades ───────────────────────────────────────
        public long   CurrentScore   => _currentScore;
        public long   LocalHighScore => _store.HighScore;
        public string DeviceId       => _store.DeviceId;
        public bool   HasPendingSync => _store.NeedsSync;
        public long   PendingScore   => _store.PendingScore;

        public string Username
        {
            get => _store.Username;
            set { _store.Username = value; _store.ForceFlush(); }
        }

        public int MaxUnlockedLevel => _store.MaxUnlockedLevel;
        public int LevelsCompleted  => _store.LevelsCompleted;
        public int TimesPlayed      => _store.TimesPlayed;
        public int TotalStars       => _store.TotalStars;

        public int GetBestScoreForLevel(int levelIndex) =>
            _store.GetBestScoreForLevel(levelIndex);

        public int GetStarsForLevel(int levelIndex) =>
            _store.GetStarsForLevel(levelIndex);

        public long GetTotalCumulativeScore()
        {
            long total = 0;
            for (int i = 0; i <= _store.MaxUnlockedLevel; i++)
                total += _store.GetBestScoreForLevel(i);
            return total;
        }

        // ── Lifecycle ─────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _store = PlayerProgressStore.EnsureExists();
            EnsureDeviceId();
        }

        private void EnsureDeviceId()
        {
            if (string.IsNullOrEmpty(_store.DeviceId))
            {
                _store.DeviceId = Guid.NewGuid().ToString();
                _store.ForceFlush();
            }
        }

        // ── API de score de sesion ────────────────────────────

        public void ResetCurrentScore()
        {
            _currentScore = 0;
            OnScoreChanged?.Invoke(_currentScore);
        }

        public void AddPoints(long points)
        {
            if (points <= 0) return;
            _currentScore += points;
            OnScoreChanged?.Invoke(_currentScore);
            EvaluateHighScore(_currentScore);
        }

        public void SubmitLevelScore(long finalScore)
        {
            _currentScore = finalScore;
            EvaluateHighScore(finalScore);

            if (SupabaseManager.Instance != null)
                SupabaseManager.Instance.SubmitScore(LocalHighScore, Username);
            else
                MarkScoreAsPending(LocalHighScore);
        }

        public void MarkScoreAsPending(long score)
        {
            _store.NeedsSync    = true;
            _store.PendingScore = score;
            // No ForceFlush aqui — se persistira en OnApplicationPause/Quit via store.
        }

        public void ClearPendingSync()
        {
            _store.NeedsSync = false;
            // Sin ForceFlush — el store lo persiste en pause/quit.
        }

        // ── API de resultado de nivel ─────────────────────────

        public void RecordLevelResult(LevelResult result)
        {
            _currentScore = result.score;
            OnScoreChanged?.Invoke(_currentScore);

            _store.TimesPlayed = _store.TimesPlayed + 1;

            if (result.isVictory)
            {
                int prevBest = _store.GetBestScoreForLevel(result.levelIndex);
                if (result.score > prevBest)
                    _store.SetBestScoreForLevel(result.levelIndex, result.score);

                int prevStars = _store.GetStarsForLevel(result.levelIndex);
                if (result.stars > prevStars)
                {
                    int starDelta = result.stars - prevStars;
                    _store.SetStarsForLevel(result.levelIndex, result.stars);
                    _store.TotalStars = _store.TotalStars + starDelta;
                }

                // F1-T2: Idempotencia — LevelsCompleted solo crece si este nivel
                // no había sido completado antes (best score previo == 0).
                // Criterio SST: la fuente de verdad de "nivel completado" es el
                // best score guardado, no un contador de intentos.
                if (prevBest == 0)
                    _store.LevelsCompleted = _store.LevelsCompleted + 1;

                int nextLevel = result.levelIndex + 1;
                if (nextLevel > _store.MaxUnlockedLevel)
                    _store.MaxUnlockedLevel = nextLevel;
            }

            // Persistencia diferida — el store la ejecutara en pause/quit.
            // No llamar ForceFlush aqui para no generar I/O en el hot-path.

            long cumulativeScore = GetTotalCumulativeScore();
            EvaluateHighScore(cumulativeScore);

            if (SupabaseManager.Instance != null)
            {
                SupabaseManager.Instance.SubmitScore(LocalHighScore, Username);
                SupabaseManager.Instance.SyncPlayerProgress();
            }
            else
            {
                MarkScoreAsPending(LocalHighScore);
            }
        }

        // ── Privados ──────────────────────────────────────────
        private void EvaluateHighScore(long score)
        {
            if (score <= _store.HighScore) return;

            _store.HighScore    = score;
            _store.NeedsSync    = true;
            _store.PendingScore = score;
            // Persistencia diferida — sin Save() en hot-path.
            OnNewHighScore?.Invoke(score);
        }

        // ── Persistencia delegada al store ────────────────────
        // ScoreManager ya no llama PlayerPrefs.Save() directamente.
        // PlayerProgressStore captura OnApplicationPause/Quit y llama
        // FlushIfDirty(). Estos metodos existen solo como fallback
        // para componentes externos que los llamen explicitamente.

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) _store?.FlushIfDirty();
        }

        private void OnApplicationQuit() => _store?.FlushIfDirty();
    }
}
