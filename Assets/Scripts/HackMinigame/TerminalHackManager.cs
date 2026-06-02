using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TerminalHackManager : MonoBehaviour
{
    public enum HackState { MostrandoSecuencia, EsperandoJugador, Bloqueado }

    [Header("Puente de Datos (SOLID)")]
    [SerializeField] private HackSessionData sessionData;

    [Header("Componentes de la Interfaz")]
    [SerializeField] private List<AlienGlyph> glyphs;
    [SerializeField] private AudioSource centralAudioSource;

    private List<int> currentSequence = new List<int>();
    private int playerInputIndex = 0;
    private HackState currentState = HackState.Bloqueado;

    // Variables de control local
    private int sequenceLength;
    private float displaySpeed;

    private void Start()
    {
        InitializeTerminalHack();
    }

    private void InitializeTerminalHack()
    {
        // 1. Validar límite estricto de 3 intentos antes de procesar
        if (sessionData.currentAttempt > 3)
        {
            TriggerGameOver();
            return;
        }

        // 2. Obtener dificultad procedural calculada por el ScriptableObject
        sequenceLength = sessionData.GetProceduralSequenceLength();
        displaySpeed = sessionData.GetProceduralDisplaySpeed();

        // 3. Inicializar y suscribirse a los eventos de los glifos
        for (int i = 0; i < glyphs.Count; i++)
        {
            glyphs[i].Initialize(i);
            // Evitamos duplicación de suscripción restando primero el método
            glyphs[i].OnGlyphClicked -= OnGlyphSelectedByPlayer;
            glyphs[i].OnGlyphClicked += OnGlyphSelectedByPlayer;
        }

        // 4. Generar secuencia matemática aleatoria SIN patrones repetidos simples
        GenerateNonRepeatingSequence();

        // 5. Reproducir la secuencia
        StartCoroutine(PlaySequenceCoroutine());
    }

    private void GenerateNonRepeatingSequence()
    {
        currentSequence.Clear();
        int lastIndex = -1;

        for (int i = 0; i < sequenceLength; i++)
        {
            int nextIndex;
            do
            {
                nextIndex = Random.Range(0, glyphs.Count);
            } 
            // El bucle "do-while" garantiza que el mismo glifo no se repita dos veces seguidas
            while (nextIndex == lastIndex); 

            currentSequence.Add(nextIndex);
            lastIndex = nextIndex;
        }
    }

    private IEnumerator PlaySequenceCoroutine()
    {
        currentState = HackState.MostrandoSecuencia;
        SetGridInteractable(false);
        yield return new WaitForSeconds(0.5f); // Pausa inicial antes de arrancar

        foreach (int index in currentSequence)
        {
            AlienGlyph glyph = glyphs[index];
            
            // Feedback visual y auditivo
            glyph.ActivateGlow();
            if (glyph.GetSound() != null && centralAudioSource != null)
            {
                centralAudioSource.PlayOneShot(glyph.GetSound());
            }

            // Se mantiene la misma velocidad matemática exacta para todos los glifos
            yield return new WaitForSeconds(displaySpeed);
            glyph.DeactivateGlow();
            
            // Pausa constante, corta e implacable entre un glifo y otro
            yield return new WaitForSeconds(0.15f); 
        }

        // Termina el bombardeo visual y cede el control al robot
        currentState = HackState.EsperandoJugador;
        playerInputIndex = 0;
        SetGridInteractable(true);
    }

    private void OnGlyphSelectedByPlayer(int glyphIndex)
    {
        if (currentState != HackState.EsperandoJugador) return;

        // Feedback visual inmediato al toque del jugador
        StartCoroutine(FlashPlayerChoice(glyphIndex));

        // Validación de la secuencia lineal
        if (glyphIndex == currentSequence[playerInputIndex])
        {
            playerInputIndex++;

            // ¿Completó con éxito toda la secuencia actual?
            if (playerInputIndex >= currentSequence.Count)
            {
                HandleResolution(true);
            }
        }
        else
        {
            // El jugador se equivocó en el patrón
            HandleResolution(false);
        }
    }

    private IEnumerator FlashPlayerChoice(int index)
    {
        glyphs[index].ActivateGlow();
        if (glyphs[index].GetSound() != null && centralAudioSource != null)
        {
            centralAudioSource.PlayOneShot(glyphs[index].GetSound());
        }
        yield return new WaitForSeconds(0.18f);
        glyphs[index].DeactivateGlow();
    }

    private void HandleResolution(bool success)
    {
        // Bloqueamos la matriz de inmediato para que el jugador no pueda hacer más clics
        currentState = HackState.Bloqueado;
        SetGridInteractable(false);

        // Iniciamos una corrutina que hará una pausa antes de cerrar la escena
        StartCoroutine(ResolutionRoutine(success));
    }

    private IEnumerator ResolutionRoutine(bool success)
    {
        // Esta es la magia: Le damos 0.4 segundos al juego para que reproduzca
        // el sonido y termine el destello visual del último glifo que tocaste.
        yield return new WaitForSeconds(0.9f);

        if (success)
        {
            sessionData.wasHackSuccessful = true;
            sessionData.extractedDigit = Random.Range(0, 10); // Genera el dígito (0-9) para la puerta final
            
            Debug.Log($"<color=green>[HACK EXITOSO]</color> Recompensa calculada: {sessionData.CalculateScoreReward()} puntos.");
            ExitScene();
        }
        else
        {
            sessionData.wasHackSuccessful = false;
            sessionData.RegisterFailedAttempt(); // Incrementa el intento internamente en el ScriptableObject

            if (sessionData.isGameOverDueToFailure)
            {
                TriggerGameOver();
            }
            else
            {
                Debug.Log($"<color=yellow>[INTRUSIÓN]</color> Intento fallido. Iniciando intento número: {sessionData.currentAttempt}");
                // Reinicia el juego con la nueva dificultad calculada proceduralmente
                InitializeTerminalHack();
            }
        }
    }

    private void SetGridInteractable(bool state)
    {
        foreach (AlienGlyph glyph in glyphs)
        {
            glyph.SetInteractable(state);
        }
    }

    private void TriggerGameOver()
    {
        Debug.Log("<color=red>[GAME OVER]</color> El robot falló los 3 intentos. Nivel perdido.");
        
        // Limpiamos los eventos para evitar fugas de memoria
        foreach (AlienGlyph glyph in glyphs) glyph.OnGlyphClicked -= OnGlyphSelectedByPlayer;

        // --- MODO DE PRUEBA AISLADA ---
        if (SceneManager.sceneCount == 1)
        {
            Debug.Log("<color=cyan>[MODO PRUEBA]</color> Reiniciando datos y escena automáticamente...");
            sessionData.ResetForNewTerminal(); // Limpia los fallos
            SceneManager.LoadScene(gameObject.scene.name); // Recarga esta misma escena
            return;
        }
        // ------------------------------

        Time.timeScale = 1f;
        SceneManager.LoadScene("NombreDeTuEscenaGameOver"); 
    }

    private void ExitScene()
    {
        // Limpieza de eventos
        foreach (AlienGlyph glyph in glyphs) glyph.OnGlyphClicked -= OnGlyphSelectedByPlayer;

        // --- MODO DE PRUEBA AISLADA ---
        if (SceneManager.sceneCount == 1)
        {
            Debug.Log("<color=cyan>[MODO PRUEBA]</color> Hackeo completado con éxito. Reiniciando para probar de nuevo...");
            sessionData.ResetForNewTerminal(); // Limpia los datos para el siguiente test
            SceneManager.LoadScene(gameObject.scene.name); // Recarga esta misma escena
            return;
        }
        // ------------------------------

        Time.timeScale = 1f;
        SceneManager.UnloadSceneAsync(gameObject.scene.name);
    }
}




// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.SceneManagement;

// public class TerminalHackManager : MonoBehaviour
// {
//     public enum HackState { MostrandoSecuencia, EsperandoJugador, Bloqueado }

//     [Header("Puente de Datos (SOLID)")]
//     [SerializeField] private HackSessionData sessionData;

//     [Header("Componentes de la Interfaz")]
//     [SerializeField] private List<AlienGlyph> glyphs;
//     [SerializeField] private AudioSource centralAudioSource;

//     private List<int> currentSequence = new List<int>();
//     private int playerInputIndex = 0;
//     private HackState currentState = HackState.Bloqueado;

//     // Variables de control local
//     private int sequenceLength;
//     private float displaySpeed;

//     private void Start()
//     {
//         InitializeTerminalHack();
//     }

//     private void InitializeTerminalHack()
//     {
//         // 1. Validar límite estricto de 3 intentos antes de procesar
//         if (sessionData.currentAttempt > 3)
//         {
//             TriggerGameOver();
//             return;
//         }

//         // 2. Obtener dificultad procedural calculada por el ScriptableObject
//         sequenceLength = sessionData.GetProceduralSequenceLength();
//         displaySpeed = sessionData.GetProceduralDisplaySpeed();

//         // 3. Inicializar y suscribirse a los eventos de los glifos
//         for (int i = 0; i < glyphs.Count; i++)
//         {
//             glyphs[i].Initialize(i);
//             // Evitamos duplicación de suscripción restando primero el método
//             glyphs[i].OnGlyphClicked -= OnGlyphSelectedByPlayer;
//             glyphs[i].OnGlyphClicked += OnGlyphSelectedByPlayer;
//         }

//         // 4. Generar secuencia matemática aleatoria SIN patrones repetidos simples
//         GenerateNonRepeatingSequence();

//         // 5. Reproducir la secuencia
//         StartCoroutine(PlaySequenceCoroutine());
//     }

//     private void GenerateNonRepeatingSequence()
//     {
//         currentSequence.Clear();
//         int lastIndex = -1;

//         for (int i = 0; i < sequenceLength; i++)
//         {
//             int nextIndex;
//             do
//             {
//                 nextIndex = Random.Range(0, glyphs.Count);
//             } 
//             // El bucle "do-while" garantiza que el mismo glifo no se repita dos veces seguidas
//             while (nextIndex == lastIndex); 

//             currentSequence.Add(nextIndex);
//             lastIndex = nextIndex;
//         }
//     }

//     private IEnumerator PlaySequenceCoroutine()
//     {
//         currentState = HackState.MostrandoSecuencia;
//         SetGridInteractable(false);
//         yield return new WaitForSeconds(0.5f); // Pausa inicial antes de arrancar

//         foreach (int index in currentSequence)
//         {
//             AlienGlyph glyph = glyphs[index];
            
//             // Feedback visual y auditivo
//             glyph.ActivateGlow();
//             if (glyph.GetSound() != null && centralAudioSource != null)
//             {
//                 centralAudioSource.PlayOneShot(glyph.GetSound());
//             }

//             // Se mantiene la misma velocidad matemática exacta para todos los glifos
//             yield return new WaitForSeconds(displaySpeed);
//             glyph.DeactivateGlow();
            
//             // Pausa constante, corta e implacable entre un glifo y otro
//             yield return new WaitForSeconds(0.15f); 
//         }

//         // Termina el bombardeo visual y cede el control al robot
//         currentState = HackState.EsperandoJugador;
//         playerInputIndex = 0;
//         SetGridInteractable(true);
//     }

//     private void OnGlyphSelectedByPlayer(int glyphIndex)
//     {
//         if (currentState != HackState.EsperandoJugador) return;

//         // Feedback visual inmediato al toque del jugador
//         StartCoroutine(FlashPlayerChoice(glyphIndex));

//         // Validación de la secuencia lineal
//         if (glyphIndex == currentSequence[playerInputIndex])
//         {
//             playerInputIndex++;

//             // ¿Completó con éxito toda la secuencia actual?
//             if (playerInputIndex >= currentSequence.Count)
//             {
//                 HandleResolution(true);
//             }
//         }
//         else
//         {
//             // El jugador se equivocó en el patrón
//             HandleResolution(false);
//         }
//     }

//     private IEnumerator FlashPlayerChoice(int index)
//     {
//         glyphs[index].ActivateGlow();
//         if (glyphs[index].GetSound() != null && centralAudioSource != null)
//         {
//             centralAudioSource.PlayOneShot(glyphs[index].GetSound());
//         }
//         yield return new WaitForSeconds(0.18f);
//         glyphs[index].DeactivateGlow();
//     }

//     private void HandleResolution(bool success)
//     {
//         // Bloqueamos la matriz de inmediato para que el jugador no pueda hacer más clics
//         currentState = HackState.Bloqueado;
//         SetGridInteractable(false);

//         // Iniciamos una corrutina que hará una pausa antes de cerrar la escena
//         StartCoroutine(ResolutionRoutine(success));
//     }

//     private IEnumerator ResolutionRoutine(bool success)
//     {
//         // Esta es la magia: Le damos 0.4 segundos al juego para que reproduzca
//         // el sonido y termine el destello visual del último glifo que tocaste.
//         yield return new WaitForSeconds(0.4f);

//         if (success)
//         {
//             sessionData.wasHackSuccessful = true;
//             sessionData.extractedDigit = Random.Range(0, 10); // Genera el dígito (0-9) para la puerta final
            
//             Debug.Log($"<color=green>[HACK EXITOSO]</color> Recompensa calculada: {sessionData.CalculateScoreReward()} puntos.");
//             ExitScene();
//         }
//         else
//         {
//             sessionData.wasHackSuccessful = false;
//             sessionData.RegisterFailedAttempt(); // Incrementa el intento internamente en el ScriptableObject

//             if (sessionData.isGameOverDueToFailure)
//             {
//                 TriggerGameOver();
//             }
//             else
//             {
//                 Debug.Log($"<color=yellow>[INTRUSIÓN]</color> Intento fallido. Iniciando intento número: {sessionData.currentAttempt}");
//                 // Reinicia el juego con la nueva dificultad calculada proceduralmente
//                 InitializeTerminalHack();
//             }
//         }
//     }

//     private void SetGridInteractable(bool state)
//     {
//         foreach (AlienGlyph glyph in glyphs)
//         {
//             glyph.SetInteractable(state);
//         }
//     }

//     private void TriggerGameOver()
//     {
//         Debug.Log("<color=red>[GAME OVER]</color> El robot falló los 3 intentos. Nivel perdido.");
        
//         // Limpiamos los eventos para evitar fugas de memoria
//         foreach (AlienGlyph glyph in glyphs) glyph.OnGlyphClicked -= OnGlyphSelectedByPlayer;

//         // --- MODO DE PRUEBA AISLADA ---
//         if (SceneManager.sceneCount == 1)
//         {
//             Debug.Log("<color=cyan>[MODO PRUEBA]</color> Reiniciando datos y escena automáticamente...");
//             sessionData.ResetForNewTerminal(); // Limpia los fallos
//             SceneManager.LoadScene(gameObject.scene.name); // Recarga esta misma escena
//             return;
//         }
//         // ------------------------------

//         Time.timeScale = 1f;
//         SceneManager.LoadScene("NombreDeTuEscenaGameOver"); 
//     }

//     private void ExitScene()
//     {
//         // Limpieza de eventos
//         foreach (AlienGlyph glyph in glyphs) glyph.OnGlyphClicked -= OnGlyphSelectedByPlayer;

//         // --- MODO DE PRUEBA AISLADA ---
//         if (SceneManager.sceneCount == 1)
//         {
//             Debug.Log("<color=cyan>[MODO PRUEBA]</color> Hackeo completado con éxito. Reiniciando para probar de nuevo...");
//             sessionData.ResetForNewTerminal(); // Limpia los datos para el siguiente test
//             SceneManager.LoadScene(gameObject.scene.name); // Recarga esta misma escena
//             return;
//         }
//         // ------------------------------

//         Time.timeScale = 1f;
//         SceneManager.UnloadSceneAsync(gameObject.scene.name);
//     }
// }