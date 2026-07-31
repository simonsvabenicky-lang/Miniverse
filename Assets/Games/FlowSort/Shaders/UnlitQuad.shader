// Unlit textured quad tinted by vertex colour, with material-driven blend mode. Original code.
//
// One shader covers both the ball streaks (no texture, additive — the vertex-alpha gradient
// becomes the motion trail) and particle debris (textured, alpha-blended), which keeps the
// shader count down and avoids a second near-identical file.

Shader "FlowSort/UnlitQuad"
{
    Properties
    {
        _MainTex  ("Texture", 2D) = "white" {}
        _Color    ("Tint", Color) = (1,1,1,1)

        // 1 = One, 5 = SrcAlpha, 10 = OneMinusSrcAlpha (UnityEngine.Rendering.BlendMode)
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend Dst", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_BlendSrc] [_BlendDst]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _BlendSrc;
                float  _BlendDst;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color      = IN.color;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 c = tex * IN.color * _Color;

                // Premultiply so the additive path (SrcAlpha One) fades correctly with alpha.
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
