using System.Collections;
using UnityEngine;

namespace Celeris.Core
{
    public class EnergiaTileEffect : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Light nuclearLight;
        [SerializeField] private Renderer nucleoRenderer;
        [SerializeField] private ParticleSystem drainParticles;

        [Header("Luz")]
        [SerializeField] private float minLightIntensity = 0.5f;
        [SerializeField] private float maxLightIntensity = 3f;
        [SerializeField] private float pulseSpeed = 5f;

        [Header("Emisión")]
        [SerializeField] private float minEmission = 0.5f;
        [SerializeField] private float maxEmission = 2f;

        [Header("Colores por batería")]
        [SerializeField] private Color colorFull    = new Color(0.6f, 0.9f, 1f);
        [SerializeField] private Color colorLow     = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private float warningRatio = 0.30f;

        private Material _nucleoMat;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private Color _baseEmissionColor;
        private bool _isDraining = false;
        private Coroutine _pulseCoroutine;

        private void Awake()
        {
            if (nucleoRenderer != null)
            {
                _nucleoMat = nucleoRenderer.material;
                _baseEmissionColor = _nucleoMat.GetColor(EmissionColor);
            }
        }

        private void Start()
        {
             if (drainParticles != null)
            {
                drainParticles.Stop();
                drainParticles.Clear();
            }

            if (nuclearLight != null)
                nuclearLight.intensity = minLightIntensity;

            if (_nucleoMat != null)
                _nucleoMat.SetColor(EmissionColor, _baseEmissionColor * minEmission);
        }

        public void StartDraining()
        {
            if (_isDraining) return;
            _isDraining = true;

            if (drainParticles != null)
            {
                drainParticles.Clear();
                drainParticles.Play();
            }

            if (_pulseCoroutine != null)
                StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseLoop());
        }

        public void StopEffect()
        {
            _isDraining = false;

            if (drainParticles != null)
                drainParticles.Stop();

            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            if (nuclearLight != null)
                nuclearLight.intensity = minLightIntensity;

            if (_nucleoMat != null)
                _nucleoMat.SetColor(EmissionColor, _baseEmissionColor * minEmission);
        }

        public void UpdateBatteryColor(float batteryRatio)
        {
            Color current = Color.Lerp(colorLow, colorFull, batteryRatio);

            if (nuclearLight != null)
                nuclearLight.color = current;

            if (drainParticles != null)
            {
                var main = drainParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(current);
            }

            if (_nucleoMat != null)
            {
                float emissionValue = _isDraining ? maxEmission : minEmission;
                _nucleoMat.SetColor(EmissionColor, current * emissionValue);
            }
        }

        public void TriggerTapBurst()
        {
            if (drainParticles == null || !_isDraining) return;

            var burst = new ParticleSystem.Burst(0f, 8);
            drainParticles.emission.SetBursts(new ParticleSystem.Burst[] { burst });
            drainParticles.Emit(8);
        }

        private IEnumerator PulseLoop()
        {
            while (_isDraining)
            {
                yield return StartCoroutine(LerpEffect(minLightIntensity, maxLightIntensity,
                                                        minEmission, maxEmission, 1f / pulseSpeed));
                yield return StartCoroutine(LerpEffect(maxLightIntensity, minLightIntensity,
                                                        maxEmission, minEmission, 1f / pulseSpeed));
            }
        }

        private IEnumerator LerpEffect(float fromLight, float toLight,
                                        float fromEmission, float toEmission, float duration)
        {
            float elapsed = 0f;
            Color currentColor = nuclearLight != null ? nuclearLight.color : colorFull;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (nuclearLight != null)
                    nuclearLight.intensity = Mathf.Lerp(fromLight, toLight, t);

                if (_nucleoMat != null)
                {
                    float emissionValue = Mathf.Lerp(fromEmission, toEmission, t);
                    _nucleoMat.SetColor(EmissionColor, currentColor * emissionValue);
                }

                yield return null;
            }
        }
    }
}