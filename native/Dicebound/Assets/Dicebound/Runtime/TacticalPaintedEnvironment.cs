using System;
using System.Collections.Generic;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dicebound.Presentation
{
    /// <summary>Projection runtime is separate from the immutable tactical state and picking geometry.</summary>
    [DefaultExecutionOrder(950)] public sealed class TacticalPaintedEnvironment : MonoBehaviour
    {
        public bool Active {get;private set;}
        public string ResourceInfo {get;private set;}="Painted assets have not been installed; procedural fallback is active.";
        public TacticalPaintedProjection Projection {get;private set;}
        public Rect AvailableFrame => Projection==null?new Rect():new Rect(-Projection.worldWidth*.5f,-Projection.worldHeight*.5f,Projection.worldWidth,Projection.worldHeight);
        internal Transform VisualRoot => layerRoot;
        public int DepthValidatedCells {get;private set;}
        public bool OriginalColorDiagnostic {get;private set;}
        private readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        private Renderer[] hidden=Array.Empty<Renderer>();
        private bool[] hiddenEnabled=Array.Empty<bool>();
        private Camera sceneCamera;
        private bool oldPostProcessing;
        private Transform layerRoot;
        private Material baseMaterial,frontMaterial,waterMaterial;
        private Renderer waterRenderer;
        private TacticalEnvironment owner;

        public bool Install(TacticalEnvironment environment,TacticalState state,Camera camera)
        {
            Release();
            if(!environment||state==null||!camera||state.chapter!=2||TacticalRules.BoardWidth(state)!=12||TacticalRules.BoardHeight(state)!=10){ResourceInfo="Other chapters and legacy boards retain their procedural scene.";return false;}
            var metadata=Resources.Load<TextAsset>("Tactics/Painted/Bridge/projection");
            if(!metadata){ResourceInfo="No bridge projection.json; procedural scene retained.";return false;}
            TacticalPaintedProjection projection;
            try{projection=JsonUtility.FromJson<TacticalPaintedProjection>(metadata.text);}catch(Exception e){ResourceInfo="Invalid bridge projection: "+e.Message;return false;}
            if(!ValidProjection(projection,state,camera,out var error)){ResourceInfo=error;return false;}
            // The jade plate is an authored surface-style edit of this exact calibrated
            // bridge, never a screenshot with baked actors or range indicators.
            const string jadeBeautyResource="Tactics/Jade/battle-plate";
            var beauty=Resources.Load<Texture2D>(jadeBeautyResource);
            string beautyResource=beauty?jadeBeautyResource:projection.beautyResource;
            if(!beauty)beauty=Resources.Load<Texture2D>(projection.beautyResource);
            var depth=Resources.Load<Texture2D>(projection.depthResource);
            var mask=Resources.Load<Texture2D>(projection.foregroundMaskResource);
            if(!beauty||!depth||!mask){ResourceInfo="Bridge beauty/depth/foreground-mask is incomplete; procedural scene retained.";return false;}
            Vector4 uv=projection.beautyUvRect;
            if(!Finite(uv.x)||!Finite(uv.y)||!Finite(uv.z)||!Finite(uv.w)||uv.z<=0||uv.w<=0||uv.x<0||uv.y<0||uv.x+uv.z>1.0001f||uv.y+uv.w>1.0001f){ResourceInfo="Beauty UV crop must be inside the untouched original PNG.";return false;}
            float artworkAspect=beauty.width*uv.z/(beauty.height*uv.w),frameAspect=projection.worldWidth/projection.worldHeight;
            if(Mathf.Abs(artworkAspect/frameAspect-1)>.015f){ResourceInfo="Beauty aspect does not match the guide. Supply an explicit beautyUvRect; images are never stretched.";return false;}
            if(depth.width!=projection.pixelWidth||depth.height!=projection.pixelHeight||mask.width!=depth.width||mask.height!=depth.height||!depth.isReadable){ResourceInfo="Depth/mask must match guide resolution; depth must be readable, linear and uncompressed.";return false;}
            if(depth.filterMode!=FilterMode.Point||mask.filterMode!=FilterMode.Point||UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(depth.graphicsFormat)||UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(mask.graphicsFormat)){ResourceInfo="Depth and mask require linear Point sampling; sRGB sampling would corrupt their geometry.";return false;}
            if(!ValidateDepth(environment,state,projection,depth,out error)){ResourceInfo=error;return false;}
            var shader=Shader.Find("Dicebound/PaintedEnvironment");
            if(!shader||!shader.isSupported){ResourceInfo="PaintedEnvironment shader is missing or unsupported; procedural scene retained.";return false;}
            Projection=projection;sceneCamera=camera;owner=environment;
            hidden=environment.StaticRenderers.Where(r=>r&&r.gameObject.activeInHierarchy).ToArray();hiddenEnabled=hidden.Select(r=>r.enabled).ToArray();
            // Disable only the old static visual surfaces. Colliders, markers and the
            // object's live meshes remain, as do every actor and VFX owned by Stage.
            for(int i=0;i<hidden.Length;i++)hidden[i].enabled=false;
            layerRoot=new GameObject("回水桥 · 原画投影与前景遮挡层").transform;layerRoot.SetParent(environment.StaticGeometry,false);
            baseMaterial=Material(shader,beauty,depth,mask,projection,0);
            frontMaterial=Material(shader,beauty,depth,mask,projection,1);
            Card("静态地点原画",baseMaterial,projection);Card("原画前景遮挡",frontMaterial,projection);
            var waterShader=Shader.Find("Dicebound/PaintedEnvironmentWater");
            if(waterShader&&waterShader.isSupported){waterMaterial=Material(waterShader,beauty,depth,mask,projection,0);waterMaterial.SetFloat("_Motion",environment.ReducedMotion?0:1);waterRenderer=Card("原画水面的细微流光",waterMaterial,projection);}
            // Rule-defined floor features are installed after the static plate by
            // TacticalTerrainFeatures, shared with the other four locations.
            oldPostProcessing=camera.GetUniversalAdditionalCameraData().renderPostProcessing;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            Active=true;OriginalColorDiagnostic=false;
            ResourceInfo=beautyResource+" / "+beauty.width+"x"+beauty.height+" / proxy depth "+depth.width+"x"+depth.height+" / "+DepthValidatedCells+" validated walkable cells";
            ConstrainCamera();return true;
        }
        private static bool ValidProjection(TacticalPaintedProjection p,TacticalState state,Camera camera,out string error)
        {
            error="Invalid bridge projection metadata.";
            if(p==null||p.version!=1||p.chapter!=2||p.boardWidth!=12||p.boardHeight!=10||p.pixelWidth<256||p.pixelHeight<256||!Finite(p.worldWidth)||!Finite(p.worldHeight)||!Finite(p.nearClip)||!Finite(p.farClip)||p.worldWidth<=0||p.worldHeight<=0||p.nearClip<=0||p.farClip<=p.nearClip||!Finite(p.cameraPosition)||!Finite(p.cameraRight)||!Finite(p.cameraUp)||!Finite(p.cameraForward))return false;
            if(Mathf.Abs(p.tileSize-TacticalEnvironment.TileSize)>.0001f||p.blockingSignature!=TacticalPaintedEnvironmentGuide.BlockingSignature(state)){error="Stored terrain topology does not match the painted bridge; procedural scene retained.";return false;}
            if(Mathf.Abs(p.cameraRight.sqrMagnitude-1)>.002f||Mathf.Abs(p.cameraUp.sqrMagnitude-1)>.002f||Mathf.Abs(p.cameraForward.sqrMagnitude-1)>.002f||Mathf.Abs(Vector3.Dot(p.cameraRight,p.cameraUp))>.001f||Vector3.Dot(Vector3.Cross(p.cameraRight,p.cameraUp),p.cameraForward)<.999f){error="Projection camera basis is not orthonormal.";return false;}
            if(!camera.orthographic||Vector3.Dot(camera.transform.forward,p.cameraForward)<.9999f||Vector3.Dot(camera.transform.up,p.cameraUp)<.9999f){error="Painted scene requires its calibrated fixed orthographic orientation.";return false;}
            if(p.cells==null||p.cells.Length!=120||p.cells.Any(c=>c==null)||p.cells.Select(c=>c.x+12*c.y).Distinct().Count()!=120||p.cells.Any(c=>c.x<0||c.x>=12||c.y<0||c.y>=10||!Finite(c.world))){error="Projection lacks the 120 unique calibrated cell centres.";return false;}
            error=null;return true;
        }
        private bool ValidateDepth(TacticalEnvironment environment,TacticalState state,TacticalPaintedProjection p,Texture2D depth,out string error)
        {
            DepthValidatedCells=0;var pixels=depth.GetPixels32();
            foreach(var cell in p.cells){
                Vector3 world=environment.CellWorld(cell.x,cell.y);
                if((world-cell.world).sqrMagnitude>.0001f){error="Saved high platforms differ from the art guide.";return false;}
                if(TacticalRules.TerrainAt(state,cell.x,cell.y)=="gap"||TacticalRules.TerrainAt(state,cell.x,cell.y)=="wall")continue;
                Vector3 offset=world-p.cameraPosition;
                float u=Vector3.Dot(offset,p.cameraRight)/p.worldWidth+.5f,v=Vector3.Dot(offset,p.cameraUp)/p.worldHeight+.5f;
                if(u<0||u>=1||v<0||v>=1){error="A playable cell falls outside the authored plate.";return false;}
                var rgba=pixels[Mathf.Clamp((int)(v*depth.height),0,depth.height-1)*depth.width+Mathf.Clamp((int)(u*depth.width),0,depth.width-1)];
                float eye=p.nearClip+(rgba.r/255f+rgba.g/(255f*255f)+rgba.b/(255f*65025f))*(p.farClip-p.nearClip);
                Vector3 reconstructed=p.cameraPosition+p.cameraRight*((u-.5f)*p.worldWidth)+p.cameraUp*((v-.5f)*p.worldHeight)+p.cameraForward*eye;
                if(rgba.a<128||Mathf.Abs(reconstructed.y-world.y)>.14f){error="Depth guide occludes traversable cell "+TacticalRules.CellName(cell.x,cell.y)+"; expected a clear landing surface.";return false;}
                DepthValidatedCells++;
            }
            error=null;return true;
        }
        private Material Material(Shader shader,Texture beauty,Texture depth,Texture mask,TacticalPaintedProjection p,int layer)
        {
            var material=new Material(shader){name=layer==0?"原画地点 · 未二次调色":"原画前景 · 真实深度"};owned.Add(material);
            material.SetTexture("_Beauty",beauty);material.SetTexture("_ProxyDepth",depth);material.SetTexture("_ForegroundMask",mask);
            material.SetVector("_CapturePosition",p.cameraPosition);material.SetVector("_CaptureRight",p.cameraRight);material.SetVector("_CaptureUp",p.cameraUp);material.SetVector("_CaptureForward",p.cameraForward);
            material.SetVector("_CaptureSize",new Vector4(p.worldWidth,p.worldHeight,p.nearClip,p.farClip));material.SetVector("_BeautyUvRect",p.beautyUvRect);material.SetFloat("_Layer",layer);material.SetFloat("_DynamicShadow",.35f);
            return material;
        }
        private Renderer Card(string name,Material material,TacticalPaintedProjection p)
        {
            float w=p.worldWidth*.5f,h=p.worldHeight*.5f;
            var mesh=new Mesh{name=name};owned.Add(mesh);mesh.vertices=new[]{new Vector3(-w,-h,0),new Vector3(w,-h,0),new Vector3(w,h,0),new Vector3(-w,h,0)};mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.normals=Enumerable.Repeat(Vector3.back,4).ToArray();mesh.bounds=new Bounds(Vector3.zero,new Vector3(p.worldWidth,p.worldHeight,p.farClip*2));
            var root=new GameObject(name);root.transform.SetParent(layerRoot,false);root.transform.position=p.cameraPosition+p.cameraForward*((p.nearClip+p.farClip)*.5f);root.transform.rotation=Quaternion.LookRotation(p.cameraForward,p.cameraUp);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            return renderer;
        }
        public void SetOriginalColorDiagnostic(bool enabled)
        {
            OriginalColorDiagnostic=enabled;if(baseMaterial)baseMaterial.SetFloat("_DynamicShadow",enabled?0:.35f);if(frontMaterial)frontMaterial.SetFloat("_DynamicShadow",enabled?0:.35f);
            if(waterRenderer)waterRenderer.enabled=!enabled;if(owner)owner.SetTerrainFeaturesVisible(!enabled);
        }
        public void SetVisible(bool visible)
        {if(Active&&sceneCamera)sceneCamera.GetUniversalAdditionalCameraData().renderPostProcessing=visible?false:oldPostProcessing;}
        private void LateUpdate(){if(Active&&layerRoot&&layerRoot.gameObject.activeInHierarchy){if(waterMaterial&&owner)waterMaterial.SetFloat("_Motion",owner.ReducedMotion?0:1);ConstrainCamera();}}
        private void ConstrainCamera()
        {
            if(!sceneCamera||Projection==null)return;
            float size=sceneCamera.orthographicSize;
            sceneCamera.transform.position=ClampView(sceneCamera.transform.position,sceneCamera.aspect,ref size);sceneCamera.orthographicSize=size;
        }
        public Vector3 ClampView(Vector3 desiredCameraPosition,float aspect,ref float orthographicSize)
        {
            if(!Active||Projection==null)return desiredCameraPosition;
            var p=Projection;
            float maxSize=Mathf.Min(p.worldHeight*.5f,p.worldWidth*.5f/aspect);
            orthographicSize=Mathf.Min(orthographicSize,maxSize*.998f);
            float halfH=orthographicSize,halfW=halfH*aspect;
            Vector3 offset=desiredCameraPosition-p.cameraPosition;
            float x=Vector3.Dot(offset,p.cameraRight),y=Vector3.Dot(offset,p.cameraUp);
            float clampedX=Mathf.Clamp(x,-p.worldWidth*.5f+halfW,p.worldWidth*.5f-halfW),clampedY=Mathf.Clamp(y,-p.worldHeight*.5f+halfH,p.worldHeight*.5f-halfH);
            return desiredCameraPosition+(clampedX-x)*p.cameraRight+(clampedY-y)*p.cameraUp;
        }
        public void Release()
        {
            if(Active&&sceneCamera)sceneCamera.GetUniversalAdditionalCameraData().renderPostProcessing=oldPostProcessing;
            for(int i=0;i<hidden.Length;i++)if(hidden[i])hidden[i].enabled=hiddenEnabled[i];
            hidden=Array.Empty<Renderer>();hiddenEnabled=Array.Empty<bool>();
            if(layerRoot){layerRoot.gameObject.SetActive(false);Destroy(layerRoot.gameObject);}layerRoot=null;
            foreach(var resource in owned)if(resource)Destroy(resource);owned.Clear();
            Active=false;Projection=null;sceneCamera=null;baseMaterial=frontMaterial=waterMaterial=null;waterRenderer=null;owner=null;DepthValidatedCells=0;
        }
        private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        private static bool Finite(Vector3 value){return Finite(value.x)&&Finite(value.y)&&Finite(value.z);}
        private void OnDestroy(){Release();}
    }
}
