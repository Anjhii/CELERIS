// ============================================================
// LevelButtonPrefabCreator.cs  |  Assets/Editor/
// Herramienta de menú: Tools > CELERIS > Create Level Button Prefab
// Crea el prefab LevelButtonUI listo para asignar en LevelSelectController.
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using Celeris.UI;

public static class LevelButtonPrefabCreator
{
    [MenuItem("Tools/CELERIS/Create Level Button Prefab")]
    public static void Create()
    {
        // ── Raíz: Button ──────────────────────────────────────
        var root = new GameObject("LevelButtonPrefab");
        var rootImage = root.AddComponent<Image>();
        rootImage.color = new Color(0.18f, 0.18f, 0.28f);

        var button = root.AddComponent<Button>();
        var btnColors = button.colors;
        btnColors.highlightedColor = new Color(0.30f, 0.30f, 0.45f);
        btnColors.pressedColor     = new Color(0.10f, 0.10f, 0.20f);
        button.colors = btnColors;
        button.targetGraphic = rootImage;

        // RectTransform raíz
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(150, 100);

        // ── LevelNumText ──────────────────────────────────────
        var numGO   = new GameObject("LevelNumText");
        numGO.transform.SetParent(root.transform, false);
        var numRect = numGO.AddComponent<RectTransform>();
        numRect.anchorMin  = new Vector2(0,    0.4f);
        numRect.anchorMax  = new Vector2(1,    1f);
        numRect.offsetMin  = new Vector2(8,    0);
        numRect.offsetMax  = new Vector2(-8,   -6);
        var numTmp = numGO.AddComponent<TextMeshProUGUI>();
        numTmp.text      = "01";
        numTmp.fontSize  = 28;
        numTmp.fontStyle = FontStyles.Bold;
        numTmp.alignment = TextAlignmentOptions.Center;
        numTmp.color     = Color.white;

        // ── StarsText ─────────────────────────────────────────
        var starsGO   = new GameObject("StarsText");
        starsGO.transform.SetParent(root.transform, false);
        var starsRect = starsGO.AddComponent<RectTransform>();
        starsRect.anchorMin = new Vector2(0,    0.2f);
        starsRect.anchorMax = new Vector2(1,    0.5f);
        starsRect.offsetMin = new Vector2(4,    0);
        starsRect.offsetMax = new Vector2(-4,   0);
        var starsTmp = starsGO.AddComponent<TextMeshProUGUI>();
        starsTmp.text      = "☆☆☆";
        starsTmp.fontSize  = 16;
        starsTmp.alignment = TextAlignmentOptions.Center;
        starsTmp.color     = new Color(1f, 0.85f, 0.2f);

        // ── ScoreText ─────────────────────────────────────────
        var scoreGO   = new GameObject("ScoreText");
        scoreGO.transform.SetParent(root.transform, false);
        var scoreRect = scoreGO.AddComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0,    0f);
        scoreRect.anchorMax = new Vector2(1,    0.25f);
        scoreRect.offsetMin = new Vector2(4,    2);
        scoreRect.offsetMax = new Vector2(-4,  -2);
        var scoreTmp = scoreGO.AddComponent<TextMeshProUGUI>();
        scoreTmp.text      = "";
        scoreTmp.fontSize  = 11;
        scoreTmp.alignment = TextAlignmentOptions.Center;
        scoreTmp.color     = new Color(0.75f, 0.75f, 0.75f);

        // ── LockIcon ──────────────────────────────────────────
        var lockGO   = new GameObject("LockIcon");
        lockGO.transform.SetParent(root.transform, false);
        var lockRect = lockGO.AddComponent<RectTransform>();
        lockRect.anchorMin = new Vector2(0.25f, 0.15f);
        lockRect.anchorMax = new Vector2(0.75f, 0.85f);
        lockRect.offsetMin = Vector2.zero;
        lockRect.offsetMax = Vector2.zero;
        var lockImage = lockGO.AddComponent<Image>();
        lockImage.color = new Color(1, 1, 1, 0.7f);
        lockGO.SetActive(false);   // oculto por defecto; Setup() lo activa si locked

        // ── LevelButtonUI ─────────────────────────────────────
        var lvlBtn = root.AddComponent<LevelButtonUI>();
        lvlBtn.levelNumText = numTmp;
        lvlBtn.starsText    = starsTmp;
        lvlBtn.scoreText    = scoreTmp;
        lvlBtn.lockIcon     = lockGO;
        lvlBtn.background   = rootImage;

        // ── Guardar como prefab ───────────────────────────────
        string folder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        string path = $"{folder}/LevelButtonPrefab.prefab";
        bool success;
        PrefabUtility.SaveAsPrefabAsset(root, path, out success);
        Object.DestroyImmediate(root);

        if (success)
        {
            Debug.Log($"[CELERIS] LevelButtonPrefab creado en {path}");
            EditorUtility.DisplayDialog("Prefab creado",
                $"LevelButtonPrefab guardado en:\n{path}\n\n" +
                "Asígnalo al campo 'Level Button Prefab' del LevelSelectController.",
                "OK");

            // Seleccionar el prefab recién creado en el Project
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        else
        {
            Debug.LogError("[CELERIS] Error al guardar LevelButtonPrefab.");
        }
    }
}
