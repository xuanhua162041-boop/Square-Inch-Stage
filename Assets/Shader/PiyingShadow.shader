Shader "Custom/URP_PiyingLit_Final"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Leather Texture)]
        _NoiseTex ("Leather Noise (Grayscale)", 2D) = "gray" {} 
        _NoisePower ("Noise Intensity", Range(0, 1)) = 0.3
        
        [Header(Translucency)]
        [HDR]_TransColor ("Translucency Color (Glow)", Color) = (1, 0.5, 0.1, 1) // 默认橙色
        _TransPower ("Translucency Power", Range(0, 10)) = 2.0
        
        [Header(System)]
        _AmbientStrength ("Min Brightness", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off 

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 务必开启多光源变体，否则找不到 Spotlight
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 positionWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _TransColor;
                float _TransPower;
                float _NoisePower;
                float _AmbientStrength;
                float4 _NoiseTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color; 
                return output;
            }

            // 核心计算函数：计算单个光源对皮影的影响
            half3 CalculateLight(Light light, half3 albedo, float3 normalWS, float3 viewDirWS)
            {
                // 1. 光照衰减 (Spotlight 的光圈范围)
                float attenuation = light.distanceAttenuation * light.shadowAttenuation;
                
                // 2. 双面受光逻辑 (不关心法线方向，只关心有没有光)
                // 只要光照强度(attenuation)存在，就视为照亮
                // 这里加一点点 NdotL 避免完全没有立体感，但用 abs 保证背面也亮
                float NdotL = abs(dot(normalWS, light.direction));
                // 为了皮影更像“透光”，我们弱化 NdotL，主要靠 attenuation
                float lightStrength = lerp(1.0, NdotL, 0.3) * attenuation;

                // 3. 漫反射 Diffuse
                half3 diffuse = albedo * light.color * lightStrength;

                // 4. 透射 Translucency (光越强，透出来的橙色越亮)
                half3 transmission = _TransColor.rgb * light.color * lightStrength * _TransPower * albedo;

                // 混合两者
                return diffuse + transmission;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- 材质采样 ---
                half4 mainCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                mainCol *= input.color;
                if(mainCol.a < 0.05) discard;

                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv).r;
                half3 albedo = mainCol.rgb * lerp(1.0, noise, _NoisePower);
                
                // 准备数据
                float3 normalWS = float3(0,0,-1); // 假定法线
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 finalColor = half3(0,0,0);

                // --- 1. 计算主光 (Directional Light) ---
                Light mainLight = GetMainLight();
                finalColor += CalculateLight(mainLight, albedo, normalWS, viewDirWS);

                // --- 2. 计算额外光源 (Spotlight 在这里！) ---
                // 这一步是关键！循环场景里所有的额外灯光
                int lightCount = GetAdditionalLightsCount();
                for(int i = 0; i < lightCount; ++i)
                {
                    // 获取第 i 个光源的信息 (位置、颜色、衰减)
                    Light light = GetAdditionalLight(i, input.positionWS);
                    finalColor += CalculateLight(light, albedo, normalWS, viewDirWS);
                }

                // --- 3. 环境光保底 ---
                finalColor += albedo * _AmbientStrength;

                return half4(finalColor, mainCol.a);
            }
            ENDHLSL
        }
    }
}