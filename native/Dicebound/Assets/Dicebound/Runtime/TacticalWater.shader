Shader "Dicebound/TacticalWater"
{
    Properties
    {
        _DeepColor("Deep canal",Color)=(0.025,0.29,0.31,1)
        _ShallowColor("Shallow jade water",Color)=(0.105,0.48,0.43,1)
        _FoamColor("Small white foam",Color)=(0.82,0.96,0.95,1)
        _Flow("Flow direction and speed",Vector)=(0.16,0.09,0.12,0)
        _Bounds("Plane width and depth",Vector)=(1,1,0,0)
        _RippleOrigins("Two outlet world XZ centres",Vector)=(0,0,0,0)
        _Motion("Reduced motion",Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5"}
        Pass
        {
            Name "PaintedFlowingWater"
            Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor,_ShallowColor,_FoamColor;
                float4 _Flow,_Bounds,_RippleOrigins;
                half _Motion;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float2 uv:TEXCOORD0;float2 dimensions:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;half fog:TEXCOORD2;float2 dimensions:TEXCOORD3;};
            float Hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Surface(float2 p,float time)
            {
                float broad=Noise(p*.47+_Flow.xy*time);
                float fine=Noise(p*1.31+float2(-.10,.14)*time);
                return broad*.69+fine*.24+Noise(p*5.1+float2(.12,-.13)*time)*.07;
            }
            float SmallFoamBubbles(float2 p,float time)
            {
                float2 q=(p-_Flow.xy*time*.37)*14,cell=floor(q),f=frac(q);
                float bubbles=0;
                // Irregular centimetre-scale rings break up whitewater at the real shore
                // and outlets. They do not form metre-wide soft cream islands.
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++){
                    float2 neighbor=float2(x,y),key=cell+neighbor;
                    float2 bubbleCenter=float2(Hash(key+3.71),Hash(key+19.43))*.72+.14;
                    float radius=.10+Hash(key+41.6)*.12;
                    float distance=abs(length(f-neighbor-bubbleCenter)-radius);
                    float aa=max(fwidth(distance),.018);
                    float ring=1-smoothstep(.014,.024+aa,distance);
                    bubbles=max(bubbles,ring*smoothstep(.33,.66,Hash(key+77.2)));
                }
                return bubbles;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS);
                output.positionCS=p.positionCS;output.positionWS=p.positionWS;output.uv=input.uv;output.dimensions=max(input.dimensions,float2(.1,.1));output.fog=ComputeFogFactor(p.positionCS.z);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float time=_Time.y*_Flow.z*_Motion;
                float2 p=input.positionWS.xz;
                float wave=Surface(p,time);
                float shore=min(min(input.uv.x,1-input.uv.x)*input.dimensions.x,min(input.uv.y,1-input.uv.y)*input.dimensions.y);
                float shallow=saturate(.13+wave*.61+exp(-shore*3.1)*.36);
                half3 base=lerp(_DeepColor.rgb,_ShallowColor.rgb,shallow);
                float2 delta=float2(Surface(p+float2(.055,0),time)-wave,Surface(p+float2(0,.055),time)-wave);
                half3 normal=normalize(half3(-delta.x*7,1,-delta.y*7));
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normal,sun.direction));
                half shadow=lerp(.31h,1.0h,sun.shadowAttenuation);
                base*=SampleSH(normal)*.48h+sun.color*diffuse*.65h*shadow+half3(.085,.125,.135);
                // Short, discontinuous contour glints accompany transparent turquoise
                // planes; white foam mixes at small coverage rather than adding a glaze.
                float2 drift=p-_Flow.xy*time;
                float warp=Noise(drift*.81)*1.9;
                float crest=Noise(float2(drift.x*.46+warp,drift.y*1.70-warp*.39));
                float flowLine=(1-smoothstep(.007,.024,abs(crest-.69)))*smoothstep(.50,.76,Noise(drift*3.2));
                float shoreFoam=exp(-shore*25)*smoothstep(.36,.70,Noise(p*6.2-time*.06));
                float distance=min(length(p-_RippleOrigins.xy),length(p-_RippleOrigins.zw));
                float ringPhase=abs(sin(distance*13+Noise(p*4)*2.1-time*3.6));
                float ring=(1-smoothstep(.02,.10,ringPhase))*exp(-distance*2.8)*smoothstep(.30,.69,Noise(p*9));
                float impact=exp(-distance*distance*22);
                float bubble=SmallFoamBubbles(p,time);
                float foam=saturate(flowLine*.18+shoreFoam*.32+ring*.29+bubble*(impact*.80+exp(-shore*9)*.33));
                half3 view=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfway=SafeNormalize(sun.direction+view);
                half highlight=pow(saturate(dot(normal,halfway)),64)*.09h*shadow*smoothstep(.58,.82,Noise(drift*7.3));
                half fresnel=pow(1-saturate(dot(normal,view)),4)*.10h;
                half3 color=lerp(base,_FoamColor.rgb,foam)+_FoamColor.rgb*highlight+half3(.07,.16,.17)*fresnel;
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float3 positionOS:POSITION;};
            float4 VertDepth(A input):SV_POSITION{return TransformObjectToHClip(input.positionOS);}
            half4 FragDepth():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormals"}
            ZWrite On Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VertNormal
            #pragma fragment FragNormal
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float3 positionOS:POSITION;};
            float4 VertNormal(A input):SV_POSITION{return TransformObjectToHClip(input.positionOS);}
            half4 FragNormal():SV_Target
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(float3(0,1,0));return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                return half4(0,1,0,0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
