// ============================================================
// LevelButtonUI.cs  |  Assets/Scripts/UI/
//
// Adjuntar al prefab de botón de nivel.
// LevelSelectController llama a Setup() en cada botón al generarlos.
//
// JERARQUÍA DEL PREFAB:
//   Button  ← este GameObject, con LevelButtonUI + Button
//   ├── LevelNumText   (TMP_Text)  — "01", "02", …
//   ├── StarsText      (TMP_Text)  — "★★★" / "★★☆" / "☆☆☆"
//   └── LockIcon       (GameObject) — visible solo si bloqueado
// ============================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Celeris.UI
{
    public class LevelButtonUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Textos")]
        public TMP_Text levelNumText;
        public TMP_Text starsText;
        [Tooltip("Texto opcional para mostrar el mejor puntaje del nivel (puede ser null)")]
        public TMP_Text scoreText;

        [Header("Estado visual")]
        public GameObject lockIcon;   // GameObject con candado (se activa si locked)
        public Image      background; // Imagen del fondo del botón

        [Header("Colores")]
        public Color colorCompleted = new Color(0.37f, 0.60f, 0.13f); // verde
        public Color colorCurrent   = new Color(0.20f, 0.55f, 1.00f); // azul
        public Color colorLocked    = new Color(0.25f, 0.25f, 0.25f); // gris
        public Color colorAvailable = new Color(0.18f, 0.18f, 0.28f); // oscuro neutro

        // ─────────────────────────────────────────────────────
        public void Setup(int levelNumber, int stars, bool isLocked,
                          bool isCurrent, System.Action onClick, int bestScore = 0)
        {
            // Número del nivel
            if (levelNumText != null)
                levelNumText.text = levelNumber.ToString("D2");

            // Estrellas (★ llena / ☆ vacía)
            if (starsText != null)
                starsText.text = BuildStarString(stars, maxStars: 3);

            // Mejor puntaje del nivel
            if (scoreText != null)
                scoreText.text = bestScore > 0 ? $"{bestScore} pts" : "";

            // Ícono de candado
            if (lockIcon != null)
                lockIcon.SetActive(isLocked);

            // Color de fondo según estado
            if (background != null)
            {
                background.color = isLocked    ? colorLocked
                                 : isCurrent   ? colorCurrent
                                 : stars > 0   ? colorCompleted
                                               : colorAvailable;
            }

            // Interacción
            var btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = !isLocked;
                btn.onClick.RemoveAllListeners();
                if (!isLocked && onClick != null)
                    btn.onClick.AddListener(() => onClick());
            }
        }

        // "★★☆" para stars=2, maxStars=3
        private static string BuildStarString(int stars, int maxStars)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < maxStars; i++)
                sb.Append(i < stars ? "★" : "☆");
            return sb.ToString();
        }
    }
}
