// ============================================================
// TileModelRegistry.cs  |  Assets/Scripts/Grid/
//
// Singleton que implementa ITileModelProvider.
// Se registra en Awake() con Script Execution Order = -100
// para garantizar que cualquier TileComponent.Awake() posterior
// encuentre Instance != null.
//
// ACCION MANUAL requerida (sin cambios respecto a v1):
//   Edit > Project Settings > Script Execution Order
//   TileModelRegistry = -100
// ============================================================
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Grid
{
    public class TileModelRegistry : MonoBehaviour, ITileModelProvider
    {
        // ── Singleton ─────────────────────────────────────────
        public static ITileModelProvider Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────
        [Header("Modelos especiales por tipo de tile")]
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private GameObject laserPrefab;
        [SerializeField] private GameObject chargePrefab;
        [SerializeField] private GameObject goalPrefab;

        // ── ITileModelProvider ────────────────────────────────
        public GameObject ArrowPrefab  => arrowPrefab;
        public GameObject LaserPrefab  => laserPrefab;
        public GameObject ChargePrefab => chargePrefab;
        public GameObject GoalPrefab   => goalPrefab;

        // ── Lifecycle ─────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != (ITileModelProvider)this)
            {
                Debug.LogWarning("[TileModelRegistry] Instancia duplicada destruida.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // No DontDestroyOnLoad — el registry vive en la escena de gameplay.
            // Si la escena se recarga, Awake() vuelve a registrar la instancia.
        }

        private void OnDestroy()
        {
            if (Instance == (ITileModelProvider)this)
                Instance = null;
        }
    }
}
