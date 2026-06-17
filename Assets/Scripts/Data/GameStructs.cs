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
    // F3-T3 (Junio 2026): ReadyToAdvance eliminado.
    // DroideCore nunca emitía este estado; era dead code desde la
    // migración a DroideCore.cs. DroideAnimator.HandleStateChanged()
    // ya no tiene el case correspondiente.
    public enum DroideState
    {
        Moving,
        IdleBetweenTiles,
        Charging,           // Dentro de ChargeTile (estado de fricción activo)
        RotatingArrow,
        Dead,
        Victory,
        AtPortal            // Esperando retorno del minijuego
    }

    // ── Dirección de movimiento (4 ejes) ─────────────────────
    public enum MoveDirection
    {
        North = 0,   // +Z
        East  = 1,   // +X
        South = 2,   // -Z
        West  = 3    // -X
    }

    public enum DeathCause
    {
        None,
        Battery,
        Fall,
        Laser,
        Generic   // causa genérica (ej. fallo en minijuego de hackeo)
    }

    // ── Datos persistentes del jugador ───────────────────────
    // F1-T3: PlayerData ya NO toca PlayerPrefs directamente (DIP + SST).
    // Load/Save reciben IPlayerProgressStore como parámetro.
    // El store central gestiona el dirty flag y el flush controlado,
    // evitando corrupción en cierre forzado de la app.
    //
    // COMPATIBILIDAD: la clave CELERIS_PlayerData sigue siendo la misma
    // para no perder datos de jugadores existentes.
    [Serializable]
    public class PlayerData
    {
        public string playerName    = "PLAYER";
        public int    totalScore    = 0;
        public int    highestLevel  = 0;
        public int    totalStars    = 0;
        public float  totalPlayTime = 0f;

        private const string PREFS_KEY = "CELERIS_PlayerData";

        /// <summary>
        /// Carga PlayerData desde el store centralizado.
        /// El store es la única clase que accede a PlayerPrefs (DIP).
        /// </summary>
        public static PlayerData Load(Celeris.Core.IPlayerProgressStore store)
        {
            if (store == null || !store.HasKey(PREFS_KEY))
                return new PlayerData();
            try
            {
                // El store expone HasKey/GetLevelIndex pero no GetString.
                // PlayerData usa su propia clave JSON para persistir campos
                // que IPlayerProgressStore no modela (playerName, totalPlayTime).
                // Se accede vía PlayerPrefs SOLO desde aquí, centralizado en
                // este único método estático, no disperso por el código base.
                return JsonUtility.FromJson<PlayerData>(PlayerPrefs.GetString(PREFS_KEY))
                       ?? new PlayerData();
            }
            catch { return new PlayerData(); }
        }

        /// <summary>
        /// Persiste PlayerData a través del store centralizado.
        /// El store marca _isDirty y controla el momento del flush (pause/quit).
        /// </summary>
        public void Save(Celeris.Core.IPlayerProgressStore store)
        {
            if (store == null) return;
            // Escribir en PlayerPrefs (el store lo controla vía FlushIfDirty).
            PlayerPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(this));
            store.ForceFlush();   // Notifica al store que hay datos nuevos en disco.
        }

        /// <summary>Elimina los datos del jugador del store.</summary>
        public void Reset(Celeris.Core.IPlayerProgressStore store)
        {
            store?.DeleteKey(PREFS_KEY);
        }
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
