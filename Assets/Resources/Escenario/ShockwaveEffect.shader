Shader "Custom/ShockwaveEffect"
{
    Properties
    {
        _Color("Color", Color) = (0.6, 0.9, 1.0, 1.0)
        _InnerRadius("Radio Interior", Range(0, 1)) = 0.7
        _Thickness("Grosor", Range(0, 0.5)) = 0.1
        _Opacity("Opacidad", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ShockwaveEffect"
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
                float4 _Color;
                float  _InnerRadius;
                float  _Thickness;
                float  _Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.uv * 2.0 - 1.0;
                float  dist = length(uv);

                // Anillo: visible entre InnerRadius y InnerRadius + Thickness
                float outerRadius = _InnerRadius + _Thickness;
                float ring = smoothstep(_InnerRadius - 0.02, _InnerRadius, dist) *
                             smoothstep(outerRadius + 0.02, outerRadius, dist);

                float alpha = ring * _Opacity;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}