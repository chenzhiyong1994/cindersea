Shader "Dicebound/UI/RouteBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex("City artwork",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _BlurPixels("Blur radius in source pixels",Range(0,8))=3.5
        _DetailContrast("Detail contrast",Range(.7,1))=.88
        _Saturation("Saturation",Range(0,1.2))=.94
        _StencilComp("Stencil comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil operation",Float)=0
        _StencilWriteMask("Stencil write mask",Float)=255
        _StencilReadMask("Stencil read mask",Float)=255
        _ColorMask("Color mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use alpha clip",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "SoftCityBackdrop"
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color,_TextureSampleAdd;
            float4 _MainTex_ST,_MainTex_TexelSize,_ClipRect;
            float _BlurPixels,_DetailContrast,_Saturation;
            float _UIMaskSoftnessX,_UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;
            struct appdata
            {
                float4 vertex:POSITION;
                fixed4 color:COLOR;
                float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex:SV_POSITION;
                fixed4 color:COLOR;
                float2 uv:TEXCOORD0;
                float4 mask:TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                #ifndef UNITY_COLORSPACE_GAMMA
                    if(_UIVertexColorAlwaysGammaSpace!=0) v.color.rgb=UIGammaToLinear(v.color.rgb);
                #endif
                o.color=v.color*_Color;
                float2 pixelSize=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 clipRect=clamp(_ClipRect,-2e10,2e10);
                o.mask=float4(v.vertex.xy*2-clipRect.xy-clipRect.zw,
                    .25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                return o;
            }
            fixed3 city(float2 uv)
            {
                float2 halfTexel=abs(_MainTex_TexelSize.xy)*.5;
                return tex2D(_MainTex,clamp(uv,halfTexel,1-halfTexel)).rgb;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 halfTexel=abs(_MainTex_TexelSize.xy)*.5;
                fixed4 original=tex2D(_MainTex,clamp(i.uv,halfTexel,1-halfTexel));
                // A bounded 13-tap kernel softens texture noise without replacing the painted city.
                // Keep the foreground gate and outer architecture slightly more defined.
                float horizontal=smoothstep(.02,.22,i.uv.x)*(1-smoothstep(.78,.98,i.uv.x));
                float aboveGate=smoothstep(.035,.30,i.uv.y);
                float region=lerp(.68,1,horizontal*aboveGate);
                float2 d=abs(_MainTex_TexelSize.xy)*max(0,_BlurPixels)*.5*region;
                fixed3 rgb=original.rgb*.20;
                rgb+=(city(i.uv+float2(d.x,0))+city(i.uv-float2(d.x,0))
                    +city(i.uv+float2(0,d.y))+city(i.uv-float2(0,d.y)))*.10;
                rgb+=(city(i.uv+d)+city(i.uv-d)
                    +city(i.uv+float2(d.x,-d.y))+city(i.uv+float2(-d.x,d.y)))*.075;
                rgb+=(city(i.uv+float2(d.x*2,0))+city(i.uv-float2(d.x*2,0))
                    +city(i.uv+float2(0,d.y*2))+city(i.uv-float2(0,d.y*2)))*.025;
                fixed luminance=dot(rgb,fixed3(.2126,.7152,.0722));
                rgb=lerp(luminance.xxx,rgb,_Saturation);
                // Preserve deep ink shadows while gently reducing busy middle-tone contrast.
                fixed3 compressed=(rgb-.42)*_DetailContrast+.42;
                rgb=lerp(rgb,compressed,saturate(luminance*4)*region);
                fixed4 color=fixed4(saturate(rgb),original.a)+_TextureSampleAdd;
                color*=i.color;
                #ifdef UNITY_UI_CLIP_RECT
                    float2 mask=saturate((_ClipRect.zw-_ClipRect.xy-abs(i.mask.xy))*i.mask.zw);
                    color.a*=mask.x*mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a-.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
