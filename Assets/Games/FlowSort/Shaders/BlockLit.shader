// Block shader for the wall. Original code.
//
// Each block quad samples one tile out of the block atlas — a scaled-down Basic GUI Bundle button
// face. The sprite already carries the whole look: top highlight, inner shadow, hard black
// outline. So this shader deliberately does NOT bevel or gloss procedurally the way the earlier
// flat-tint version had to; doing both would fight the art and muddy the outline.
//
// What is left is an alpha cutout for the rounded corners, a wrapped diffuse term so the wall
// still responds to the key light without crushing to black, and vertex colour as a per-block
// shade multiplier. One texture, one material, so all chunks batch.
//
// Cull is Off deliberately: the wall is only ever viewed head-on, so back-face culling saves
// nothing measurable, and dropping it removes any winding-order risk from the procedurally
// generated chunk meshes.

Shader "FlowSort/BlockLit"
{
    Properties
    {
        _MainTex      ("Block Atlas",     2D)         = "white" {}
        _Tint         ("Tint",            Color)      = (1,1,1,1)
        _Cutoff       ("Alpha Cutoff",    Range(0,1)) = 0.35
        _AmbientBoost ("Ambient Boost",   Range(0,2)) = 0.55
        _Saturate     ("Saturation Lift", Range(0,1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float  _Cutoff;
                float  _AmbientBoost;
                float  _Saturate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color      = IN.color;
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 face = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(face.a - _Cutoff);

                float3 albedo = face.rgb * IN.color.rgb * _Tint.rgb;

                // Small saturation lift: the sprites are already vivid, and pushing them a touch
                // further keeps them from washing out once ambient is added back below.
                float luma = dot(albedo, float3(0.299, 0.587, 0.114));
                albedo = saturate(lerp(albedo, albedo + (albedo - luma), _Saturate));

                float3 n = normalize(IN.normalWS);
                Light mainLight = GetMainLight();

                // Wrapped diffuse: keeps the unlit side readable instead of crushing to black,
                // which suits a flat-facing wall of saturated colours.
                float ndotl = saturate(dot(n, mainLight.direction)) * 0.5 + 0.5;

                float3 lit = albedo * mainLight.color * ndotl;
                lit += albedo * SampleSH(n) * _AmbientBoost;

                return half4(lit, 1.0);
            }
            ENDHLSL
        }

        // Present so the wall still writes depth if URP's depth texture is ever enabled.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float  _Cutoff;
                float  _AmbientBoost;
                float  _Saturate;
            CBUFFER_END

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
