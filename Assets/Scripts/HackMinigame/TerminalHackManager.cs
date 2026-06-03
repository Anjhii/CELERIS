// ============================================================
// TerminalHackManager.cs  |  Assets/Scripts/HackMinigame/
//
// v3 — Doble UnloadScene corregido.
//
// CAMBIOS v3:
//
//   ExitScene() (éxito) ya NO llama SceneManager.UnloadSceneAsync().
//   Solo dispara OnTerminalExited. GameFlowManager suscribe a ese
//   evento y es el ÚNICO responsable de descargar la escena del
//   minijuego y llamar droide.RestoreFromPortal(). Esto elimina
//   el bug donde la escena se intentaba descargar dos veces.
//
//   TriggerGameOver() sigue cargando la escena de game over
//   directamente (es un flujo distinto al portal).
//
//   OnDisable() limpia todos los delegados de glifos.
//   HackedTerminalsCount / RequiredHacks / ResetHackedCount()
//   siguen siendo la fuente de verdad para la validación de la meta.
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TerminalHackManager : MonoBehaviour
{
    public enum HackState { MostrandoSecuencia, EsperandoJugador, Bloqueado }

    // ── Eventos globales ──────────────────────────────────────────────────────
    /// <summary>
    /// Disparado cuando el hack termina con ÉXITO y la escena está lista para cerrarse.
    /// GameFlowManager suscribe a esto para descargar MiniGameScene y restaurar el Droide.
    /// NO se dispara en game-over.
    /// </summary>
    public static event System.Action OnTerminalExited;

    /// <summary>
    /// Disparado cuando el jugador agota los 3 intentos del hackeo.
    /// GameFlowManager suscribe para descargar MiniGameScene y mostrar el panel de Game Over.
    /// </summary>
    public static event System.Action OnHackGameOver;

    // ── Contador de terminales ────────────────────────────────────────────────
    /// <summary>Terminales completadas con éxito en la run actual.</summary>
    public static int HackedTerminalsCount { get; private set; } = 0;

    /// <summary>Terminales requeridas para desbloquear la meta.</summary>
    public const int RequiredHacks = 3;

    /// <summary>Llamado por DroideController.Init() al comenzar un nivel nuevo.</summary>
    public static void ResetHackedCount() => HackedTerminalsCount = 0;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Puente de Datos (SOLID)")]
    [SerializeField] private HackSessionData sessionData;

    [Header("Componentes de la Interfaz")]
    [SerializeField] private List<AlienGlyph> glyphs;
    [SerializeField] private AudioSource centralAudioSource;

    // ── Estado interno ─────────────────────────────────────────────────────────
    private List<int> currentSequence  = new List<int>();
    private int       playerInputIndex = 0;
    private HackState currentState     = HackState.Bloqueado;

    private int   sequenceLength;
    private float displaySpeed;

    // ─────────────────────────────────────────────────────────────────────────
    private void Start()
    {
        InitializeTerminalHack();
    }

    /// <summary>
    /// Limpia todos los delegados al desactivarse para evitar fugas de memoria.
    /// Cubre tanto el cierre de escena como cualquier destrucción en play mode.
    /// </summary>
    private void OnDisable()
    {
        if (glyphs == null) return;
        foreach (AlienGlyph glyph in glyphs)
        {
            if (glyph != null)
                glyph.OnGlyphClicked -= OnGlyphSelectedByPlayer;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void InitializeTerminalHack()
    {
        if (sessionData.currentAttempt > 3)
        {
            TriggerGameOver();
            return;
        }

        sequenceLength = sessionData.GetProceduralSequenceLength();
        displaySpeed   = sessionData.GetProceduralDisplaySpeed();

        for (int i = 0; i < glyphs.Count; i++)
        {
            glyphs[i].Initialize(i);
            glyphs[i].OnGlyphClicked -= OnGlyphSelectedByPlayer;
            glyphs[i].OnGlyphClicked += OnGlyphSelectedByPlayer;
        }

        GenerateNonRepeatingSequence();
        StartCoroutine(PlaySequenceCoroutine());
    }

    private void GenerateNonRepeatingSequence()
    {
        currentSequence.Clear();
        int lastIndex = -1;
        for (int i = 0; i < sequenceLength; i++)
        {
            int nextIndex;
            do { nextIndex = Random.Range(0, glyphs.Count); }
            while (nextIndex == lastIndex);
            currentSequence.Add(nextIndex);
            lastIndex = nextIndex;
        }
    }

    private IEnumerator PlaySequenceCoroutine()
    {
        currentState = HackState.MostrandoSecuencia;
        SetGridInteractable(false);
        yield return new WaitForSeconds(0.5f);

        foreach (int index in currentSequence)
        {
            AlienGlyph glyph = glyphs[index];
            glyph.ActivateGlow();
            if (glyph.GetSound() != null && centralAudioSource != null)
                centralAudioSource.PlayOneShot(glyph.GetSound());
            yield return new WaitForSeconds(displaySpeed);
            glyph.DeactivateGlow();
            yield return new WaitForSeconds(0.15f);
        }

        currentState     = HackState.EsperandoJugador;
        playerInputIndex = 0;
        SetGridInteractable(true);
    }

    private void OnGlyphSelectedByPlayer(int glyphIndex)
    {
        if (currentState != HackState.EsperandoJugador) return;
        StartCoroutine(FlashPlayerChoice(glyphIndex));

        if (glyphIndex == currentSequence[playerInputIndex])
        {
            playerInputIndex++;
            if (playerInputIndex >= currentSequence.Count)
                HandleResolution(true);
        }
        else
        {
            HandleResolution(false);
        }
    }

    private IEnumerator FlashPlayerChoice(int index)
    {
        glyphs[index].ActivateGlow();
        if (glyphs[index].GetSound() != null && centralAudioSource != null)
            centralAudioSource.PlayOneShot(glyphs[index].GetSound());
        yield return new WaitForSeconds(0.18f);
        glyphs[index].DeactivateGlow();
    }

    private void HandleResolution(bool success)
    {
        currentState = HackState.Bloqueado;
        SetGridInteractable(false);
        StartCoroutine(ResolutionRoutine(success));
    }

    private IEnumerator ResolutionRoutine(bool success)
    {
        yield return new WaitForSeconds(0.9f);

        if (success)
        {
            sessionData.wasHackSuccessful = true;
            sessionData.extractedDigit    = Random.Range(0, 10);
            HackedTerminalsCount++;

            Debug.Log($"<color=green>[HACK EXITOSO]</color> Terminal {HackedTerminalsCount}/{RequiredHacks}. " +
                      $"Recompensa: {sessionData.CalculateScoreReward()} pts.");
            ExitScene();
        }
        else
        {
            sessionData.wasHackSuccessful = false;
            sessionData.RegisterFailedAttempt();

            if (sessionData.isGameOverDueToFailure)
            {
                TriggerGameOver();
            }
            else
            {
                Debug.Log($"<color=yellow>[INTRUSIÓN]</color> Intento {sessionData.currentAttempt} iniciado.");
                InitializeTerminalHack();
            }
        }
    }

    private void SetGridInteractable(bool state)
    {
        foreach (AlienGlyph glyph in glyphs)
            glyph.SetInteractable(state);
    }

    // ── Salida por éxito ──────────────────────────────────────────────────────
    private void ExitScene()
    {
        // Limpiar delegados (OnDisable también lo hace, pero por seguridad)
        foreach (AlienGlyph glyph in glyphs)
            glyph.OnGlyphClicked -= OnGlyphSelectedByPlayer;

        // MODO TEST AISLADO (solo esta escena cargada)
        if (SceneManager.sceneCount == 1)
        {
            Debug.Log("<color=cyan>[MODO PRUEBA]</color> Hackeo completado. Reiniciando para re-test...");
            sessionData.ResetForNewTerminal();
            // Notificar de todas formas por coherencia de listeners
            OnTerminalExited?.Invoke();
            SceneManager.LoadScene(gameObject.scene.name);
            return;
        }

        // MODO JUEGO REAL: disparar evento y ceder el control a GameFlowManager.
        // GameFlowManager.OnTerminalHackExited() recibe esto y llama OnPortalComplete(),
        // que descarga la escena aditiva y llama droide.RestoreFromPortal().
        // TerminalHackManager NO llama SceneManager.UnloadSceneAsync() aquí.
        Time.timeScale = 1f;
        OnTerminalExited?.Invoke();
    }

    // ── Game Over ─────────────────────────────────────────────────────────────
    private void TriggerGameOver()
    {
        Debug.Log("<color=red>[GAME OVER]</color> El robot falló los 3 intentos.");

        foreach (AlienGlyph glyph in glyphs)
            glyph.OnGlyphClicked -= OnGlyphSelectedByPlayer;

        // MODO TEST AISLADO
        if (SceneManager.sceneCount == 1)
        {
            Debug.Log("<color=cyan>[MODO PRUEBA]</color> Reiniciando datos y escena...");
            sessionData.ResetForNewTerminal();
            SceneManager.LoadScene(gameObject.scene.name);
            return;
        }

        // MODO JUEGO REAL: notificar a GameFlowManager para que descargue MiniGameScene
        // y muestre el panel de Game Over en-juego. NO usar SceneManager.LoadScene aquí
        // (causaría congelamiento si la escena de destino no existe).
        Time.timeScale = 1f;
        OnHackGameOver?.Invoke();
    }
}
