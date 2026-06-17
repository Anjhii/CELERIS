using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Celeris.Player
{

    public class LightPulse : MonoBehaviour
    {
        public Light pointLight;
        private float baseIntensity = 3f;
        private float pulseIntensity = 6f;
        private float pulseSpeed = 8f;
        private bool isPulsing = false;

        void Start()
        {
            if (pointLight == null)
                pointLight = GetComponent<Light>();
        }

        void Update()
        {
            if (isPulsing)
            {
                pointLight.intensity = Mathf.Lerp(
                    pointLight.intensity, 
                    baseIntensity, 
                    Time.deltaTime * pulseSpeed
                );

                if (Mathf.Abs(pointLight.intensity - baseIntensity) < 0.05f)
                    isPulsing = false;
            }
        }

        public void Pulse()
        {
            if (pointLight == null) return;
            pointLight.intensity = pulseIntensity;
            isPulsing = true;
        }
    }

}
