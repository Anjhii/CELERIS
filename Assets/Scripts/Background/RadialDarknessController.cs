using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Celeris.Core
{
    public class RadialDarknessController : MonoBehaviour
    {
        [SerializeField] private Volume radialDarknessVolume;

        private void Awake()
        {
            // Solo activo en GameplayScene
            bool isGameplay = SceneManager.GetActiveScene().name == "GameplayScene";
            if (radialDarknessVolume != null)
                radialDarknessVolume.enabled = isGameplay;
        }
    }
}