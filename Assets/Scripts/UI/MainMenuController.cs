// ============================================================
// MainMenuController.cs  |  Assets/Scripts/UI/
//
// SETUP EN MainMenuScene:
//   Canvas (Screen Space – Overlay)
//   └── Panel (fondo oscuro, stretch full screen)
//       ├── TitleText       → TMP "CELERIS"
//       ├── PlayerNameText  → TMP "Hola, [nombre]"
//       ├── PlayButton      → Button  "JUGAR"       → GameplayScene
//       ├── LevelSelectBtn  → Button  "NIVELES"     → LevelSelectScene
//       └── RankingButton   → Button  "RANKING"     → RankingScene
// ============================================================
using Celeris.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Celeris.UI
{
    public class MainMenuController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Textos")]
        public TMP_Text playerNameText;

        [Header("Botones")]
        public Button playButton;
        public Button levelSelectButton;
        public Button rankingButton;

        // ─────────────────────────────────────────────────────
        private void Start()
        {
            RefreshPlayerName();

            if (playButton        != null) playButton.onClick.AddListener(OnPlay);
            if (levelSelectButton != null) levelSelectButton.onClick.AddListener(OnLevelSelect);
            if (rankingButton     != null) rankingButton.onClick.AddListener(OnRanking);
        }

        private void OnDestroy()
        {
            if (playButton        != null) playButton.onClick.RemoveListener(OnPlay);
            if (levelSelectButton != null) levelSelectButton.onClick.RemoveListener(OnLevelSelect);
            if (rankingButton     != null) rankingButton.onClick.RemoveListener(OnRanking);
        }

        // ── Callbacks ─────────────────────────────────────────

        // Jugar: arranca desde el nivel actual guardado (continúa el progreso)
        private void OnPlay()
        {
            SceneManager.LoadScene("GameplayScene");
        }

        // Niveles: abre el selector para elegir nivel específico
        private void OnLevelSelect()
        {
            SceneManager.LoadScene("LevelSelectScene");
        }

        private void OnRanking()
        {
            SceneManager.LoadScene("RankingScene");
        }

        // ── Helpers ───────────────────────────────────────────
        private void RefreshPlayerName()
        {
            if (playerNameText == null) return;

            // Leer el mismo nombre que usa ScoreManager / Ranking:
            // ScoreManager lo guarda en PlayerPrefs con la clave "Username".
            string name = PlayerPrefs.GetString("Username", "");

            // Fallback: intentar ScoreManager.Instance si está disponible
            if (string.IsNullOrWhiteSpace(name) && ScoreManager.Instance != null)
                name = ScoreManager.Instance.Username;

            // Último fallback
            if (string.IsNullOrWhiteSpace(name))
                name = "Jugador";

            playerNameText.text = $"Hola, {name}";
        }
    }
}
