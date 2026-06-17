using UnityEngine;
using UnityEngine.Rendering;
using Celeris.Player;

namespace Celeris.Background
{
    public class RadialLightSync : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Light pointLight;
        [SerializeField] private Volume radialVolume;
        [SerializeField] private Camera mainCamera;

        [Header("Mapeo Intensidad → Radio")]
        [Tooltip("Intensidad mínima del Point Light (base)")]
        [SerializeField] private float minLightIntensity = 3f;
        [Tooltip("Intensidad máxima del Point Light (pulse)")]
        [SerializeField] private float maxLightIntensity = 6f;
        [Tooltip("Radio de máscara cuando la luz está al mínimo")]
        [SerializeField] private float minRadius = 0.25f;
        [Tooltip("Radio de máscara cuando la luz está al máximo")]
        [SerializeField] private float maxRadius = 0.45f;

        [Header("Suavizado")]
        [SerializeField] private float smoothSpeed = 5f;

        private RadialDarknessEffect _effect;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (radialVolume != null)
                radialVolume.profile.TryGet(out _effect);

            if (_effect == null)
            {
                Debug.LogError("[RadialLightSync] No se encontró RadialDarknessEffect en el Volume.");
                return;
            }

            // ── Inicializar centro y radio al estado actual ──
            Vector3 screenPos = mainCamera.WorldToViewportPoint(transform.position);
            _effect.center.value = new Vector2(screenPos.x, screenPos.y);
            _effect.center.overrideState = true;

            float t = Mathf.InverseLerp(minLightIntensity, maxLightIntensity, pointLight.intensity);
            _effect.radius.value = Mathf.Lerp(minRadius, maxRadius, t);
            _effect.radius.overrideState = true;
        }

        private void Update()
        {
            if (_effect == null || pointLight == null || mainCamera == null) return;

            // ── Centro: posición del Droide en pantalla ──────
            Vector3 screenPos = mainCamera.WorldToViewportPoint(transform.position);
            Vector2 targetCenter = new Vector2(screenPos.x, screenPos.y);
            _effect.center.value = Vector2.Lerp(
                _effect.center.value, targetCenter, Time.deltaTime * smoothSpeed * 2f);

            // ── Radio: mapeado desde intensidad de luz ───────
            float t = Mathf.InverseLerp(minLightIntensity, maxLightIntensity, pointLight.intensity);
            float targetRadius = Mathf.Lerp(minRadius, maxRadius, t);
            _effect.radius.value = Mathf.Lerp(
                _effect.radius.value, targetRadius, Time.deltaTime * smoothSpeed);
        }
    }
}