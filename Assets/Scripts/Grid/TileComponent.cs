// ============================================================
// TileComponent.cs  |  Assets/Scripts/Grid/
//
// Componente MonoBehaviour de un tile instanciado en escena.
// Combina identidad visual + coordenada de grid + dirección.
//
// MIGRACIÓN FASE 1 — Vector2Int → Vector3Int:
//   gridCoord ahora es Vector3Int. Y = 0 en plano horizontal.
//   En Fase 3/4 (wall-walking), Y podrá ser != 0.
//   DirectionToVector y GetExitDirection devuelven Vector3Int.
//
//   BREAKING CHANGE para los consumidores de gridCoord y
//   GetExitDirection (principalmente DroideController y
//   ModularDecorationSpawner). Ver roadmap Fase 4 para la
//   adaptación de ModularDecorationSpawner.
//
// NOTA — TileModelRegistry:
//   Los prefabs estáticos (ArrowPrefab, etc.) se asignan en
//   Awake() de TileModelRegistry. Ese script debe tener
//   Script Execution Order = -100 para garantizar que se
//   ejecute antes que cualquier TileComponent.Awake().
//   Acción manual: Edit → Project Settings → Script Execution Order.
// ============================================================
using Celeris.Data;
using System.Collections;
using UnityEngine;

namespace Celeris.Grid
{
    public class TileComponent : MonoBehaviour
    {
        [Header("Tipo")]
        public TileType tileType = TileType.BaseTile;

        [Header("Arrow")]
        public MoveDirection arrowDirection = MoveDirection.North;

        [Header("Estado interactivo")]
        public bool isActive = true;

        // ── Proveedor de prefabs (DIP, reemplaza statics) ────
        // Resuelto una vez en Awake(). TileModelRegistry implementa
        // ITileModelProvider con Script Execution Order -100, garantizando
        // que Instance != null cuando TileComponent.Awake() corre.
        private static ITileModelProvider _modelProvider;

        [Header("Portal — tile que activa el minijuego")]
        [Tooltip("Color del tile portal (identificador visual del minijuego)")]
        public Color portalColor = new Color(0.58f, 0.08f, 1.00f);

        // ── MIGRACIÓN Fase 1: Vector2Int → Vector3Int ─────────
        // Y = 0 siempre hasta Fase 3/4 (wall-walking).
        [HideInInspector] public Vector3Int gridCoord;

        private Renderer _rend;
        private GameObject _specialModel;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            // Resolver provider una vez por escena (TileModelRegistry.Awake corre antes).
            if (_modelProvider == null)
                _modelProvider = TileModelRegistry.Instance;

            _rend = GetComponentInChildren<Renderer>();
            Refresh();
        }

        public void RotateArrow90Degrees()
        {
            if (tileType != TileType.ArrowTile) return;
            arrowDirection = (MoveDirection)(((int)arrowDirection + 1) % 4);
            Refresh();
        }

        public void ToggleLaser()
        {
            if (tileType != TileType.LaserTile) return;
            isActive = !isActive;
            Refresh();
        }

        /// <summary>
        /// Devuelve la dirección de salida de este tile.
        /// Si es ArrowTile, devuelve su dirección configurada.
        /// Si no, devuelve la dirección de entrada sin cambios.
        /// Retorna Vector3Int (Fase 1: Y siempre 0).
        /// </summary>
        public Vector3Int GetExitDirection(Vector3Int currentDir) =>
            tileType == TileType.ArrowTile
                ? DirectionToVector(arrowDirection)
                : currentDir;

        public void Refresh()
        {
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            SpawnSpecialModel();
            UpdateLasers();

            if (_rend == null) _rend = GetComponentInChildren<Renderer>();
            if (_rend == null) return;

            if (tileType == TileType.PortalTile)
                _rend.material.color = portalColor;
        }

        private void UpdateLasers()
        {
            if (tileType != TileType.LaserTile || _specialModel == null) return;

            Transform lasersGroup = _specialModel.transform.Find("Lasers");
            if (lasersGroup == null) return;

            lasersGroup.gameObject.SetActive(isActive);
        }

        private void SpawnSpecialModel()
        {
            if (_specialModel != null)
            {
                Destroy(_specialModel);
                _specialModel = null;
            }

            GameObject prefab = _modelProvider == null ? null : tileType switch
            {
                TileType.ArrowTile  => _modelProvider.ArrowPrefab,
                TileType.LaserTile  => _modelProvider.LaserPrefab,
                TileType.ChargeTile => _modelProvider.ChargePrefab,
                TileType.GoalTile   => _modelProvider.GoalPrefab,
                _                   => null
            };

            if (prefab == null) return;

            _specialModel = Instantiate(prefab, transform);
            _specialModel.transform.localPosition = Vector3.zero;

            float angle = tileType switch
            {
                TileType.LaserTile or TileType.GoalTile => arrowDirection switch
                {
                    MoveDirection.North => 180f,
                    MoveDirection.South => 0f,
                    MoveDirection.East  => 270f,
                    MoveDirection.West  => 90f,
                    _                   => 0f
                },
                TileType.ArrowTile => arrowDirection switch
                {
                    MoveDirection.North => 0f,
                    MoveDirection.East  => 90f,
                    MoveDirection.South => 180f,
                    MoveDirection.West  => 270f,
                    _                   => 0f
                },
                _ => 0f
            };
            _specialModel.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        public void PulseEmission(float duration = 0.3f)
        {
            if (_rend == null) return;
            StartCoroutine(EmissionPulseRoutine(duration));
        }

        private IEnumerator EmissionPulseRoutine(float duration)
        {
            var mat = _rend.material;

            Color baseEmission = mat.GetColor(EmissionColorID);
            Color peakEmission = baseEmission + new Color(0.4f, 0.5f, 0.6f) * 2f;

            float elapsed = 0f;
            float half    = duration * 0.5f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / half);
                mat.SetColor(EmissionColorID, Color.Lerp(baseEmission, peakEmission, t));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / half);
                mat.SetColor(EmissionColorID, Color.Lerp(peakEmission, baseEmission, t));
                yield return null;
            }

            mat.SetColor(EmissionColorID, baseEmission);
        }

        /// <summary>
        /// Convierte MoveDirection a Vector3Int en el plano XZ.
        /// Y = 0 siempre (plano horizontal, Fase 1).
        /// En Fase 3/4 con wall-walking, esta función se expandirá
        /// para incluir direcciones en el eje Y (rampas, paredes).
        /// </summary>
        public static Vector3Int DirectionToVector(MoveDirection dir) => dir switch
        {
            MoveDirection.North => new Vector3Int( 0, 0,  1),
            MoveDirection.South => new Vector3Int( 0, 0, -1),
            MoveDirection.East  => new Vector3Int( 1, 0,  0),
            MoveDirection.West  => new Vector3Int(-1, 0,  0),
            _                   => new Vector3Int( 0, 0,  1)
        };
    }
}
