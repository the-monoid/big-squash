// Steading/PainterlyLit
// Stylized URP lit shader inspired by Valheim's painterly look.
//   - Banded N·L lighting (shadow / midtone / highlight)
//   - Per-band tint colors (cool shadow, warm highlight)
//   - Rim light (golden-hour fresnel)
//   - Vertex color tint
//   - URP fog
// SRP-batcher compatible: all uniforms in UnityPerMaterial, all passes share layout.

Shader "Steading/PainterlyLit"
{
    Properties
    {
        [Header(Surface)]
        _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.4
        _Metallic("Metallic", Range(0,1)) = 0
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)

        [Header(Painterly Bands)]
        _ShadowBand("Shadow Band Threshold", Range(0,1)) = 0.42
        _MidtoneBand("Midtone Band Threshold", Range(0,1)) = 0.72
        _BandSoftness("Band Softness", Range(0.001, 0.5)) = 0.045
        _ShadowTint("Shadow Tint",   Color) = (0.36, 0.42, 0.55, 1)
        _MidtoneTint("Midtone Tint", Color) = (0.85, 0.82, 0.78, 1)
        _HighlightTint("Highlight Tint", Color) = (1.05, 1.00, 0.92, 1)
        _AmbientStrength("Ambient Strength", Range(0,2)) = 0.55

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1.10, 0.85, 0.50, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.6
        _RimIntensity("Rim Intensity", Range(0, 2)) = 0.55

        [Header(Variation)]
        _VertexColorInfluence("Vertex Color Influence", Range(0,1)) = 1.0

        // SrcBlend/DstBlend/ZWrite hidden so future transparent variant doesn't fight URP.
        [HideInInspector] _Surface("__surface", Float) = 0
        [HideInInspector] _AlphaClip("__alphaclip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
            "IgnoreProjector"="True"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float  _Smoothness;
            float  _Metallic;
            float4 _EmissionColor;
            float  _ShadowBand;
            float  _MidtoneBand;
            float  _BandSoftness;
            float4 _ShadowTint;
            float4 _MidtoneTint;
            float4 _HighlightTint;
            float  _AmbientStrength;
            float4 _RimColor;
            float  _RimPower;
            float  _RimIntensity;
            float  _VertexColorInfluence;
            float  _Surface;
            float  _AlphaClip;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        // ---------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   PainterlyVert
            #pragma fragment PainterlyFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 vcolor     : TEXCOORD3;
                float  fogCoord   : TEXCOORD4;
                float4 shadowCoord: TEXCOORD5;
            };

            Varyings PainterlyVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vn = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = vp.positionCS;
                OUT.positionWS  = vp.positionWS;
                OUT.normalWS    = vn.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.vcolor      = IN.color;
                OUT.fogCoord    = ComputeFogFactor(vp.positionCS.z);
                OUT.shadowCoord = GetShadowCoord(vp);
                return OUT;
            }

            // Three smooth bands. Returns the per-band tint multiplied by lit factor.
            half3 BandLighting(half lit)
            {
                half shadowMask    = 1.0h - smoothstep(_ShadowBand  - _BandSoftness, _ShadowBand  + _BandSoftness, lit);
                half highlightMask = smoothstep(_MidtoneBand - _BandSoftness, _MidtoneBand + _BandSoftness, lit);
                half midtoneMask   = saturate(1.0h - shadowMask - highlightMask);

                return  _ShadowTint.rgb    * shadowMask +
                        _MidtoneTint.rgb   * midtoneMask +
                        _HighlightTint.rgb * highlightMask;
            }

            half4 PainterlyFrag(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = baseSample.rgb * _BaseColor.rgb;

                // Vertex color tints for procedural variation. influence==0 => no tint.
                half3 vertTint = lerp(half3(1,1,1), IN.vcolor.rgb, _VertexColorInfluence);
                albedo *= vertTint;

                half3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight(IN.shadowCoord);
                half  NdotL  = saturate(dot(normalWS, mainLight.direction));
                half  shadow = mainLight.shadowAttenuation;
                half  lit    = NdotL * shadow;

                half3 banded = BandLighting(lit) * mainLight.color;

                // Stylized ambient via spherical harmonics, leaning toward _ShadowTint
                // when surfaces face down (faking sky/ground occlusion).
                half3 sh        = SampleSH(normalWS);
                half3 ambient   = lerp(_ShadowTint.rgb, sh, 0.6h) * _AmbientStrength;

                half3 color = albedo * (banded + ambient);

                // Additional lights (torches, fires) — banded too so torches feel painted.
                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light addLight = GetAdditionalLight(lightIndex, IN.positionWS, half4(1,1,1,1));
                        half  addNdotL = saturate(dot(normalWS, addLight.direction));
                        half  addLit   = addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation;
                        color += albedo * BandLighting(addLit) * addLight.color;
                    LIGHT_LOOP_END
                #endif

                // Rim — subtle golden-hour edge glow, modulated by the main light direction
                // so back-lit surfaces glow but front-lit surfaces don't get false rim.
                half3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half  rim = 1.0h - saturate(dot(viewDir, normalWS));
                rim = pow(rim, _RimPower);
                half  rimFacing = saturate(dot(-mainLight.direction, normalWS) * 0.5h + 0.5h);
                color += rim * rimFacing * _RimIntensity * _RimColor.rgb * mainLight.color;

                color += _EmissionColor.rgb;
                color = MixFog(color, IN.fogCoord);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Shadow caster ----------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVary { float4 positionCS : SV_POSITION; };

            float4 GetShadowPositionHClip(ShadowAttr input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            ShadowVary ShadowVert(ShadowAttr IN)
            {
                ShadowVary OUT;
                OUT.positionCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 ShadowFrag(ShadowVary IN) : SV_TARGET { return 0; }
            ENDHLSL
        }

        // Depth-only -------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            struct DepthAttr { float4 positionOS : POSITION; };
            struct DepthVary { float4 positionCS : SV_POSITION; };

            DepthVary DepthVert(DepthAttr IN)
            {
                DepthVary OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DepthVary IN) : SV_TARGET { return 0; }
            ENDHLSL
        }

        // Depth-Normals (used by SSAO) -------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            struct DNAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct DNVary { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVary DepthNormalsVert(DNAttr IN)
            {
                DNVary OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DepthNormalsFrag(DNVary IN) : SV_TARGET
            {
                float3 n = normalize(IN.normalWS);
                return half4(n * 0.5h + 0.5h, 0.0h);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
