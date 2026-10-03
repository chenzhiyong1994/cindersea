Shader "Dicebound/PaperStroke"
{
    Properties
    {
        _MainTex ("Licensed ink mask",2D)="white"{}
        _NoiseTex ("Licensed paper erosion",2D)="white"{}
        _Tint ("Printed pigment",Color)=(.3,.2,.1,1)
        _Mode ("0 engraving 1 wash 2 dot",Float)=0
        _Reveal ("Written portion",Range(0,1))=1
        _Tail ("Travelling tail",Range(0,1))=0
        _Dissolve ("Drying edge",Range(0,1))=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "Paper pigment"
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Mode,_Reveal,_Tail,_Dissolve;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings Vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                half grain=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv*float2(2.7,1.3)).r;
                float width=max(fwidth(uv.y),.045);
                half edge=smoothstep(0,width,uv.y)*smoothstep(0,width,1-uv.y);
                half alpha=edge*(.78+.22*grain);
                if(_Mode>.5&&_Mode<1.5)
                {
                    half4 ink=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
                    alpha=ink.a*ink.r;
                }
                else if(_Mode>1.5)
                {
                    float radius=length(uv-.5)*2;
                    alpha=1-smoothstep(.65,.98,radius);
                }
                float head=1-smoothstep(_Reveal-.015,_Reveal+.015,uv.x);
                float tail=_Tail<.001?1:smoothstep(_Tail-.025,_Tail+.025,uv.x);
                float erode=smoothstep(_Dissolve-.15,_Dissolve+.12,grain+.18);
                return half4(_Tint.rgb,alpha*head*tail*erode*_Tint.a);
            }
            ENDHLSL
        }
    }
}
