Shader "Dicebound/Backdrop"
{
    Properties {_BaseMap("Scene",2D)="white"{} _BaseColor("Atmosphere",Color)=(.76,.84,.97,1)}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
            half4 frag(Varyings input):SV_Target
            {
                half3 scene=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                float lower=smoothstep(.12,.58,input.uv.y);float edge=smoothstep(.72,.22,abs(input.uv.x-.5));
                half3 color=scene*_BaseColor.rgb*lerp(.28,.86,lower)*lerp(.72,1,edge);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
