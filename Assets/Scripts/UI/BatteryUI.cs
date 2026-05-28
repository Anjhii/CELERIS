// ============================================================
// BatteryUI.cs  |  Assets/Scripts/UI/
//
// ESCENA: Crear en GameplayScene:
//   Canvas
//   └── BatterySlider  (GameObject con Slider + este script)
//       └── Fill Area > Fill  ← arrastrar al campo fillImage
//
// INSPECTOR: Asignar droide (DroideController) y los dos
//            campos del Slider (slider + fillImage).
//            Los colores tienen valores por defecto listos.
// ============================================================
using Celeris.Data;
using Celeris.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Celeris.UI
{
    public class BatteryUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Referencias")]
        public DroideController droide;
        public Slider           slider;
        [Tooltip("Slider > Fill Area > Fill (Image)")]
        public Image            fillImage;

        [Header("Colores")]
        public Color colorNormal         = Color.green;                       // movimiento normal
        public Color colorDraining       = Color.red;                         // Charging: batería drenándose
        public Color colorReadyToAdvance = new Color(0f, 0.85f, 1f);         // ReadyToAdvance: carga al 100%
        public Color colorWarning        = new Color(1f, 0.55f, 0f);         // batería baja

        [Tooltip("Proporción (0-1) por debajo de la cual se muestra el color de advertencia")]
        [Range(0f, 1f)]
        public float warningRatio = 0.30f;

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
                slider.interactable = false;   // el slider es solo lectura
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

        private void HandleStateChanged(DroideState state)
        {
            RefreshColor(state);
        }

        // ── Helpers ───────────────────────────────────────────
        private void SetSliderValue(int battery)
        {
            if (slider == null || droide == null) return;
            slider.value = battery / (float)droide.MaxBattery;
        }

        private void RefreshColor(DroideState state)
        {
            if (fillImage == null || droide == null) return;

            // Rojo: Phase 1 — batería drenándose durante el Stress Test
            if (state == DroideState.Charging)
            {
                fillImage.color = colorDraining;
                return;
            }

            // Cian: Phase 2 — carga al 100%, Droide esperando orden del jugador
            if (state == DroideState.ReadyToAdvance)
            {
                fillImage.color = colorReadyToAdvance;
                return;
            }

            // Verde / Naranja según nivel de batería restante
            float ratio = droide.Battery / (float)droide.MaxBattery;
            fillImage.color = ratio <= warningRatio ? colorWarning : colorNormal;
        }
    }
}
