// ============================================================
// HackSequenceController.cs  |  Assets/Scripts/HackMinigame/
//
// RESPONSABILIDAD ÚNICA (SRP):
//   Generar una secuencia de glifos no repetitiva y reproducirla
//   visualmente. No sabe si el jugador acertó; no gestiona intentos.
//
// CONTRATO:
//   Init(glyphs, sessionData, audioSource) — llamado por TerminalHackManager
//     antes de cada intento. Suscribirse a OnSequenceComplete ANTES de llamar Play().
//   Play() — inicia la corrutina de reproducción (debe llamarse via StartCoroutine).
//   CurrentSequence — acceso de solo lectura para HackInputValidator.
//
// EVENTOS:
//   OnSequenceComplete — emitido cuando el último glifo se apagó y el
//     jugador puede comenzar a ingresar su respuesta.
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Celeris.HackMinigame
{
    public class HackSequenceController
    {
        // ── Eventos ───────────────────────────────────────────
        public event Action OnSequenceComplete;

        // ── Estado ────────────────────────────────────────────
        public IReadOnlyList<int> CurrentSequence => _sequence;

        private readonly List<int> _sequence = new List<int>();

        // ── Dependencias (inyectadas en Init) ─────────────────
        private List<AlienGlyph> _glyphs;
        private AudioSource      _audioSource;
        private float            _displaySpeed;
        private System.Random    _rng;

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Prepara el controlador para un intento nuevo.
        /// Genera la secuencia inmediatamente; Play() la reproduce.
        /// </summary>
        public void Init(
            List<AlienGlyph> glyphs,
            HackSessionData  sessionData,
            AudioSource      audioSource)
        {
            _glyphs       = glyphs;
            _audioSource  = audioSource;
            _displaySpeed = sessionData.GetProceduralDisplaySpeed();

            // F0-T2: System.Random seeded para reproducibilidad determinista.
            // proceduralSeed es inyectado por LevelManager (config.proceduralSeed + 7777).
            // Fallback a globalLevel si el seed no fue asignado.
            int seed = sessionData.proceduralSeed != 0
                ? sessionData.proceduralSeed
                : sessionData.globalLevel + 7777;
            _rng = new System.Random(seed);

            int length = sessionData.GetProceduralSequenceLength();
            GenerateNonRepeatingSequence(length);
        }

        /// <summary>
        /// Corrutina de reproducción. Usa: StartCoroutine(sequenceController.Play()).
        /// Emite OnSequenceComplete al terminar.
        /// </summary>
        public IEnumerator Play()
        {
            yield return new WaitForSeconds(0.5f);

            foreach (int index in _sequence)
            {
                AlienGlyph glyph = _glyphs[index];
                glyph.ActivateGlow();

                AudioClip sound = glyph.GetSound();
                if (sound != null && _audioSource != null)
                    _audioSource.PlayOneShot(sound);

                yield return new WaitForSeconds(_displaySpeed);
                glyph.DeactivateGlow();
                yield return new WaitForSeconds(0.15f);
            }

            OnSequenceComplete?.Invoke();
        }

        // ── Generación ────────────────────────────────────────

        private void GenerateNonRepeatingSequence(int length)
        {
            _sequence.Clear();
            int lastIndex = -1;
            for (int i = 0; i < length; i++)
            {
                int next;
                do { next = _rng.Next(0, _glyphs.Count); }
                while (next == lastIndex);
                _sequence.Add(next);
                lastIndex = next;
            }
        }
    }
}
