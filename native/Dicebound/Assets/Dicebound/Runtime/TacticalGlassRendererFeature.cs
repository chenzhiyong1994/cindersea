using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Dicebound.Presentation
{
    /// <summary>Post-processed camera colour, including transparent actors, blurred for overlay UI.</summary>
    public sealed class TacticalGlassRendererFeature : ScriptableRendererFeature
    {
        public Shader blurShader;
        [Range(2,4)] public int downsample=2;
        [Range(.5f,4f)] public float blurRadius=1.75f;
        private static readonly int SceneId=Shader.PropertyToID("_TacticalGlassScene");
        private static readonly int AvailableId=Shader.PropertyToID("_TacticalGlassAvailable");
        private static readonly int StepId=Shader.PropertyToID("_TacticalBlurStep");
        private static readonly int FocusBlurId=Shader.PropertyToID("_TacticalFocusBlur");
        private static readonly int FocusDepthId=Shader.PropertyToID("_TacticalFocusDepth");
        private static readonly int FocusBoardId=Shader.PropertyToID("_TacticalFocusBoard");
        private static TacticalGlassRendererFeature current;
        private Material material;
        private GlassPass pass;
        private bool ready,subscribed;
        private int capturedFrame=-1;
        public static Texture SceneTexture => current&&current.pass!=null?current.pass.OutputTexture:null;
        public static bool IsReady => current&&current.ready&&current.pass!=null&&current.pass.OutputTexture&&current.pass.OutputTexture.IsCreated();
        public static int FrameCaptured => current?current.capturedFrame:-1;

        public override void Create()
        {
            ReleaseResources();
            current=this;
            ResetGlobals();
            if(!blurShader)blurShader=Resources.Load<Shader>("Shaders/Tactics/TacticalGlassBlur");
            if(!blurShader||!blurShader.isSupported)
                throw new InvalidOperationException("The tactical glass blur shader is missing or unsupported.");
            material=CoreUtils.CreateEngineMaterial(blurShader);
            pass=new GlassPass(this,material);
            if(!subscribed)
            {
                RenderPipelineManager.beginContextRendering+=BeginContext;
                RenderPipelineManager.endContextRendering+=EndContext;
                Application.quitting+=Quit;
                subscribed=true;
            }
        }
        private static Camera TacticalCamera
        {
            get
            {
                var director=TacticalDirector.Instance;
                return director&&director.Stage?director.Stage.Camera:null;
            }
        }
        private void BeginContext(ScriptableRenderContext context,List<Camera> cameras)
        {
            if(current!=this)return;
            ready=false;Shader.SetGlobalFloat(AvailableId,0);
            var camera=TacticalCamera;bool found=false;
            if(isActive&&material&&camera&&camera.isActiveAndEnabled)
                for(int i=0;i<cameras.Count;i++)if(cameras[i]==camera){found=true;break;}
            if(!found){pass?.Release();ResetGlobals();}
        }
        private void EndContext(ScriptableRenderContext context,List<Camera> cameras)
        {
            if(current==this&&!ready){pass?.Release();ResetGlobals();}
            // RenderGraph clears its global bindings at graph end; overlay UI also needs
            // this persistent texture when it renders outside that graph.
            else if(current==this&&pass.OutputTexture){Shader.SetGlobalTexture(SceneId,pass.OutputTexture);Shader.SetGlobalFloat(AvailableId,1);}
        }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
        {
            if(pass==null||!material||renderingData.cameraData.camera!=TacticalCamera||renderingData.cameraData.cameraType!=CameraType.Game)return;
            renderer.EnqueuePass(pass);
        }
        private void MarkReady()
        {ready=true;capturedFrame=Time.frameCount;}
        private static void ResetGlobals()
        {
            Shader.SetGlobalFloat(AvailableId,0);
            Shader.SetGlobalTexture(SceneId,Texture2D.blackTexture);
            if(current){current.ready=false;current.capturedFrame=-1;}
        }
        private void ReleaseResources()
        {
            pass?.Release();pass=null;
            CoreUtils.Destroy(material);material=null;
        }
        private void Quit(){Dispose(true);}
        private void OnDisable(){Dispose(true);}
        protected override void Dispose(bool disposing)
        {
            if(subscribed)
            {
                RenderPipelineManager.beginContextRendering-=BeginContext;
                RenderPipelineManager.endContextRendering-=EndContext;
                Application.quitting-=Quit;subscribed=false;
            }
            if(current==this){ResetGlobals();current=null;}
            ReleaseResources();
        }

        private sealed class GlassPass:ScriptableRenderPass
        {
            private readonly TacticalGlassRendererFeature owner;
            private readonly Material material;
            private readonly ProfilingSampler horizontalSampler=new ProfilingSampler("Tactical glass horizontal");
            private readonly ProfilingSampler verticalSampler=new ProfilingSampler("Tactical glass vertical");
            private RTHandle output;
            public RenderTexture OutputTexture => output?.rt;
            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public Vector4 step;
                public TacticalGlassRendererFeature owner;
                public bool publish;
            }
            private sealed class FocusData
            {
                public TextureHandle source,blur,depth;
                public Material material;
                public Vector4 board;
            }
            public GlassPass(TacticalGlassRendererFeature owner,Material material)
            {
                this.owner=owner;this.material=material;
                renderPassEvent=RenderPassEvent.AfterRenderingPostProcessing;
                // This preserves sampleable post-process colour rather than resolving directly to the backbuffer.
                requiresIntermediateTexture=true;
            }
            public void Release(){output?.Release();output=null;}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
            {
                var resources=frameData.Get<UniversalResourceData>();
                var camera=frameData.Get<UniversalCameraData>();
                if(resources.isActiveTargetBackBuffer||!resources.activeColorTexture.IsValid())return;
                var source=resources.activeColorTexture;
                var sourceDesc=graph.GetTextureDesc(source);
                var descriptor=camera.cameraTargetDescriptor;
                int scale=owner.downsample>=4?4:2;
                descriptor.width=Mathf.Max(1,(sourceDesc.width+scale-1)/scale);
                descriptor.height=Mathf.Max(1,(sourceDesc.height+scale-1)/scale);
                descriptor.graphicsFormat=sourceDesc.format;
                descriptor.depthStencilFormat=GraphicsFormat.None;descriptor.depthBufferBits=0;
                descriptor.msaaSamples=1;descriptor.bindMS=false;
                descriptor.useMipMap=false;descriptor.autoGenerateMips=false;
                descriptor.enableRandomWrite=false;descriptor.memoryless=RenderTextureMemoryless.None;
                descriptor.useDynamicScale=false;descriptor.useDynamicScaleExplicit=false;
                descriptor.dimension=TextureDimension.Tex2D;descriptor.volumeDepth=1;descriptor.vrUsage=VRTextureUsage.None;
                RenderingUtils.ReAllocateHandleIfNeeded(ref output,descriptor,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"Tactical glass persistent scene");
                var destination=graph.ImportTexture(output);
                var intermediate=UniversalRenderer.CreateRenderGraphTexture(graph,descriptor,"Tactical glass horizontal scene",false,FilterMode.Bilinear,TextureWrapMode.Clamp);
                float radius=Mathf.Clamp(owner.blurRadius,.5f,4f);
                AddBlur(graph,source,intermediate,new Vector4(radius/descriptor.width,0,0,0),false);
                AddBlur(graph,intermediate,destination,new Vector4(0,radius/descriptor.height,0,0),true);
                var director=TacticalDirector.Instance;
                if(director&&director.State?.phase=="battle"&&director.Stage.Environment&&!director.Stage.Environment.PaintedActive&&resources.cameraDepthTexture.IsValid())
                {
                    // Reuse the camera blur for the close architectural frame only. The
                    // entire playable rectangle (including sprite margins) stays sharp.
                    var framedDesc=sourceDesc;framedDesc.name="Tactical foreground focus";
                    var framed=graph.CreateTexture(framedDesc);
                    using(var builder=graph.AddRasterRenderPass<FocusData>("Tactical foreground focus",out var data))
                    {
                        data.source=source;data.blur=destination;data.depth=resources.cameraDepthTexture;data.material=material;
                        data.board=new Vector4(director.Stage.Environment.HalfWidth+.9f,director.Stage.Environment.HalfDepth+.9f,12,20);
                        builder.UseTexture(source,AccessFlags.Read);builder.UseTexture(destination,AccessFlags.Read);builder.UseTexture(data.depth,AccessFlags.Read);
                        builder.SetRenderAttachment(framed,0,AccessFlags.WriteAll);builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc(static(FocusData pass,RasterGraphContext context)=>
                        {
                            RTHandle sourceTexture=pass.source;
                            Vector4 scale=sourceTexture.useScaling?sourceTexture.rtHandleProperties.rtHandleScale:Vector4.one;
                            context.cmd.SetGlobalTexture(FocusBlurId,pass.blur);context.cmd.SetGlobalTexture(FocusDepthId,pass.depth);
                            context.cmd.SetGlobalVector(FocusBoardId,pass.board);
                            Blitter.BlitTexture(context.cmd,sourceTexture,new Vector4(scale.x,scale.y,0,0),pass.material,1);
                        });
                    }
                    resources.cameraColor=framed;
                }
            }
            private void AddBlur(RenderGraph graph,TextureHandle source,TextureHandle destination,Vector4 step,bool publish)
            {
                using(var builder=graph.AddRasterRenderPass<PassData>(publish?"Tactical glass vertical":"Tactical glass horizontal",out var data,publish?verticalSampler:horizontalSampler))
                {
                    data.source=source;data.material=material;data.step=step;data.owner=owner;data.publish=publish;
                    builder.UseTexture(source,AccessFlags.Read);
                    builder.SetRenderAttachment(destination,0,AccessFlags.WriteAll);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    // UI rendering occurs outside this graph; the imported output must survive its final graph use.
                    if(publish)builder.SetGlobalTextureAfterPass(destination,SceneId);
                    builder.SetRenderFunc(static(PassData pass,RasterGraphContext context)=>
                    {
                        RTHandle sourceTexture=pass.source;
                        Vector4 scale=sourceTexture.useScaling?sourceTexture.rtHandleProperties.rtHandleScale:Vector4.one;
                        context.cmd.SetGlobalVector(StepId,pass.step);
                        // Both source and destination are render textures: preserve their native orientation.
                        // UI maps its physical pixels to bottom-left viewport UV before sampling this output.
                        Blitter.BlitTexture(context.cmd,sourceTexture,new Vector4(scale.x,scale.y,0,0),pass.material,0);
                        if(pass.publish){context.cmd.SetGlobalFloat(AvailableId,1);pass.owner.MarkReady();}
                    });
                }
            }
        }
    }
}
