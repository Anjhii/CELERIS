using Celeris.Data;
using Celeris.Player;
using UnityEngine;

namespace Celeris.Player
{
    public class DroideLightController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Light     pointLight;
        [SerializeField] private DroideCore droide;

        [Header("Intensidad por batería")]
        [SerializeField] private float maxIntensity = 3f;
        [SerializeField] private float minIntensity = 0.5f;
        [SerializeField] private float deadIntensity = 0f;

        [Header("Color por batería")]
        [SerializeField] private Color colorFull    = new Color(0.6f, 0.9f, 1f);   // cian
        [SerializeField] private Color colorLow     = new Color(1f, 0.3f, 0.1f);   // naranja/rojo
        [SerializeField] private Color colorDead    = new Color(0.1f, 0.1f, 0.1f); // casi apagado

        [Header("Suavizado")]
        [SerializeField] private float smoothSpeed = 3f;

        [Header("Estado de carga")]
        [SerializeField] private Color colorCharging = new Color(0.4f, 1f, 0.8f);  // verde cian

        private float _targetIntensity;
        private Color _targetColor;

        private void OnEnable()
        {
            if (droide != null)
            {
                droide.OnBatteryChanged += HandleBatteryChanged;
                droide.OnStateChanged   += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (droide != null)
            {
                droide.OnBatteryChanged -= HandleBatteryChanged;
                droide.OnStateChanged   -= HandleStateChanged;
            }
        }

        private void Start()
        {
            if (droide != null)
                UpdateTargets(droide.Battery, droide.State);
        }

        private void Update()
        {
            if (pointLight == null) return;
            // Guard: no actualizar luz post-muerte (evita Update corriendo zombie)
            if (droide != null && droide.State == DroideState.Dead) return;

            pointLight.intensity = Mathf.Lerp(
                pointLight.intensity, _targetIntensity, Time.deltaTime * smoothSpeed);
            pointLight.color = Color.Lerp(
                pointLight.color, _targetColor, Time.deltaTime * smoothSpeed);
        }

        private void HandleBatteryChanged(int newBattery)
        {
            // Guard: evita NullRef si droide fue destruido entre el subscribe y el
            // callback, y evita actualizar la luz cuando ya esta en estado Dead.
            if (!droide || droide.State == DroideState.Dead) return;
            UpdateTargets(newBattery, droide.State);
        }

        private void HandleStateChanged(DroideState state)
        {
            UpdateTargets(droide.Battery, state);
        }

        private void UpdateTargets(int battery, DroideState state)
        {
            if (state == DroideState.Dead || state == DroideState.Victory)
            {
                _targetIntensity = deadIntensity;
                _targetColor     = colorDead;
                return;
            }

            if (state == DroideState.Charging)
            {
                _targetColor = colorCharging;
                // Durante carga la intensidad sube y baja con LightPulse
                // solo ajustamos el color
                return;
            }

            float ratio      = battery / (float)droide.MaxBattery;
            _targetIntensity = Mathf.Lerp(minIntensity, maxIntensity, ratio);
            _targetColor     = Color.Lerp(colorLow, colorFull, ratio);
        }
    }
}