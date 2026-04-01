Shader "Custom/WallReceiveShadow"
{
    Properties
    {
        _ShadowColor    ("Shadow Color",     Color)        = (0, 0, 0, 1)
        _ShadowStrength ("Shadow Strength",  Range(0, 1))  = 0.85
        _NoiseTex       ("Shadow Texture",   2D)           = "white" {}
        _NoiseStrength  ("Texture Strength", Range(0, 1))  = 0.3
        _WaveStrength   ("Wave Strength",    Range(0, 1))  = 0.0
        _WaveSpeed      ("Wave Speed",       Range(0, 5))  = 1.0
        _WaveScale      ("Wave Scale",       Range(0, 10)) = 3.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _ShadowColor;
                half   _ShadowStrength;
                float4 _NoiseTex_ST;
                half   _NoiseStrength;
                half   _WaveStrength;
                half   _WaveSpeed;
                half   _WaveScale;
            CBUFFER_END

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv         = TRANSFORM_TEX(v.uv, _NoiseTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float shadow = 1.0;
                uint lightCount = GetAdditionalLightsCount();
                for (uint idx = 0; idx < lightCount; idx++)
                {
                    #if defined(_ADDITIONAL_LIGHT_SHADOWS)
                        Light light = GetAdditionalLight(idx, i.positionWS, half4(1,1,1,1));
                    #else
                        Light light = GetAdditionalLight(idx, i.positionWS);
                    #endif
                    shadow = min(shadow, light.shadowAttenuation);
                }

                float shadowMask = (1.0 - shadow) * _ShadowStrength;
                clip(shadowMask - 0.01);

                // 波动：用时间偏移 UV，采样 noise 得到扰动量
                float2 waveUV = i.uv * _WaveScale + _Time.y * _WaveSpeed * float2(0.1, 0.07);
                half wave = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, waveUV).r * 2.0 - 1.0;
                float2 distortedUV = i.uv + wave * _WaveStrength * 0.05;

                // 纹理调制阴影透明度，产生斑驳效果
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, distortedUV).r;
                float finalAlpha = shadowMask * lerp(1.0, noise, _NoiseStrength);
                clip(finalAlpha - 0.01);

                return half4(_ShadowColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
