// ============================================================
// HackInputValidator.cs  |  Assets/Scripts/HackMinigame/
//
// RESPONSABILIDAD ÚNICA (SRP):
//   Escuchar los clicks del jugador sobre AlienGlyph, compararlos
//   contra la secuencia esperada (suministrada por HackSequenceController)
//   y emitir OnAttemptResult(bool success).
//
// LO QUE NO HACE:
//   No genera la secuencia — eso lo hace HackSequenceController.
//   No decide si reintentar o hacer game over — eso lo hace TerminalHackManager.
//   No toca sessionData.currentAttempt ni isGameOverDueToFailure.
//
// CICLO DE VIDA:
//   1. TerminalHackManager llama Init() con la lista de glifos y la secuencia.
//   2. TerminalHackManager llama Enable() cuando la secuencia termina.
//   3. El jugador hace click → el validador emite OnAttemptResult.
//   4. TerminalHackManager llama Disable() antes de iniciar el siguiente intento.
//   5. TerminalHackManager llama Cleanup() en OnDisable para liberar delegados.
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Celeris.HackMinigame
{
    public class HackInputValidator
    {
        // ── Eventos ───────────────────────────────────────────
        /// <summary>
        /// Emitido cuando el jugador completa la secuencia correctamente (true)
        /// o comete un error (false). Solo se emite una vez por intento.
        /// </summary>
        public event Action<bool> OnAttemptResult;

        // ── Estado interno ────────────────────────────────────
        private List<AlienGlyph>   _glyphs;
        private IReadOnlyList<int> _sequence;
        private AudioSource        _audioSource;

        private int  _inputIndex = 0;
        private bool _active     = false;

        // Referencia a MonoBehaviour para StartCoroutine (AlienGlyph es MonoBehaviour)
        private MonoBehaviour _coroutineRunner;

        // ── API pública ───────────────────────────────────────

        public void Init(
            List<AlienGlyph>   glyphs,
            IReadOnlyList<int> sequence,
            AudioSource        audioSource,
            MonoBehaviour      coroutineRunner)
        {
            _glyphs          = glyphs;
            _sequence        = sequence;
            _audioSource     = audioSource;
            _coroutineRunner = coroutineRunner;
            _inputIndex      = 0;
        }

        /// <summary>Habilita la recepción de clicks. Suscribir delegados.</summary>
        public void Enable()
        {
            _active     = true;
            _inputIndex = 0;
            foreach (AlienGlyph g in _glyphs)
                g.OnGlyphClicked += HandleClick;
        }

        /// <summary>Deshabilita clicks. Llamar antes de reiniciar o al salir.</summary>
        public void Disable()
        {
            _active = false;
            foreach (AlienGlyph g in _glyphs)
                g.OnGlyphClicked -= HandleClick;
        }

        /// <summary>
        /// Alias explícito para OnDisable del MonoBehaviour.
        /// Garantiza que los delegados se liberen incluso si TerminalHackManager
        /// es destruido sin haber llamado Disable().
        /// </summary>
        public void Cleanup() => Disable();

        // ── Lógica privada ────────────────────────────────────

        private void HandleClick(int glyphIndex)
        {
            if (!_active) return;

            // Flash del glifo pulsado (feedback visual inmediato)
            _coroutineRunner.StartCoroutine(FlashGlyph(glyphIndex));

            if (glyphIndex == _sequence[_inputIndex])
            {
                _inputIndex++;
                if (_inputIndex >= _sequence.Count)
                {
                    // Secuencia completada correctamente
                    Disable();
                    OnAttemptResult?.Invoke(true);
                }
                // Si no, esperar el siguiente click
            }
            else
            {
                // Error: emitir resultado y bloquear nuevos inputs
                Disable();
                OnAttemptResult?.Invoke(false);
            }
        }

        private IEnumerator FlashGlyph(int index)
        {
            AlienGlyph glyph = _glyphs[index];
            glyph.ActivateGlow();

            AudioClip sound = glyph.GetSound();
            if (sound != null && _audioSource != null)
                _audioSource.PlayOneShot(sound);

            yield return new WaitForSeconds(0.18f);
            glyph.DeactivateGlow();
        }
    }
}
