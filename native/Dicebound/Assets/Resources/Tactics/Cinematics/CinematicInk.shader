Shader "Dicebound/UI/CinematicInk"
{
    Properties
    {
        [PerRendererData] _MainTex("Painted action",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Accent("Element",Color)=(1,.4,.1,1)
        _Progress("Animation phase",Range(0,1))=.44
        _Motion("Motion",Float)=1
        _StencilComp("Stencil comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil operation",Float)=0
        _StencilWriteMask("Stencil write mask",Float)=255
        _StencilReadMask("Stencil read mask",Float)=255
        _ColorMask("Color mask",Float)=15
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 localPosition:TEXCOORD1;fixed4 color:COLOR;};
            sampler2D _MainTex;fixed4 _Color,_Accent;float4 _ClipRect;float _Progress,_Motion;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.localPosition=v.vertex.xy;o.color=v.color*_Color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 art=tex2D(_MainTex,i.uv)*i.color;
                // Light travels only over bright painted effects; no UV warp on the face or hands.
                float lum=dot(art.rgb,float3(.2126,.7152,.0722));
                float sweep=exp(-pow((i.uv.x+i.uv.y*.18-(_Progress*1.65-.28))*17,2));
                float light=sweep*smoothstep(.56,.96,lum)*.24*_Motion;
                art.rgb+=light*lerp(_Accent.rgb,1,.82);
                #ifdef UNITY_UI_CLIP_RECT
                art.a*=UnityGet2DClipping(i.localPosition,_ClipRect);
                #endif
                return art;
            }
            ENDCG
        }
    }
}
