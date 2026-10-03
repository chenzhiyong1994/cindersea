using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace Dicebound.Presentation
{
    /// <summary>Draw range ink after grading. Its custom LightMode never enters the normal colour pass.</summary>
    public sealed class TacticalRangeRendererFeature : ScriptableRendererFeature
    {
        public static bool IsInstalled {get;private set;}
        public static int LastRenderedFrame {get;private set;}=-1;
        public static int LastDepthCopiedFrame {get;private set;}=-1;
        public static int LastLiveDepthFrame => LastDepthCopiedFrame;
        public static bool DepthCopyExecuted => LastDepthCopiedFrame==Time.frameCount;
        public Shader depthCopyShader;
        private RangePass pass;
        public override void Create()
        {
            pass?.Dispose();pass=null;IsInstalled=false;LastRenderedFrame=LastDepthCopiedFrame=-1;
            if(!depthCopyShader)depthCopyShader=Shader.Find("Hidden/Universal Render Pipeline/CopyDepth");
            if(!depthCopyShader)throw new InvalidOperationException("Missing URP tactical range depth-copy shader.");
            pass=new RangePass(depthCopyShader);IsInstalled=true;
        }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            var game=TacticalDirector.Instance;
            if(pass!=null&&game&&game.Stage&&data.cameraData.camera==game.Stage.Camera&&game.State?.phase=="battle")renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing){pass?.Dispose();pass=null;IsInstalled=false;LastRenderedFrame=LastDepthCopiedFrame=-1;}
        private sealed class RangePass : ScriptableRenderPass
        {
            private static readonly ShaderTagId RangeTag=new ShaderTagId("TacticalRangeOverlay");
            private static readonly int DepthId=Shader.PropertyToID("_TacticalRangeSceneDepth");
            private readonly ProfilingSampler sampler=new ProfilingSampler("Tactical range after grade");
            private readonly CopyDepthPass liveDepthCopy;
            private sealed class Data{public RendererListHandle renderers;public TextureHandle depth;}
            public RangePass(Shader copyShader)
            {
                renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.AfterRenderingPostProcessing+2);
                requiresIntermediateTexture=true;ConfigureInput(ScriptableRenderPassInput.Depth);
                liveDepthCopy=new CopyDepthPass(renderPassEvent,copyShader,customPassName:"Tactical range live alpha depth");
            }
            public void Dispose(){liveDepthCopy.Dispose();}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var resources=frame.Get<UniversalResourceData>();
                if(resources.isActiveTargetBackBuffer||!resources.activeColorTexture.IsValid()||!resources.activeDepthTexture.IsValid())return;
                // SSAO may populate cameraDepthTexture in an early DepthNormals prepass.
                // Sprite colour passes write their exact alpha silhouette to the live
                // attachment later. Resolve that final attachment instead of sampling
                // the earlier, incomplete texture. URP handles MSAA and RT orientation.
                var depthDescription=graph.GetTextureDesc(resources.activeDepthTexture);
                depthDescription.name="Tactical range final alpha depth";
                depthDescription.format=GraphicsFormat.R32_SFloat;
                depthDescription.depthBufferBits=DepthBits.None;
                depthDescription.msaaSamples=MSAASamples.None;
                depthDescription.bindTextureMS=false;
                depthDescription.clearBuffer=false;
                depthDescription.filterMode=FilterMode.Point;
                var liveDepth=graph.CreateTexture(depthDescription);
                liveDepthCopy.Render(graph,frame,liveDepth,resources.activeDepthTexture,passName:"Tactical range live alpha depth");
                var camera=frame.Get<UniversalCameraData>();var rendering=frame.Get<UniversalRenderingData>();var lights=frame.Get<UniversalLightData>();
                var drawing=RenderingUtils.CreateDrawingSettings(RangeTag,rendering,camera,lights,SortingCriteria.CommonTransparent);
                var filtering=new FilteringSettings(RenderQueueRange.transparent);
                var parameters=new RendererListParams(rendering.cullResults,drawing,filtering);
                using(var builder=graph.AddRasterRenderPass<Data>("Tactical range after grade",out var data,sampler))
                {
                    data.renderers=graph.CreateRendererList(parameters);data.depth=liveDepth;
                    builder.UseRendererList(data.renderers);builder.UseTexture(data.depth,AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static(Data value,RasterGraphContext context)=>
                    {
                        context.cmd.SetGlobalTexture(DepthId,value.depth);
                        // Reading this texture establishes the graph dependency on the
                        // completed URP copy; this marker never reports an early prepass.
                        context.cmd.DrawRendererList(value.renderers);LastRenderedFrame=LastDepthCopiedFrame=Time.frameCount;
                    });
                }
            }
        }
    }
}
