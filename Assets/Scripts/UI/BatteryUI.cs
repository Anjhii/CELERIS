// ============================================================
// BatteryUI.cs  |  Assets/Scripts/UI/
//
// Barra de batería del Droide. La batería empieza en 100%.
//
// COLORES:
//   Normal    (verde)  — movimiento estándar
//   Draining  (rojo)   — dentro de ChargeTile (FrictionMovementState)
//   Warning   (naranja)— batería baja (ratio < warningRatio)
// ============================================================
using Celeris.Data;
using Celeris.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Celeris.UI
{
    public class BatteryUI : MonoBehaviour
    {
        [Header("Referencias")]
        public DroideController droide;
        public Slider           slider;
        [Tooltip("Slider > Fill Area > Fill (Image)")]
        public Image            fillImage;

        [Header("Colores")]
        public Color colorNormal   = new Color(0.20f, 0.85f, 0.20f);
        public Color colorDraining = new Color(0.90f, 0.15f, 0.10f);
        public Color colorWarning  = new Color(1.00f, 0.50f, 0.00f);

        [Range(0f, 0.4f)]
        [Tooltip("Ratio de batería por debajo del cual mostrar aviso")]
        public float warningRatio = 0.25f;

        // ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (droide == null) return;
            droide.OnBatteryChanged += HandleBatteryChanged;
            droide.OnStateChanged   += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (droide == null) return;
            droide.OnBatteryChanged -= HandleBatteryChanged;
            droide.OnStateChanged   -= HandleStateChanged;
        }

        private void Start()
        {
            if (slider != null)
            {
                slider.interactable = false;
                slider.minValue     = 0f;
                slider.maxValue     = 1f;
            }

            if (droide != null)
            {
                SetSliderValue(droide.Battery);
                RefreshColor(droide.State);
            }
        }

        // ── Callbacks ─────────────────────────────────────────
        private void HandleBatteryChanged(int newBattery)
        {
            SetSliderValue(newBattery);
            RefreshColor(droide.State);
        }

        private void HandleStateChanged(DroideState state) => RefreshColor(state);

        // ── Helpers ───────────────────────────────────────────
        private void SetSliderValue(int battery)
        {
            if (slider == null || droide == null) return;
            slider.value = droide.MaxBattery > 0
                ? battery / (float)droide.MaxBattery
                : 0f;
        }

        private void RefreshColor(DroideState state)
        {
            if (fillImage == null || droide == null) return;

            // Rojo mientras el ChargeTile drena batería
            if (state == DroideState.Charging)
            {
                fillImage.color = colorDraining;
                return;
            }

            float ratio = droide.MaxBattery > 0
                ? droide.Battery / (float)droide.MaxBattery
                : 0f;

            fillImage.color = ratio <= warningRatio ? colorWarning : colorNormal;
        }
    }
}
