Shader "Dicebound/TacticalRange"
{
    Properties
    {
        _Color("Semantic ink",Color)=(.03,.7,.95,1)
        _FillAlpha("Fill opacity",Range(0,1))=.44
        _BorderPixels("Border pixels",Float)=2.6
        _LinePixels("Path pixels",Float)=3.2
        _Shape("Cell or path",Float)=0
        _PathOutline("Dark path edge",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent"}
        Pass
        {
            Name "DepthTestedRangeAfterGrade"
            Tags {"LightMode"="TacticalRangeOverlay"}
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D_X_FLOAT(_TacticalRangeSceneDepth);
            SAMPLER(sampler_TacticalRangeSceneDepth);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FillAlpha,_BorderPixels,_LinePixels,_Shape,_PathOutline;
            CBUFFER_END
            float4 _TacticalRangeProtectedRects[32];
            int _TacticalRangeProtectedCount;
            struct Attributes {float3 positionOS:POSITION;float3 directionOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings Vert(Attributes input)
            {
                Varyings output;float3 world=TransformObjectToWorld(input.positionOS);output.positionCS=TransformWorldToHClip(world);output.uv=input.uv;
                if(_Shape>.5)
                {
                    float4 next=TransformWorldToHClip(world+TransformObjectToWorldDir(input.directionOS));
                    float2 direction=(next.xy/next.w-output.positionCS.xy/output.positionCS.w)*_ScaledScreenParams.xy;
                    float lengthSquared=max(dot(direction,direction),.00001);float2 normal=float2(-direction.y,direction.x)*rsqrt(lengthSquared);
                    output.positionCS.xy+=normal*input.uv.x*_LinePixels*2/_ScaledScreenParams.xy*output.positionCS.w;
                }
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 screenUV=GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth=SAMPLE_TEXTURE2D_X(_TacticalRangeSceneDepth,sampler_TacticalRangeSceneDepth,screenUV).r;
                // Final graded colour has no compatible MSAA depth attachment. Test the real
                // scene depth explicitly; walls, alpha-cutout actors and props still occlude ink.
                #if UNITY_REVERSED_Z
                    clip(input.positionCS.z-sceneDepth+.000005);
                #else
                    clip(sceneDepth-input.positionCS.z+.000005);
                #endif
                for(int i=0;i<_TacticalRangeProtectedCount;i++)
                {
                    float4 r=_TacticalRangeProtectedRects[i];
                    if(screenUV.x>=r.x&&screenUV.y>=r.y&&screenUV.x<=r.z&&screenUV.y<=r.w)discard;
                }
                float alpha;
                if(_Shape>.5)alpha=1-smoothstep(.5-fwidth(input.uv.x),.5,abs(input.uv.x));
                else
                {
                    float2 edge=min(input.uv,1-input.uv)/max(fwidth(input.uv),.00001);
                    float border=1-smoothstep(max(2,_BorderPixels)-.5,max(2,_BorderPixels)+.5,min(edge.x,edge.y));
                    alpha=lerp(_FillAlpha,1,border);
                }
                float3 ink=_Color.rgb;
                if(_Shape>.5&&_PathOutline>.5)ink=lerp(ink,float3(.035,.045,.045),smoothstep(.28,.39,abs(input.uv.x)));
                return half4(ink,_Color.a*alpha);
            }
            ENDHLSL
        }
    }
}
