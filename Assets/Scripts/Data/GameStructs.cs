// ============================================================
// GameStructs.cs  |  Assets/Scripts/Data/
// Enums globales + PlayerData serializable (PlayerPrefs JSON)
//
// v4:
//   TileType  — solo 6 tipos activos: Base, Arrow, Laser,
//               Charge, Goal, Portal. Eliminados Void y Resonance.
//   SegmentType — alineado con los 6 tipos.
//   DroideState — mantiene AtPortal, elimina ReadyToAdvance.
// ============================================================
using System;
using UnityEngine;

namespace Celeris.Data
{
    // ── Tipos de Tile (6 activos) ────────────────────────────
    public enum TileType
    {
        BaseTile   = 0,
        ArrowTile  = 1,
        LaserTile  = 2,
        ChargeTile = 3,
        GoalTile   = 4,
        PortalTile = 5
    }

    // ── Tipos de Segmento para generación procedural ─────────
    public enum SegmentType
    {
        Start,
        Arrow,      // BaseTile recto (sin giro)
        Laser,
        Charge,
        Portal,
        Goal
    }

    // ── Estados del Droide ───────────────────────────────────
    public enum DroideState
    {
        Moving,
        IdleBetweenTiles,
        Charging,           // Dentro de ChargeTile (estado de fricción activo)
        RotatingArrow,
        Dead,
        Victory,
        AtPortal,           // Esperando retorno del minijuego
        ReadyToAdvance      // Feedback visual breve post-ExitPortal
    }

    // ── Dirección de movimiento (4 ejes) ─────────────────────
    public enum MoveDirection
    {
        North,   // +Z
        South,   // -Z
        East,    // +X
        West     // -X
    }

    public enum DeathCause
    {
        None,
        Battery,
        Fall,
        Laser
    }

    // ── Datos persistentes del jugador ───────────────────────
    [Serializable]
    public class PlayerData
    {
        public string playerName    = "PLAYER";
        public int    totalScore    = 0;
        public int    highestLevel  = 0;
        public int    totalStars    = 0;
        public float  totalPlayTime = 0f;

        private const string PREFS_KEY = "CELERIS_PlayerData";

        public static PlayerData Load()
        {
            if (!PlayerPrefs.HasKey(PREFS_KEY))
                return new PlayerData();
            try
            {
                return JsonUtility.FromJson<PlayerData>(PlayerPrefs.GetString(PREFS_KEY));
            }
            catch { return new PlayerData(); }
        }

        public void Save()
        {
            PlayerPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void Reset() => PlayerPrefs.DeleteKey(PREFS_KEY);
    }

    // ── Resultado de nivel ────────────────────────────────────
    [Serializable]
    public class LevelResult
    {
        public int   levelIndex;
        public int   score;
        public int   stars;
        public int   batteryLeft;
        public float completionTime;
        public bool  isVictory;
    }

    // ── Dificultad en runtime (calculada por ProceduralLevelConfig) ──
    [Serializable]
    public struct RuntimeDifficulty
    {
        public int   Seed;
        public float LaserWeightMultiplier;
        public float ChargeWeightMultiplier;
        public float LaserActiveDuration;
        public float LaserInactiveDuration;
        public int   ExtraSegments;
    }
}
