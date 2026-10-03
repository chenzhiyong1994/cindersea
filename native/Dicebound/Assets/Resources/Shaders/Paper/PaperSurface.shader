Shader "Dicebound/PaperSurface"
{
    Properties
    {
        _BaseMap("Painting",2D)="white"{}
        _BaseColor("Ink and paper",Color)=(1,1,1,1)
        _UvRect("Crop",Vector)=(1,1,0,0)
        _Effect("0 painting 1 mist 2 lamplight",Float)=0
        _Paper("Paper grain",Float)=0
        _Clock("Animation clock",Float)=0
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
            float4 _BaseColor,_UvRect;
            float _Effect,_Paper,_Clock;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
            float grain(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
            half4 frag(Varyings input):SV_Target
            {
                float2 p=input.uv-.5;
                if(_Effect>.5)
                {
                    float fade=exp(-dot(p*float2(2.3,2.8),p*float2(2.3,2.8))*2.7);
                    float edge=smoothstep(0,.16,input.uv.x)*smoothstep(0,.16,1-input.uv.x)*smoothstep(0,.18,input.uv.y)*smoothstep(0,.18,1-input.uv.y);
                    float curl=.70+.15*sin(input.uv.x*21+input.uv.y*9+_Clock*.10)+.10*sin(input.uv.x*37-input.uv.y*14-_Clock*.13);
                    if(_Effect>1.5)curl=.96+.04*sin(_Clock*2.7);
                    return half4(_BaseColor.rgb,_BaseColor.a*fade*edge*curl);
                }
                half4 sample=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv*_UvRect.xy+_UvRect.zw);
                float paper=1+(grain(floor(input.uv*1600))-.5)*.035*_Paper;
                return half4(sample.rgb*_BaseColor.rgb*paper,sample.a*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
