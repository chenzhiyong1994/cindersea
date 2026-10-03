Shader "Dicebound/PaperPortrait"
{
    Properties
    {
        _BaseMap("Portrait artwork",2D)="white"{}
        _UvRect("Crop",Vector)=(1,1,0,0)
        _Motion("Motion amount",Float)=1
        _Clock("Animation clock",Float)=0
        _Seed("Portrait phase",Float)=0
        _Enemy("Enemy",Float)=0
        _Gesture("Action",Float)=0
        _Hit("Recoil",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _UvRect;
            float _Motion,_Clock,_Seed,_Enemy,_Gesture,_Hit;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
            float region(float2 p,float2 center,float2 radius){float2 d=(p-center)/radius;return exp(-dot(d,d)*2);}
            half4 frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                float edge=smoothstep(0,.12,uv.x)*smoothstep(0,.12,1-uv.x)*smoothstep(0,.12,uv.y)*smoothstep(0,.12,1-uv.y);
                float t=_Clock+_Seed;
                float torso=region(uv,float2(.50,.43),float2(.27,.23));
                float head=region(uv,float2(.51,.76),float2(.17,.18));
                float breath=sin(t*1.38)*_Motion;
                // Local texture displacement keeps the card edge and printed frame entirely fixed.
                uv.x+=(uv.x-.5)*torso*breath*.004*edge;
                uv.y+=torso*breath*.0014*edge;
                uv.x+=(sin(t*.39)*.0016+_Gesture*.0018)*head*edge*_Motion;
                uv.y+=sin(t*.49)*head*.0008*edge*_Motion;
                float cloth=region(uv,float2(.65,.24),float2(.24,.23));
                uv.x+=sin(t*1.1+uv.y*7)*cloth*.0015*edge*_Motion;
                half4 paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv*_UvRect.xy+_UvRect.zw);
                paint.rgb=lerp(paint.rgb,half3(.68,.27,.14),_Hit*.15);
                return paint;
            }
            ENDHLSL
        }
    }
}
