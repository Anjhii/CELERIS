using UnityEngine;

[CreateAssetMenu(fileName = "NewHackSessionData", menuName = "Sistema Alienigena/Hack Session Data")]
public class HackSessionData : ScriptableObject
{
    [Header("Dificultad Global (Progreso del Juego)")]
    [Tooltip("Dificultad base del nivel actual (ej: Terminal 1 = Nivel 1, Terminal 2 = Nivel 2...)")]
    public int globalLevel = 1;

    [Header("Dificultad Local (Intentos de esta Terminal)")]
    [Tooltip("Intentos realizados en la terminal actual. Máximo 3.")]
    public int currentAttempt = 1; 

    [Header("Configuración Base de Secuencias")]
    public int baseSequenceLength = 5; // Actualizado a base 5 según el nuevo balance
    public float baseDisplaySpeed = 0.8f;

    [Header("Resultados y Estado Actual")]
    public bool wasHackSuccessful = false;
    public bool isGameOverDueToFailure = false;
    public int extractedDigit = -1;

    /// <summary>
    /// Calcula de forma procedural la longitud de la secuencia actual sin repetir patrones.
    /// Base 5. Aumenta gradualmente 1 por nivel global y 1 por fallo (intento local).
    /// </summary>
    public int GetProceduralSequenceLength()
    {
        return baseSequenceLength + (globalLevel - 1) + (currentAttempt - 1);
    }

    /// <summary>
    /// Calcula proceduralmente la velocidad a la que se muestran los glifos.
    /// El límite es 0.30s. Garantiza que por más rápido que vaya, el ojo humano siempre vea el color.
    /// </summary>
    public float GetProceduralDisplaySpeed()
    {
        float speedReduction = ((globalLevel - 1) * 0.05f) + ((currentAttempt - 1) * 0.10f);
        return Mathf.Max(0.30f, baseDisplaySpeed - speedReduction);
    }

    /// <summary>
    /// Procesa el puntaje obtenido según el intento actual basándose en las reglas del juego.
    /// </summary>
    public int CalculateScoreReward()
    {
        if (!wasHackSuccessful) return 0;

        return currentAttempt switch
        {
            1 => 100, // Primer intento
            2 => 50,  // Segundo intento
            3 => 25,  // Tercer intento
            _ => 0
        };
    }

    /// <summary>
    /// Registra un fallo en la terminal actual. Si supera los 3 intentos, dispara el fin del juego.
    /// </summary>
    public void RegisterFailedAttempt()
    {
        currentAttempt++;
        if (currentAttempt > 3)
        {
            isGameOverDueToFailure = true;
            Debug.Log("<color=red>[ALERTA]</color> Tercer intento fallido. Robot bloqueado de la base.");
        }
    }

    /// <summary>
    /// Prepara los datos para una nueva terminal limpia en el mapa, manteniendo la dificultad global.
    /// </summary>
    public void ResetForNewTerminal()
    {
        currentAttempt = 1;
        wasHackSuccessful = false;
        isGameOverDueToFailure = false;
        extractedDigit = -1;
    }
}

