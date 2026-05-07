// Steading/PainterlySky
// Procedural skybox: zenith → horizon gradient + warm horizon haze + sun disc.
// Designed to be assigned via RenderSettings.skybox.
// Knobs feed into RenderPipelineSetup so a scripted "golden hour" / "overcast"
// preset can swap the same material asset.

Shader "Steading/PainterlySky"
{
    Properties
    {
        [Header(Sky Gradient)]
        _ZenithColor   ("Zenith",   Color) = (0.18, 0.30, 0.46, 1)
        _HorizonColor  ("Horizon",  Color) = (0.78, 0.66, 0.52, 1)
        _GroundColor   ("Ground",   Color) = (0.10, 0.09, 0.08, 1)
        _HorizonPower  ("Horizon Falloff", Range(0.5, 12)) = 4.0
        _GroundPower   ("Ground Falloff",  Range(0.5, 12)) = 6.0

        [Header(Sun)]
        _SunDir        ("Sun Direction (xyz, w=intensity)", Vector) = (0.4, 0.6, 0.7, 1)
        _SunColor      ("Sun Color", Color) = (1.2, 1.05, 0.7, 1)
        _SunSize       ("Sun Disc Size", Range(0.999, 1.0)) = 0.9985
        _SunHaloSize   ("Sun Halo Size", Range(0.5, 1.0)) = 0.92
        _SunHaloIntensity("Sun Halo Intensity", Range(0,2)) = 0.55

        [Header(Atmosphere)]
        _AtmoScatter   ("Horizon Haze", Range(0,1)) = 0.55
        _CloudTint     ("Cloud Tint",   Color) = (1.05, 1.0, 0.95, 1)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _HorizonColor;
                float4 _GroundColor;
                float  _HorizonPower;
                float  _GroundPower;
                float4 _SunDir;
                float4 _SunColor;
                float  _SunSize;
                float  _SunHaloSize;
                float  _SunHaloIntensity;
                float  _AtmoScatter;
                float4 _CloudTint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Skybox geometry is a unit sphere/cube around the camera; treat OS.xyz as direction.
                OUT.dir = IN.positionOS.xyz;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half3 SkyGradient(float3 dir)
            {
                float y = saturate(dir.y);              // 0 horizon, 1 zenith
                float yNeg = saturate(-dir.y);          // 0 horizon, 1 nadir

                half3 toZenith   = lerp(_HorizonColor.rgb, _ZenithColor.rgb,  pow(y,    _HorizonPower));
                half3 belowGround = lerp(_HorizonColor.rgb, _GroundColor.rgb, pow(yNeg, _GroundPower));

                return dir.y >= 0.0 ? toZenith : belowGround;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);

                // Base sky gradient
                half3 sky = SkyGradient(dir);

                // Horizon haze: pull color toward horizon tint near horizon line
                half horizonBand = exp(-pow(abs(dir.y) * 6.0, 2.0));
                sky = lerp(sky, _HorizonColor.rgb * _CloudTint.rgb, horizonBand * _AtmoScatter * 0.5h);

                // Sun
                float3 sunDir = normalize(_SunDir.xyz);
                float  sunDot = saturate(dot(dir, sunDir));
                half   disc = smoothstep(_SunSize, 1.0h, sunDot);
                half   halo = pow(saturate((sunDot - _SunHaloSize) / max(1.0 - _SunHaloSize, 1e-4)), 2.4);

                sky += disc * _SunColor.rgb * _SunDir.w * 4.0h;
                sky += halo * _SunHaloIntensity * _SunColor.rgb;

                return half4(sky, 1.0h);
            }
            ENDHLSL
        }
    }
}
