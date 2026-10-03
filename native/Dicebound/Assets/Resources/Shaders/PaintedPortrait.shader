Shader "Dicebound/PaintedPortrait"
{
    Properties
    {
        _BaseMap("Portrait",2D)="white"{}
        _CastMap("Casting portrait",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Kind("Anatomy",Float)=0
        _Motion("Motion",Float)=1
        _Cast("Gesture",Float)=0
        _Hit("Recoil",Float)=0
        _Blink("Blink",Float)=0
        _Gaze("Gaze",Vector)=(0,0,0,0)
        _PlaneScale("Portrait dimensions",Vector)=(5,7.8,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CastMap);SAMPLER(sampler_CastMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_Gaze,_PlaneScale;
            float _Kind,_Motion,_Cast,_Hit,_Blink;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            float region(float2 p,float2 center,float2 radius){float2 d=(p-center)/radius;return exp(-dot(d,d)*2);}
            float2 turn(float2 p,float2 pivot,float a){float2 d=p-pivot;float c=cos(a),s=sin(a);return pivot+float2(c*d.x-s*d.y,s*d.x+c*d.y);}
            Varyings vert(Attributes input)
            {
                Varyings output;output.uv=input.uv;float2 uv=float2(input.uv.x,1-input.uv.y),p=uv;
                float time=_Time.y+_Kind*1.7,m=_Motion,anchor=1-smoothstep(.78,.96,p.y);
                float breath=sin(time*1.6)*m,torso=region(p,float2(.5,.31),float2(.22,.21));
                p.x+=(p.x-.5)*breath*.015*torso+sin(time*.73)*.004*anchor*m;p.y-=breath*.003*torso;
                if(_Kind<.5){
                    float2 head=float2(.56,.115);
                    float weight=region(uv,head,float2(.14,.115));
                    p=lerp(p,turn(p,head+float2(0,.07),(sin(time*.62)*.026+_Gaze.x*.027-_Hit*.08)*m),weight);
                    p+=float2(_Gaze.x*.009,_Gaze.y*.004)*weight*m;
                    float hair=region(uv,head,float2(.2,.15))*smoothstep(.035,.1,abs(uv.x-head.x));
                    float hem=smoothstep(.36,.72,p.y)*(1-smoothstep(.87,.97,p.y))*smoothstep(.08,.24,abs(p.x-.5));
                    p.x+=(sin(time*1.9+p.y*17)*.006+sin(time*.83+p.x*13)*.004)*hair*m;
                    p.x+=(sin(time*1.35-p.y*6)*.009+sin(time*2.1-p.y*10)*.0025)*hem*m;
                    p.y+=sin(time*1.35-p.y*6+1.3)*.003*hem*m;
                }else if(_Kind>4.5){
                    // New full-body paintings: restrained fabric motion with planted feet.
                    float cloth=smoothstep(.3,.58,p.y)*(1-smoothstep(.72,.88,p.y))*smoothstep(.08,.22,abs(p.x-.5));
                    p.x+=sin(time*1.15-p.y*6)*.004*cloth*m;
                }else if(_Kind>2.5&&_Kind<3.5){
                    float wing=smoothstep(.10,.31,abs(p.x-.63));p.x=.63+(p.x-.63)*(1-(.05+.04*sin(time*3.7))*wing*m);p.y+=cos(time*3.7)*.009*wing*m+sin(time*1.9)*.006*m;
                }else if(_Kind>3.5){p=lerp(p,turn(p,float2(.48,.43),sin(time*.78)*.02*m-_Cast*.03),1-smoothstep(.1,.38,p.y));}
                else if(_Kind>1.5){float cap=1-smoothstep(.23,.51,p.y);p.x+=(p.x-.5)*sin(time*1.4)*.024*cap*m;p.y+=sin(time*1.4+.6)*.006*cap*m;}
                else {p.x+=sin(time*1.4+p.y*8)*.006*anchor*m;p.y+=sin(time*1.8+p.x*9)*.003*anchor*m;}
                float3 position=input.positionOS.xyz;
                // Mesh dimensions are encoded by the original plane; UV deformation stays anatomical.
                position.xy+=float2(p.x-uv.x,uv.y-p.y)*_PlaneScale.xy;
                output.positionCS=TransformObjectToHClip(position);return output;
            }
            half4 portrait(TEXTURE2D_PARAM(tex,samp),float2 uv,float2 eye1,float2 eye2)
            {
                half4 c=SAMPLE_TEXTURE2D(tex,samp,uv);
                if(_Kind<.5&&_Blink>.01){float2 eye=distance(uv,eye1)<distance(uv,eye2)?eye1:eye2;float2 d=(uv-eye)/float2(.014,.004);
                    float mask=(1-smoothstep(.65,1.15,length(d)))*_Blink;
                    half3 lid=SAMPLE_TEXTURE2D(tex,samp,float2(uv.x,eye.y+.009)).rgb;
                    float lash=1-smoothstep(.0004,.0012,abs(uv.y-eye.y+.001));lid*=1-lash*.44;c.rgb=lerp(c.rgb,lid,mask);}
                return c;
            }
            half4 frag(Varyings input):SV_Target
            {
                half4 a=portrait(TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap),input.uv,float2(.548,.926),float2(.598,.913));
                half4 b=portrait(TEXTURE2D_ARGS(_CastMap,sampler_CastMap),input.uv,float2(.548,.926),float2(.598,.913));
                float mixValue=_Kind<.5?_Cast:0;float alpha=lerp(a.a,b.a,mixValue);
                half3 color=lerp(a.rgb*a.a,b.rgb*b.a,mixValue)/max(alpha,.001);
                color=lerp(color,half3(1,.73,.57),_Hit*.38);float feetFade=_Kind>4.5?1:_Kind<.5?smoothstep(.01,.10,input.uv.y):smoothstep(0,.025,input.uv.y);return half4(color*_BaseColor.rgb,alpha*_BaseColor.a*feetFade);
            }
            ENDHLSL
        }
    }
}
