// ============================================================
// GameFlowManager.cs  |  Assets/Scripts/Core/
//
// SETUP EN GameplayScene:
//   Seleccionar el GameObject "GridGenerator" → Add Component →
//   GameFlowManager. No necesitas arrastrar nada al Inspector;
//   el script encuentra ProceduralGridGenerator y DroideController
//   automáticamente con FindObjectOfType.
//
//   Si prefieres asignarlos manualmente, arrastra los GameObjects
//   a los campos "generator" y "droide" en el Inspector.
//
// FLUJO:
//   Awake()  → llama a LevelManager.EnsureExists() para garantizar
//              que el singleton exista incluso si se testea desde
//              GameplayScene directamente.
//            → inyecta ProceduralLevelConfig del nivel actual en
//              generator.config ANTES de que Start() llame a BuildGrid.
//   Victory  → espera victoryDelay segundos → LevelManager.LoadMiniGame().
// ============================================================
using System.Collections;
using Celeris.Data;
using Celeris.Grid;
using Celeris.Player;
using UnityEngine;

namespace Celeris.Core
{
    public class GameFlowManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Referencias (se encuentran automáticamente si se dejan vacías)")]
        public ProceduralGridGenerator generator;
        public DroideController        droide;

        [Header("Timing")]
        [Tooltip("Segundos de pausa entre la victoria y la carga de MiniGameScene")]
        public float victoryDelay = 1.0f;

        // ── Awake ─────────────────────────────────────────────
        private void Awake()
        {
            // Garantizar que LevelManager existe antes de hacer cualquier cosa.
            // Si viene de LoginScene normalmente ya existirá; si se testea
            // directamente desde GameplayScene, EnsureExists() lo crea.
            var lm = LevelManager.EnsureExists();

            // Auto-conectar referencias si no están asignadas en Inspector
            if (generator == null)
                generator = FindObjectOfType<ProceduralGridGenerator>();
            if (droide == null)
                droide = FindObjectOfType<DroideController>();

            if (generator == null)
            {
                Debug.LogError("[GameFlowManager] No se encontró ProceduralGridGenerator. " +
                               "¿Está el GameObject GridGenerator en la escena?");
                return;
            }

            // Inyectar config del nivel actual ANTES de que Start() llame a BuildGrid
            var config = lm.CurrentConfig;
            if (config != null)
            {
                generator.config = config;
                Debug.Log($"[GameFlowManager] Config '{config.name}' inyectada " +
                          $"(Nivel {LevelManager.CurrentLevelNumber}).");
            }
            else
            {
                Debug.LogError($"[GameFlowManager] ⚠ No hay config para el nivel " +
                               $"{LevelManager.CurrentLevelNumber}. " +
                               $"Verifica que Level{LevelManager.CurrentLevelNumber:D2}.asset " +
                               $"está en Assets/Resources/LevelConfigs/ y que Level01 también está ahí.");
            }
        }

        // ── Suscripción a eventos ─────────────────────────────
        private void OnEnable()
        {
            if (droide != null) droide.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (droide != null) droide.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            // Re-suscribir si droide se encontró después de OnEnable
            if (droide == null)
            {
                droide = FindObjectOfType<DroideController>();
                if (droide != null)
                    droide.OnStateChanged += HandleStateChanged;
                else
                    Debug.LogError("[GameFlowManager] No se encontró DroideController en la escena.");
            }
        }

        // ── Manejador de estado ───────────────────────────────
        private void HandleStateChanged(DroideState state)
        {
            if (state == DroideState.Victory)
                StartCoroutine(OnVictory());
        }

        private IEnumerator OnVictory()
        {
            Debug.Log($"[GameFlowManager] ¡Victoria! Nivel {LevelManager.CurrentLevelNumber} superado.");
            yield return new WaitForSeconds(victoryDelay);
            LevelManager.EnsureExists().LoadMiniGame();
        }
    }
}
