Shader "Custom/PuppetLayer"
{
    Properties
    {
        [Header(Base Textures)]
        [MainTexture] _BaseMap("Base Color (Alpha)", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 5)) = 2.0
        
        [Header(Base Visibility)]
        _BaseOpacity("Base Texture Visibility", Range(0, 1)) = 0.4
        
        [Header(Transmission and Color)]
        _TransIntensity("Light Glow Intensity", Range(0, 10)) = 4.0
        _ColorVibrance("Skin Saturation Boost", Range(1, 4)) = 1.5
        _InkThreshold("Ink Cutoff", Range(0, 1)) = 0.2
        
        [Header(Surface and Specular)]
        _Smoothness("Surface Smoothness", Range(0, 1)) = 0.8
        _RimIntensity("Edge Rim Highlight", Range(0, 10)) = 2.0
        _RimPower("Rim Power", Range(1, 10)) = 5.0
        
        [Header(Blur Background)]
        _BlurAmount("Blur Amount", Range(0, 0.02)) = 0.006
        _BlurSamples("Blur Samples", Int) = 12
        
        [Header(System)]
        _Cutoff("Alpha Cutoff (0=off)", Range(0, 1)) = 0.0
    }
    
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode" = "UniversalForward"}
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _NormalStrength;
                half _BaseOpacity;
                half _TransIntensity;
                half _ColorVibrance;
                half _InkThreshold;
                half _Smoothness;
                half _RimIntensity;
                half _RimPower;
                half _BlurAmount;
                int _BlurSamples;
                half _Cutoff;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexPos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vertexNorm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexPos.positionCS;
                output.positionWS = vertexPos.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = vertexNorm.normalWS;
                output.tangentWS = float4(vertexNorm.tangentWS, input.tangentOS.w);
                output.screenPos = ComputeScreenPos(vertexPos.positionCS);
                return output;
            }
            
            float3 GetPuppetLight(Light light, float3 nWS, float3 vDir, float3 albedo, float isSkin)
            {
                float atten = light.distanceAttenuation * light.shadowAttenuation;
                float3 vibrantColor = pow(albedo, 1.0 / _ColorVibrance);
                float3 transmission = vibrantColor * _TransIntensity * light.color * atten * isSkin;
                
                float3 h = normalize(light.direction + vDir);
                float spec = pow(saturate(dot(nWS, h)), _Smoothness * 128.0) * _Smoothness;
                float3 specular = spec * light.color * atten;
                
                return transmission + specular;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 基础纹理
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                if (albedo.a < _Cutoff) discard; // 可选切出
                
                // 法线
                half3 nTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                nTS.xy *= _NormalStrength;
                nTS = normalize(nTS);
                float sgn = input.tangentWS.w;
                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * sgn;
                float3x3 tbn = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 nWS = normalize(mul(nTS, tbn));
                
                float3 vDir = normalize(GetCameraPositionWS() - input.positionWS);
                
                // 墨线/皮肤区分
                half lum = dot(albedo.rgb, half3(0.299, 0.587, 0.114));
                half isSkin = smoothstep(_InkThreshold, _InkThreshold + 0.1, lum);
                
                // 基础颜色（暗部可见度）
                half3 finalRGB = albedo.rgb * _BaseOpacity;
                
                // 主光
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                finalRGB += GetPuppetLight(mainLight, nWS, vDir, albedo.rgb, isSkin);
                
                // 额外光源
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    finalRGB += GetPuppetLight(addLight, nWS, vDir, albedo.rgb, isSkin);
                }
                
                // 边缘光
                half rim = pow(1.0 - saturate(dot(nWS, vDir)), _RimPower) * _RimIntensity * isSkin;
                finalRGB += rim * albedo.rgb;
                
                // 模糊背景
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half3 blurColor = 0;
                for (int i = 0; i < _BlurSamples; i++)
                {
                    half angle = i * (6.28318 / _BlurSamples);
                    float2 offset = float2(cos(angle), sin(angle)) * _BlurAmount;
                    blurColor += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + offset).rgb;
                }
                blurColor /= _BlurSamples;
                
                // 混合：半透明区域显示更多模糊背景
                half alpha = albedo.a;
                half3 color = lerp(blurColor, finalRGB, alpha);
                
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}