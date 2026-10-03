Shader "Dicebound/PaintedEnvironment"
{
    Properties
    {
        [MainTexture] _Beauty("Untouched sRGB artwork",2D)="white"{}
        _ProxyDepth("Linear RGB eye depth; A occupancy",2D)="black"{}
        _ForegroundMask("Original geometry foreground mask",2D)="black"{}
        _BeautyUvRect("Original artwork UV crop",Vector)=(0,0,1,1)
        _CapturePosition("Capture position",Vector)=(0,0,0,0)
        _CaptureRight("Capture right",Vector)=(1,0,0,0)
        _CaptureUp("Capture up",Vector)=(0,1,0,0)
        _CaptureForward("Capture forward",Vector)=(0,0,1,0)
        _CaptureSize("World width height near far",Vector)=(30,20,.1,100)
        _Layer("Base=0 Foreground=1",Float)=0
        _DynamicShadow("Only live actors and objects cast",Range(0,1))=.35
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+20"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_Beauty);SAMPLER(sampler_Beauty);
        TEXTURE2D(_ProxyDepth);SAMPLER(sampler_ProxyDepth);
        TEXTURE2D(_ForegroundMask);SAMPLER(sampler_ForegroundMask);
        CBUFFER_START(UnityPerMaterial)
        float4 _BeautyUvRect,_CapturePosition,_CaptureRight,_CaptureUp,_CaptureForward,_CaptureSize;
        float _Layer,_DynamicShadow;
        CBUFFER_END
        struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
        struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
        struct PixelOutput {float4 color:SV_Target;float depth:SV_Depth;};
        Varyings Vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output;}
        float3 ReconstructStaticWorld(float2 uv)
        {
            float4 packed=SAMPLE_TEXTURE2D_LOD(_ProxyDepth,sampler_ProxyDepth,uv,0);
            float eyeDepth=lerp(_CaptureSize.z,_CaptureSize.w,dot(packed.rgb,float3(1,1.0/255,1.0/65025)));
            eyeDepth=packed.a>.5?eyeDepth:_CaptureSize.w-.2;
            return _CapturePosition.xyz+_CaptureRight.xyz*((uv.x-.5)*_CaptureSize.x)+_CaptureUp.xyz*((uv.y-.5)*_CaptureSize.y)+_CaptureForward.xyz*eyeDepth;
        }
        float StaticDeviceDepth(float3 positionWS)
        {
            float4 positionCS=TransformWorldToHClip(positionWS);
            float deviceDepth=positionCS.z/positionCS.w;
            // Unity expands this macro to a float literal; shader preprocessor
            // integer expressions cannot compare it. HLSL folds this constant.
            deviceDepth=UNITY_NEAR_CLIP_VALUE<0?deviceDepth*.5+.5:deviceDepth;
            return saturate(deviceDepth);
        }
        void ClipLayer(float2 uv)
        {
            float foreground=SAMPLE_TEXTURE2D_LOD(_ForegroundMask,sampler_ForegroundMask,uv,0).r;
            // Both layers sample the original PNG directly; the mask splits it on the
            // GPU. No destructive image slicing or rectangular foreground boards.
            clip(_Layer>.5?foreground-.5:.5-foreground);
            clip(SAMPLE_TEXTURE2D_LOD(_Beauty,sampler_Beauty,_BeautyUvRect.xy+uv*_BeautyUvRect.zw,0).a-.01);
        }
        PixelOutput BeautyFragment(Varyings input)
        {
            ClipLayer(input.uv);
            float3 positionWS=ReconstructStaticWorld(input.uv);
            float4 shadowCoord=TransformWorldToShadowCoord(positionWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                shadowCoord=ComputeScreenPos(TransformWorldToHClip(positionWS));
            #endif
            Light light=GetMainLight(shadowCoord);
            PixelOutput output;
            output.color=SAMPLE_TEXTURE2D(_Beauty,sampler_Beauty,_BeautyUvRect.xy+input.uv*_BeautyUvRect.zw);
            output.color.rgb*=lerp(1,light.shadowAttenuation,_DynamicShadow);
            output.color.a=1;
            output.depth=StaticDeviceDepth(positionWS);
            return output;
        }
        PixelOutput DepthFragment(Varyings input)
        {
            ClipLayer(input.uv);PixelOutput output;output.color=0;output.depth=StaticDeviceDepth(ReconstructStaticWorld(input.uv));return output;
        }
        PixelOutput NormalsFragment(Varyings input)
        {
            ClipLayer(input.uv);float3 positionWS=ReconstructStaticWorld(input.uv);
            float3 normalWS=SafeNormalize(cross(ddy(positionWS),ddx(positionWS)));
            normalWS*=dot(normalWS,_WorldSpaceCameraPos-positionWS)<0?-1:1;
            PixelOutput output;
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS=PackNormalOctQuadEncode(normalWS);
                output.color=float4(PackFloat2To888(saturate(octNormalWS*.5+.5)),0);
            #else
                output.color=float4(normalWS,0);
            #endif
            output.depth=StaticDeviceDepth(positionWS);return output;
        }
        ENDHLSL
        Pass
        {
            Name "UnlitArtworkAndLiveShadows" Tags {"LightMode"="UniversalForwardOnly"}
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment BeautyFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "SameProxyDepth" Tags {"LightMode"="DepthOnly"}
            Cull Off ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFragment
            ENDHLSL
        }
        Pass
        {
            Name "SameProxyDepthNormals" Tags {"LightMode"="DepthNormalsOnly"}
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
