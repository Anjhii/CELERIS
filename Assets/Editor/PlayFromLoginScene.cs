// ============================================================
// PlayFromLoginScene.cs  |  Assets/Editor/
//
// Script de SOLO EDITOR — no se incluye en builds.
// Hace que al presionar Play en Unity, el juego siempre
// arranque desde LoginScene (índice 0 en Build Settings),
// independientemente de qué escena tengas abierta.
//
// Al salir del Play Mode, reabre automáticamente la escena
// que tenías antes para no interrumpir el flujo de trabajo.
//
// Sin configuración necesaria — se activa solo al importar.
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Celeris.Editor
{
    [InitializeOnLoad]
    public static class PlayFromLoginScene
    {
        // Clave para guardar la escena activa antes de entrar en Play Mode
        private const string PREV_SCENE_KEY = "CELERIS_EditorPrevScene";

        // Nombre de la escena de inicio (debe coincidir con Build Settings índice 0)
        private const string LOGIN_SCENE_PATH = "Assets/Scenes/LoginScene.unity";

        static PlayFromLoginScene()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                // ── Justo ANTES de entrar en Play Mode ──────────
                case PlayModeStateChange.ExitingEditMode:
                    // Guardar la escena actualmente abierta
                    string currentScene = EditorSceneManager.GetActiveScene().path;
                    EditorPrefs.SetString(PREV_SCENE_KEY, currentScene);

                    // Si ya estamos en LoginScene, no hacer nada
                    if (currentScene == LOGIN_SCENE_PATH) break;

                    // Pedir al usuario que guarde los cambios pendientes
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        // El usuario canceló → abortar Play Mode
                        EditorApplication.isPlaying = false;
                        break;
                    }

                    // Abrir LoginScene antes de que Unity entre en Play Mode
                    EditorSceneManager.OpenScene(LOGIN_SCENE_PATH);
                    break;

                // ── Justo DESPUÉS de salir de Play Mode ─────────
                case PlayModeStateChange.EnteredEditMode:
                    string prevScene = EditorPrefs.GetString(PREV_SCENE_KEY, "");
                    if (!string.IsNullOrEmpty(prevScene) && prevScene != LOGIN_SCENE_PATH)
                    {
                        // Volver a la escena que tenías abierta antes
                        EditorSceneManager.OpenScene(prevScene);
                    }
                    EditorPrefs.DeleteKey(PREV_SCENE_KEY);
                    break;
            }
        }
    }
}
#endif
