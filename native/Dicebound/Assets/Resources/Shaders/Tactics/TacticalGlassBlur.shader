Shader "Hidden/Dicebound/TacticalGlassBlur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "SeparableGaussian"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _TacticalBlurStep;
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float2 step=_TacticalBlurStep.xy;
                half4 colour=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv,0)*.227027027h;
                colour+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv+step*1.384615385,0)*.316216216h;
                colour+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv-step*1.384615385,0)*.316216216h;
                colour+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv+step*3.230769231,0)*.070270270h;
                colour+=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv-step*3.230769231,0)*.070270270h;
                return half4(colour.rgb,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ArchitecturalForegroundFocus"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Focus
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_TacticalFocusBlur);
            TEXTURE2D_X_FLOAT(_TacticalFocusDepth);
            float4 _TacticalFocusBoard;
            half4 Focus(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float depth=SAMPLE_TEXTURE2D_X(_TacticalFocusDepth,sampler_PointClamp,uv).r;
                #if !UNITY_REVERSED_Z
                depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 world=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float outside=max(abs(world.x)-_TacticalFocusBoard.x,abs(world.z)-_TacticalFocusBoard.y);
                float eyeDepth=-TransformWorldToView(world).z;
                float amount=smoothstep(.12,2.2,outside)*(1-smoothstep(_TacticalFocusBoard.z,_TacticalFocusBoard.w,eyeDepth));
                half3 sharp=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv,0).rgb;
                half3 soft=SAMPLE_TEXTURE2D_X_LOD(_TacticalFocusBlur,sampler_LinearClamp,uv,0).rgb;
                return half4(lerp(sharp,soft,amount),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
