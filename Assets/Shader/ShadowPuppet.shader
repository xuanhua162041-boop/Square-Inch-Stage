Shader "Custom/ShadowPuppet"
{
    Properties
    {
        [Header(Base Textures)]
        [MainTexture] _MainTex("Base Color (Alpha)", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 5)) = 2.0
        
        [Header(Base Visibility)]
        _BaseOpacity("Base Texture Visibility", Range(0, 1)) = 0.5 // û��ʱ�Ļ�������

        [Header(Transmission and Color)]
        _TransIntensity("Light Glow Intensity", Range(0, 10)) = 4.0
        _ColorVibrance("Skin Saturation Boost", Range(1, 4)) = 1.5
        _InkThreshold("Ink Cutoff", Range(0, 1)) = 0.2

        [Header(Surface and Specular)]
        _Smoothness("Surface Smoothness", Range(0, 1)) = 0.8
        _RimIntensity("Edge Rim Highlight", Range(0, 10)) = 2.0

        [Header(System)]
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline"}
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // �������Դ֧��
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float3 normalWS : NORMAL;
                float4 tangentWS : TANGENT;
            };

            sampler2D _MainTex, _NormalMap;
            float _NormalStrength, _BaseOpacity, _TransIntensity, _ColorVibrance, _InkThreshold, _Smoothness, _RimIntensity, _Cutoff;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                return output;
            }

            // ����ֲ��ƹ⺯��
            float3 GetPuppetLight(Light light, float3 nWS, float3 vDir, float3 albedo, float isSkin)
            {
                float atten = light.distanceAttenuation * light.shadowAttenuation;
                float3 lightDir = light.direction;
                
                // 1. ͸����ǿ��ֻ�б����յ���Ƥ�ϲ��ֻ��ø�����������
                float3 vibrantColor = pow(albedo, 1.0 / _ColorVibrance);
                float3 transmission = vibrantColor * _TransIntensity * light.color * atten * isSkin;

                // 2. ����߹⣺���ַ���ϸ��
                float3 h = normalize(lightDir + vDir);
                float spec = pow(saturate(dot(nWS, h)), _Smoothness * 128.0) * _Smoothness;
                float3 specular = spec * light.color * atten;

                return transmission + specular;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. ��������
                half4 albedo = tex2D(_MainTex, input.uv);
                clip(albedo.a - _Cutoff);

                // 2. ������ɫ����֤������û��ʱ�ɼ�
                float3 finalRGB = albedo.rgb * _BaseOpacity;

                // 3. �������ӽǼ���
                half3 nTS = UnpackNormal(tex2D(_NormalMap, input.uv));
                nTS.xy *= _NormalStrength;
                nTS = normalize(nTS);
                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 tbn = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 nWS = normalize(mul(nTS, tbn));
                float3 vDir = normalize(GetCameraPositionWS() - input.positionWS);

                // 4. īˮ�ж�
                float lum = dot(albedo.rgb, half3(0.299, 0.587, 0.114));
                float isSkin = smoothstep(_InkThreshold, _InkThreshold + 0.1, lum);

                // 5. �ۼӵƹ⹱��
                // ����Դ
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                finalRGB += GetPuppetLight(mainLight, nWS, vDir, albedo.rgb, isSkin);

                // ���ӹ�Դ (���Դ��)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < pixelLightCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    finalRGB += GetPuppetLight(addLight, nWS, vDir, albedo.rgb, isSkin);
                }

                // 6. ��Ե������
                float rim = pow(1.0 - saturate(dot(nWS, vDir)), 5.0) * _RimIntensity * isSkin;
                finalRGB += rim * albedo.rgb;

                return half4(finalRGB, albedo.a);
            }
            ENDHLSL
        }
    }
}