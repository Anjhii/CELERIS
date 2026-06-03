Shader "Custom/PortalEffect"
{
    Properties
    {
        _ColorInner("Color Interior", Color) = (1, 0.9, 0.2, 1)
        _ColorOuter("Color Exterior", Color) = (1, 0.5, 0.0, 1)
        _RotationSpeed("Velocidad de rotación", Float) = 1.0
        _RingCount("Cantidad de anillos", Float) = 5.0
        _RingWidth("Grosor de anillos", Range(0.01, 0.5)) = 0.1
        _NoiseScale("Escala del ruido", Float) = 8.0
        _PulseSpeed("Velocidad de pulso", Float) = 2.0
        _EmissionStrength("Intensidad emisión", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite On

        Pass
        {
            Name "PortalEffect"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorInner;
                float4 _ColorOuter;
                float  _RotationSpeed;
                float  _RingCount;
                float  _RingWidth;
                float  _NoiseScale;
                float  _PulseSpeed;
                float  _EmissionStrength;
            CBUFFER_END

            // Función de ruido simple
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(hash(i + float2(0,0)), hash(i + float2(1,0)), u.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x),
                    u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Centrar UVs en (0,0)
                float2 uv = IN.uv * 2.0 - 1.0;

                // Distancia al centro
                float dist = length(uv);

                // Recortar fuera del círculo
                clip(1.0 - dist);

                // Ángulo y rotación en el tiempo
                float angle = atan2(uv.y, uv.x);
                float time   = _Time.y;

                // Coordenadas polares rotadas
                float rotatedAngle = angle + time * _RotationSpeed;
                float2 polar = float2(rotatedAngle / (3.14159 * 2.0), dist);

                // Ruido para distorsión orgánica
                float n = noise(polar * _NoiseScale + time * 0.5);
                float distorted = dist + (n - 0.5) * 0.08;

                // Anillos concéntricos
                float rings = frac(distorted * _RingCount + time * _PulseSpeed * -0.3);
                float ringMask = smoothstep(0.0, _RingWidth, rings) *
                                 smoothstep(_RingWidth * 2.0, _RingWidth, rings);

                // Espiral giratoria
                float spiral = frac(dist * _RingCount + rotatedAngle / (3.14159 * 2.0) * 2.0
                                    + time * _PulseSpeed * 0.2);
                float spiralMask = smoothstep(0.0, _RingWidth, spiral) *
                                   smoothstep(_RingWidth * 2.0, _RingWidth, spiral);

                // Combinar anillos y espiral
                float pattern = saturate(ringMask + spiralMask * 0.6);

                // Color: interior más brillante, exterior más oscuro
                float4 col = lerp(_ColorOuter, _ColorInner, 1.0 - dist);

                // Borde suave del círculo
                float edge = smoothstep(1.0, 0.85, dist);

                // Núcleo brillante en el centro
                float core = smoothstep(0.3, 0.0, dist) * 0.8;

                float4 finalColor = col * (pattern + core) * edge * _EmissionStrength;
                finalColor.a = 1.0;

                return finalColor;
            }
            ENDHLSL
        }
    }
}