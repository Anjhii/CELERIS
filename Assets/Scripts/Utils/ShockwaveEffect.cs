using System.Collections;
using UnityEngine;

namespace Celeris.Core
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

            // Instanciar material para no afectar otras ondas
            var renderer = _shockwavePlane.GetComponent<Renderer>();
            renderer.material = new Material(shockwaveMaterial);

            // Quitar collider
            Destroy(_shockwavePlane.GetComponent<Collider>());

            _shockwavePlane.SetActive(false);
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

            var mat     = _shockwavePlane.GetComponent<Renderer>().material;
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
                mat.SetFloat(OpacityID, opacity);

                yield return null;
            }

            _shockwavePlane.SetActive(false);
        }
    }
}