using Celeris.Core;
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

        [Header("Modelos especiales")]
        public static GameObject ArrowPrefab;
        public static GameObject LaserPrefab;
        public static GameObject ChargePrefab;
        public static GameObject GoalPrefab;

        [HideInInspector] public Vector2Int gridCoord;

        private Renderer _rend;
        private GameObject _specialModel;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _rend = GetComponentInChildren<Renderer>();
        }

        public void RotateArrow90Degrees()
        {
            if (tileType != TileType.ArrowTile) return;
            arrowDirection = (MoveDirection)(((int)arrowDirection + 1) % 4);
            transform.Rotate(Vector3.up, 90f, Space.World);
        }

        public void ToggleLaser()
        {
            if (tileType != TileType.LaserTile) return;
            isActive = !isActive;
            ApplyVisual();
        }

        public Vector2Int GetExitDirection(Vector2Int currentDir)
        {
            return tileType switch
            {
                TileType.ArrowTile => DirectionToVector(arrowDirection),
                TileType.VoidTile  => Vector2Int.zero,
                _                  => currentDir
            };
        }

        public void Refresh()
        {
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            SpawnSpecialModel();
            UpdateLasers();
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

            GameObject prefab = tileType switch
            {
                TileType.ArrowTile  => ArrowPrefab,
                TileType.LaserTile  => LaserPrefab,
                TileType.ChargeTile => ChargePrefab,
                TileType.GoalTile   => GoalPrefab,
                _                   => null
            };

            if (prefab == null) return;

            _specialModel = Instantiate(prefab, transform);
            _specialModel.transform.localPosition = Vector3.zero;

            if (tileType == TileType.LaserTile || tileType == TileType.GoalTile)
            {
                float angle = arrowDirection switch
                {
                    MoveDirection.North => 180f,
                    MoveDirection.South => 0f,
                    MoveDirection.East  => 270f,
                    MoveDirection.West  => 90f,
                    _                   => 0f
                };
                _specialModel.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
            else if (tileType == TileType.ArrowTile)
            {
                float angle = arrowDirection switch
                {
                    MoveDirection.North => 0f,
                    MoveDirection.East  => 90f,
                    MoveDirection.South => 180f,
                    MoveDirection.West  => 270f,
                    _                   => 0f
                };
                _specialModel.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
            else
            {
                _specialModel.transform.localRotation = Quaternion.identity;
            }

            if (tileType == TileType.ChargeTile)
            {
                var effect = _specialModel.GetComponent<EnergiaTileEffect>();
                if (effect != null)
                    effect.StopEffect();
            }
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

        public static Vector2Int DirectionToVector(MoveDirection dir) =>
            dir switch
            {
                MoveDirection.North => Vector2Int.up,
                MoveDirection.South => Vector2Int.down,
                MoveDirection.East  => Vector2Int.right,
                MoveDirection.West  => Vector2Int.left,
                _                   => Vector2Int.up
            };
    }
}