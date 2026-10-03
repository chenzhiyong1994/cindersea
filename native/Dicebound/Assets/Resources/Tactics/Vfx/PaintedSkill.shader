Shader "Dicebound/PaintedSkill"
{
    Properties
    {
        _MainTex("Original painted RGBA atlas",2D)="white"{}
        _NoiseTex("Licensed erosion detail",2D)="white"{}
        _AtlasRect("Isolated component UV rectangle",Vector)=(0,0,1,1)
        _Tint("Pigment tint and opacity",Color)=(1,1,1,1)
        _Emission("Bright pigment emission",Float)=1
        _Reveal("Written portion",Range(0,1))=1
        _Tail("Erased wake",Range(0,1))=0
        _SweepAxis("0 horizontal 1 vertical writing",Range(0,1))=0
        _Dissolve("Dissolve",Range(0,1))=0
        _Phase("Authored animation time",Float)=0
        _Flow("Local fluid distortion",Vector)=(0,0,0,0)
        _Facet("Crystal facet lighting",Range(0,1))=0
        _GroundFade("Slash ground height and soft contact distance",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+60"}
        Pass
        {
            Name "Painted translucent spell"
            Tags {"LightMode"="UniversalForward"}
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _AtlasRect,_Tint,_Flow,_GroundFade;
                float _Emission,_Reveal,_Tail,_SweepAxis,_Dissolve,_Phase,_Facet;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half3 normalWS:TEXCOORD1;float heightWS:TEXCOORD2;};
            Varyings Vert(Attributes input)
            {
                Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;output.color=input.color;output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.heightWS=TransformObjectToWorld(input.positionOS.xyz).y;return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                // The phase is supplied by the finite presentation, so held keyframes are stable.
                float inside=16*uv.x*(1-uv.x)*uv.y*(1-uv.y);
                uv+=_Flow.xy*sin(float2(uv.y*17,uv.x*13)+_Phase*float2(8,-6))*inside;
                uv=saturate(uv);
                half4 pigment=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,_AtlasRect.xy+uv*_AtlasRect.zw);
                half grain=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,input.uv*2.7).r;
                float along=lerp(input.uv.x,input.uv.y,_SweepAxis);
                float feather=max(fwidth(along)*1.5,.012);
                half reveal=1-smoothstep(_Reveal-feather,_Reveal+feather,along);
                half tail=_Tail<.001?1:smoothstep(_Tail-feather,_Tail+feather,along);
                half dissolve=smoothstep(_Dissolve-.18,_Dissolve+.10,grain+.22);
                half alpha=saturate(pigment.a*_Tint.a*input.color.a*reveal*tail*dissolve);
                // Low sweeping blades dissolve before intersecting the paving. Keep
                // ordinary depth testing so walls and actors still occlude the spell.
                half contact=smoothstep(0,max(.001,_GroundFade.y),input.heightWS-_GroundFade.x);
                alpha*=lerp(1.0h,contact,step(.001,_GroundFade.y));
                clip(alpha-.002h);
                half3 rgb=pigment.rgb*_Tint.rgb*input.color.rgb;
                half light=.48h+.52h*saturate(dot(normalize(input.normalWS),GetMainLight().direction));
                rgb*=lerp(1.0h,light,_Facet);
                // Only the luminous painted cores emit. Dark red ink and teal water retain volume.
                half core=smoothstep(.74h,.98h,dot(pigment.rgb,half3(.2126,.7152,.0722)));
                rgb+=rgb*core*_Emission*.32h;
                return half4(rgb*alpha,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
