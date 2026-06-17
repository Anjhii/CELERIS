using System.Collections;
using UnityEngine;

namespace Celeris.Utils
{
    public class ShockwaveEffect : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Material shockwaveMaterial;

        [Header("Configuración")]
        [SerializeField] private float duration    = 0.4f;
        [SerializeField] private float maxScale    = 3f;
        [SerializeField] private float startScale  = 0.1f;

        private GameObject _shockwavePlane;
        private Material   _instanceMaterial;   // instancia propia — destruir en OnDestroy
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");

        private void Start()
        {
            // Crear plano circular proceduralmente
            _shockwavePlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _shockwavePlane.name = "ShockwavePlane";
            _shockwavePlane.transform.SetParent(transform);
            _shockwavePlane.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            _shockwavePlane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _shockwavePlane.transform.localScale    = Vector3.one * startScale;

            // Instanciar material para no afectar otras ondas.
            // Guardamos referencia para destruirlo en OnDestroy (evita GPU leak).
            var renderer = _shockwavePlane.GetComponent<Renderer>();
            _instanceMaterial  = new Material(shockwaveMaterial);
            renderer.material  = _instanceMaterial;

            // Quitar collider
            Destroy(_shockwavePlane.GetComponent<Collider>());

            _shockwavePlane.SetActive(false);
        }

        private void OnDestroy()
        {
            // Destruir la instancia de material para liberar memoria GPU.
            // Unity no la destruye automáticamente al destruir el GameObject.
            if (_instanceMaterial != null)
                Destroy(_instanceMaterial);
        }

        public void Trigger()
        {
            if (_shockwavePlane == null) return;
            StopAllCoroutines();
            StartCoroutine(PlayShockwave());
        }

        private IEnumerator PlayShockwave()
        {
            _shockwavePlane.SetActive(true);

            // Usar _instanceMaterial directamente — no llamar .material (crearía otra instancia).
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);

                // Expandir escala
                float scale = Mathf.Lerp(startScale, maxScale, t);
                _shockwavePlane.transform.localScale = Vector3.one * scale;

                // Fade out
                float opacity = Mathf.Lerp(1f, 0f, t);
                _instanceMaterial.SetFloat(OpacityID, opacity);

                yield return null;
            }

            _shockwavePlane.SetActive(false);
        }
    }
}