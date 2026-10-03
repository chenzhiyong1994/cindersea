using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    internal enum TacticalGlassKind { JadeGlass, JadePolish, Cinnabar, Pearl, Lens }

    /// <summary>Shared optical materials; rect geometry travels in vertex data, never per-panel materials.</summary>
    public sealed class TacticalGlassSurface : BaseMeshEffect
    {
        public const string ShaderName="Dicebound/UI/TacticalGlass";
        private static readonly Dictionary<TacticalGlassKind,Material> materials=new Dictionary<TacticalGlassKind,Material>();
        private static int viewportFrame=-1;
        private float radius=24;
        internal void Configure(TacticalGlassKind kind,float cornerRadius)
        {
            radius=cornerRadius;graphic.material=Material(kind);EnableVertexData();graphic.SetVerticesDirty();UpdateViewport();
        }
        internal void SetRadius(float value){radius=value;if(graphic)graphic.SetVerticesDirty();}
        protected override void OnEnable(){base.OnEnable();EnableVertexData();UpdateViewport();}
        private void LateUpdate(){UpdateViewport();}
        private void EnableVertexData()
        {
            var canvas=GetComponentInParent<Canvas>();
            if(canvas)canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2;
        }
        private static void UpdateViewport()
        {
            if(viewportFrame==Time.frameCount)return;viewportFrame=Time.frameCount;
            float width=Mathf.Max(1,Screen.width),height=Mathf.Max(1,Screen.height);var rect=PaperViewport.Pixels;
            Shader.SetGlobalVector("_TacticalGlassViewport",new Vector4(rect.x/width,rect.y/height,rect.width/width,rect.height/height));
            Shader.SetGlobalVector("_TacticalGlassScreen",new Vector4(1/width,1/height,width,height));
        }
        public override void ModifyMesh(VertexHelper helper)
        {
            if(!IsActive())return;var rect=((RectTransform)transform).rect;
            float corner=Mathf.Clamp(radius,0,Mathf.Min(rect.width,rect.height)*.5f);
            var vertex=UIVertex.simpleVert;
            for(int i=0;i<helper.currentVertCount;i++)
            {
                helper.PopulateUIVertex(ref vertex,i);
                // Negative w marks shadow geometry, which must not sample or reflect the scene.
                vertex.uv1=new Vector4(vertex.position.x-rect.center.x,vertex.position.y-rect.center.y,0,vertex.uv1.w);
                vertex.uv2=new Vector4(rect.width,rect.height,corner,0);helper.SetUIVertex(vertex,i);
            }
        }
        private static Material Material(TacticalGlassKind kind)
        {
            if(materials.TryGetValue(kind,out var cached)&&cached)return cached;
            var shader=Shader.Find(ShaderName);
            if(!shader)throw new InvalidOperationException("缺少玻璃界面 Shader："+ShaderName);
            var material=Visuals.Own(new Material(shader){name="Tactical optical surface · "+kind});
            bool pearl=kind==TacticalGlassKind.Pearl,cinnabar=kind==TacticalGlassKind.Cinnabar,lens=kind==TacticalGlassKind.Lens;
            material.SetFloat("_Treatment",pearl?2:lens?3:kind==TacticalGlassKind.JadeGlass?0:1);
            material.SetColor("_BodyColor",pearl?new Color(.91f,.87f,.77f,1):cinnabar?new Color(.50f,.145f,.085f,1):new Color(.035f,.115f,.135f,1));
            material.SetColor("_RimColor",pearl?new Color(.70f,.55f,.32f,1):lens?new Color(.78f,.65f,.40f,1):cinnabar?new Color(.95f,.70f,.43f,1):new Color(.56f,.76f,.70f,1));
            material.SetColor("_ReflectionColor",pearl?new Color(1,.98f,.88f,1):cinnabar?new Color(1,.78f,.54f,1):new Color(.64f,.83f,.78f,1));
            material.SetFloat("_Transmission",kind==TacticalGlassKind.JadeGlass?.40f:lens?.27f:0);
            material.SetFloat("_Roughness",kind==TacticalGlassKind.JadeGlass?.52f:pearl?.58f:.34f);
            materials[kind]=material;return material;
        }
    }
}
