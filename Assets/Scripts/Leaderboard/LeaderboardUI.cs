using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;  // Requiere TextMeshPro (está incluido en Unity 2019+)

/// <summary>
/// LeaderboardUI — Dibuja el ranking en pantalla con los datos del SupabaseManager.
/// Asigna los campos del Inspector y suscribe los eventos automáticamente.
/// 
/// Flujo:
///   1. Al abrir la pantalla llama ShowLeaderboard().
///   2. Muestra un spinner mientras carga.
///   3. Instancia un prefab (EntryPrefab) por cada jugador en el ranking.
///   4. Si hay error de red muestra el panel de error.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    // ─── Referencias de UI (asignar en el Inspector) ──────────────────────────
    [Header("Contenedor de Entradas")]
    [SerializeField] private Transform       entriesContainer;   // Scroll View > Viewport > Content
    [SerializeField] private GameObject      entryPrefab;        // Ver comentario al final

    [Header("Paneles de Estado")]
    [SerializeField] private GameObject      loadingPanel;       // Panel con spinner/animación
    [SerializeField] private GameObject      errorPanel;         // Panel "Sin conexión"
    [SerializeField] private GameObject      emptyPanel;         // Panel "Sé el primero"

    [Header("Info del Jugador Actual")]
    [SerializeField] private TextMeshProUGUI txtCurrentUsername;
    [SerializeField] private TextMeshProUGUI txtCurrentHighScore;
    [SerializeField] private TextMeshProUGUI txtCurrentRank;      // Opcional: "#5 en el ranking"

    [Header("Botones")]
    [SerializeField] private Button          btnRefresh;
    [SerializeField] private Button          btnClose;

    [Header("Opciones")]
    [SerializeField] private int             topLimit = 10;
    [SerializeField] private float           entryAnimDelay = 0.05f;  // Segundos entre entradas (efecto cascada)

    // ─── Estado ───────────────────────────────────────────────────────────────
    private bool _isLoading = false;

    // ─────────────────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        // Suscribirse al evento para recibir datos automáticamente
        if (SupabaseManager.Instance != null)
            SupabaseManager.Instance.OnLeaderboardReceived += HandleLeaderboardData;

        btnRefresh?.onClick.AddListener(ShowLeaderboard);
        btnClose?.onClick.AddListener(() => SceneManager.LoadScene("MainMenuScene"));
    }

    private void OnDisable()
    {
        if (SupabaseManager.Instance != null)
            SupabaseManager.Instance.OnLeaderboardReceived -= HandleLeaderboardData;

        btnRefresh?.onClick.RemoveAllListeners();
        btnClose?.onClick.RemoveAllListeners();
    }

    private void Start()
    {
        RefreshCurrentPlayerInfo();
        ShowLeaderboard();
    }

    // ─── API pública ──────────────────────────────────────────────────────────
    /// <summary>Abre la pantalla y carga el ranking. Llamar al abrir la UI.</summary>
    public void ShowLeaderboard()
    {
        if (_isLoading) return;
        gameObject.SetActive(true);
        StartCoroutine(LoadLeaderboardRoutine());
    }

    // ─── Carga con feedback visual ────────────────────────────────────────────
    private IEnumerator LoadLeaderboardRoutine()
    {
        _isLoading = true;

        SetPanelState(loading: true, error: false, empty: false);
        ClearEntries();

        // Pequeña pausa mínima para que el spinner siempre sea visible
        yield return new WaitForSeconds(0.3f);

        bool receivedData = false;

        // El evento OnLeaderboardReceived (suscrito en OnEnable) llama HandleLeaderboardData.
        // El callback solo marca que llegaron datos para salir del while.
        SupabaseManager.Instance?.FetchLeaderboard(data =>
        {
            receivedData = true;
        }, topLimit);

        // Esperar hasta 10 segundos a que lleguen los datos
        float timeout = 10f;
        while (!receivedData && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!receivedData)
        {
            SetPanelState(loading: false, error: true, empty: false);
            Debug.LogWarning("[LeaderboardUI] Timeout esperando datos del ranking.");
        }

        _isLoading = false;
    }

    // ─── Dibujo del ranking ───────────────────────────────────────────────────
    private void HandleLeaderboardData(LeaderboardWrapper data)
    {
        if (data == null || data.players == null || data.players.Length == 0)
        {
            SetPanelState(loading: false, error: false, empty: true);
            return;
        }

        SetPanelState(loading: false, error: false, empty: false);
        ClearEntries();

        StartCoroutine(PopulateEntriesWithAnimation(data));
        HighlightCurrentPlayer(data);
    }

    private IEnumerator PopulateEntriesWithAnimation(LeaderboardWrapper data)
    {
        for (int i = 0; i < data.players.Length; i++)
        {
            PlayerScore jugador = data.players[i];
            SpawnEntry(jugador);
            yield return new WaitForSeconds(entryAnimDelay);
        }
    }

    private void SpawnEntry(PlayerScore jugador)
    {
        if (entryPrefab == null || entriesContainer == null) return;

        GameObject entry = Instantiate(entryPrefab, entriesContainer);

        // Busca los componentes por nombre de objeto hijo o etiqueta.
        // Adapta estos nombres a los de tu prefab real.
        SetChildText(entry, "TxtRank",      $"#{jugador.posicion}");
        SetChildText(entry, "TxtUsername",  jugador.username);
        SetChildText(entry, "TxtScore",     FormatScore(jugador.high_score));

        // Resaltar visualmente si es el jugador actual
        bool isCurrentPlayer = ScoreManager.Instance != null &&
                               jugador.username == ScoreManager.Instance.Username;

        Image bg = entry.GetComponent<Image>();
        if (bg != null && isCurrentPlayer)
            bg.color = new Color(1f, 0.9f, 0.2f, 0.25f);  // Fondo dorado suave

        // Ícono de trofeo para top 3
        Transform trophy = entry.transform.Find("ImgTrophy");
        if (trophy != null)
            trophy.gameObject.SetActive(jugador.posicion <= 3);
    }

    // ─── Info del jugador actual ──────────────────────────────────────────────
    private void RefreshCurrentPlayerInfo()
    {
        if (ScoreManager.Instance == null) return;

        if (txtCurrentUsername  != null) txtCurrentUsername.text  = ScoreManager.Instance.Username;
        if (txtCurrentHighScore != null) txtCurrentHighScore.text = FormatScore(ScoreManager.Instance.LocalHighScore);
    }

    private void HighlightCurrentPlayer(LeaderboardWrapper data)
    {
        if (ScoreManager.Instance == null || txtCurrentRank == null) return;

        string myName = ScoreManager.Instance.Username;
        foreach (PlayerScore p in data.players)
        {
            if (p.username == myName)
            {
                if (txtCurrentRank != null)
                    txtCurrentRank.text = $"You: #{p.posicion}";
                return;
            }
        }
    
        if (txtCurrentRank != null)
            txtCurrentRank.text = "You are not in the Top";
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private void SetPanelState(bool loading, bool error, bool empty)
    {
        if (loadingPanel) loadingPanel.SetActive(loading);
        if (errorPanel)   errorPanel.SetActive(error);
        if (emptyPanel)   emptyPanel.SetActive(empty);
    }

    private void ClearEntries()
    {
        if (entriesContainer == null) return;
        foreach (Transform child in entriesContainer)
            Destroy(child.gameObject);
    }

    private void SetChildText(GameObject root, string childName, string value)
    {
        Transform t = root.transform.Find(childName);
        if (t == null) return;

        TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = value; return; }

        Text legacy = t.GetComponent<Text>();
        if (legacy != null) legacy.text = value;
    }

    /// <summary>Formatea puntajes grandes: 1200 → "1,200" | 1500000 → "1.5M"</summary>
    private string FormatScore(long score)
    {
        if (score >= 1_000_000) return $"{score / 1_000_000.0:0.#}M";
        if (score >= 1_000)     return score.ToString("N0");
        return score.ToString();
    }
}

/*
 ══════════════════════════════════════════════════════════
  GUÍA DE ESTRUCTURA DEL PREFAB "EntryPrefab"
 ══════════════════════════════════════════════════════════
  EntryPrefab (GameObject con Image)
  ├── ImgTrophy   (Image — ícono trofeo, se oculta si posición > 3)
  ├── TxtRank     (TextMeshProUGUI — "#1")
  ├── TxtUsername (TextMeshProUGUI — "NombreJugador")
  └── TxtScore    (TextMeshProUGUI — "12,500")
 
  Los nombres deben coincidir exactamente con los usados en SetChildText().
 ══════════════════════════════════════════════════════════
*/
