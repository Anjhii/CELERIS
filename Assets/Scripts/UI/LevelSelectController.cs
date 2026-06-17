// ============================================================
// LevelSelectController.cs  |  Assets/Scripts/UI/
//
// SETUP EN LevelSelectScene:
//
//   Canvas (Screen Space – Overlay)
//   └── Panel (fondo oscuro, full stretch)
//       ├── TitleText        → TMP "SELECCIONA NIVEL"
//       ├── ProgressText     → TMP "Niveles completados: X / 10"
//       ├── LevelsGrid       → GameObject con GridLayoutGroup
//       │     (se populará automáticamente con los botones)
//       └── BackButton       → Button "← VOLVER"
//
//   Configuración del GridLayoutGroup en LevelsGrid:
//     Cell Size: 150 x 80  |  Spacing: 12 x 12
//     Constraint: Fixed Column Count = 2
//
//   LevelButtonPrefab (prefab que debes crear):
//     Button
//     ├── LevelNumText  (TMP — número del nivel, ej "01")
//     ├── StarsText     (TMP — estrellas: "★★★", "★★☆", etc.)
//     └── LockIcon      (GameObject con Image de candado, se activa si bloqueado)
//     Adjuntar LevelButtonUI.cs al Button raíz.
// ============================================================
using Celeris.Core;
using Celeris.Leaderboard;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Celeris.UI
{
    public class LevelSelectController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Referencias de UI")]
        [Tooltip("Transform con GridLayoutGroup donde se instancian los botones")]
        public Transform levelsContainer;

        [Tooltip("Prefab de botón de nivel (con LevelButtonUI adjunto)")]
        public GameObject levelButtonPrefab;

        public TMP_Text progressText;
        public Button   backButton;

        [Header("Configuración")]
        [Tooltip("Total de niveles a mostrar")]
        public int totalLevels = 10;

        // ─────────────────────────────────────────────────────
        private void Start()
        {
            if (backButton != null)
                backButton.onClick.AddListener(OnBack);

            BuildLevelGrid();
            RefreshProgress();
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(OnBack);
        }

        // ── Construir grid de niveles ─────────────────────────
        private void BuildLevelGrid()
        {
            if (levelsContainer == null || levelButtonPrefab == null) return;

            // Limpiar hijos previos (útil si se regenera en caliente)
            foreach (Transform child in levelsContainer)
                Destroy(child.gameObject);

            int maxUnlocked = Mathf.Max(PlayerPrefsMaxUnlocked(), 1); // Nivel 1 siempre accesible

            for (int i = 1; i <= totalLevels; i++)
            {
                // Captura explicita de i para evitar closure-capture-by-reference.
                // Sin esto, todos los lambdas capturarian el valor final de i (totalLevels+1).
                int captured = i;

                var go   = Instantiate(levelButtonPrefab, levelsContainer);
                var btn  = go.GetComponent<LevelButtonUI>();
                if (btn == null) btn = go.AddComponent<LevelButtonUI>();

                int  stars     = GetStarsForLevel(captured);
                int  bestScore = GetBestScoreForLevel(captured);
                bool locked    = (captured > maxUnlocked);
                bool current   = (captured == LevelManager.CurrentLevelNumber);

                btn.Setup(
                    levelNumber : captured,
                    stars       : stars,
                    isLocked    : locked,
                    isCurrent   : current,
                    onClick     : () => OnLevelSelected(captured),
                    bestScore   : bestScore
                );
            }
        }

        private void RefreshProgress()
        {
            if (progressText == null) return;

            var sm = ScoreManager.Instance;
            int completed  = sm != null ? sm.LevelsCompleted : 0;
            int totalStars = sm != null ? sm.TotalStars      : 0;

            progressText.text =
                $"Completados: {Mathf.Min(completed, totalLevels)} / {totalLevels}   " +
                $"★ {totalStars}";
        }

        // ── Navegación ────────────────────────────────────────
        private void OnLevelSelected(int levelNumber)
        {
            LevelManager.EnsureExists().GoToLevel(levelNumber);
        }

        private void OnBack()
        {
            SceneManager.LoadScene("MainMenuScene");
        }

        // ── Helpers de progreso ───────────────────────────────
        // Delegan en ScoreManager (que usa IPlayerProgressStore internamente).
        // Eliminados los accesos directos a PlayerPrefs de este archivo.

        private static int GetStarsForLevel(int levelNumber) =>
            ScoreManager.Instance != null
                ? ScoreManager.Instance.GetStarsForLevel(levelNumber)
                : 0;

        // LevelBestScore usa indice 0-based; levelNumber es 1-based.
        private static int GetBestScoreForLevel(int levelNumber) =>
            ScoreManager.Instance != null
                ? ScoreManager.Instance.GetBestScoreForLevel(levelNumber - 1)
                : 0;

        private static int PlayerPrefsMaxUnlocked() =>
            ScoreManager.Instance != null
                ? ScoreManager.Instance.MaxUnlockedLevel
                : 0;
    }
}
