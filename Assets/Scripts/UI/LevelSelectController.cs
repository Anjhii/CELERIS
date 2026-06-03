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
                var go   = Instantiate(levelButtonPrefab, levelsContainer);
                var btn  = go.GetComponent<LevelButtonUI>();
                if (btn == null) btn = go.AddComponent<LevelButtonUI>();

                int  stars     = GetStarsForLevel(i);
                int  bestScore = GetBestScoreForLevel(i);
                bool locked    = (i > maxUnlocked);
                bool current   = (i == LevelManager.CurrentLevelNumber);

                btn.Setup(
                    levelNumber : i,
                    stars       : stars,
                    isLocked    : locked,
                    isCurrent   : current,
                    onClick     : () => OnLevelSelected(i),  // captura correcta con variable local
                    bestScore   : bestScore
                );
            }
        }

        private void RefreshProgress()
        {
            if (progressText == null) return;

            int completed  = PlayerPrefs.GetInt("LevelsCompleted", 0);
            int total      = totalLevels;
            int totalStars = PlayerPrefs.GetInt("TotalStars", 0);

            progressText.text =
                $"Completados: {Mathf.Min(completed, total)} / {total}   " +
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
        // Leen directamente de PlayerPrefs con las mismas claves que ScoreManager
        private static int GetStarsForLevel(int levelNumber) =>
            PlayerPrefs.GetInt($"LevelStars_{levelNumber}", 0);

        // Nota: LevelButtonUI usa índice 0-based para LevelBestScore pero
        // LevelSelectController itera levelNumber (1-based). El índice es levelNumber-1.
        private static int GetBestScoreForLevel(int levelNumber) =>
            PlayerPrefs.GetInt($"LevelBestScore_{levelNumber - 1}", 0);

        private static int PlayerPrefsMaxUnlocked() =>
            PlayerPrefs.GetInt("MaxUnlockedLevel", 0);
    }
}
