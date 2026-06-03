using Celeris.Grid;
using UnityEngine;

namespace Celeris.Core
{
    public class TileModelRegistry : MonoBehaviour
    {
        [Header("Modelos especiales por tipo de tile")]
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private GameObject laserPrefab;
        [SerializeField] private GameObject chargePrefab;
        [SerializeField] private GameObject goalPrefab;

        private void Awake()
        {
            TileComponent.ArrowPrefab  = arrowPrefab;
            TileComponent.LaserPrefab  = laserPrefab;
            TileComponent.ChargePrefab = chargePrefab;
            TileComponent.GoalPrefab   = goalPrefab;
        }
    }
}
