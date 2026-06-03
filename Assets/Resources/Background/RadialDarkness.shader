Shader "Custom/RadialDarkness"
{
    Properties
    {
        _Center("Center (Screen UV)", Vector) = (0.5, 0.5, 0, 0)
        _Radius("Radius", Float) = 0.3
        _Softness("Softness", Float) = 0.2
        _Color("Darkness Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RadialDarkness"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Center;
            float  _Radius;
            float  _Softness;
            float4 _Color;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 center = _Center.xy;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 diff = (uv - center) * float2(aspect, 1.0);
                float dist = length(diff);
                float darkness = smoothstep(_Radius, _Radius + _Softness, dist);
                return half4(_Color.rgb, darkness);
            }
            ENDHLSL
        }
    }
}