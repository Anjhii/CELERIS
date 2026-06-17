// ============================================================
// MiniGameSceneService.cs  |  Assets/Scripts/Core/
//
// RESPONSABILIDAD ÚNICA:
//   Ser el único lugar del proyecto que llama
//   SceneManager.LoadSceneAsync(Additive) y UnloadSceneAsync
//   para MiniGameScene.
//
// LO QUE NO HACE (SRP):
//   No sabe cuándo cargar ni descargar — eso lo decide GameFlowManager.
//   No restaura el Droide ni gestiona estado de portal.
//   No pausa ni reanuda el juego.
//
// GUARD _isLoaded:
//   Previene double-load y double-unload si dos sistemas
//   llaman Load() o Unload() concurrentemente.
//   GameFlowManager confía en IsLoaded para decidir si actuar.
//
// IMPLEMENTA IMiniGameSceneService (DIP):
//   GameFlowManager recibe esta interfaz, no la clase concreta.
// ============================================================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Celeris.Core
{
    public class MiniGameSceneService : IMiniGameSceneService
    {
        // ── Estado ────────────────────────────────────────────
        public bool IsLoaded { get; private set; } = false;

        // ── Eventos ───────────────────────────────────────────
        public event Action OnMiniGameLoaded;
        public event Action OnMiniGameUnloaded;

        // ── Config ────────────────────────────────────────────
        private readonly string _sceneName;
        private const string GAMEPLAY_SCENE = "GameplayScene";

        public MiniGameSceneService(string miniGameSceneName)
        {
            _sceneName = miniGameSceneName;
        }

        // ── IMiniGameSceneService ─────────────────────────────

        /// <summary>
        /// Carga MiniGameScene de forma aditiva y la activa.
        /// Guard: si ya está cargada, no-op con log de advertencia.
        /// </summary>
        public IEnumerator Load()
        {
            if (IsLoaded)
            {
                Debug.LogWarning($"[MiniGameSceneService] Load() llamado pero " +
                                 $"'{_sceneName}' ya está cargada. Ignorado.");
                yield break;
            }

            // Marcar ANTES de iniciar la carga para que cualquier llamada
            // concurrente sea rechazada por el guard.
            IsLoaded = true;

            AsyncOperation op = SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
            yield return op;

            Scene miniScene = SceneManager.GetSceneByName(_sceneName);
            if (miniScene.IsValid())
                SceneManager.SetActiveScene(miniScene);

            Debug.Log($"[MiniGameSceneService] '{_sceneName}' cargada y activa.");
            OnMiniGameLoaded?.Invoke();
        }

        /// <summary>
        /// Descarga MiniGameScene y restaura GameplayScene como activa.
        /// Guard: si no está cargada, no-op con log de advertencia.
        /// Marca IsLoaded = false ANTES de iniciar la descarga para
        /// descartar llamadas concurrentes inmediatamente.
        /// </summary>
        public IEnumerator Unload()
        {
            if (!IsLoaded)
            {
                Debug.LogWarning($"[MiniGameSceneService] Unload() llamado pero " +
                                 $"'{_sceneName}' no está cargada. Ignorado.");
                yield break;
            }

            // Marcar ANTES para rechazar llamadas concurrentes.
            IsLoaded = false;

            // Restaurar escena de gameplay como activa antes de descargar.
            Scene gameplayScene = SceneManager.GetSceneByName(GAMEPLAY_SCENE);
            if (gameplayScene.IsValid())
                SceneManager.SetActiveScene(gameplayScene);

            AsyncOperation op = SceneManager.UnloadSceneAsync(_sceneName);
            if (op != null) yield return op;

            Debug.Log($"[MiniGameSceneService] '{_sceneName}' descargada.");
            OnMiniGameUnloaded?.Invoke();
        }
    }
}
