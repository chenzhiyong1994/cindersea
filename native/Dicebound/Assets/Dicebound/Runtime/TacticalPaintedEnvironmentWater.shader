Shader "Dicebound/PaintedEnvironmentWater"
{
    Properties
    {
        _Beauty("Original water colour",2D)="white"{}
        _ProxyDepth("Linear true geometry depth",2D)="black"{}
        _ForegroundMask("Reserved",2D)="black"{}
        _BeautyUvRect("Artwork UV crop",Vector)=(0,0,1,1)
        _CapturePosition("Capture position",Vector)=(0,0,0,0)
        _CaptureRight("Capture right",Vector)=(1,0,0,0)
        _CaptureUp("Capture up",Vector)=(0,1,0,0)
        _CaptureForward("Capture forward",Vector)=(0,0,1,0)
        _CaptureSize("World width height near far",Vector)=(30,20,.1,100)
        _Layer("Reserved",Float)=0
        _DynamicShadow("Reserved",Float)=0
        _Motion("Reduced motion",Float)=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "SubtleLiveWater" Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite Off ZTest LEqual Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Beauty);SAMPLER(sampler_Beauty);
            TEXTURE2D(_ProxyDepth);SAMPLER(sampler_ProxyDepth);
            CBUFFER_START(UnityPerMaterial)
            float4 _BeautyUvRect,_CapturePosition,_CaptureRight,_CaptureUp,_CaptureForward,_CaptureSize;
            float _Layer,_DynamicShadow,_Motion;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            struct PixelOutput {float4 color:SV_Target;float depth:SV_Depth;};
            Varyings Vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output;}
            PixelOutput Frag(Varyings input)
            {
                float4 packed=SAMPLE_TEXTURE2D_LOD(_ProxyDepth,sampler_ProxyDepth,input.uv,0);clip(packed.a-.5);
                float eyeDepth=lerp(_CaptureSize.z,_CaptureSize.w,dot(packed.rgb,float3(1,1.0/255,1.0/65025)));
                float3 positionWS=_CapturePosition.xyz+_CaptureRight.xyz*((input.uv.x-.5)*_CaptureSize.x)+_CaptureUp.xyz*((input.uv.y-.5)*_CaptureSize.y)+_CaptureForward.xyz*eyeDepth;
                // These are the real two canal planes in the exported bridge, never
                // the height-zero picking planes over rule-defined gaps.
                float waterCoverage=1-smoothstep(.006,.023,min(abs(positionWS.y+.78),abs(positionWS.y+.81)));clip(waterCoverage-.01);
                float seconds=_Time.y*_Motion;
                float longWave=sin(positionWS.x*2.3+positionWS.z*.74-seconds*.72);
                float crossing=sin(positionWS.z*2.7-positionWS.x*.33+seconds*.36);
                float sparkle=pow(saturate(longWave),18)*(.30+.70*saturate(crossing));
                float3 original=SAMPLE_TEXTURE2D(_Beauty,sampler_Beauty,_BeautyUvRect.xy+input.uv*_BeautyUvRect.zw).rgb;
                PixelOutput output;output.color=float4(lerp(original,sqrt(max(original,0)),.45)*sparkle*waterCoverage*.065,0);
                float4 positionCS=TransformWorldToHClip(positionWS);float deviceDepth=positionCS.z/positionCS.w;
                output.depth=saturate(UNITY_NEAR_CLIP_VALUE<0?deviceDepth*.5+.5:deviceDepth);
                return output;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
