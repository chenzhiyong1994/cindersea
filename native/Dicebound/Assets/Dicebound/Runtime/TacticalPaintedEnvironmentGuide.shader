Shader "Dicebound/PaintedGuideDepth"
{
    Properties { _GuideMode("Export mode",Float)=0 _GuideNearFar("Near far",Vector)=(.1,100,0,0) _GuideBoardBounds("Board bounds",Vector)=(-7.5,7.5,-6.25,6.25) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Guide" Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _GuideNearFar,_GuideBoardBounds;
            float _GuideMode;
            CBUFFER_END
            struct Input {float4 positionOS:POSITION;};
            struct Output {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;};
            Output Vert(Input input){Output output;output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);return output;}
            float4 Frag(Output input):SV_TARGET
            {
                if(_GuideMode>.5){float foreground=step(.15,input.positionWS.y)*max(step(_GuideBoardBounds.y+1,input.positionWS.x),step(input.positionWS.z,_GuideBoardBounds.z-1));return float4(foreground.xxx,1);}
                float eyeDepth=-TransformWorldToView(input.positionWS).z;
                float normalizedDepth=clamp((eyeDepth-_GuideNearFar.x)/(_GuideNearFar.y-_GuideNearFar.x),0,.999999);
                float3 encoded=frac(normalizedDepth*float3(1,255,65025));encoded-=encoded.yzz*float3(1.0/255,1.0/255,0);
                return float4(encoded,1);
            }
            ENDHLSL
        }
    }
}
