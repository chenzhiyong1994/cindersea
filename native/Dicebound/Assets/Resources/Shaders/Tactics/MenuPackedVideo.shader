Shader "Dicebound/UI/MenuPackedVideo"
{
    Properties
    {
        [PerRendererData] _MainTex("Color above coverage",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _MaskIsSRGB("Decode coverage from sRGB RT",Float)=1
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
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize,_ClipRect;
            fixed4 _Color;
            float _MaskIsSRGB,_UIMaskSoftnessX,_UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;
            struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 mask:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                #ifndef UNITY_COLORSPACE_GAMMA
                    if(_UIVertexColorAlwaysGammaSpace!=0)v.color.rgb=UIGammaToLinear(v.color.rgb);
                #endif
                o.color=v.color*_Color;
                float2 pixelSize=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 clipRect=clamp(_ClipRect,-2e10,2e10);
                o.mask=float4(v.vertex.xy*2-clipRect.xy-clipRect.zw,.25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Both halves share the original artwork UV crop; never stretch the stacked frame.
                float halfTexel=abs(_MainTex_TexelSize.y)*.5;
                float y=clamp(i.uv.y*.5,halfTexel,.5-halfTexel);
                fixed3 rgb=tex2D(_MainTex,float2(i.uv.x,y+.5)).rgb;
                float3 mask=tex2D(_MainTex,float2(i.uv.x,y)).rgb;
                // Coverage is data encoded into RGB, so undo the texture's sRGB decode for this half only.
                if(_MaskIsSRGB>.5)mask=LinearToGammaSpace(mask);
                fixed4 color=fixed4(rgb,saturate(mask.r))*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                    float2 m=saturate((_ClipRect.zw-_ClipRect.xy-abs(i.mask.xy))*i.mask.zw);color.a*=m.x*m.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a-.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
