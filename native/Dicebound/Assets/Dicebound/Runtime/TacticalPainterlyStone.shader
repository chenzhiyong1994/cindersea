Shader "Dicebound/PainterlyStone"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted stone or timber",2D)="white"{}
        [MainColor] _BaseColor("Pigment",Color)=(.85,.83,.75,1)
        _Smoothness("Matte wax",Range(0,1))=.16
        _Relief("Surface relief",Range(0,.2))=.035
        _Moss("Damp mineral and moss",Range(0,1))=.18
        _Wear("Weathered veins",Range(0,1))=.35
        _SunlitGain("Painted sunlit plane brightness",Range(.5,2.5))=1
        _ShadowDebug("Realtime shadow diagnostic",Float)=0
        _Cull("Culling",Float)=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness,_Relief,_Moss,_Wear,_Cull,_ShadowDebug,_SunlitGain;
        CBUFFER_END
        struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
        struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;};
        Varyings Vert(Attributes input)
        {
            Varyings output;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS);
            output.positionCS=p.positionCS;output.positionWS=p.positionWS;output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.uv=TRANSFORM_TEX(input.uv,_BaseMap);output.color=input.color;output.fog=ComputeFogFactor(p.positionCS.z);return output;
        }
        float Hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float Noise(float2 p)
        {
            float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
        }
        float Relief(float2 uv)
        {
            return Noise(uv*9.3)*.60+Noise(uv*23.1)*.25+Noise(uv*3.1)*.15;
        }
        ENDHLSL
        Pass
        {
            Name "PaintedMass"
            Tags {"LightMode"="UniversalForward"}
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            half4 Frag(Varyings input):SV_Target
            {
                half3 n=normalize(input.normalWS);
                float2 uv=input.uv;
                // Restrained metre-scale relief follows the actual carved face, never a
                // specular noise texture. The silhouette, bevels and joints are geometry.
                half3 paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                half3 paintX=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv+float2(.004,0)).rgb;
                half3 paintY=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv+float2(0,.004)).rgb;
                float relief=Relief(uv);
                float hx=dot(paintX-paint,half3(.22,.68,.10))*.84+(Relief(uv+float2(.004,0))-relief)*.16;
                float hy=dot(paintY-paint,half3(.22,.68,.10))*.84+(Relief(uv+float2(0,.004))-relief)*.16;
                float3 dpdx=ddx(input.positionWS),dpdy=ddy(input.positionWS);
                float2 duvdx=ddx(uv),duvdy=ddy(uv);
                float3 tangent=SafeNormalize(dpdx*duvdy.y-dpdy*duvdx.y);
                float3 bitangent=SafeNormalize(dpdy*duvdx.x-dpdx*duvdy.x);
                n=normalize(n-tangent*hx*_Relief*14-bitangent*hy*_Relief*14);
                half3 pigment=paint*_BaseColor.rgb;
                // Mesh colours encode the recessed foot of each individual stone. Meshes
                // without this channel remain neutral rather than becoming black.
                half3 localPigment=lerp(half3(1,1,1),input.color.rgb,input.color.a);
                pigment*=localPigment*(.955h+relief*.09h);
                // Broad weathered facets replace mathematical fine scratches which read
                // as regular engraved lines at the actual game-camera distance.
                float wornFacet=smoothstep(.35,.69,Noise(uv*2.4));
                pigment*=1-wornFacet*_Wear*.075;
                float damp=(1-saturate(n.y))*smoothstep(.36,.72,Noise(input.positionWS.xz*.91+input.positionWS.y*.17));
                pigment=lerp(pigment,pigment*half3(.58,.70,.58),damp*_Moss*.42);
                float4 shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                Light sun=GetMainLight(shadowCoord);
                if(_ShadowDebug>.5)return half4(lerp(half3(.04,.16,.62),half3(1,1,1),sun.shadowAttenuation),1);
                half visibility=sun.shadowAttenuation;
                half ao=1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor screenAo=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                ao=screenAo.indirectAmbientOcclusion;
                #endif
                half3 ambient=(SampleSH(n)*.50h+half3(.033,.052,.080))*ao;
                half diffuse=saturate(dot(n,sun.direction)*.94h+.02h);
                // Gain applies to actual sun visibility, never to UI/post exposure or the
                // cool ambient shade. Lit limestone can approach ivory without washing
                // the realtime roof, wall, foliage and character silhouettes away.
                half sunlitGain=lerp(.92h,_SunlitGain,visibility*visibility);
                half3 illumination=ambient+sun.color*sun.distanceAttenuation*diffuse*visibility*.66h*sunlitGain;
                // Cool reflected sky remains visible in the shade, while direct sun paints
                // wide light planes. No polished PBR specular flattens the pale pavement.
                illumination+=half3(.020,.041,.070)*(1-visibility);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count=GetAdditionalLightsCount();
                for(uint i=0;i<count;i++){
                    Light local=GetAdditionalLight(i,input.positionWS,half4(1,1,1,1));
                    illumination+=local.color*local.distanceAttenuation*local.shadowAttenuation*saturate(dot(n,local.direction))*.65h;
                }
                #endif
                half3 color=pigment*illumination;
                half3 halfway=SafeNormalize(sun.direction+SafeNormalize(GetWorldSpaceViewDir(input.positionWS)));
                color+=sun.color*pow(saturate(dot(n,halfway)),lerp(12,32,_Smoothness))*.014h*visibility;
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            float4 ShadowVert(Attributes input):SV_POSITION
            {
                float3 p=TransformObjectToWorld(input.positionOS),n=TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-p);
                #else
                float3 direction=_LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p,n,direction)));
            }
            half4 DepthFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(Attributes input):SV_POSITION{return TransformObjectToHClip(input.positionOS);}
            half4 DepthFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormals"}
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFrag(Varyings input):SV_Target
            {
                half3 normal=normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(normal);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
