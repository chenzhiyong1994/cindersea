Shader "Dicebound/BattleParticle"
{
    Properties
    {
        _MainTex ("Particle texture", 2D) = "white" {}
        _NoiseTex ("Dissolve texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _UvRect ("Atlas rectangle", Vector) = (1,1,0,0)
        _Dissolve ("Dissolve", Range(-0.2,1.2)) = -0.2
        _Luminance ("Use luminance mask", Range(0,1)) = 0
        _Gain ("Light gain", Range(0,4)) = 1
        _OpacityGain ("Texture opacity gain", Range(0,4)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "BattleParticle"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _UvRect;
                float _Dissolve;
                float _Luminance;
                float _Gain;
                float _OpacityGain;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                output.color=input.color;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 atlasUv=input.uv*_UvRect.xy+_UvRect.zw;
                half4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,atlasUv);
                half noise=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,input.uv*1.7).r;
                half erode=smoothstep(_Dissolve-.13,_Dissolve+.13,noise);
                half alpha=saturate(texel.a*lerp(1,texel.r,_Luminance)*_OpacityGain)*_Tint.a*input.color.a*erode;
                // Luminance-masked textures carry shape in alpha, not in both alpha and RGB.
                half3 color=lerp(texel.rgb,half3(1,1,1),_Luminance)*_Tint.rgb*input.color.rgb*_Gain;
                color=MixFog(color,input.fog);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
