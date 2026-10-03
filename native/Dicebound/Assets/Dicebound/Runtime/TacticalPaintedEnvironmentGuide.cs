using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dicebound.Presentation
{
    [Serializable] public sealed class TacticalPaintedProjection
    {
        public int version=1,chapter=2,boardWidth=12,boardHeight=10,pixelWidth=2560,pixelHeight=1440;
        public float tileSize=1.25f,worldWidth,worldHeight,nearClip,farClip;
        public Vector3 cameraPosition,cameraRight,cameraUp,cameraForward;
        public Vector4 beautyUvRect=new Vector4(0,0,1,1);
        public string blockingSignature,beautyResource="Tactics/Painted/Bridge/beauty",depthResource="Tactics/Painted/Bridge/depth",foregroundMaskResource="Tactics/Painted/Bridge/foreground-mask";
        public string colorContract="Beauty is sRGB, unlit, no second tone mapping. Depth is linear RGB packed eye depth, alpha occupancy.";
        public string layoutContract="Lock the grid, wall/gap silhouettes and landing heights to the guide. No people, UI, crates, canisters or skill effects.";
        public TacticalPaintedCellGuide[] cells;
    }
    [Serializable] public sealed class TacticalPaintedCellGuide
    {
        public int x,y; public string name,kind; public Vector3 world; public Vector2 pixel;
        public Vector2[] corners;
    }

    /// <summary>Read-only art export. Does not advance, save, rebuild or edit the rules state.</summary>
    public static class TacticalPaintedEnvironmentGuide
    {
        public static string BlockingSignature(TacticalState state)
        {
            // Random passable surface statuses are live overlays, not a different plate.
            string text=TacticalRules.BoardWidth(state)+"x"+TacticalRules.BoardHeight(state)+":"+
                string.Join("|",state.terrain.OrderBy(c=>c.y).ThenBy(c=>c.x).Select(c=>c.x+","+c.y+"="+StructuralKind(c.kind)));
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();
        }
        public static string StructuralKind(string kind)
        {return kind=="wall"||kind=="gap"||kind=="high"||kind=="pipe"||kind=="rubble"?kind:"plain";}

        public static IEnumerator Export(TacticalEnvironment owner,TacticalStage stage,TacticalState state,string directory)
        {
            if(!owner||!stage||!stage.Camera||state==null)throw new InvalidOperationException("Art-guide export requires a live tactical stage.");
            if(state.chapter!=2||state.sideBattle||TacticalRules.BoardWidth(state)!=12||TacticalRules.BoardHeight(state)!=10)
                throw new InvalidOperationException("This production guide is only the canonical 12x10 main bridge battle.");
            Directory.CreateDirectory(directory);
            yield return new WaitForEndOfFrame();
            var cameraObject=new GameObject("Temporary bridge art export camera");
            var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(stage.Camera);camera.enabled=false;
            camera.transform.SetPositionAndRotation(stage.Camera.transform.position,stage.Camera.transform.rotation);
            camera.aspect=16f/9;camera.orthographicSize=stage.Camera.orthographicSize*1.20f;
            camera.cullingMask=1<<30;camera.allowHDR=false;camera.allowMSAA=false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var projection=new TacticalPaintedProjection{
                chapter=state.chapter,boardWidth=TacticalRules.BoardWidth(state),boardHeight=TacticalRules.BoardHeight(state),
                worldHeight=2*camera.orthographicSize,worldWidth=2*camera.orthographicSize*camera.aspect,
                nearClip=camera.nearClipPlane,farClip=camera.farClipPlane,
                cameraPosition=camera.transform.position,cameraRight=camera.transform.right,cameraUp=camera.transform.up,cameraForward=camera.transform.forward,
                blockingSignature=BlockingSignature(state)
            };
            var renderers=owner.StaticRenderers.Where(r=>r&&r.gameObject.activeInHierarchy&&!(r is ParticleSystemRenderer)).ToArray();
            var savedLayers=renderers.Select(r=>r.gameObject.layer).ToArray();
            var savedEnabled=renderers.Select(r=>r.enabled).ToArray();
            var savedMaterials=renderers.Select(r=>r.sharedMaterials).ToArray();
            var shader=Shader.Find("Dicebound/PaintedGuideDepth");
            if(!shader)throw new InvalidOperationException("Guide shader Dicebound/PaintedGuideDepth was stripped or not compiled.");
            var depthMaterial=new Material(shader){name="Temporary true-geometry depth guide"};
            depthMaterial.SetVector("_GuideNearFar",new Vector4(projection.nearClip,projection.farClip,0,0));
            depthMaterial.SetVector("_GuideBoardBounds",new Vector4(-owner.HalfWidth,owner.HalfWidth,-owner.HalfDepth,owner.HalfDepth));
            var guides=new List<UnityEngine.Object>();
            RenderTexture target=null;
            try{
                for(int i=0;i<renderers.Length;i++){renderers[i].gameObject.layer=30;renderers[i].enabled=true;}
                target=new RenderTexture(projection.pixelWidth,projection.pixelHeight,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name="Bridge clean guide"};target.Create();
                camera.targetTexture=target;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                WriteTexture(target,Path.Combine(directory,"bridge-clean.png"),false);
                projection.cells=Cells(owner,state,camera,projection);
                var gridRoot=new GameObject("Temporary exact rule grid");guides.Add(gridRoot);gridRoot.layer=30;
                BuildGrid(gridRoot.transform,owner,state,guides);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                WriteTexture(target,Path.Combine(directory,"bridge-grid.png"),false);
                gridRoot.SetActive(false);
                target.Release();UnityEngine.Object.Destroy(target);
                target=new RenderTexture(projection.pixelWidth,projection.pixelHeight,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){name="Bridge linear true depth"};target.Create();camera.targetTexture=target;
                camera.backgroundColor=new Color(0,0,0,0);camera.clearFlags=CameraClearFlags.SolidColor;
                for(int i=0;i<renderers.Length;i++){
                    // A mist/foam/decal quad is not an opaque building silhouette. Water
                    // itself is Geometry+5 and contributes its real, lower canal depth.
                    renderers[i].enabled=savedMaterials[i].Length>0&&savedMaterials[i].All(m=>m&&m.renderQueue<3000&&!m.name.Contains("深青哑光地面"));
                    renderers[i].sharedMaterials=savedMaterials[i].Select(m=>depthMaterial).ToArray();
                }
                depthMaterial.SetFloat("_GuideMode",0);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                WriteTexture(target,Path.Combine(directory,"bridge-depth.png"),true);
                depthMaterial.SetFloat("_GuideMode",1);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                WriteTexture(target,Path.Combine(directory,"bridge-front-mask.png"),true);
                File.WriteAllText(Path.Combine(directory,"bridge-projection.json"),JsonUtility.ToJson(projection,true),Encoding.UTF8);
                File.WriteAllText(Path.Combine(directory,"bridge-export.json"),"{\"complete\":true,\"stateRevision\":"+state.revision+",\"charactersUiDynamicObjectsExcluded\":true,\"coordinateOrigin\":\"bottom-left\",\"colorPassHasPostProcessing\":false,\"depthEncoding\":\"dot(rgb,[1,1/255,1/65025])*(far-near)+near; alpha occupancy\"}",Encoding.UTF8);
                Debug.Log("Bridge production guides exported: "+directory+" / "+projection.pixelWidth+"x"+projection.pixelHeight+" / "+projection.blockingSignature);
            }finally{
                for(int i=0;i<renderers.Length;i++)if(renderers[i]){renderers[i].gameObject.layer=savedLayers[i];renderers[i].enabled=savedEnabled[i];renderers[i].sharedMaterials=savedMaterials[i];}
                camera.targetTexture=null;if(target){target.Release();UnityEngine.Object.Destroy(target);}UnityEngine.Object.Destroy(cameraObject);UnityEngine.Object.Destroy(depthMaterial);
                foreach(var resource in guides)if(resource)UnityEngine.Object.Destroy(resource);
            }
            yield return null;
        }
        private static TacticalPaintedCellGuide[] Cells(TacticalEnvironment owner,TacticalState state,Camera camera,TacticalPaintedProjection projection)
        {
            return state.terrain.OrderBy(c=>c.y).ThenBy(c=>c.x).Select(c=>{
                Vector3 position=owner.CellWorld(c.x,c.y),uv=camera.WorldToViewportPoint(position);
                var corners=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f)}.Select(v=>camera.WorldToViewportPoint(position+v*TacticalEnvironment.TileSize)).Select(v=>new Vector2(v.x*projection.pixelWidth,v.y*projection.pixelHeight)).ToArray();
                return new TacticalPaintedCellGuide{x=c.x,y=c.y,name=TacticalRules.CellName(c.x,c.y),kind=c.kind,world=position,pixel=new Vector2(uv.x*projection.pixelWidth,uv.y*projection.pixelHeight),corners=corners};
            }).ToArray();
        }
        private static void BuildGrid(Transform parent,TacticalEnvironment owner,TacticalState state,List<UnityEngine.Object> owned)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit");
            var materials=new Dictionary<string,Material>();
            foreach(var c in state.terrain){
                string kind=StructuralKind(c.kind);
                if(!materials.TryGetValue(kind,out var material)){
                    Color color=kind=="wall"?new Color(.95f,.20f,.15f,.72f):kind=="gap"?new Color(.13f,.25f,.75f,.72f):kind=="high"?new Color(.85f,.70f,.10f,.55f):kind=="pipe"?new Color(.85f,.35f,.10f,.50f):kind=="rubble"?new Color(.57f,.32f,.69f,.55f):new Color(.06f,.90f,.70f,.32f);
                    material=new Material(shader);owned.Add(material);material.SetColor("_BaseColor",color);material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3100;materials[kind]=material;
                }
                Vector3 centre=owner.CellWorld(c.x,c.y)+Vector3.up*(c.kind=="wall"?1.04f:.034f);
                var go=new GameObject("Guide "+TacticalRules.CellName(c.x,c.y));go.layer=30;go.transform.SetParent(parent,false);go.transform.position=centre;
                var mesh=new Mesh{name="Exact cell footprint"};owned.Add(mesh);
                float half=TacticalEnvironment.TileSize*.48f;mesh.vertices=new[]{new Vector3(-half,0,-half),new Vector3(half,0,-half),new Vector3(half,0,half),new Vector3(-half,0,half)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
                var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=5;line.loop=false;line.SetPositions(new[]{new Vector3(-half,.002f,-half),new Vector3(half,.002f,-half),new Vector3(half,.002f,half),new Vector3(-half,.002f,half),new Vector3(-half,.002f,-half)});line.startWidth=line.endWidth=.032f;line.sharedMaterial=material;line.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        private static void WriteTexture(RenderTexture source,string path,bool linear)
        {
            var previous=RenderTexture.active;Texture2D texture=null;
            try{RenderTexture.active=source;texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,linear);texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);texture.Apply(false,false);File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;if(texture)UnityEngine.Object.Destroy(texture);}
        }
    }
}
