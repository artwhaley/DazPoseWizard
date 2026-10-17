Shader "DazPose/Performer/Glowing Wireframe"
{
 Properties {
  [HDR] _WireColor("Wire emission",Color)=(3,0.08,5,1)
  _WireWidth("Wire width in pixels",Range(0.1,4))=0.8
  _Fill("Projection fill",Range(0,0.2))=0.025
 }
 SubShader {
  Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass {
   Name "ForwardOnly"
   Tags { "LightMode"="ForwardOnly" }
   Cull Back ZWrite On ZTest LEqual
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma geometry Geometry
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
   #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _WireColor;
    float _WireWidth;
    float _Fill;
   CBUFFER_END
   struct Attributes { float3 positionOS:POSITION; };
   struct Vertex { float4 positionCS:SV_POSITION; };
   struct Fragment { float4 positionCS:SV_POSITION; float3 barycentric:TEXCOORD0; };
   Vertex Vert(Attributes input) { Vertex output; output.positionCS=TransformObjectToHClip(input.positionOS); return output; }
   [maxvertexcount(3)]
   void Geometry(triangle Vertex input[3],inout TriangleStream<Fragment> stream) {
    Fragment output;
    output.positionCS=input[0].positionCS; output.barycentric=float3(1,0,0); stream.Append(output);
    output.positionCS=input[1].positionCS; output.barycentric=float3(0,1,0); stream.Append(output);
    output.positionCS=input[2].positionCS; output.barycentric=float3(0,0,1); stream.Append(output);
    stream.RestartStrip();
   }
   float4 Frag(Fragment input):SV_Target {
    float3 width=max(fwidth(input.barycentric),0.000001);
    float3 edges=smoothstep(0,width*_WireWidth,input.barycentric);
    float wire=1-min(edges.x,min(edges.y,edges.z));
    return float4(_WireColor.rgb*max(wire,_Fill)*GetCurrentExposureMultiplier(),1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}