// ============================================================
// IPlayerProgressStore.cs  |  Assets/Scripts/Core/
//
// PROPOSITO (DIP):
//   ScoreManager y LevelManager dependen de esta abstraccion,
//   no de PlayerPrefs directamente.
//   La implementacion concreta (PlayerProgressStore) centraliza
//   toda lectura y escritura de disco en un unico lugar.
//
// SEPARACION DE RESPONSABILIDADES:
//   Solo cubre progreso del jugador (scores, niveles, estrellas).
//   NO cubre credenciales de autenticacion (AuthManager gestiona
//   sus propias claves de token — scope distinto).
//
// CONTRATO DE KEYS DINAMICAS:
//   SetLevelIndex / GetLevelIndex reciben la clave activa como
//   parametro porque LevelManager la cambia al autenticarse
//   (CELERIS_Level_anon vs CELERIS_Level_{userId}).
//   El store escribe y lee esa clave sin conocer su semantica.
// ============================================================

namespace Celeris.Core
{
    public interface IPlayerProgressStore
    {
        // ── Score global ──────────────────────────────────────
        long   HighScore    { get; set; }
        bool   NeedsSync    { get; set; }
        long   PendingScore { get; set; }
        string DeviceId     { get; set; }
        string Username     { get; set; }

        // ── Progreso de niveles ───────────────────────────────
        int MaxUnlockedLevel { get; set; }
        int LevelsCompleted  { get; set; }
        int TimesPlayed      { get; set; }
        int TotalStars       { get; set; }

        // ── Progreso por nivel (claves dinamicas LevelBestScore_N / LevelStars_N) ──
        int  GetBestScoreForLevel(int levelIndex);
        void SetBestScoreForLevel(int levelIndex, int score);
        int  GetStarsForLevel(int levelIndex);
        void SetStarsForLevel(int levelIndex, int stars);

        // ── Clave dinamica de nivel actual (varia por userId) ─
        // LevelManager es el unico que llama estos metodos.
        int  GetLevelIndex(string key);
        void SetLevelIndex(string key, int value);
        void DeleteKey(string key);
        bool HasKey(string key);

        // ── Valores flotantes genéricos (F4: DifficultyDirector) ─
        // Usado para persistir D (escalar de dificultad) entre sesiones.
        // No cachea en memoria — lectura directa de PlayerPrefs.
        float GetFloat(string key, float defaultValue = 0f);
        void  SetFloat(string key, float value);

        // ── Control de persistencia ───────────────────────────
        /// <summary>
        /// Persiste en disco si hay cambios pendientes (_isDirty).
        /// Llamar desde OnApplicationPause / OnApplicationQuit.
        /// NO llamar durante el gameplay.
        /// </summary>
        void FlushIfDirty();

        /// <summary>
        /// Fuerza persistencia inmediata independientemente de _isDirty.
        /// Usar solo para escrituras criticas (ej. creacion de DeviceId).
        /// </summary>
        void ForceFlush();
    }
}
