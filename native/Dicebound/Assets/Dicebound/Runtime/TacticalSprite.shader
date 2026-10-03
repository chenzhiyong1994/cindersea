Shader "Dicebound/TacticalSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Pixel sheet",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _ImpactColor("Contact flash color",Color)=(1,0.97,0.91,1)
        _ImpactMix("Contact flash amount",Range(0,1))=0
        _Cutoff("Transparent edge",Range(0,0.5))=0.035
        _LightInfluence("Environment influence",Range(0,1))=0.24
        [Toggle] _ZWrite("Write depth",Float)=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest+20" "CanUseSpriteAtlas"="True"}
        Pass
        {
            Name "PixelCharacter"
            Tags {"LightMode"="UniversalForward"}
            Cull Off
            ZWrite [_ZWrite]
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ImpactColor;
                half _ImpactMix;
                half _Cutoff;
                half _LightInfluence;
                half _ZWrite;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 positionWS:TEXCOORD1;half4 color:COLOR;half fog:TEXCOORD2;};
            Varyings Vert(Attributes input)
            {
                Varyings output;VertexPositionInputs positions=GetVertexPositionInputs(input.positionOS);
                output.positionCS=positions.positionCS;output.positionWS=positions.positionWS;output.uv=input.uv;output.color=input.color*_Color;output.fog=ComputeFogFactor(positions.positionCS.z);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 pixel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*input.color;clip(pixel.a-_Cutoff);
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 ambient=SampleSH(half3(0,1,0));half attenuation=lerp(0.70h,1.0h,sun.shadowAttenuation);
                half3 context=saturate(ambient*0.72h+sun.color*(0.38h+0.24h*saturate(sun.direction.y))*attenuation+0.24h);
                pixel.rgb*=lerp(half3(1,1,1),context,_LightInfluence);
                // Mix after environment shading so even a dark costume reads the
                // contact flash. Keep source alpha and silhouette unchanged.
                pixel.rgb=lerp(pixel.rgb,_ImpactColor.rgb,saturate(_ImpactMix));
                pixel.rgb=MixFog(pixel.rgb,input.fog);return pixel;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Blend Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ImpactColor;
                half _ImpactMix;
                half _Cutoff;
                half _LightInfluence;
                half _ZWrite;
            CBUFFER_END
            float3 _LightDirection;
            float3 _LightPosition;
            struct ShadowAttributes
            {
                float3 positionOS:POSITION;
                float2 uv:TEXCOORD0;
                half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct ShadowVaryings
            {
                float4 positionCS:SV_POSITION;
                float2 uv:TEXCOORD0;
                half alpha:TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                float3 positionWS=TransformObjectToWorld(input.positionOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS=normalize(_LightPosition-positionWS);
                #else
                    float3 lightDirectionWS=_LightDirection;
                #endif
                // Alpha-run meshes have no NORMAL stream. Their actual billboard
                // plane is local XY; face its bias normal towards the casting light.
                // The current camera-aligned transform and negative X mirror apply
                // unchanged, so this casts the visible pose rather than a fake decal.
                float3 normalWS=TransformObjectToWorldNormal(float3(0,0,-1));
                normalWS*=dot(normalWS,lightDirectionWS)<0?-1:1;
                output.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,lightDirectionWS)));
                output.uv=input.uv;
                output.alpha=input.color.a*_Color.a;
                return output;
            }
            half4 ShadowFrag(ShadowVaryings input):SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a*input.alpha;
                clip(alpha-_Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
