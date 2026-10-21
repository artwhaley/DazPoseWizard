// Full-screen fade to black for the player camera. HDRP forward transparent pass, no depth test,
// drawn after the other transparents so dissolve particles are covered too. The vertex shader ignores the
// object matrices and stretches the tiny quad over the whole screen.
Shader "Hidden/DazPose/PlayerScreenFade"
{
    Properties
    {
        _FadeColor ("Fade Color", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+900" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            float4 _FadeColor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.uv * 2.0 - 1.0, 0.5, 1.0);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return _FadeColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
