// ============================================================
// GameStructs.cs  |  Assets/Scripts/Data/
// Enums globales + PlayerData serializable (PlayerPrefs JSON)
// ============================================================
using System;
using UnityEngine;

namespace Celeris.Data
{
    // ── Tipos de Tile ────────────────────────────────────────
    public enum TileType
    {
        BaseTile,
        ArrowTile,
        ResonanceTile,
        LaserTile,
        ChargeTile,
        VoidTile,
        GoalTile
    }

    // ── Tipos de Segmento para generación procedural ─────────
    public enum SegmentType
    {
        Start,
        Arrow,
        Laser,
        Resonance,
        Charge,
        Goal
    }

    // ── Estados del Droide ───────────────────────────────────
    public enum DroideState
    {
        Moving,
        IdleBetweenTiles,
        Charging,
        RotatingArrow,
        Dead,
        Victory
    }

    // ── Dirección de movimiento (4 ejes) ─────────────────────
    public enum MoveDirection
    {
        North,   // +Z
        South,   // -Z
        East,    // +X
        West     // -X
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
                return JsonUtility.FromJson<PlayerData>(
                    PlayerPrefs.GetString(PREFS_KEY));
            }
            catch { return new PlayerData(); }
        }

        public void Save()
        {
            PlayerPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void Reset()
        {
            PlayerPrefs.DeleteKey(PREFS_KEY);
        }
    }

    // ── Resultado de nivel (por sesión, no persiste) ─────────
    [Serializable]
    public class LevelResult
    {
        public int   levelIndex;
        public int   score;
        public int   stars;          // 0-3
        public int   batteryLeft;
        public float completionTime;
        public bool  isVictory;
    }
}
