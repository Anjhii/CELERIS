// ============================================================
// HackSessionData.cs  |  Assets/Scripts/Data/
//
// ScriptableObject de configuración y estado del minijuego de hackeo.
//
// ⚠ ADVERTENCIA DE EDITOR:
//   Los campos de estado mutable (currentAttempt, wasHackSuccessful,
//   extractedDigit) se modifican en runtime. En el Editor de Unity,
//   los cambios a ScriptableObjects durante Play Mode persisten al
//   salir de Play Mode, contaminando el asset en disco.
//
//   SOLUCIÓN: TerminalHackManager llama RuntimeReset() en OnEnable()
//   para garantizar que el estado siempre parte limpio al cargar la
//   escena del minijuego, independientemente de lo que quedó grabado
//   en el asset tras la sesión anterior del Editor.
//
// NOTA ARQUITECTURAL — FASE 0:
//   Las coordenadas del proyecto usan actualmente Vector2Int (plano XZ).
//   En Fase 3 se migrará a Vector3Int para soportar alturas y wall-walking.
//   No introducir nuevas dependencias de Vector2Int en este archivo ni en
//   los archivos de Data/ hasta que se ejecute esa migración.
// ============================================================
using UnityEngine;

namespace Celeris.HackMinigame
{

    [CreateAssetMenu(fileName = "NewHackSessionData", menuName = "Sistema Alienigena/Hack Session Data")]
    public class HackSessionData : ScriptableObject
    {
        [Header("Dificultad Global (Progreso del Juego)")]
        [Tooltip("Dificultad base del nivel actual (ej: Terminal 1 = Nivel 1, Terminal 2 = Nivel 2...)")]
        public int globalLevel = 1;

        [Header("Semilla Procedural")]
        [Tooltip("Seed inyectada por LevelManager desde ProceduralLevelConfig.proceduralSeed + 7777. " +
                 "0 = fallback a globalLevel como seed.")]
        public int proceduralSeed = 0;

        [Header("Dificultad Local (Intentos de esta Terminal)")]
        [Tooltip("Intentos realizados en la terminal actual. Máximo 3.")]
        public int currentAttempt = 1;

        [Header("Configuración Base de Secuencias")]
        [Tooltip("Longitud inicial de la secuencia en el tier 0 (niveles 0-19).")]
        public int   baseSequenceLength = 3;
        [Tooltip("Velocidad de visualización base en el tier 0 (segundos por glifo).")]
        public float baseDisplaySpeed   = 0.8f;

        [Header("Escalado por Tiers")]
        [Tooltip("Cada cuántos niveles globales sube un tier (longitud +1, velocidad -0.10s).")]
        public int   tierStep = 20;
        [Tooltip("Longitud máxima de secuencia permitida (límite cognitivo humano ~7).")]
        public int   maxSequenceLength = 7;
        [Tooltip("Reducción de velocidad de visualización por tier (segundos).")]
        public float speedReductionPerTier = 0.10f;
        [Tooltip("Velocidad mínima garantizada (0.30s = percepción visual humana mínima).")]
        public float minDisplaySpeed = 0.30f;

        [Header("Resultados y Estado Actual")]
        public bool wasHackSuccessful    = false;
        public bool isGameOverDueToFailure = false;
        public int  extractedDigit       = -1;

        // ── API de Runtime ────────────────────────────────────────

        /// <summary>
        /// Reinicia SOLO el estado mutable de sesión al valor canónico inicial.
        /// Llamar desde TerminalHackManager.OnEnable() cada vez que la escena
        /// del minijuego se carga para contrarrestar la persistencia del Editor.
        ///
        /// NO modifica globalLevel ni los parámetros de configuración base
        /// (baseSequenceLength, baseDisplaySpeed), que son datos de diseño
        /// y deben mantenerse tal como están en el asset.
        /// </summary>
        public void RuntimeReset()
        {
            currentAttempt         = 1;
            wasHackSuccessful      = false;
            isGameOverDueToFailure = false;
            extractedDigit         = -1;
        }

        /// <summary>
        /// Calcula el tier actual basado en el nivel global.
        /// Tier = floor(globalLevel / tierStep).
        /// Cada tierStep niveles, la secuencia crece 1 y la velocidad cae 0.10s.
        /// </summary>
        private int CurrentTier() => globalLevel / Mathf.Max(1, tierStep);

        /// <summary>
        /// Calcula la longitud de secuencia para el tier actual.
        ///
        /// MODELO POR TIERS:
        ///   length = baseSequenceLength + tier
        ///   Techo duro en maxSequenceLength (7 por defecto — límite cognitivo humano).
        ///
        ///   Tier 0 (niveles  0-19): longitud = 3
        ///   Tier 1 (niveles 20-39): longitud = 4
        ///   Tier 2 (niveles 40-59): longitud = 5
        ///   ...
        ///   Tier 4+ (nivel 80+):    longitud = 7 (plateau)
        ///
        /// DIFERENCIA CON v1 (base + globalLevel):
        ///   El modelo anterior crecía sin límite — en nivel 50 la secuencia
        ///   tenía 54 elementos. Humanamente injugable.
        /// </summary>
        public int GetProceduralSequenceLength()
        {
            int length = baseSequenceLength + CurrentTier();
            return Mathf.Min(length, maxSequenceLength);
        }

      /// <summary>
        /// Calcula la velocidad de visualizacion de glifos (segundos por glifo) para el tier actual.
        ///
        /// MODELO POR TIERS:
        ///   speed = baseDisplaySpeed - (tier * speedReductionPerTier)
        ///   Techo inferior en minDisplaySpeed (0.30s).
        /// </summary>
        public float GetProceduralDisplaySpeed()
        {
            float speed = baseDisplaySpeed - (CurrentTier() * speedReductionPerTier);
            return Mathf.Max(speed, minDisplaySpeed);
        }

        /// <summary>
        /// Calcula la recompensa en puntos por hackear la terminal con exito.
        /// base = (globalLevel + 1) * 100
        /// factor = (4 - currentAttempt) / 3f
        /// </summary>
        public int CalculateScoreReward()
        {
            int   baseReward    = (globalLevel + 1) * 100;
            float attemptFactor = (4 - Mathf.Clamp(currentAttempt, 1, 3)) / 3f;
            return Mathf.RoundToInt(baseReward * attemptFactor);
        }

        /// <summary>
        /// Registra un intento fallido. Incrementa currentAttempt y activa
        /// isGameOverDueToFailure cuando se supera el limite de 3 intentos.
        /// </summary>
        public void RegisterFailedAttempt()
        {
            currentAttempt++;
            if (currentAttempt > 3)
                isGameOverDueToFailure = true;
        }

        /// <summary>
        /// Reinicia el estado de sesion para una nueva terminal.
        /// </summary>
        public void ResetForNewTerminal()
        {
            currentAttempt         = 1;
            wasHackSuccessful      = false;
            isGameOverDueToFailure = false;
            extractedDigit         = -1;
        }
    }
}

