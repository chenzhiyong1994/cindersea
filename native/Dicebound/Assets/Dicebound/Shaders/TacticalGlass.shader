Shader "Dicebound/UI/TacticalGlass"
{
    Properties
    {
        [PerRendererData] _MainTex("Rounded silhouette",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _BodyColor("Body",Color)=(.035,.115,.135,1)
        _RimColor("Edge reflection",Color)=(.56,.76,.70,1)
        _ReflectionColor("Softbox reflection",Color)=(.64,.83,.78,1)
        _Treatment("0 glass 1 polish 2 pearl 3 lens",Float)=0
        _Transmission("Scene transmission",Range(0,1))=.4
        _Roughness("Reflection roughness",Range(.05,1))=.4
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"}
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "OpticalSurface"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex,_TacticalGlassScene,_TacticalGlassBackdrop;
            float4 _Color,_BodyColor,_RimColor,_ReflectionColor,_ClipRect;
            float4 _TacticalGlassViewport,_TacticalGlassScreen;
            float4 _TacticalGlassBackdropUV,_TacticalGlassBackdropColor,_TacticalGlassBackdropWash,_TacticalGlassBackdrop_TexelSize;
            float _Treatment,_Transmission,_Roughness,_TacticalGlassAvailable,_TacticalGlassUseBackdrop;
            float _UIMaskSoftnessX,_UIMaskSoftnessY;
            struct appdata
            {
                float4 vertex:POSITION;
                float4 color:COLOR;
                float2 uv:TEXCOORD0;
                float4 local:TEXCOORD1;
                float4 shape:TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex:SV_POSITION;
                float4 color:COLOR;
                float2 uv:TEXCOORD0;
                float4 local:TEXCOORD1;
                float4 shape:TEXCOORD2;
                float4 mask:TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;o.local=v.local;o.shape=v.shape;
                float2 pixelSize=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 clipRect=clamp(_ClipRect,-2e10,2e10);
                o.mask=float4(v.vertex.xy*2-clipRect.xy-clipRect.zw,.25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                return o;
            }
            float RoundedDistance(float2 p,float2 halfSize,float radius)
            {
                float2 q=abs(p)-(halfSize-radius);
                return min(max(q.x,q.y),0)+length(max(q,0))-radius;
            }
            float2 RoundedNormal(float2 p,float2 halfSize,float radius)
            {
                float2 q=abs(p)-(halfSize-radius);
                float2 outside=max(q,0);
                float2 n=dot(outside,outside)>.0001?normalize(outside):(q.x>q.y?float2(1,0):float2(0,1));
                return n*sign(p);
            }
            float3 Background(float2 uv)
            {
                if(_TacticalGlassUseBackdrop>.5)
                {
                    float2 crop=uv*_TacticalGlassBackdropUV.zw+_TacticalGlassBackdropUV.xy;
                    float2 delta=_TacticalGlassBackdrop_TexelSize.xy*7;
                    float3 color=tex2D(_TacticalGlassBackdrop,crop).rgb*.28;
                    color+=tex2D(_TacticalGlassBackdrop,crop+float2(delta.x,delta.y)).rgb*.18;
                    color+=tex2D(_TacticalGlassBackdrop,crop-float2(delta.x,delta.y)).rgb*.18;
                    color+=tex2D(_TacticalGlassBackdrop,crop+float2(delta.x,-delta.y)).rgb*.18;
                    color+=tex2D(_TacticalGlassBackdrop,crop+float2(-delta.x,delta.y)).rgb*.18;
                    return lerp(color*_TacticalGlassBackdropColor.rgb,_TacticalGlassBackdropWash.rgb,_TacticalGlassBackdropWash.a);
                }
                return tex2D(_TacticalGlassScene,saturate(uv)).rgb;
            }
            float4 frag(v2f i):SV_Target
            {
                float maskAlpha=1;
                #ifdef UNITY_UI_CLIP_RECT
                    float2 m=saturate((_ClipRect.zw-_ClipRect.xy-abs(i.mask.xy))*i.mask.zw);maskAlpha=m.x*m.y;
                #endif
                if(i.local.w<-.5)
                {
                    float4 shadow=float4(i.color.rgb,i.color.a*maskAlpha);
                    #ifdef UNITY_UI_ALPHACLIP
                        clip(shadow.a-.001);
                    #endif
                    return shadow;
                }
                float2 halfSize=max(i.shape.xy*.5,.001),p=i.local.xy;
                float distance=RoundedDistance(p,halfSize,i.shape.z);
                float aa=max(fwidth(distance),.55),coverage=saturate(.5-distance/aa);
                float inside=max(-distance,0);
                float2 uv=saturate(p/(halfSize*2)+.5);
                float2 normal=RoundedNormal(p,halfSize,i.shape.z);
                float illumination=saturate(dot(normal,normalize(float2(-.55,.83))));
                bool pearl=_Treatment>1.5&&_Treatment<2.5;
                float bevel=exp(-inside/(pearl?6.5:4.2)),rim=exp(-inside/1.05);
                float thickness=exp(-inside/5.2)*saturate(-normal.y);
                float broadWidth=lerp(.15,.32,_Roughness);
                // An oblique softbox reflection falls off in both axes instead of forming a full-width stripe.
                float2 reflection=float2((uv.x-.18)*.88,uv.y-.90+.30*(uv.x-.18));
                float broad=exp(-dot(reflection,reflection)/(broadWidth*broadWidth*2.8));
                float2 bounce=(uv-float2(.82,.18))*float2(1.6,2.1);
                float secondary=exp(-dot(bounce,bounce)/.22);
                float faceLight=pearl?lerp(.97,1.015,smoothstep(0,1,uv.y)):lerp(.86,1.10,smoothstep(0,1,uv.y));
                float3 color=_BodyColor.rgb*faceLight;
                if(_Treatment<.5||_Treatment>2.5)
                {
                    float2 screenUV=i.vertex.xy*_TacticalGlassScreen.xy;
                    #if UNITY_UV_STARTS_AT_TOP
                        screenUV.y=1-screenUV.y;
                    #endif
                    float2 viewportUV=(screenUV-_TacticalGlassViewport.xy)/max(_TacticalGlassViewport.zw,.001);
                    // A small edge offset suggests thickness while keeping transmitted scene details soft.
                    float2 opticalOffset=normal*bevel*float2(.0009,.0016);
                    float available=max(_TacticalGlassAvailable,_TacticalGlassUseBackdrop);
                    if(available>.5)
                    {
                        float3 scene=Background(saturate(viewportUV+opticalOffset));
                        float3 filtered=scene*float3(.53,.75,.72);
                        color=lerp(color,filtered,_Transmission);
                    }
                    color+=_ReflectionColor.rgb*(broad*(_Treatment>2.5?.073:.058)+secondary*.010);
                }
                else
                {
                    color+=_ReflectionColor.rgb*(broad*(pearl?.026:.052)+secondary*(pearl?.008:.010));
                }
                color*=1-thickness*(pearl?.075:.24);
                color+=_ReflectionColor.rgb*bevel*illumination*(pearl?.030:.040);
                // Fresnel-like edge weighting is restrained and directional, never an emissive outline.
                color=lerp(color,_RimColor.rgb,rim*(pearl?(.10+.23*illumination):(.16+.30*illumination)));
                float innerBevel=exp(-pow((inside-(pearl?3.0:2.3))/(pearl?1.9:1.1),2))*illumination;
                color+=_ReflectionColor.rgb*innerBevel*(pearl?.026:.035);
                float topLip=exp(-pow((inside-1.5)/.65,2))*pow(saturate(normal.y),3);
                color+=_ReflectionColor.rgb*topLip*(pearl?.045:.085);
                float alpha=coverage*i.color.a*maskAlpha;
                #ifdef UNITY_UI_ALPHACLIP
                    clip(alpha-.001);
                #endif
                return float4(color*i.color.rgb,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
