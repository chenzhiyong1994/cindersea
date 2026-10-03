using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dicebound.Presentation
{
    /// <summary>Original pixel sheets supply real animation frames. World movement and facing remain owned by TacticalStage.</summary>
    public sealed class TacticalActor : MonoBehaviour
    {
        public const float VisibleHeight=1.65f;
        public bool HasAnimationFrames => sheet!=null&&sheet.frames.Length==12&&sheet.frames.All(frame=>frame);
        public int FrameCount => sheet==null?0:sheet.frames.Length;
        public int RendererCount => body&&shadow?2:body?1:0;
        public bool BodyVisible => body&&body.enabled;
        public float HorizontalExtent => sheet==null?0:sheet.frames.Max(frame=>Mathf.Max(Mathf.Abs(frame.bounds.min.x),Mathf.Abs(frame.bounds.max.x)));
        public float FrameHeight => sheet==null?0:sheet.frames.Max(frame=>frame.bounds.max.y);
        public float StandingHeight => sheet==null?VisibleHeight:sheet.frames.Take(4).Max(frame=>frame.bounds.max.y);
        public float SecondsPerTile => sheetKey=="sunwheel"?.52f:sheetKey=="steamwarden"?.46f:.40f;
        // Compatibility alias for the existing stage. Pixel characters have no skeletal rig.
        public bool HasRig => HasAnimationFrames;
        public int BoneCount => 0;
        private static readonly Dictionary<string,SheetFrames> sheets=new Dictionary<string,SheetFrames>();
        private sealed class SheetFrames {public Mesh[] frames;public Rect[][] coverage;public Texture2D texture;public Rect[] regions;public Vector2[] pivots;public float pixelsPerUnit;public int references;}
        [Serializable] private sealed class SheetLayout {public int version=0;public string origin=null;public float pixelsPerUnit=0;public LayoutFrame[] frames=null;}
        [Serializable] private sealed class LayoutFrame {public int x=0,y=0,width=0,height=0;public float pivotX=0,pivotY=0;public int[] alphaSeeds=null;}
        private sealed class FrameRegion {public int minX,minY,maxX,maxY;public float anchorX,anchorY;}
        private sealed class PixelRun {public int left,right,bottom,top;}
        private SheetFrames sheet;
        private string sheetKey;
        private Transform billboard;
        private MeshRenderer body;
        private MeshFilter bodyMesh;
        private SpriteRenderer shadow;
        private Material bodyMaterial,shadowMaterial;
        private Sprite shadowSprite;
        private Texture2D shadowTexture;
        private bool posing,mirror;
        private int currentFrame;
        private float idleTime;
        private float impactElapsed,impactDuration,impactStrength,impactCompression;
        private bool impactReduced;
        private Color impactTint;
        private static readonly Color ImpactWhite=new Color(1,.97f,.91f,1);
        private static readonly Color ContactTint=new Color(.025f,.035f,.042f,.28f);
        private static readonly int ImpactColor=Shader.PropertyToID("_ImpactColor");
        private static readonly int ImpactMix=Shader.PropertyToID("_ImpactMix");
        private bool Reduced => TacticalDirector.Instance&&TacticalDirector.Instance.ReducedMotion;

        public void Build(string id,bool enemy)
        {
            Clear();
            if(string.IsNullOrEmpty(id))throw new ArgumentException("像素角色缺少资源 ID。",nameof(id));
            var shader=Shader.Find("Dicebound/TacticalSprite");
            if(!shader)throw new InvalidOperationException("缺少像素角色 Shader：Dicebound/TacticalSprite。请将其纳入原生构建。");
            sheet=Acquire(id);sheetKey=id;
            var visual=new GameObject(id+" · twelve-frame pixel character");visual.transform.SetParent(transform,false);billboard=visual.transform;
            bodyMesh=visual.AddComponent<MeshFilter>();bodyMesh.sharedMesh=sheet.frames[0];body=visual.AddComponent<MeshRenderer>();
            bodyMaterial=new Material(shader){name=id+" · pixel character material"};bodyMaterial.SetColor("_Color",Color.white);bodyMaterial.SetFloat("_Cutoff",.035f);bodyMaterial.SetFloat("_LightInfluence",.34f);bodyMaterial.SetFloat("_ZWrite",1);
            bodyMaterial.SetTexture("_MainTex",sheet.texture);
            body.sharedMaterial=bodyMaterial;body.shadowCastingMode=ShadowCastingMode.On;body.receiveShadows=true;body.sortingOrder=1;
            var contact=new GameObject("Pixel foot contact shadow");contact.transform.SetParent(transform,false);contact.transform.localPosition=new Vector3(0,.009f,0);contact.transform.localRotation=Quaternion.Euler(90,0,0);
            shadow=contact.AddComponent<SpriteRenderer>();shadowTexture=ContactTexture();shadowSprite=Sprite.Create(shadowTexture,new Rect(0,0,64,32),Vector2.one*.5f,64/.80f,0,SpriteMeshType.FullRect);shadow.sprite=shadowSprite;
            shadowMaterial=new Material(shader){name="pixel foot contact material",renderQueue=2990};shadowMaterial.SetColor("_Color",ContactTint);shadowMaterial.SetFloat("_Cutoff",.006f);shadowMaterial.SetFloat("_LightInfluence",0);shadowMaterial.SetFloat("_ZWrite",0);shadow.sharedMaterial=shadowMaterial;shadow.shadowCastingMode=ShadowCastingMode.Off;shadow.receiveShadows=false;shadow.sortingOrder=-5;
            var collider=GetComponent<CapsuleCollider>()??gameObject.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,.66f,0);collider.height=1.32f;collider.radius=.27f;
            ResetMotion();FaceCamera();Debug.Log("DICEBOUND_TACTICAL_PIXEL "+id+" frames="+FrameCount+" renderers="+RendererCount+" enemy="+enemy);
        }
        private static SheetFrames Acquire(string id)
        {
            if(sheets.TryGetValue(id,out var cached)){cached.references++;return cached;}
            var texture=Resources.Load<Texture2D>("Tactics/Sprites/"+id+"-sheet-v2");
            if(!texture)throw new InvalidOperationException("缺少自有 Q 版像素角色图集：Tactics/Sprites/"+id+"-sheet-v2.png。");
            if(!texture.isReadable)throw new InvalidOperationException("像素图集需要 Read/Write Enabled 以核对透明基线："+id);
            int width=texture.width/4,height=texture.height/3;
            if(width<16||height<24)throw new InvalidOperationException("像素图集尺寸不足，必须包含四列三行完整人物："+id);
            texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;texture.anisoLevel=0;
            var pixels=texture.GetPixels32();if(!pixels.Any(pixel=>pixel.a<12))throw new InvalidOperationException("像素图集必须透明，不能带矩形背景："+id);
            // Reviewed rectangles and explicit component seeds preserve whole weapons
            // while excluding a neighbor's cape inside an overlapping bounding rectangle.
            var metadata=Resources.Load<TextAsset>("Tactics/Sprites/"+id+"-layout-v2");
            if(!metadata)throw new InvalidOperationException("缺少 Q 版图集切帧配置："+id+"-layout-v2.json。");
            SheetLayout layout;try{layout=JsonUtility.FromJson<SheetLayout>(metadata.text);}catch(Exception e){throw new InvalidOperationException("像素图集 layout JSON 无法读取："+id,e);}
            if(layout==null||layout.version!=1||layout.origin!="top-left"||layout.frames==null||layout.frames.Length!=12||float.IsNaN(layout.pixelsPerUnit)||float.IsInfinity(layout.pixelsPerUnit)||layout.pixelsPerUnit<=0)throw new InvalidOperationException("像素图集 layout 需要 version=1、origin=top-left、有效统一缩放和十二帧："+id);
            var regions=new FrameRegion[12];float pixelsPerUnit=layout.pixelsPerUnit;
            for(int i=0;i<12;i++){var f=layout.frames[i];if(f==null||f.x<0||f.y<0||f.width<1||f.height<1||f.x>=texture.width||f.y>=texture.height||f.width>texture.width-f.x||f.height>texture.height-f.y||float.IsNaN(f.pivotX)||float.IsNaN(f.pivotY)||float.IsInfinity(f.pivotX)||float.IsInfinity(f.pivotY)||f.pivotX<0||f.pivotX>1||f.pivotY<0||f.pivotY>1)throw new InvalidOperationException("像素 layout 中的 Rect 或脚锚越界："+id+" 帧 "+i);
                int bottom=texture.height-f.y-f.height;regions[i]=new FrameRegion{minX=f.x,maxX=f.x+f.width-1,minY=bottom,maxY=bottom+f.height-1,anchorX=f.x+f.width*f.pivotX,anchorY=bottom+f.height*f.pivotY};}
            var ownership=LabelAlpha(id,layout,pixels,texture.width,texture.height);
            var result=new SheetFrames{frames=new Mesh[12],coverage=new Rect[12][],texture=texture,regions=new Rect[12],pivots=new Vector2[12],pixelsPerUnit=pixelsPerUnit,references=1};
            for(int index=0;index<12;index++){
                var r=regions[index];result.regions[index]=new Rect(r.minX,r.minY,r.maxX-r.minX+1,r.maxY-r.minY+1);result.pivots[index]=new Vector2(r.anchorX-r.minX,r.anchorY-r.minY);
                try{result.frames[index]=CreateFrameMesh(id+" Q frame "+index,r,index,pixels,ownership,texture.width,texture.height,pixelsPerUnit,out result.coverage[index]);}catch{foreach(var frame in result.frames)if(frame)DestroyOwned(frame);throw;}
            }
            sheets[id]=result;return result;
        }
        private static int[] LabelAlpha(string id,SheetLayout layout,Color32[] pixels,int width,int height)
        {
            var labels=new int[pixels.Length];var queue=new int[pixels.Length];
            for(int frame=0;frame<12;frame++){
                var seeds=layout.frames[frame].alphaSeeds;if(seeds==null||seeds.Length==0)throw new InvalidOperationException("Q 版切帧缺少明确透明主体种子："+id+" 帧 "+frame);
                foreach(int seed in seeds){
                    if(seed<0||seed>=pixels.Length)throw new InvalidOperationException("Q 版透明种子越界："+id);
                    int start=(height-1-seed/width)*width+seed%width;
                    if(pixels[start].a<20||labels[start]!=0&&labels[start]!=frame+1)throw new InvalidOperationException("Q 版透明种子为空或与另一帧相连："+id+" 帧 "+frame);
                    if(labels[start]!=0)continue;int read=0,write=0;queue[write++]=start;labels[start]=frame+1;
                    while(read<write){int index=queue[read++],x=index%width,y=index/width;
                        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=width||ny>=height)continue;int next=ny*width+nx;if(labels[next]==0&&pixels[next].a>=20){labels[next]=frame+1;queue[write++]=next;}}
                    }
                }
            }
            for(int i=0;i<pixels.Length;i++)if(pixels[i].a>=20){
                if(labels[i]==0)throw new InvalidOperationException("Q 版图集含未登记的透明主体，配置与 PNG 不符："+id);
                var frame=layout.frames[labels[i]-1];int x=i%width,y=height-1-i/width;
                if(x<frame.x||x>=frame.x+frame.width||y<frame.y||y>=frame.y+frame.height)throw new InvalidOperationException("Q 版切帧矩形截断了完整人物或附件："+id+" 帧 "+(labels[i]-1));
            }
            return labels;
        }
        private static Mesh CreateFrameMesh(string name,FrameRegion region,int frame,Color32[] pixels,int[] ownership,int width,int height,float scale,out Rect[] coverage)
        {
            var rectangles=new List<PixelRun>();var lastRuns=new Dictionary<long,PixelRun>();
            for(int y=region.minY;y<=region.maxY;y++){
                var currentRuns=new Dictionary<long,PixelRun>();int x=region.minX;
                while(x<=region.maxX){int pixel=y*width+x;if(pixels[pixel].a<20||ownership[pixel]!=frame+1){x++;continue;}
                    int left=x++;while(x<=region.maxX){pixel=y*width+x;if(pixels[pixel].a<20||ownership[pixel]!=frame+1)break;x++;}int right=x;long key=((long)left<<32)|(uint)right;
                    if(lastRuns.TryGetValue(key,out var run)&&run.top==y)run.top=y+1;else {run=new PixelRun{left=left,right=right,bottom=y,top=y+1};rectangles.Add(run);}currentRuns[key]=run;
                }lastRuns=currentRuns;
            }
            if(rectangles.Count==0)throw new InvalidOperationException("像素帧的透明主体为空："+name);
            if(rectangles.Count>16383)throw new InvalidOperationException("像素帧边缘过于零碎，无法生成稳定 Sprite 网格："+name);
            var vertices=new Vector3[rectangles.Count*4];var uv=new Vector2[vertices.Length];var colors=Enumerable.Repeat(Color.white,vertices.Length).ToArray();var triangles=new int[rectangles.Count*6];
            coverage=new Rect[rectangles.Count];
            // Explicit sprite mesh avoids Unity 6000.3's stale OverrideGeometry result and preserves original atlas bytes.
            for(int i=0;i<rectangles.Count;i++){var r=rectangles[i];int v=i*4,t=i*6;float left=(r.left-region.anchorX)/scale,right=(r.right-region.anchorX)/scale,bottom=(r.bottom-region.anchorY)/scale,top=(r.top-region.anchorY)/scale;vertices[v]=new Vector3(left,bottom,0);vertices[v+1]=new Vector3(left,top,0);vertices[v+2]=new Vector3(right,top,0);vertices[v+3]=new Vector3(right,bottom,0);uv[v]=new Vector2((float)r.left/width,(float)r.bottom/height);uv[v+1]=new Vector2((float)r.left/width,(float)r.top/height);uv[v+2]=new Vector2((float)r.right/width,(float)r.top/height);uv[v+3]=new Vector2((float)r.right/width,(float)r.bottom/height);triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;}
            for(int i=0;i<rectangles.Count;i++){int v=i*4;coverage[i]=Rect.MinMaxRect(vertices[v].x,vertices[v].y,vertices[v+2].x,vertices[v+2].y);}
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt16};try{mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;}catch{DestroyOwned(mesh);throw;}
        }
        public void Pose(string action,float progress)
        {
            if(!HasAnimationFrames)throw new InvalidOperationException("像素人物动作尚未配置。");
            if(action!="idle"&&action!="run"&&action!="attack"&&action!="cast"&&action!="hit")throw new InvalidOperationException("未知像素动作："+action);
            posing=action!="idle";float p=Mathf.Clamp01(progress);int frame=Mathf.Min(3,Mathf.FloorToInt(p*4));
            // A brief anticipation, quick strike and readable contact frame give the
            // existing four original drawings weight without stretching the sprite.
            if(action=="attack")frame=p<.16f?0:p<.32f?1:p<.70f?2:3;
            else if(action=="cast")frame=p<.22f?0:p<.42f?1:p<.76f?2:3;
            currentFrame=(action=="run"?4:action=="attack"||action=="cast"?8:0)+(action=="hit"?0:Reduced&&action=="idle"?0:frame);bodyMesh.sharedMesh=sheet.frames[currentFrame];
            if(billboard)billboard.localPosition=Vector3.zero;
            FaceCamera();
        }
        public void Idle()
        {
            if(!HasAnimationFrames)return;posing=false;if(billboard)billboard.localPosition=Vector3.zero;
            currentFrame=Reduced?0:Mathf.FloorToInt(idleTime*3.25f)%4;bodyMesh.sharedMesh=sheet.frames[currentFrame];FaceCamera();
        }
        /// <summary>Stage calls this once at contact. Strength 1 is ordinary, 1.35 is a heavy hit.</summary>
        public void HitFeedback(float strength,Color tint,bool reduced)
        {
            if(!bodyMaterial)return;
            impactStrength=Mathf.Clamp(float.IsNaN(strength)||float.IsInfinity(strength)?1:strength,.4f,1.6f);
            impactReduced=reduced;impactDuration=reduced||Reduced?.12f:.18f;impactElapsed=0;
            impactTint=Color.Lerp(ImpactWhite,tint,.24f);impactTint.a=1;
            ApplyImpact();FaceCamera();
        }
        // Only opacity changes during defeat; root motion remains owned by Stage.
        public void FadeOut(float progress)
        {
            float opacity=1-Mathf.Clamp01(progress);
            if(bodyMaterial)bodyMaterial.SetColor("_Color",new Color(1,1,1,opacity));
            if(shadowMaterial){var tint=ContactTint;tint.a*=opacity;shadowMaterial.SetColor("_Color",tint);}
        }
        public void ResetMotion()
        {
            idleTime=0;posing=false;impactElapsed=impactDuration=impactCompression=0;impactReduced=false;
            if(bodyMaterial){bodyMaterial.SetColor("_Color",Color.white);bodyMaterial.SetColor(ImpactColor,ImpactWhite);bodyMaterial.SetFloat(ImpactMix,0);}
            if(shadowMaterial)shadowMaterial.SetColor("_Color",ContactTint);
            Idle();
        }
        private void ApplyImpact()
        {
            bool reduced=impactReduced||Reduced;
            float hold=reduced?.018f:.028f;
            float fade=Mathf.Clamp01((impactElapsed-hold)/Mathf.Max(.01f,impactDuration-hold));
            float mix=Mathf.Pow(1-fade,2.1f)*(reduced?.46f:Mathf.Lerp(.69f,.94f,(impactStrength-.4f)/1.2f));
            bodyMaterial.SetColor(ImpactColor,Color.Lerp(ImpactWhite,impactTint,Mathf.SmoothStep(0,1,Mathf.Clamp01((impactElapsed-.04f)/.09f))));
            bodyMaterial.SetFloat(ImpactMix,mix);
            // Frame pivots are the foot anchor. Scaling the visual child keeps that
            // anchor and its contact shadow fixed; reduced motion never deforms it.
            float recoil=Mathf.Clamp01(impactElapsed/.09f);
            impactCompression=reduced?0:Mathf.Sin(recoil*Mathf.PI)*.016f*impactStrength;
        }
        private void TickImpact()
        {
            if(impactDuration<=0)return;
            impactElapsed+=Time.unscaledDeltaTime;
            if(impactElapsed>=impactDuration){impactDuration=impactCompression=0;bodyMaterial.SetFloat(ImpactMix,0);}
            else ApplyImpact();
        }
        private void Update()
        {
            if(!HasAnimationFrames)return;TickImpact();if(posing)return;if(!Reduced&&Application.isFocused)idleTime+=Time.deltaTime;Idle();
        }
        private void LateUpdate() {FaceCamera();}
        private void FaceCamera()
        {
            if(!billboard||!body)return;var camera=Camera.main;if(!camera)return;
            // An orthographic scene has parallel view rays: every sprite uses the exact same
            // camera basis, preserving its source aspect without per-cell yaw or foreshortening.
            billboard.rotation=camera.transform.rotation;
            float facing=Vector3.Dot(transform.forward,camera.transform.right);
            if(Mathf.Abs(facing)>.04f)mirror=facing<0;
            // Mirror only the visual child: this custom shader does not depend on Unity's _Flip property.
            float compression=Reduced?0:impactCompression;
            billboard.localScale=new Vector3((mirror?-1:1)*(1+compression*.65f),1-compression,1);
        }
        public Vector3 VisualPoint(float height)
        {FaceCamera();return billboard?billboard.TransformPoint(new Vector3(0,height,0)):transform.position+Vector3.up*height;}
        public void SetBodyVisible(bool visible) {if(body)body.enabled=visible;}
        public bool TryHit(Ray ray,out float distance)
        {
            if(!TryHitPlane(ray,out distance,out var local))return false;
            var bounds=sheet.frames[currentFrame].bounds;if(local.x<bounds.min.x||local.x>bounds.max.x||local.y<bounds.min.y||local.y>bounds.max.y){distance=float.PositiveInfinity;return false;}
            if(!sheet.coverage[currentFrame].Any(rect=>rect.Contains(local))){distance=float.PositiveInfinity;return false;}
            return true;
        }
        // Picking probes must not rely on a transparent hole in one idle frame.
        // This reads the same four silhouettes without changing the displayed pose.
        public bool TryHitIdleFrames(Ray ray,out float distance,out bool everyFrame)
        {
            everyFrame=false;if(!TryHitPlane(ray,out distance,out var local))return false;
            int hits=0;for(int frame=0;frame<4;frame++){var bounds=sheet.frames[frame].bounds;if(local.x>=bounds.min.x&&local.x<=bounds.max.x&&local.y>=bounds.min.y&&local.y<=bounds.max.y&&sheet.coverage[frame].Any(rect=>rect.Contains(local)))hits++;}
            everyFrame=hits==4;if(hits==0){distance=float.PositiveInfinity;return false;}return true;
        }
        private bool TryHitPlane(Ray ray,out float distance,out Vector2 point)
        {
            distance=float.PositiveInfinity;point=Vector2.zero;if(!HasAnimationFrames||!gameObject.activeInHierarchy||!billboard||!BodyVisible)return false;FaceCamera();
            Vector3 normal=billboard.forward;float denominator=Vector3.Dot(normal,ray.direction);if(Mathf.Abs(denominator)<.00001f)return false;
            float candidate=Vector3.Dot(billboard.position-ray.origin,normal)/denominator;if(candidate<0)return false;
            Vector3 local=billboard.InverseTransformPoint(ray.GetPoint(candidate));point=new Vector2(local.x,local.y);distance=candidate;return true;
        }
        private static Texture2D ContactTexture()
        {
            var texture=new Texture2D(64,32,TextureFormat.RGBA32,false){name="原创脚底接地椭圆",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};var colors=new Color[2048];
            for(int y=0;y<32;y++)for(int x=0;x<64;x++){float px=(x-31.5f)/31.5f,py=(y-15.5f)/15.5f,r=px*px+py*py;colors[y*64+x]=new Color(1,1,1,Mathf.Pow(Mathf.Max(0,1-r),1.8f));}texture.SetPixels(colors);texture.Apply(false,true);return texture;
        }
        private void Clear()
        {
            if(billboard){billboard.gameObject.SetActive(false);Destroy(billboard.gameObject);}if(shadow){shadow.gameObject.SetActive(false);Destroy(shadow.gameObject);}
            if(bodyMaterial)DestroyOwned(bodyMaterial);if(shadowMaterial)DestroyOwned(shadowMaterial);if(shadowSprite)DestroyOwned(shadowSprite);if(shadowTexture)DestroyOwned(shadowTexture);
            if(sheet!=null&&--sheet.references==0){foreach(var frame in sheet.frames)if(frame)DestroyOwned(frame);sheets.Remove(sheetKey);}
            sheet=null;sheetKey=null;billboard=null;body=null;bodyMesh=null;shadow=null;bodyMaterial=null;shadowMaterial=null;shadowSprite=null;shadowTexture=null;posing=false;impactElapsed=impactDuration=impactCompression=0;impactReduced=false;
        }
        private static void DestroyOwned(UnityEngine.Object value) {if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        private void OnDestroy() {Clear();}
    }
}
