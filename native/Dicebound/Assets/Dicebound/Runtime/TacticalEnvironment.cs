using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dicebound.Presentation
{
    public sealed class TacticalTileHit : MonoBehaviour { public int X,Y; }
    public sealed class TacticalObjectMarker : MonoBehaviour { public string Id; public int X,Y; }

    /// <summary>
    /// Project-authored architectural meshes, authored in metres at runtime. Existing Poly Haven
    /// CC0 PBR surfaces and lantern/stone meshes retain their existing source manifests. Geometry
    /// is presentation only: terrain and object positions are read from the immutable rules state.
    /// </summary>
    public sealed class TacticalEnvironment : MonoBehaviour, ITacticalArtGuideExporter
    {
        public const float TileSize=1.25f;
        public int BoardWidth => state==null?8:TacticalRules.BoardWidth(state);
        public int BoardHeight => state==null?8:TacticalRules.BoardHeight(state);
        public float HalfWidth => BoardWidth*TileSize*.5f;
        public float HalfDepth => BoardHeight*TileSize*.5f;
        public int SceneModelCount {get{return geometry?geometry.GetComponentsInChildren<Renderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy):0;}}
        public int ArchitecturalPartCount {get;private set;}
        public bool ReducedMotion;
        public bool PaintedActive => painted && painted.Active;
        public string PaintedResourceInfo => painted ? painted.ResourceInfo : "procedural fallback";
        public TacticalPaintedEnvironment PaintedScene => painted;
        private TacticalPaintedEnvironment painted;
        private TacticalTerrainFeatures terrainFeatures;
        public int TerrainFeatureCellCount => terrainFeatures ? terrainFeatures.FeatureCellCount : 0;
        public void SetTerrainFeaturesVisible(bool visible){if(terrainFeatures)terrainFeatures.SetVisible(visible);}
        internal Transform StaticGeometry => geometry;
        internal Transform DynamicObjects => objectsRoot;
        internal Renderer[] StaticRenderers => geometry ? geometry.GetComponentsInChildren<Renderer>().Where(r=>(!objectsRoot||!r.transform.IsChildOf(objectsRoot))&&(!painted||!painted.VisualRoot||!r.transform.IsChildOf(painted.VisualRoot))).ToArray() : Array.Empty<Renderer>();
        public System.Collections.IEnumerator ExportGuide(TacticalStage stage,TacticalState value,string directory)
        {return TacticalPaintedEnvironmentGuide.Export(this,stage,value,directory);}
        private TacticalState state;
        private Transform geometry,objectsRoot;
        private Material stone,stoneWarm,stoneCool,masonry,jade,bronze,iron,wood,cloth,paper,water,gapWater,ember,fog,lanternSurface,shadowWash,shadowTrace,wetWash,wetTrace,backdrop,contactShadow;
        private Material roof,soil,mortar,moss,leaf,leafLight,leafDark,flower,waterFoam,banner;
        private readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        private readonly Dictionary<string,GameObject> objects=new Dictionary<string,GameObject>();
        private readonly List<ParticleSystem> mist=new List<ParticleSystem>();
        private readonly List<Mesh> transientMeshes=new List<Mesh>();
        private readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
        private bool mistPaused;
        private HashSet<(Material material,int x,int z)> surfaceAccentBatches;

        public void Build(TacticalState value) {Build(value,transform);}
        public void Build(TacticalState value,Transform parent)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));state=value;
            painted=GetComponent<TacticalPaintedEnvironment>();
            if(painted)painted.Release();
            if(terrainFeatures)terrainFeatures.Release();
            if(geometry){geometry.gameObject.SetActive(false);Destroy(geometry.gameObject);}
            foreach(var mesh in transientMeshes)if(mesh)Destroy(mesh);transientMeshes.Clear();
            geometry=new GameObject("曜京 · "+TacticalStory.Stage(value.chapter).location).transform;geometry.SetParent(parent?parent:transform,false);
            objects.Clear();mist.Clear();mistPaused=false;Materials();
            Vector3 outletA=geometry.TransformPoint(new Vector3(HalfWidth*.24f,-.78f,-HalfDepth-.82f)),outletB=geometry.TransformPoint(new Vector3(-HalfWidth*.24f,-.78f,-HalfDepth-.82f));
            water.SetVector("_RippleOrigins",new Vector4(outletA.x,outletA.z,outletB.x,outletB.z));water.SetFloat("_Motion",ReducedMotion?0:1);
            var ground=Visuals.MeshObject("曜京水岸延续地面",geometry,GroundPlane(96,96),backdrop);ground.transform.localPosition=new Vector3(0,-2.15f,0);
            var contact=Visuals.MeshObject("石岸与水渠下接触暗部",geometry,GroundPlane(HalfWidth*2+9,HalfDepth*2+9),contactShadow);contact.transform.localPosition=new Vector3(0,-2.143f,0);
            // Foundations are individual masonry courses, leaving real openings below gaps
            // and bridge spans. The board is a place in the city, not a single floating tray.
            Foundations();CityMasses();Paving();Canal();
            foreach(var cell in value.terrain.Where(c=>c.kind=="wall"||c.kind=="gap"))Terrain(cell);
            District(value.chapter);Waterfront();Planting(value.chapter);NearCity();StreetLightPools();SurfaceAccents();
            CombineStatic();
            StartCoroutine(LightingEvidence());
            objectsRoot=new GameObject("可操作的实物").transform;objectsRoot.SetParent(geometry,false);RefreshObjects(value);
            if(!painted)painted=gameObject.AddComponent<TacticalPaintedEnvironment>();
            painted.Install(this,value,Camera.main);
            if(!terrainFeatures)terrainFeatures=gameObject.AddComponent<TacticalTerrainFeatures>();
            terrainFeatures.Build(this,value);
        }
        public static Vector3 CellPosition(int x,int y) {return CellPosition(x,y,8,8);}
        public static Vector3 CellPosition(int x,int y,int width,int height) {return new Vector3((x-(width-1)*.5f)*TileSize,0,(y-(height-1)*.5f)*TileSize);}
        private Vector3 BoardCell(int x,int y) {return CellPosition(x,y,BoardWidth,BoardHeight);}
        public float HeightAt(int x,int y) {return state!=null&&TacticalRules.TerrainAt(state,x,y)=="high"?.25f:0;}
        public Vector3 CellWorld(int x,int y) {return geometry?geometry.TransformPoint(BoardCell(x,y)+Vector3.up*HeightAt(x,y)):BoardCell(x,y);}
        public Transform ObjectVisual(string id) {return id!=null&&objects.TryGetValue(id,out var obj)?obj.transform:null;}
        public void SetShadowDebug(bool enabled)
        {foreach(var material in new[]{stone,stoneWarm,stoneCool,masonry,mortar,jade,wood,roof,soil,moss,leaf,leafLight,leafDark,flower})if(material)material.SetFloat("_ShadowDebug",enabled?1:0);}
        public void SetVisible(bool visible) {if(geometry)geometry.gameObject.SetActive(visible);if(painted)painted.SetVisible(visible);}
        public bool CellFromHit(Ray ray,out int x,out int y)
        {
            foreach(var hit in Physics.RaycastAll(ray,100).OrderBy(h=>h.distance)){
                var tile=hit.collider.GetComponent<TacticalTileHit>();if(tile&&tile.transform.IsChildOf(geometry)){x=tile.X;y=tile.Y;return true;}
                var obj=hit.collider.GetComponentInParent<TacticalObjectMarker>();if(obj&&obj.transform.IsChildOf(geometry)){x=obj.X;y=obj.Y;return true;}
            }
            x=y=-1;return false;
        }
        public void RefreshObjects(TacticalState value)
        {
            state=value;if(!objectsRoot)return;
            foreach(string id in objects.Keys.ToArray())if(!value.objects.Any(o=>o.id==id)){var old=objects[id];old.SetActive(false);Destroy(old);objects.Remove(id);}
            foreach(var item in value.objects){
                if(objects.TryGetValue(item.id,out var existing)){existing.transform.localPosition=BoardCell(item.x,item.y)+Vector3.up*HeightAt(item.x,item.y);var existingMarker=existing.GetComponent<TacticalObjectMarker>();existingMarker.X=item.x;existingMarker.Y=item.y;continue;}
                var root=new GameObject(item.kind=="valve"?"青铜蒸汽阀 · "+item.id:item.kind=="canister"?"封口余热罐 · "+item.id:"榫接药箱 · "+item.id);root.transform.SetParent(objectsRoot,false);root.transform.localPosition=BoardCell(item.x,item.y)+Vector3.up*HeightAt(item.x,item.y);
                var marker=root.AddComponent<TacticalObjectMarker>();marker.Id=item.id;marker.X=item.x;marker.Y=item.y;
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.34f,0);collider.size=item.kind=="canister"?new Vector3(.60f,.76f,.60f):new Vector3(.88f,.68f,.70f);
                if(item.kind=="valve")SteamValve(root.transform);else if(item.kind=="canister")Canister(root.transform);else Crate(root.transform);objects[item.id]=root;
            }
        }
        private void Materials()
        {
            if(stone)return;
            stone=PaintedSurface("浅暖手绘石路",new Color(.97f,.97f,.93f),"yaojing-painted-stone-v1",.23f);
            stone.SetFloat("_SunlitGain",1.85f);
            stoneWarm=Own(new Material(stone));stoneWarm.SetColor("_BaseColor",new Color(1,.97f,.90f));
            stoneCool=Own(new Material(stone));stoneCool.SetColor("_BaseColor",new Color(.89f,.95f,.98f));
            masonry=PaintedSurface("厚重手绘岸墙",new Color(.84f,.88f,.85f),"yaojing-painted-stone-v1",.16f);
            masonry.SetFloat("_SunlitGain",1.68f);
            mortar=PaintedSurface("填平石缝的冷灰石浆",new Color(.69f,.73f,.72f),"yaojing-painted-stone-v1",.08f);mortar.SetFloat("_Relief",.006f);mortar.SetFloat("_Wear",.06f);
            jade=PaintedSurface("青玉与铜色石饰",new Color(.34f,.49f,.43f),"yaojing-painted-stone-v1",.28f);
            jade.SetFloat("_SunlitGain",1.30f);
            bronze=Material(new Color(.60f,.42f,.20f),.64f,.32f);
            wood=PaintedSurface("厚木板与榫接梁",new Color(.90f,.84f,.73f),"yaojing-painted-wood-v1",.21f);
            cloth=Surface("rough_linen",new Color(.065f,.073f,.092f));cloth.SetFloat("_Cull",0);cloth.SetFloat("_Smoothness",.08f);
            iron=Material(new Color(.19f,.25f,.25f),.36f,.24f);paper=Material(new Color(.88f,.80f,.63f),0,.12f);
            roof=PaintedSurface("青灰手绘屋瓦",new Color(.30f,.37f,.36f),"yaojing-painted-stone-v1",.18f);
            roof.SetFloat("_SunlitGain",1.12f);
            soil=PaintedSurface("岸边湿土",new Color(.29f,.34f,.27f),"yaojing-painted-stone-v1",.05f);
            moss=PaintedSurface("苔生石缝",new Color(.40f,.49f,.28f),"yaojing-painted-stone-v1",.07f);
            leaf=LeafSurface("成团深绿阔叶",new Color(.34f,.50f,.24f));leafLight=LeafSurface("日照黄绿新叶",new Color(.59f,.66f,.32f));leafDark=LeafSurface("植丛冷绿内叶",new Color(.18f,.32f,.22f));
            flower=LeafSurface("暖黄色野花",new Color(.94f,.69f,.18f));banner=Material(new Color(.52f,.14f,.11f),0,.04f);banner.SetFloat("_Cull",0);
            var waterShader=Shader.Find("Dicebound/TacticalWater");if(!waterShader)throw new InvalidOperationException("缺少原创水面 Shader：Dicebound/TacticalWater。");
            water=Own(new Material(waterShader){name="清澈青碧渠水与细分白沫"});water.SetColor("_DeepColor",new Color(.025f,.29f,.31f));water.SetColor("_ShallowColor",new Color(.105f,.48f,.43f));water.SetColor("_FoamColor",new Color(.82f,.96f,.95f));water.SetVector("_Flow",new Vector4(.16f,.09f,.34f,0));
            waterFoam=Material(new Color(.77f,.91f,.87f,.80f),0,.08f,true);
            gapWater=water;
            backdrop=Material(Color.white,0,.10f);backdrop.name="深青哑光地面";backdrop.SetTexture("_BaseMap",BackdropTexture());
            // The dark stage floor receives light and shadows, but no white studio sheen.
            backdrop.SetFloat("_SpecularHighlights",0);backdrop.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            backdrop.SetFloat("_EnvironmentReflections",0);backdrop.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            contactShadow=Material(new Color(.015f,.025f,.035f,.25f),0,0,true);contactShadow.SetTexture("_BaseMap",MistTexture());
            shadowWash=Material(new Color(.29f,.27f,.39f,.24f),0,0,true);shadowWash.SetTexture("_BaseMap",GroundInkTexture(false));
            shadowTrace=Material(new Color(.34f,.31f,.43f,.23f),0,0,true);
            wetWash=Material(new Color(.23f,.42f,.46f,.19f),0,0,true);wetWash.SetTexture("_BaseMap",GroundInkTexture(true));
            wetTrace=Material(new Color(.40f,.57f,.59f,.28f),0,0,true);
            ember=Material(new Color(.99f,.53f,.20f),.10f,.31f);ember.EnableKeyword("_EMISSION");ember.SetColor("_EmissionColor",new Color(2.5f,.88f,.24f));
            fog=Material(new Color(.61f,.75f,.79f,.25f),0,0,true);fog.SetTexture("_BaseMap",MistTexture());
            lanternSurface=Surface("wooden_lantern_01",new Color(.60f,.49f,.31f));
            foreach(var material in new[]{stone,stoneWarm,stoneCool,masonry,jade,bronze,wood,cloth,iron,paper,roof,soil,moss,leaf,leafLight,leafDark,flower,banner})material.enableInstancing=true;
        }
        private Material PaintedSurface(string name,Color tint,string textureId,float smoothness)
        {
            var shader=Shader.Find("Dicebound/PainterlyStone");if(!shader)throw new InvalidOperationException("缺少手绘场景表面 Shader：Dicebound/PainterlyStone。");
            var result=Own(new Material(shader){name=name});result.SetColor("_BaseColor",tint);result.SetFloat("_Smoothness",smoothness);
            result.SetFloat("_SunlitGain",textureId.Contains("wood")?1.22f:1.50f);
            result.SetFloat("_Relief",textureId.Contains("wood")?.021f:.042f);result.SetFloat("_Moss",textureId.Contains("wood")?.02f:.21f);result.SetFloat("_Wear",textureId.Contains("wood")?.12f:.45f);
            string surfaceId=textureId.Contains("stone")?"yaojing-painted-stone-relief-v2":textureId;
            var texture=Resources.Load<Texture2D>("Tactics/Textures/"+surfaceId);
            if(texture)result.SetTexture("_BaseMap",texture);
            else {
                // Existing originals remain usable while the new hand-painted maps import.
                var existing=Resources.Load<Texture2D>("Tactics/Textures/yaojing-stone-v1");
                if(existing&&textureId.Contains("stone"))result.SetTexture("_BaseMap",existing);
            }
            return result;
        }
        private Material LeafSurface(string name,Color color)
        {
            var shader=Shader.Find("Dicebound/PainterlyStone");if(!shader)throw new InvalidOperationException("缺少手绘植物材质 Shader。");
            var result=Own(new Material(shader){name=name});result.SetColor("_BaseColor",color);result.SetFloat("_SunlitGain",1.20f);result.SetFloat("_Smoothness",.12f);result.SetFloat("_Relief",.006f);result.SetFloat("_Moss",0);result.SetFloat("_Wear",0);return result;
        }
        private Material StyledSurface(string name,Color tint,float smoothness,float metallic,string pattern,string pbrSource,string mask,float normalStrength,float coat=0)
        {
            // Broad, restrained grain reads at the battle camera distance. The original
            // source PNGs remain untouched; these small mipmapped maps are authored here.
            var result=Material(tint,metallic,smoothness);result.name=name;
            if(coat>0){result.shader=Shader.Find("Universal Render Pipeline/Complex Lit");result.SetFloat("_ClearCoatMask",coat);result.SetFloat("_ClearCoatSmoothness",.78f);result.EnableKeyword("_CLEARCOAT");}
            const int size=128;var texture=Own(new Texture2D(size,size,TextureFormat.RGBA32,true){name=name+" · 原创柔纹",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4});
            var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                float u=(x+.5f)/size,v=(y+.5f)/size,coarse=Mathf.PerlinNoise(u*3.1f+2.3f,v*3.1f+5.7f),fine=Mathf.PerlinNoise(u*25+9,v*25+3);
                float grain=.958f+.025f*coarse+.012f*fine;
                if(pattern=="wood")grain=.943f+.025f*coarse+.024f*Mathf.Sin(u*14+coarse*.8f);
                else if(pattern=="jade")grain=.950f+.035f*coarse+.009f*Mathf.Sin(v*5+coarse*2);
                else if(pattern=="bronze")grain=.960f+.028f*coarse+.009f*fine;
                pixels[y*size+x]=new Color(grain,grain,grain,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);result.SetTexture("_BaseMap",texture);
            var normal=Resources.Load<Texture2D>("Environment/"+pbrSource+"/normal");
            var gloss=Resources.Load<Texture2D>("Tactics/Environment/Polish/"+mask);
            if(!normal||!gloss)throw new InvalidOperationException("Missing tactical PBR surface: "+pbrSource+" / "+mask);
            result.SetTexture("_BumpMap",normal);result.SetFloat("_BumpScale",normalStrength);result.EnableKeyword("_NORMALMAP");
            result.SetTexture("_MetallicGlossMap",gloss);result.EnableKeyword("_METALLICSPECGLOSSMAP");
            result.SetFloat("_SmoothnessTextureChannel",0);result.SetFloat("_EnvironmentReflections",1);result.SetFloat("_SpecularHighlights",1);
            // Model UV atlases contain hardware/blades as well as their named material.
            // Wood therefore keeps our continuous warm grain, never the entire saber atlas.
            return result;
        }
        private Texture2D BackdropTexture()
        {
            const int size=256;var texture=Own(new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="原创深青柔光场地 · 线性色",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear});
            var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                float u=(x+.5f)/size-.5f,v=(y+.5f)/size-.5f;
                float cool=Mathf.Exp(-((u+.13f)*(u+.13f)*24+(v-.05f)*(v-.05f)*42));
                float warm=Mathf.Exp(-((u-.17f)*(u-.17f)*42+(v+.16f)*(v+.16f)*26));
                pixels[y*size+x]=new Color(.012f+.018f*cool+.010f*warm,.032f+.060f*cool+.015f*warm,.046f+.060f*cool+.008f*warm,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);return texture;
        }
        private Material Surface(string id,Color tint,string optionalTexture=null)
        {
            var source=Resources.Load<Material>("Environment/"+id+"/surface");var result=source?Own(new Material(source)):Material(tint);
            result.SetColor("_BaseColor",tint);
            if(optionalTexture!=null){var texture=Resources.Load<Texture2D>("Tactics/Textures/"+optionalTexture);if(texture){result.SetTexture("_BaseMap",texture);result.SetColor("_BaseColor",Color.white);}}
            return result;
        }
        private T Own<T>(T resource) where T:UnityEngine.Object {resources.Add(resource);return resource;}
        private Material Material(Color color,float metallic=0,float smoothness=.35f,bool unlit=false)
        {
            var material=Own(new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")));
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);
            if(color.a<1){material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_Cull",(int)CullMode.Off);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;}
            return material;
        }
        private bool IsBridgeDeck(int x,int y)
        {return state.chapter==2&&x>=BoardWidth/2-2&&x<=BoardWidth/2+1&&TacticalRules.TerrainAt(state,x,y)!="gap";}

        private void Foundations()
        {
            for(int y=0;y<BoardHeight;y++)for(int x=0;x<BoardWidth;x++){
                if(TacticalRules.TerrainAt(state,x,y)=="gap")continue;
                Vector3 p=BoardCell(x,y);
                if(IsBridgeDeck(x,y)){
                    if(y==0||y==BoardHeight-1||!IsBridgeDeck(x,Mathf.Clamp(y-1,0,BoardHeight-1)))
                        Block("桥拱承重石墩",geometry,p+new Vector3(0,-.77f,0),new Vector3(.54f,1.38f,.74f),masonry,x+y*7,.10f);
                    continue;
                }
                // Recessed mortar joins the paving instead of exposing black crevices at
                // every tile. Gaps and timber spans deliberately have no stone infill.
                Block("连续灰浆落脚基底",geometry,p+new Vector3(0,HeightAt(x,y)-.070f,0),new Vector3(1.25f,.12f,1.25f),mortar,x+y,.006f);
                if(y==0)for(int course=0;course<3;course++)
                    Block("前渠厚岸石层",geometry,p+new Vector3(0,-.31f-course*.38f,0),new Vector3(1.25f,.375f,1.25f),masonry,x+course*3,.08f);
                else Block("落脚石路厚基",geometry,p+new Vector3(0,-.44f,0),new Vector3(1.25f,.74f,1.25f),masonry,x+y*3,.045f);
            }
        }

        private void CityMasses()
        {
            // The playable paving belongs to one continuous city street. There is no
            // all-round moat or exposed model base separating it from the district.
            for(int side=-1;side<=1;side+=2){
                float near=side*(HalfWidth+1.98f);
                Block("与战场相连的整条岸街",geometry,new Vector3(near,-.59f,1.3f),new Vector3(4.0f,1.14f,HalfDepth*2+3.8f),masonry,side+5,.09f);
                StreetPaving(new Vector3(near,0,1.3f),3.98f,HalfDepth*2+3.8f,side+23);
                // A narrow street remains at board level. Its outside is an ascending
                // stone bank, not a second expanse of identical flat tactical paving.
                for(int terrace=0;terrace<3;terrace++){
                    float top=.61f+terrace*.22f,z=-HalfDepth+1.68f+terrace*4.30f;
                    Block("两层岸街的升高大石台",geometry,new Vector3(side*(HalfWidth+2.66f),(top-1.20f)*.5f,z),new Vector3(2.90f,top+1.20f,3.94f),masonry,side+terrace+12,.15f);
                    StreetPaving(new Vector3(side*(HalfWidth+2.66f),top,z),2.88f,3.92f,side+terrace+69);
                    CourtyardWall(new Vector3(side*(HalfWidth+1.28f),0,z+.1f),2.95f,top+.04f,true,side+terrace+21);
                    for(int step=0;step<4;step++){
                        float height=top*(step+1)/4;
                        Block("侧街升岸宽石阶",geometry,new Vector3(side*(HalfWidth+1.86f+step*.22f),height*.5f,z-2.20f),new Vector3(.36f,height,1.24f),stone,terrace+step,.065f);
                    }
                }
                for(int district=0;district<3;district++){
                    float x=side*(HalfWidth+6.35f),z=-HalfDepth+2.0f+district*6.3f;
                    Block("延伸到画外的连续城区地层",geometry,new Vector3(x+side*4,-.69f,z),new Vector3(13.1f,1.40f,6.5f),masonry,district+4,.12f);
                    StreetPaving(new Vector3(x,.025f,z),5.07f,6.47f,district+35);
                    if(district>0)CourtyardWall(new Vector3(side*(HalfWidth+4.65f),0,z+2.5f),2.20f,1.30f,true,district+side+8);
                }
            }
            // Raised back streets have broad stairs, gateway walls and substantial rear
            // buildings, rather than a row of detached flat flags on a blank tabletop.
            for(int block=0;block<3;block++){
                float x=(block-1)*(HalfWidth*.94f),top=state.chapter==3?(block==1?.38f:.24f):(block==0?.88f:block==1?1.16f:.72f);
                Block("后街连贯上层台地",geometry,new Vector3(x,top-.66f,HalfDepth+4.23f),new Vector3(HalfWidth*.99f,1.32f,8.5f),masonry,block+5,.12f);
                StreetPaving(new Vector3(x,top,HalfDepth+4.23f),HalfWidth*.99f-.01f,8.45f,block+51);
                for(int step=0;step<8;step++){
                    float height=top*(step+1)/8f;
                    Block("宽石阶真实坡切踏面",geometry,new Vector3(x,height*.5f,HalfDepth+.22f+step*.22f),new Vector3(HalfWidth*.69f,height,.36f),stone,step+block,.035f);
                }
            }
            // Front banks continue beyond the camera, with a deep open canal between.
            // The nearest structures stay low so all tactical cells and sprites are visible.
            for(int side=-1;side<=1;side+=2){
                float x=side*(HalfWidth*.60f);
                Block("前街延入画外的厚石地层",geometry,new Vector3(x,-.80f,-HalfDepth-14),new Vector3(HalfWidth*3.0f,1.6f,20),masonry,side+6,.16f);
                StreetPaving(new Vector3(x,0,-HalfDepth-6.2f),HalfWidth*1.42f-.04f,3.2f,side+75);
                for(int section=0;section<3;section++)CourtyardWall(new Vector3(side*(HalfWidth*.22f+section*2.02f),-.08f,-HalfDepth-4.62f),1.62f,.58f,false,section+11);
            }
        }
        private void StreetPaving(Vector3 center,float width,float depth,int variant)
        {
            int rows=Mathf.CeilToInt(depth/1.19f);float rowDepth=depth/rows;
            for(int row=0;row<rows;row++){
                float left=-width*.5f,remain=width;int flag=0;
                while(remain>.025f){
                    float size=flag==0&&row%2==0?.68f:1.24f+Mathf.Floor(TacticalSceneryMesh.Noise(variant+row,flag+18)*5)*.18f;
                    size=Mathf.Min(size,remain);
                    var mat=(variant+row+flag)%8==0?stoneCool:(row+flag)%11==0?stoneWarm:stone;
                    Block("街巷不齐宽料铺石",geometry,center+new Vector3(left+size*.5f,-.072f,(row-(rows-1)*.5f)*rowDepth),new Vector3(size-.005f,.144f,rowDepth-.005f),mat,variant+row*3+flag,.023f);
                    left+=size;remain-=size;flag++;
                }
            }
        }
        private void NearCity()
        {
            // All these masses are beyond the immutable board's outer edges. They form
            // the nearby street and camera foreground, never a false tactical blocker.
            for(int side=-1;side<=1;side+=2){
                float x=side*(HalfWidth+2.58f);
                CourtyardWall(new Vector3(x,0,-HalfDepth+1.10f),3.35f,.87f,true,side+14);
                StonePier(geometry,new Vector3(side*(HalfWidth+1.36f),0,-HalfDepth+.38f),1.20f,side+6,true);
                GardenPatch(new Vector3(side*(HalfWidth+1.78f),.02f,-HalfDepth+1.55f),side+31,side,1.32f,true);
                StreetHouse(new Vector3(side*(HalfWidth*.65f),-.04f,-HalfDepth-8.35f),4.72f,2.86f,1.46f,side<0?-8:12,false);
                StreetHouse(new Vector3(side*(HalfWidth+5.25f),.10f,HalfDepth+5.1f),5.14f,3.72f,4.25f,side<0?-16:19,false);
                for(int layer=0;layer<3;layer++){
                    Vector3 p=new Vector3(side*(HalfWidth+4.5f),.14f+layer*.28f,-HalfDepth+3.4f);
                    Block("岸街层叠的大料石台阶",geometry,p,new Vector3(3.8f-layer*.54f,.29f,2.6f-layer*.32f),layer==2?stone:masonry,side+layer+8,.12f);
                }
                CrownTree(new Vector3(side*(HalfWidth+3.22f),.02f,-HalfDepth+3.35f),2.34f,side+5);
                CrownTree(new Vector3(side*(HalfWidth+4.16f),.26f,HalfDepth+4.30f),3.05f,side+10);
                CrownTree(new Vector3(side*(HalfWidth*.69f),.02f,-HalfDepth-7.6f),1.68f,side+15);
                // Cool vapour sits over the real outer canal; playable mist remains flat.
                Fog(new Vector3(side*(HalfWidth+3.2f),-.72f,-HalfDepth-2.2f));
                Fog(new Vector3(side*(HalfWidth+5.8f),.65f,HalfDepth+5.5f));
            }
        }
        private void CrownTree(Vector3 p,float height,int variant)
        {
            var branch=TacticalSceneryMesh.Branch(new[]{Vector3.zero,new Vector3(.09f,height*.48f,0),new Vector3(-.14f,height*.78f,.11f),new Vector3(.08f,height,.10f)},.095f);
            transientMeshes.Add(branch);var trunk=Visuals.MeshObject("石岸成片植冠的曲干",geometry,branch,wood);trunk.transform.localPosition=p;
            // Only actual creased leaves form the crown. No solid spherical filler sits
            // below them, so sun can pass through the irregular branch/leaf silhouette.
            for(int crown=0;crown<9;crown++){
                float angle=crown*Mathf.PI*2/5.7f,reach=.28f+crown%3*.13f;
                float scale=.75f+crown%4*.08f;
                Bush(p+new Vector3(Mathf.Cos(angle)*reach,height*.57f+crown%3*.24f,Mathf.Sin(angle)*reach),variant+crown,scale);
            }
        }
        private void StreetLightPools()
        {
            // Actual localized lights, selected per spatial batch rather than for a single
            // all-city renderer. The lanterns remain small accents outside these sun pools.
            for(int pool=0;pool<2;pool++){
                var go=new GameObject("穿过巷檐的局部暖阳 "+pool);go.transform.SetParent(geometry,false);
                go.transform.localPosition=new Vector3(pool==0?-HalfWidth*.37f:HalfWidth*.55f,5.8f,pool==0?-HalfDepth*.30f:HalfDepth*.29f);
                go.transform.localRotation=Quaternion.Euler(90,0,0);
                var light=go.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(1,.90f,.70f);light.intensity=18;light.range=10;light.spotAngle=67;light.innerSpotAngle=38;light.shadows=LightShadows.Soft;light.shadowStrength=.72f;light.shadowBias=.025f;light.shadowNormalBias=.04f;
            }
        }
        private System.Collections.IEnumerator LightingEvidence()
        {
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            var sun=RenderSettings.sun;var texture=Shader.GetGlobalTexture("_MainLightShadowmapTexture");
            int casters=geometry?geometry.GetComponentsInChildren<MeshRenderer>().Count(r=>r.shadowCastingMode!=ShadowCastingMode.Off&&r.sharedMaterial&&r.sharedMaterial.FindPass("ShadowCaster")>=0):0;
            Debug.Log("DICEBOUND_LIGHTING_EVIDENCE sun="+(sun?sun.name:"none")+" strength="+(sun?sun.shadowStrength:0)+" shadowMode="+(sun?sun.shadows.ToString():"none")+" map="+(texture?texture.width+"x"+texture.height:"none")+" mainKeyword="+Shader.IsKeywordEnabled("_MAIN_LIGHT_SHADOWS")+" cascadeKeyword="+Shader.IsKeywordEnabled("_MAIN_LIGHT_SHADOWS_CASCADE")+" opaqueCasters="+casters+" cameraPipeline="+GraphicsSettings.currentRenderPipeline);
        }
        private void CourtyardWall(Vector3 p,float length,float height,bool alongZ,int variant)
        {
            var root=new GameObject("破损厚石院墙与压顶").transform;root.SetParent(geometry,false);root.localPosition=p;root.localRotation=Quaternion.Euler(0,alongZ?90:0,0);
            int courses=Mathf.Max(2,Mathf.CeilToInt(height/.42f));float courseHeight=height/courses;
            for(int row=0;row<courses;row++){
                int blocks=row%2==0?2:3;float width=length/blocks;
                for(int col=0;col<blocks;col++)Block("大块错缝破边墙石",root,new Vector3((col-(blocks-1)*.5f)*width,(row+.5f)*courseHeight,0),new Vector3(width-.011f,courseHeight-.009f,.65f),row==courses-1?stone:masonry,variant+row*3+col,.11f);
            }
            Block("院墙厚实圆蚀压顶",root,new Vector3(0,height+.07f,0),new Vector3(length+.10f,.17f,.79f),stone,variant+6,.08f);
            for(int side=-1;side<=1;side+=2)StonePier(root,new Vector3(side*length*.5f,0,0),height+.13f,variant+side+8,false);
        }

        private void Paving()
        {
            // Continuous, staggered masonry rows cross logical cell boundaries. The grid
            // is revealed by selection overlays rather than permanent square plinth edges.
            for(int y=0;y<BoardHeight;y++)for(int subrow=0;subrow<1;subrow++){
                int x=0;
                while(x<BoardWidth){
                    if(TacticalRules.TerrainAt(state,x,y)=="gap"||IsBridgeDeck(x,y)){x++;continue;}
                    int first=x;float height=HeightAt(x,y);
                    while(x<BoardWidth&&TacticalRules.TerrainAt(state,x,y)!="gap"&&!IsBridgeDeck(x,y)&&Mathf.Approximately(HeightAt(x,y),height))x++;
                    float left=BoardCell(first,y).x-TileSize*.5f,remaining=(x-first)*TileSize;int flag=0;
                    while(remaining>.025f){
                        int salt=unchecked((int)state.seed)+y*13+subrow*97+first*5;
                        float width=flag==0&&(y+subrow)%2==1?.71f:1.22f+Mathf.Floor(TacticalSceneryMesh.Noise(salt,flag+3)*6)*.16f;
                        width=Mathf.Min(width,remaining);
                        var mat=(y+subrow+flag)%11==0?stoneWarm:(y*3+flag)%13==0?stoneCool:stone;
                        Block("宽阔不齐整料铺石",geometry,new Vector3(left+width*.5f,height-.077f,BoardCell(0,y).z),new Vector3(width-.004f,.154f,1.246f),mat,salt+flag,.020f);
                        left+=width;remaining-=width;flag++;
                    }
                }
            }
            for(int y=0;y<BoardHeight;y++)for(int x=0;x<BoardWidth;x++){
                Vector3 p=BoardCell(x,y);float h=HeightAt(x,y);string kind=TacticalRules.TerrainAt(state,x,y);
                if(kind=="gap"){
                    if(state.chapter!=2)WaterSurface(p+Vector3.down*.79f,1.25f,1.25f);
                }else if(IsBridgeDeck(x,y)){
                    for(int plank=0;plank<4;plank++){
                        float inset=(plank-1.5f)*.311f;
                        Plank("厚实旧木桥面",geometry,p+new Vector3(0,h-.115f,inset),new Vector3(1.25f,.230f,.301f),(x+y*3+plank)%12);
                        if(plank==1||plank==3)for(int end=-1;end<=1;end+=2)Stud(geometry,p+new Vector3(end*.49f,h+.002f,inset),.019f,iron);
                    }
                    Plank("桥板下承重榫梁",geometry,p+new Vector3(0,h-.34f,0),new Vector3(1.25f,.25f,.27f),(x+y)%12);
                }
                // Picking and feet use the exact same immutable board as before.
                var floor=new GameObject("落脚石 · "+TacticalRules.CellName(x,y));floor.transform.SetParent(geometry,false);floor.transform.localPosition=p+Vector3.up*(h-.014f);
                floor.AddComponent<BoxCollider>().size=new Vector3(TileSize,.028f,TileSize);var tile=floor.AddComponent<TacticalTileHit>();tile.X=x;tile.Y=y;
                if(h>0){Block("真实高台磨损侧壁",geometry,p+new Vector3(0,.06f,0),new Vector3(1.23f,.22f,1.23f),masonry,x+y,.06f);for(int step=0;step<3;step++)Block("高台三级石阶",geometry,p+new Vector3(0,.038f+step*.057f,-.55f+step*.07f),new Vector3(.75f,.067f,.12f),stone,step+1,.018f);}
            }
        }

        private void Canal()
        {
            WaterSurface(new Vector3(0,-.78f,-HalfDepth-2.21f),HalfWidth*2+27,4.52f);
            // The bridge chapter's rule-defined central water remains open below gaps.
            // Side streets and rear land are joined to the battlefield, not isolated by water.
            if(state.chapter==2){
                WaterSurface(new Vector3(0,-.81f,0),2.49f,HalfDepth*2+12.2f);
                for(int row=0;row<BoardHeight;row++)if(TacticalRules.TerrainAt(state,BoardWidth/2-1,row)!="gap"&&TacticalRules.TerrainAt(state,BoardWidth/2,row)!="gap"){
                    float z=BoardCell(0,row).z;Plank("横跨深渠的木桥承梁",geometry,new Vector3(0,-.42f,z),new Vector3(5.17f,.36f,.32f),row);
                }
            }
        }
        private void WaterSurface(Vector3 p,float width,float depth)
        {
            var surface=Visuals.MeshObject("深浅变化与流动水光",geometry,WaterPlane(width,depth),water);surface.transform.localPosition=p;
            surface.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        private void Waterfront()
        {
            int blocks=Mathf.CeilToInt((HalfWidth*2+7)/1.12f);
            for(int row=0;row<4;row++)for(int i=0;i<blocks;i++){
                float x=(i-(blocks-1)*.5f)*1.12f+(row%2)*.32f;
                Block("沿街主渠厚重挡水岸墙",geometry,new Vector3(x,-.17f-row*.33f,-HalfDepth-.14f),new Vector3(1.108f,.322f,.43f),row==0?stone:masonry,i+row*3,.075f);
                Block("前街与渠相接的深岸墙",geometry,new Vector3(x,-.29f-row*.33f,-HalfDepth-4.56f),new Vector3(1.108f,.322f,.47f),row==0?stone:masonry,i+row*7,.08f);
            }
            for(int side=-1;side<=1;side+=2){
                float x=side*(HalfWidth*.64f);OuterBridge(new Vector3(x,-.08f,-HalfDepth-2.24f),1.86f,4.76f);
                Spillway(new Vector3(side*HalfWidth*.24f,-.31f,-HalfDepth-.39f));
                for(int end=-1;end<=1;end+=2){
                    float landingZ=-HalfDepth-2.80f+end*2.05f;
                    StonePier(geometry,new Vector3(x+side*1.12f,-.04f,landingZ),end>0?.54f:.95f,side+end+6,true);
                    Block("桥头相连的石栏肩",geometry,new Vector3(x+side*1.77f,.20f,landingZ),new Vector3(.87f,.41f,.60f),masonry,side+end+9,.08f);
                }
                // Rear pillars frame the street without placing false obstacles inside.
                StonePier(geometry,new Vector3(side*(HalfWidth+1.12f),0,HalfDepth+.79f),1.48f,side+8,true);
                StreetLantern(new Vector3(side*(HalfWidth+1.37f),0,HalfDepth+1.56f),1.47f);
            }
            // Lilies and foam form a few readable water groups, rather than a uniform
            // repeated pattern. No collider or selectable marker is attached to them.
            for(int group=0;group<4;group++)for(int leafIndex=0;leafIndex<4;leafIndex++){
                float x=(group-1.5f)*HalfWidth*.46f+TacticalSceneryMesh.Noise(group,leafIndex)*.42f;
                float z=-HalfDepth-1.32f-group%2*1.54f+TacticalSceneryMesh.Noise(leafIndex,group)*.38f;
                var lily=Visuals.MeshObject("深渠水面分组浮叶",geometry,Disc(.10f+leafIndex*.012f,10),leafLight);lily.transform.localPosition=new Vector3(x,-.768f,z);lily.transform.localRotation=Quaternion.Euler(0,group*57+leafIndex*33,0);
            }
        }
        private void StonePier(Transform parent,Vector3 p,float height,int variant,bool copper)
        {
            Block("厚柱坡切石脚",parent,p+Vector3.up*.135f,new Vector3(.70f,.28f,.69f),masonry,variant,.095f);
            Block("整料破边石柱",parent,p+Vector3.up*(height*.5f+.13f),new Vector3(.47f,height,.46f),masonry,variant+3,.075f);
            Block("石柱方形圆蚀压顶",parent,p+Vector3.up*(height+.21f),new Vector3(.63f,.16f,.62f),stone,variant+5,.07f);
            Block("压顶小抬肩",parent,p+Vector3.up*(height+.32f),new Vector3(.43f,.09f,.42f),stoneWarm,variant+7,.035f);
            if(copper){
                for(int side=-1;side<=1;side+=2){
                    Slab("青玉铜柱内嵌牌",parent,p+new Vector3(0,height*.48f+.20f,side*.236f),new Vector3(.28f,height*.56f,.016f),jade);
                    for(int edge=-1;edge<=1;edge+=2)Slab("嵌牌铜边框",parent,p+new Vector3(edge*.151f,height*.48f+.20f,side*.250f),new Vector3(.022f,height*.60f,.021f),bronze);
                }
                Stud(parent,p+new Vector3(0,height*.81f,-.258f),.053f,bronze,Quaternion.Euler(90,0,0));
            }
        }
        private void OuterBridge(Vector3 p,float width,float length)
        {
            int boards=Mathf.CeilToInt(width/.27f);float boardWidth=width/boards;
            for(int i=0;i<boards;i++){
                var board=Plank("水渠上街巷小桥",geometry,p+new Vector3((i-(boards-1)*.5f)*boardWidth,0,0),new Vector3(length,.18f,boardWidth-.018f),i+4);board.transform.localRotation=Quaternion.Euler(0,90,0);
                for(int end=-1;end<=1;end+=2)Stud(geometry,p+new Vector3((i-(boards-1)*.5f)*boardWidth,.095f,end*(length*.5f-.14f)),.018f,iron);
            }
            for(int side=-1;side<=1;side+=2){
                var beam=Plank("外桥厚实承重梁",geometry,p+new Vector3(side*width*.34f,-.20f,0),new Vector3(length+.20f,.25f,.22f),side+5);beam.transform.localRotation=Quaternion.Euler(0,90,0);
            }
        }
        private void Spillway(Vector3 p)
        {
            var root=new GameObject("岸墙拱形排水口").transform;root.SetParent(geometry,false);root.localPosition=p;
            Block("出水口破边石背板",root,new Vector3(0,0,.10f),new Vector3(.69f,.60f,.22f),masonry,7,.075f);
            Torus("刻石出水环的圆蚀石唇",root,new Vector3(0,0,-.035f),.245f,.041f,stone,Quaternion.Euler(90,0,0));
            Torus("旧铜出水唇口",root,Vector3.zero,.19f,.045f,bronze,Quaternion.Euler(90,0,0));
            var dark=Visuals.MeshObject("排水口内部",root,Disc(.155f,16),iron);dark.transform.localRotation=Quaternion.Euler(90,0,0);dark.transform.localPosition=new Vector3(0,0,.012f);
            for(int wedge=0;wedge<7;wedge++){
                float a=(wedge/6f)*Mathf.PI;var stoneBlock=Block("砌石拱券楔块",root,new Vector3(Mathf.Cos(a)*.29f,Mathf.Sin(a)*.29f,.02f),new Vector3(.20f,.17f,.24f),masonry,wedge+3,.04f);stoneBlock.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90);
            }
            for(int stream=0;stream<5;stream++){
                var points=new Vector3[11];for(int k=0;k<points.Length;k++){float t=k/10f;points[k]=new Vector3((stream-2)*.028f,Mathf.Lerp(-.045f,-.47f,t*t),-.06f-.37f*t);}
                var mesh=TacticalSceneryMesh.Ribbon(points,.032f);transientMeshes.Add(mesh);Visuals.MeshObject("出水口连至水面的白色水束",root,mesh,waterFoam);
            }
        }
        private void BankPost(Vector3 p,float height)
        {
            Block("石岸灯柱台脚",geometry,p+Vector3.up*.12f,new Vector3(.54f,.27f,.53f),masonry,4);
            Block("青铜石柱身",geometry,p+Vector3.up*(height*.5f+.19f),new Vector3(.28f,height,.28f),jade,5);
            for(int side=-1;side<=1;side+=2)Slab("灯柱铜镶边",geometry,p+new Vector3(side*.146f,height*.5f+.18f,-.14f),new Vector3(.023f,height*.78f,.026f),bronze);
            Block("灯柱宽压顶",geometry,p+Vector3.up*(height+.23f),new Vector3(.46f,.14f,.45f),stone,6);
            LatheObject("柱顶铜莲座",geometry,p+Vector3.up*(height+.31f),new[]{new Vector2(0,0),new Vector2(.15f,0),new Vector2(.10f,.05f),new Vector2(.07f,.12f),new Vector2(0,.15f)},bronze,8);
        }

        private void Planting(int chapter)
        {
            int variety=unchecked((int)(state.seed%31));
            for(int side=-1;side<=1;side+=2){
                // Unequal groups gather around the walls and landings. The low front
                // foliage never forms a uniform hedge enclosing the tactical rectangle.
                for(int group=0;group<5;group++){
                    float z=-HalfDepth+1.35f+group*(HalfDepth*2-.9f)/4f;
                    float x=side*(HalfWidth+1.12f+TacticalSceneryMesh.Noise(group+variety,72)*.61f);
                    GardenPatch(new Vector3(x,.015f,z),group+chapter*7+variety,side,.80f+TacticalSceneryMesh.Noise(group,26)*.31f,chapter!=1);
                    if(group%2==0)GardenPatch(new Vector3(x+side*1.60f,.01f,z+.70f),group+18,side,1.23f,chapter!=1);
                }
                for(int group=0;group<3;group++){
                    var p=new Vector3(side*(HalfWidth+2.94f),.04f,HalfDepth+1.3f+group*1.71f);
                    GardenPatch(p,group+variety+36,side,1.30f,true);
                    Shrub(p+new Vector3(side*.20f,0,.12f),1.38f+group*.10f,group+chapter+4);
                    Vine(p+new Vector3(side*.35f,.20f,.07f),group+chapter+16);
                }
            }
            for(int group=0;group<7;group++){
                float x=(group-3)*2.19f;
                GardenPatch(new Vector3(x,-.03f,-HalfDepth-4.95f-TacticalSceneryMesh.Noise(group,61)*.51f),group+variety+21,0,1.05f+group%3*.16f,chapter!=1);
            }
            for(int side=-1;side<=1;side+=2){
                GardenPatch(new Vector3(side*(HalfWidth*.60f),.22f,HalfDepth+2.16f),side+variety+71,0,1.40f,true);
                Vine(new Vector3(side*(HalfWidth*.70f),.45f,HalfDepth+2.27f),chapter+side+19);
            }
        }

        private void GardenPatch(Vector3 p,int variant,int side,float scale,bool withFlowers)
        {
            // No rectangular planter bases: foliage grows around eroded, partly buried
            // stones, with broad leaves over a lower grass layer and a gold flower group.
            for(int rock=0;rock<3;rock++){
                float x=(TacticalSceneryMesh.Noise(variant,rock*4)-.5f)*.87f*scale;
                float z=(TacticalSceneryMesh.Noise(variant,rock*4+1)-.5f)*1.23f*scale;
                Block("植丛中半埋的苔生碎石",geometry,p+new Vector3(x,-.008f,z),new Vector3(.25f+rock*.09f,.10f+rock*.023f,.34f),rock==0?moss:masonry,variant+rock,.05f);
            }
            for(int tuft=0;tuft<5;tuft++){
                float x=(TacticalSceneryMesh.Noise(variant,tuft*5)-.5f)*.59f*scale,z=(tuft-2)*.39f*scale;
                Bush(p+new Vector3(side*.15f+x,0,z),variant+tuft,.69f*scale);
                PlantCluster(p+new Vector3(x,.008f,z),variant+tuft+9,true,.95f*scale);
            }
            if(withFlowers){Flowers(p+new Vector3(-side*.21f,.015f,-.38f),variant+3);Flowers(p+new Vector3(side*.27f,.015f,.42f),variant+10);}
        }
        private void SurfaceAccents()
        {
            // Accents are constrained to existing leaf/flower draw batches. An imported
            // older district that has no suitable batch simply omits the optional accent.
            surfaceAccentBatches=new HashSet<(Material,int,int)>();
            foreach(var renderer in geometry.GetComponentsInChildren<MeshRenderer>()){
                var material=renderer.sharedMaterial;
                if(material!=leaf&&material!=leafLight&&material!=leafDark&&material!=flower)continue;
                Vector3 center=geometry.InverseTransformPoint(renderer.bounds.center);
                surfaceAccentBatches.Add((material,Mathf.FloorToInt(center.x/6),Mathf.FloorToInt(center.z/6)));
            }
            try{
                for(int side=-1;side<=1;side+=2){
                    var foots=new[]{
                        new Vector3(side*(HalfWidth+1.42f),.015f,-HalfDepth+.49f),
                        new Vector3(side*(HalfWidth+1.15f),.015f,HalfDepth+.87f),
                        new Vector3(side*(HalfWidth*.53f),state.chapter==3?.26f:.88f,HalfDepth+1.99f)
                    };
                    for(int point=0;point<foots.Length;point++){
                        Vector3 p=foots[point];int variant=point*3+side+11;
                        // Even the leaf tips stay beyond the board rectangle. These are
                        // wall-foot plants, not objects or decorations on traversable cells.
                        for(int tuft=0;tuft<3;tuft++)PlantCluster(p+new Vector3((tuft-1)*.15f,.004f,tuft*.10f),variant+tuft,false,.55f);
                        Flowers(p+new Vector3(side*.12f,.009f,.22f),variant+4,.72f);
                        Flowers(p+new Vector3(-side*.17f,.009f,.11f),variant+8,.65f);
                    }
                }
            }finally{surfaceAccentBatches=null;}
        }
        private void ConstrainAccentBatch(GameObject go)
        {
            if(surfaceAccentBatches==null)return;var renderer=go.GetComponent<MeshRenderer>();
            Vector3 center=geometry.InverseTransformPoint(renderer.bounds.center);
            if(surfaceAccentBatches.Contains((renderer.sharedMaterial,Mathf.FloorToInt(center.x/6),Mathf.FloorToInt(center.z/6))))return;
            go.SetActive(false);Destroy(go);
        }
        private void Bush(Vector3 p,int variant,float scale)
        {
            string key="dense-bush:"+((variant%8+8)%8);if(!meshes.TryGetValue(key,out var mesh)){mesh=TacticalSceneryMesh.Bush((variant%8+8)%8);meshes[key]=mesh;}
            var go=Visuals.MeshObject("连成中尺度的密叶灌木",geometry,mesh,variant%3==0?leafLight:variant%3==1?leaf:leafDark);go.transform.localPosition=p;go.transform.localScale=Vector3.one*scale;go.transform.localRotation=Quaternion.Euler(0,variant*39,0);
        }
        private void PlantCluster(Vector3 p,int variant,bool grass,float scale)
        {
            string key="leaf:"+variant%8+":"+grass;if(!meshes.TryGetValue(key,out var mesh)){mesh=TacticalSceneryMesh.LeafCluster(variant%8,grass);meshes[key]=mesh;}
            var material=variant%3==0?leafLight:variant%3==1?leaf:leafDark;
            var go=Visuals.MeshObject(grass?"石缝弯叶草":"水岸叶丛",geometry,mesh,material);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,variant*41,0);go.transform.localScale=Vector3.one*scale;
            ConstrainAccentBatch(go);
        }

        private void Flowers(Vector3 p,int variant,float scale=1)
        {
            int shape=(variant%8+8)%8;string key="wildflowers:"+shape;
            if(!meshes.TryGetValue(key,out var mesh)){mesh=TacticalSceneryMesh.Flowers(shape);meshes[key]=mesh;}
            var go=Visuals.MeshObject("原生暖黄野花团簇",geometry,mesh,flower);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,variant*39,0);go.transform.localScale=Vector3.one*scale;ConstrainAccentBatch(go);
        }
        private void Shrub(Vector3 p,float height,int variant)
        {
            var branch=TacticalSceneryMesh.Branch(new[]{Vector3.zero,new Vector3(.06f,height*.45f,.04f),new Vector3(-.07f,height*.74f,.08f),new Vector3(.05f,height,.03f)},.060f);transientMeshes.Add(branch);
            var trunk=Visuals.MeshObject("城岸灌木枝干",geometry,branch,wood);trunk.transform.localPosition=p;
            for(int tuft=0;tuft<7;tuft++){
                float a=tuft*Mathf.PI*2/7;PlantCluster(p+new Vector3(Mathf.Cos(a)*.23f,height*.31f+tuft*.07f,Mathf.Sin(a)*.22f),variant+tuft,false,.82f);
            }
        }
        private void Vine(Vector3 p,int variant)
        {
            var points=new[]{Vector3.zero,new Vector3(.10f,.32f,-.01f),new Vector3(-.08f,.63f,.03f),new Vector3(.04f,1.03f,0),new Vector3(.32f,1.20f,.02f)};
            var branch=TacticalSceneryMesh.Branch(points,.014f);transientMeshes.Add(branch);var root=Visuals.MeshObject("岸墙攀援枝条",geometry,branch,leafDark);root.transform.localPosition=p;
            for(int i=0;i<5;i++)PlantCluster(p+points[i],variant+i,false,.31f);
        }
        private void Terrain(TacticalCell cell)
        {
            Vector3 p=BoardCell(cell.x,cell.y)+Vector3.up*HeightAt(cell.x,cell.y);
            if(cell.kind=="cover"){
                // Traversable terrain is a floor indication, never an apparent solid obstacle.
                for(int strip=0;strip<3;strip++)Slab("掩护地面木纹嵌条",geometry,p+new Vector3(0,.006f,.23f+strip*.095f),new Vector3(.92f,.008f,.065f),wood);
                for(int side=-1;side<=1;side+=2)Slab("掩护地面铜角标",geometry,p+new Vector3(side*.46f,.006f,.325f),new Vector3(.035f,.008f,.26f),bronze);
            }else if(cell.kind=="mist"){
                var puddle=Visuals.MeshObject("透出石砖的冷凝水迹",geometry,GroundPlane(1.02f,.88f),wetWash);puddle.transform.localPosition=p+Vector3.up*.004f;
                for(int seam=0;seam<3;seam++){var trace=Slab("冷凝水贴地微波纹",geometry,p+new Vector3(-.07f+seam*.06f,.006f,-.19f+seam*.17f),new Vector3(.50f+seam*.07f,.001f,.013f),wetTrace);trace.transform.localRotation=Quaternion.Euler(0,-12+seam*9,0);}
            }else if(cell.kind=="shadow"){
                var shade=Visuals.MeshObject("透出石砖的墨紫影纹",geometry,GroundPlane(1.08f,.94f),shadowWash);shade.transform.localPosition=p+Vector3.up*.004f;
                for(int strip=0;strip<3;strip++){var trace=Slab("阴影贴地细斜纹",geometry,p+new Vector3(-.22f+strip*.17f,.006f,-.13f+strip*.08f),new Vector3(.021f,.001f,.40f-strip*.045f),shadowTrace);trace.transform.localRotation=Quaternion.Euler(0,32,0);}
            }else if(cell.kind=="pipe"){
                for(int line=0;line<2;line++)Slab("地下热网铜嵌线",geometry,p+new Vector3(0,.006f,.26f+line*.10f),new Vector3(1.04f,.008f,.034f),bronze);
                for(int joint=-1;joint<=1;joint+=2)Slab("热网地面接缝",geometry,p+new Vector3(joint*.38f,.006f,.31f),new Vector3(.028f,.008f,.22f),iron);
            }else if(cell.kind=="rubble"){
                // Broken paving costs movement in the rules, but remains a low, readable surface.
                for(int chip=0;chip<7;chip++){float x=(chip%3-1)*.27f,z=(chip/3-1)*.29f;var shard=Slab("碎石慢行地面裂片",geometry,p+new Vector3(x,.006f,z),new Vector3(.22f,.008f,.17f),masonry);shard.transform.localRotation=Quaternion.Euler(0,chip*37,0);}
                for(int seam=0;seam<2;seam++)Slab("碎石地面断缝",geometry,p+new Vector3(-.08f+seam*.24f,.006f,0),new Vector3(.022f,.008f,.96f),iron);
            }else if(cell.kind=="gap"){
                bool timber=state.chapter==2&&cell.x>=BoardWidth/2-2&&cell.x<=BoardWidth/2+1;
                foreach(var edge in new[]{new Vector2Int(-1,0),new Vector2Int(1,0),new Vector2Int(0,-1),new Vector2Int(0,1)}){
                    if(!TacticalRules.Inside(state,cell.x+edge.x,cell.y+edge.y)||TacticalRules.TerrainAt(state,cell.x+edge.x,cell.y+edge.y)=="gap")continue;
                    for(int fragment=0;fragment<3;fragment++){
                        float offset=(fragment-1)*.36f;
                        Vector3 q=p+new Vector3(edge.x*.598f,-.035f,edge.y*.598f)+new Vector3(edge.y*offset,0,edge.x*offset);
                        Vector3 size=edge.x==0?new Vector3(.33f,.12f,.092f):new Vector3(.092f,.12f,.33f);
                        if(timber){var split=Plank("断桥边缘参差木茬",geometry,q,new Vector3(.34f,.12f,.08f),fragment+cell.y);split.transform.localRotation=Quaternion.Euler(0,edge.x==0?0:90,0);}
                        else Block("真正缺口破裂石茬",geometry,q,size,masonry,fragment+cell.x+cell.y,.029f);
                    }
                }
            }else if(cell.kind=="wall"){
                int ruin=cell.x+cell.y*3;
                Block("真阻挡旧垣厚石体",geometry,p+new Vector3(0,.25f,0),new Vector3(1.18f,.50f,.83f),masonry,ruin,.13f);
                Block("断垣高侧整料破石",geometry,p+new Vector3(-.26f,.64f,.009f),new Vector3(.64f,.61f,.84f),masonry,ruin+3,.11f);
                Block("断垣低侧残缺大石",geometry,p+new Vector3(.34f,.59f,-.014f),new Vector3(.47f,.46f,.82f),masonry,ruin+7,.10f);
                Block("不齐旧墙破裂高压顶",geometry,p+new Vector3(-.25f,.947f,0),new Vector3(.67f,.13f,.88f),stone,ruin+6,.075f);
                Block("不齐旧墙低压顶断面",geometry,p+new Vector3(.35f,.83f,.003f),new Vector3(.48f,.14f,.87f),stone,ruin+10,.07f);
                // Physical fine stone seams stay on the true blocking mass. No grass,
                // fencing or sculpted prop is added to a traversable neighbouring cell.
                for(int side=-1;side<=1;side+=2){
                    Slab("旧垣纵向嵌缝",geometry,p+new Vector3(-.31f,.26f,side*.415f),new Vector3(.012f,.34f,.005f),mortar);
                    Slab("旧垣短斜矿缝",geometry,p+new Vector3(.37f,.59f,side*.423f),new Vector3(.013f,.20f,.005f),mortar);
                }
                var hit=new GameObject("阻挡砌石的射线轮廓");hit.transform.SetParent(geometry,false);hit.transform.localPosition=p+Vector3.up*.5f;
                hit.AddComponent<BoxCollider>().size=new Vector3(1.20f,1,.86f);var tile=hit.AddComponent<TacticalTileHit>();tile.X=cell.x;tile.Y=cell.y;
            }
        }
        private void District(int chapter)
        {
            float x=HalfWidth,z=HalfDepth;
            if(chapter==0){Facade(-x-3.0f,1.2f,Mathf.Max(4,z*.8f),3.1f);Facade(x+3.0f,1.0f,Mathf.Max(4.5f,z*.9f),3.0f);RearGallery(false);MedicineCart(new Vector3(-x+.4f,0,z+3.2f));for(int side=-1;side<=1;side+=2)StreetLantern(new Vector3(side*(x+2.22f),-.12f,z-.8f),1.50f);}
            if(chapter==1){RearGallery(true);for(int row=0;row<3;row++)PipeSection(geometry,new Vector3(-x-1.5f,1.1f+row*.48f,z+2.20f),new Vector3(x+1.5f,1.1f+row*.48f,z+2.20f),.18f);for(int side=-1;side<=1;side+=2){float pipeX=side*(x+2.46f);PipeSection(geometry,new Vector3(pipeX,.62f,-z+1.6f),new Vector3(pipeX,.62f,z+1.3f),.22f);Valve(geometry,new Vector3(pipeX-.20f*side,1.12f,2.2f),.32f,true);ClothPanel("黑帘分隔检修廊",geometry,new Vector3(side*(x+1.80f),2.12f,z+2.15f),1.3f,1.80f,cloth);StreetLantern(new Vector3(side*(x+1.97f),-.12f,z+1.06f),1.25f);}}
            if(chapter==2){RearGallery(false);for(int side=-1;side<=1;side+=2){Rail(geometry,new Vector3(side*(x+2.85f),-.10f,0),z*2-1.2f,true);StreetLantern(new Vector3(side*(x+2.12f),-.12f,z-.6f),1.52f);ClothPanel("桥头旧红悬幅",geometry,new Vector3(side*(x*.70f),2.00f,z+2.01f),.35f,1.42f,banner);}Block("桥头系绳石",geometry,new Vector3(-x+.6f,.13f,z+1.45f),new Vector3(.72f,.29f,.67f),stone,6);Coil(geometry,new Vector3(-x+.6f,.36f,z+1.45f));}
            if(chapter==3){ArchiveWall();for(int side=-1;side<=1;side+=2){Facade(side*(x+3.0f),.6f,Mathf.Max(3.1f,z*.62f),2.7f);StreetLantern(new Vector3(side*(x+2.15f),-.12f,z-.7f),1.35f);}Slab("移出的检修桌",geometry,new Vector3(-x+.5f,.68f,z+1.5f),new Vector3(1.85f,.12f,.82f),wood);for(int side=-1;side<=1;side+=2)Slab("案桌榫脚",geometry,new Vector3(-x+.5f+side*.66f,.33f,z+1.5f),new Vector3(.12f,.64f,.55f),wood);PaperStack(geometry,new Vector3(-x+.3f,.79f,z+1.5f),6);}
            if(chapter==4){RearGallery(true);DayWheel();for(int side=-1;side<=1;side+=2){PipeSection(geometry,new Vector3(side*(x+2.65f),.25f,-z+1.2f),new Vector3(side*(x+2.65f),.25f,z+1.1f),.24f);Rail(geometry,new Vector3(side*(x+2.98f),-.10f,0),z*2-1,true);StreetLantern(new Vector3(side*(x+1.94f),-.12f,z-.8f),1.38f);}}
        }

        private void Facade(float x,float z,float depth,float height)
        {
            float side=Mathf.Sign(x);
            // Front houses were pushed so far away that they disappeared from the place.
            // They now form rear-side streets; their nearest roof remains 2.7m outside.
            float bankX=side*(HalfWidth+3.26f),bankZ=Mathf.Max(z,HalfDepth*.63f+1.31f);
            StreetHouse(new Vector3(bankX,.025f,bankZ),depth,2.92f,height+.26f,side<0?-90:90,true);
        }
        private void StreetHouse(Vector3 p,float width,float depth,float height,float yaw,bool awning)
        {
            var root=new GameObject("曜京石木街屋 · 台阶窗门与厚瓦檐").transform;root.SetParent(geometry,false);root.localPosition=p;root.localRotation=Quaternion.Euler(0,yaw,0);
            Block("街屋承台大块圆蚀石",root,new Vector3(0,.15f,depth*.45f),new Vector3(width+.30f,.32f,depth+.35f),masonry,4,.12f);
            Block("街屋完整后墙与侧墙体",root,new Vector3(0,height*.5f+.24f,depth*.5f),new Vector3(width,height,depth),masonry,8,.10f);
            int bays=Mathf.Max(3,Mathf.CeilToInt(width/1.3f));float bayWidth=width/bays;
            for(int bay=0;bay<bays;bay++){
                float x=(bay-(bays-1)*.5f)*bayWidth;
                for(int row=0;row<3;row++)Block("街屋不齐大料墙基",root,new Vector3(x,.36f+row*.25f,-.026f),new Vector3(bayWidth-.01f,.244f,.22f),row==2?stone:masonry,bay+row*3,.065f);
                bool doorway=bay==bays/2;
                float openingHeight=doorway?height*.68f:height*.37f,openingY=doorway?openingHeight*.5f+.34f:height*.62f;
                float openingWidth=bayWidth*.70f;
                Slab(doorway?"门洞退后的墨暗内衬":"格窗退后的冷暗衬",root,new Vector3(x,openingY,-.158f),new Vector3(openingWidth,openingHeight,.045f),cloth);
                for(int side=-1;side<=1;side+=2){
                    Plank("门窗粗木竖框",root,new Vector3(x+side*openingWidth*.55f,openingY,-.215f),new Vector3(.10f,openingHeight+.22f,.15f),bay+side+5);
                    Plank("门窗厚实横框",root,new Vector3(x,openingY+side*(openingHeight*.5f+.065f),-.222f),new Vector3(openingWidth+.22f,.12f,.17f),bay+side+7);
                }
                if(!doorway){
                    for(int bar=0;bar<5;bar++)Slab("旧铜格窗纵向棂",root,new Vector3(x+(bar-2)*openingWidth*.17f,openingY,-.233f),new Vector3(.028f,openingHeight-.07f,.028f),bronze);
                    for(int bar=0;bar<3;bar++)Slab("旧铜格窗横向棂",root,new Vector3(x,openingY+(bar-1)*openingHeight*.27f,-.233f),new Vector3(openingWidth-.06f,.027f,.028f),bronze);
                    Block("宽石窗台与滴水槽",root,new Vector3(x,openingY-openingHeight*.5f-.12f,-.24f),new Vector3(openingWidth+.31f,.18f,.37f),stone,bay+4,.055f);
                }else{
                    for(int plank=0;plank<5;plank++)Plank("门扉垂直厚木条",root,new Vector3(x+(plank-2)*openingWidth/5,openingY,-.196f),new Vector3(openingWidth/5-.009f,openingHeight-.05f,.063f),plank+5);
                    Torus("门扉铸铜拉环",root,new Vector3(x+.16f,openingY-.03f,-.262f),.073f,.012f,bronze,Quaternion.Euler(90,0,0));
                }
            }
            for(int side=-1;side<=1;side+=2){
                Plank("承檐粗木角柱",root,new Vector3(side*(width*.5f-.09f),height*.5f+.27f,-.105f),new Vector3(.23f,height+.06f,.25f),side+8);
                Block("立柱坡切石座",root,new Vector3(side*(width*.5f-.09f),.28f,-.105f),new Vector3(.47f,.38f,.47f),stone,side+7,.07f);
                ClothPanel("屋前垂落旧红幡",root,new Vector3(side*(width*.5f-.37f),height*.56f,-.37f),.29f,height*.51f,banner);
            }
            for(int step=0;step<3;step++)Block("街屋入口连续厚石阶",root,new Vector3(0,.055f+step*.085f,-.55f+step*.22f),new Vector3(width*.61f,.11f+step*.07f,.38f),stone,step+3,.04f);
            Plank("屋檐下粗木通长梁",root,new Vector3(0,height+.21f,-.16f),new Vector3(width+.54f,.27f,.36f),5);
            float roofDepth=depth+1.05f;int rows=Mathf.CeilToInt(roofDepth/.35f),columns=Mathf.CeilToInt((width+.85f)/.38f);
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){
                float z=-.54f+row*(roofDepth-.22f)/(rows-1),distance=Mathf.Abs(z-depth*.5f)/(roofDepth*.5f);
                float y=height+.35f+(1-distance)*.86f+distance*distance*.09f;
                var tile=RoofTile("坡面厚唇陶瓦与翘檐",root,new Vector3((col-(columns-1)*.5f)*(width+.85f)/columns,y,z),(width+.85f)/columns-.008f,.48f);tile.transform.localRotation=Quaternion.Euler(z<depth*.5f?-24:24,0,0);
            }
            Tube("屋脊圆瓦收口",root,new Vector3(-width*.5f-.35f,height+1.24f,depth*.5f),new Vector3(width*.5f+.35f,height+1.24f,depth*.5f),.066f,roof);
            for(int side=-1;side<=1;side+=2){
                Block("山墙破边石肩",root,new Vector3(side*(width*.5f+.12f),height*.64f,depth*.62f),new Vector3(.28f,height*.62f,depth*.66f),masonry,side+6,.07f);
                for(int corbel=0;corbel<3;corbel++){var brace=Slab("木檐下斜向承重肘",root,new Vector3(side*(width*.5f-.13f),height-.06f,-.09f-corbel*.11f),new Vector3(.13f,.47f,.13f),wood);brace.transform.localRotation=Quaternion.Euler(24,0,0);}
            }
            if(awning){
                Plank("侧屋檐下小木横台",root,new Vector3(-width*.32f,.90f,-.48f),new Vector3(.89f,.09f,.34f),6);
                CrateAt(root,new Vector3(-width*.32f,.94f,-.48f),.48f);
            }
        }

        private void RearGallery(bool industrial)
        {
            // Unequal rear houses, a real open arch and lower wall wings create street
            // depth. This replaces the continuous roof that read as a model display shed.
            float depth=HalfDepth;
            StreetHouse(new Vector3(-HalfWidth*.73f,state.chapter==3?.24f:.88f,depth+2.38f),Mathf.Max(3.8f,HalfWidth*.69f),3.32f,3.48f,0,false);
            StreetHouse(new Vector3(HalfWidth*.78f,state.chapter==3?.24f:.72f,depth+3.33f),Mathf.Max(3.5f,HalfWidth*.62f),3.67f,3.12f,0,false);
            var gate=new GameObject("后街真实石券门与两侧墩").transform;gate.SetParent(geometry,false);gate.localPosition=new Vector3(-.46f,state.chapter==3?.37f:1.16f,depth+2.56f);
            float radius=1.12f,spring=1.17f;
            var arch=TacticalSceneryMesh.Arch(radius,.37f,.70f,industrial?4:8);transientMeshes.Add(arch);var vault=Visuals.MeshObject("弧形错缝厚石拱券",gate,arch,stone);vault.transform.localPosition=Vector3.up*spring;
            for(int side=-1;side<=1;side+=2){
                Block("门洞两侧厚石墩",gate,new Vector3(side*(radius+.18f),spring*.5f,0),new Vector3(.43f,spring,.69f),masonry,side+8,.08f);
                Block("拱券起拱肩石",gate,new Vector3(side*(radius+.18f),spring-.08f,0),new Vector3(.60f,.17f,.87f),stone,side+6,.05f);
                CourtyardWall(new Vector3(side*(HalfWidth*.53f),.25f,depth+1.40f),Mathf.Max(2.1f,HalfWidth*.31f),1.15f,false,side+8);
                StonePier(geometry,new Vector3(side*2.30f,.25f,depth+1.96f),1.73f,side+9,true);
                ClothPanel("门墩旧红旌幡",gate,new Vector3(side*1.31f,1.64f,-.44f),.26f,1.09f,banner);
            }
            for(int step=0;step<4;step++)Block("通向后街拱门的宽石阶",gate,new Vector3(0,-.17f+step*.06f,-.62f+step*.22f),new Vector3(2.06f,.12f+step*.03f,.36f),stone,step+3,.035f);
        }
        private void ArchiveWall()
        {
            RearGallery(false);
            int bays=Mathf.Max(5,Mathf.FloorToInt(HalfWidth*2/2.1f));
            for(int bay=0;bay<bays;bay++){
                float x=(bay-(bays-1)*.5f)*2.1f;Slab("旧档柜框",geometry,new Vector3(x,1.55f,HalfDepth+1.56f),new Vector3(1.92f,2.72f,.72f),wood);
                for(int row=0;row<6;row++)for(int col=0;col<3;col++){
                    Vector3 p=new Vector3(x+(col-1)*.605f,.43f+row*.40f,HalfDepth+1.13f);Slab("抽屉倒角面",geometry,p,new Vector3(.55f,.343f,.08f),wood);Slab("工牌抽屉标签",geometry,p+new Vector3(0,.066f,-.047f),new Vector3(.21f,.096f,.014f),paper);
                    Torus("黄铜抽屉拉环",geometry,p+new Vector3(0,-.071f,-.075f),.039f,.007f,bronze,Quaternion.Euler(90,0,0));
                    if((bay+row+col)%4==0)Slab("垂落封签",geometry,p+new Vector3(.17f,-.085f,-.052f),new Vector3(.056f,.29f,.012f),paper);
                }
            }
        }
        private void DayWheel()
        {
            var root=new GameObject("昼轮冷却环 · 原创铸铜机械").transform;root.SetParent(geometry,false);root.localPosition=new Vector3(0,3.65f,HalfDepth+2.08f);root.localRotation=Quaternion.Euler(90,0,0);
            Torus("外圈分铸铜轮",root,Vector3.zero,3.10f,.19f,bronze);Torus("内圈冷却环",root,Vector3.zero,2.54f,.10f,iron);
            for(int band=0;band<3;band++)Torus("铜轮刻槽",root,new Vector3(0,.16f,0),2.97f+band*.078f,.013f,iron);
            for(int i=0;i<24;i++){
                float angle=i*Mathf.PI*2/24;Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));Tube("放射铜轮辐",root,radial*.48f,radial*2.92f,.055f,bronze);Stud(root,radial*3.10f+Vector3.up*.23f,.065f,iron);
                var cog=Slab("铸造齿段",root,radial*3.29f,new Vector3(.22f,.23f,.33f),bronze);cog.transform.localRotation=Quaternion.Euler(0,-i*15,0);
            }
            LatheObject("铜轮轴心",root,Vector3.zero,new[]{new Vector2(0,-.18f),new Vector2(.41f,-.18f),new Vector2(.49f,-.10f),new Vector2(.49f,.10f),new Vector2(.31f,.22f),new Vector2(0,.22f)},bronze);
            Torus("封闭梦流环",root,new Vector3(0,.235f,0),.32f,.035f,ember);
            for(int side=-1;side<=1;side+=2){Slab("巨轮铸铁底座",geometry,new Vector3(side*2.32f,1.0f,HalfDepth+2.01f),new Vector3(.73f,2.0f,.86f),iron);Slab("轮基阶石",geometry,new Vector3(side*2.32f,.17f,HalfDepth+1.90f),new Vector3(1.20f,.30f,1.16f),masonry);}
        }
        private void Crate(Transform parent)
        {
            for(int side=-1;side<=1;side+=2)for(int plank=0;plank<4;plank++){
                Plank("箱体长榫木条",parent,new Vector3(0,.105f+plank*.14f,side*.305f),new Vector3(.83f,.13f,.056f),plank+side+5);
                var end=Plank("箱体端面榫板",parent,new Vector3(side*.392f,.105f+plank*.14f,0),new Vector3(.62f,.13f,.055f),plank+4);end.transform.localRotation=Quaternion.Euler(0,90,0);
            }
            for(int plank=0;plank<4;plank++){var lid=Plank("手绘药箱盖板",parent,new Vector3((plank-1.5f)*.20f,.622f,0),new Vector3(.63f,.058f,.188f),plank+7);lid.transform.localRotation=Quaternion.Euler(0,90,0);}
            for(int side=-1;side<=1;side+=2){
                var brace=Plank("木箱斜向撑板",parent,new Vector3(0,.355f,side*.34f),new Vector3(.68f,.065f,.033f),side+5);brace.transform.localRotation=Quaternion.Euler(0,0,side*31);
                for(int corner=-1;corner<=1;corner+=2)Plank("药箱厚木立角",parent,new Vector3(corner*.38f,.35f,side*.324f),new Vector3(.078f,.62f,.070f),corner+side+6);
            }
            for(int strap=-1;strap<=1;strap+=2){Slab("药箱铜箍盖带",parent,new Vector3(strap*.27f,.66f,0),new Vector3(.052f,.018f,.70f),bronze);for(int side=-1;side<=1;side+=2){Slab("药箱铜箍侧带",parent,new Vector3(strap*.27f,.35f,side*.346f),new Vector3(.052f,.62f,.015f),bronze);Stud(parent,new Vector3(strap*.27f,.18f,side*.357f),.027f,iron,Quaternion.Euler(90,0,0));}}
            Slab("纸质借物封签",parent,new Vector3(.02f,.37f,-.351f),new Vector3(.16f,.30f,.006f),paper);
        }
        private void SteamValve(Transform parent)
        {
            Block("阀门石座",parent,new Vector3(0,.075f,0),new Vector3(.72f,.15f,.62f),masonry,5);
            Tube("厚铜主阀管",parent,new Vector3(-.44f,.25f,0),new Vector3(.44f,.25f,0),.17f,bronze);
            Tube("阀杆",parent,new Vector3(0,.25f,0),new Vector3(0,.70f,0),.07f,iron);
            var wheel=new GameObject("蒸汽阀独立手轮").transform;wheel.SetParent(parent,false);wheel.localPosition=new Vector3(0,.72f,0);wheel.localRotation=Quaternion.Euler(35,0,0);
            Torus("青铜手轮",wheel,Vector3.zero,.30f,.035f,bronze);
            for(int i=0;i<5;i++){float angle=i*Mathf.PI*2/5;Tube("手轮辐条",wheel,Vector3.zero,new Vector3(Mathf.Cos(angle)*.28f,0,Mathf.Sin(angle)*.28f),.022f,bronze);}
            Stud(wheel,Vector3.up*.045f,.065f,jade);
            for(int side=-1;side<=1;side+=2)Torus("管道法兰",parent,new Vector3(side*.35f,.25f,0),.185f,.036f,iron,Quaternion.Euler(0,0,90));
        }
        private void Canister(Transform parent)
        {
            LatheObject("厚壁铜热罐",parent,Vector3.zero,new[]{new Vector2(0,.015f),new Vector2(.18f,.015f),new Vector2(.255f,.10f),new Vector2(.27f,.22f),new Vector2(.245f,.53f),new Vector2(.16f,.65f),new Vector2(.13f,.68f),new Vector2(0,.68f)},bronze);
            Torus("热罐腰部焊环",parent,new Vector3(0,.30f,0),.269f,.018f,iron);Torus("热罐封口箍",parent,new Vector3(0,.661f,0),.146f,.018f,iron);
            Torus("热罐青玉护肩",parent,new Vector3(0,.575f,0),.202f,.018f,jade);
            Tube("热罐上口",parent,new Vector3(0,.68f,0),new Vector3(0,.75f,0),.087f,iron);Slab("封口刻号牌",parent,new Vector3(.05f,.46f,-.242f),new Vector3(.19f,.13f,.015f),paper);
            for(int side=-1;side<=1;side+=2)Torus("铆接提手",parent,new Vector3(side*.26f,.49f,0),.097f,.013f,iron,Quaternion.Euler(0,0,90));
            Stud(parent,new Vector3(0,.75f,0),.027f,ember);
        }
        private void MedicineCart(Vector3 position)
        {
            var root=new GameObject("灰灯巷药车").transform;root.SetParent(geometry,false);root.localPosition=position;
            for(int i=0;i<6;i++)Slab("车底嵌合长板",root,new Vector3((i-2.5f)*.22f,.59f,0),new Vector3(.204f,.10f,1.56f),wood);
            for(int side=-1;side<=1;side+=2){Tube("药车长握把",root,new Vector3(side*.38f,.55f,-.61f),new Vector3(side*.38f,.88f,-2.07f),.055f,wood);for(int rail=0;rail<3;rail++)Slab("药车栏板",root,new Vector3(side*.68f,.79f+rail*.14f,0),new Vector3(.05f,.12f,1.53f),wood);
                var wheel=new GameObject("榫接木车轮").transform;wheel.SetParent(root,false);wheel.localPosition=new Vector3(side*.79f,.39f,.22f);wheel.localRotation=Quaternion.Euler(0,0,90);Torus("木轮铜箍",wheel,Vector3.zero,.36f,.045f,iron);Torus("磨损木轮辋",wheel,Vector3.zero,.31f,.035f,wood);
                for(int spoke=0;spoke<10;spoke++){float angle=spoke*Mathf.PI/5;Tube("车轮辐榫",wheel,Vector3.zero,new Vector3(Mathf.Cos(angle)*.31f,0,Mathf.Sin(angle)*.31f),.024f,wood);}Stud(wheel,Vector3.up*.06f,.063f,bronze);
            }
            for(int box=0;box<3;box++)CrateAt(root,new Vector3((box-1)*.35f,.65f,.15f),.43f);
        }
        private void CrateAt(Transform parent,Vector3 p,float scale) {var root=new GameObject("包扎药物匣").transform;root.SetParent(parent,false);root.localPosition=p;root.localScale=Vector3.one*scale;Crate(root);}
        private void PaperStack(Transform parent,Vector3 p,int pages) {for(int i=0;i<pages;i++){var page=Slab("逐页卷宗纸",parent,p+Vector3.up*i*.008f,new Vector3(.43f,.007f,.30f),paper);page.transform.localRotation=Quaternion.Euler(0,(i%3-1)*3.8f,0);}Slab("束卷铜压条",parent,p+Vector3.up*(pages*.008f+.015f),new Vector3(.064f,.028f,.34f),bronze);}
        private void Coil(Transform parent,Vector3 p) {for(int i=0;i<4;i++)Torus("桥板麻绳盘",parent,p+Vector3.up*i*.018f,.16f+i*.017f,.014f,wood);}
        private void Rail(Transform parent,Vector3 p,float length,bool alongZ)
        {
            if(p.x>0)p.x=Mathf.Max(p.x,HalfWidth+1.28f);
            int posts=Mathf.CeilToInt(length/.9f);for(int i=0;i<=posts;i++){Vector3 q=p+(alongZ?Vector3.forward:Vector3.right)*(-length*.5f+i*length/posts);Slab("边廊立杆",parent,q+Vector3.up*.50f,new Vector3(.087f,1,.087f),bronze);Stud(parent,q+Vector3.up*1.025f,.068f,bronze);}
            for(int rail=0;rail<2;rail++)Slab("连续廊栏",parent,p+Vector3.up*(.39f+rail*.56f),alongZ?new Vector3(.074f,.09f,length):new Vector3(length,.09f,.074f),wood);
        }
        private void PipeSection(Transform parent,Vector3 a,Vector3 b,float radius)
        {
            Tube("铜质余热管体",parent,a,b,radius,bronze);Vector3 direction=(b-a).normalized;Quaternion rotation=Quaternion.FromToRotation(Vector3.up,direction);
            foreach(float t in new[]{.12f,.88f}){Vector3 p=Vector3.Lerp(a,b,t);Torus("管节法兰外圈",parent,p,radius*1.36f,radius*.14f,iron,rotation);Torus("铜法兰密封肩",parent,p+direction*.035f,radius*1.23f,radius*.12f,bronze,rotation);
                Vector3 side=Vector3.Cross(direction,Mathf.Abs(direction.y)>.85f?Vector3.right:Vector3.up).normalized,up=Vector3.Cross(direction,side);
                for(int bolt=0;bolt<8;bolt++){float angle=bolt*Mathf.PI/4;Vector3 offset=(side*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius*1.36f;Stud(parent,p+offset,.022f,bronze,rotation);}}
        }
        private void Valve(Transform parent,Vector3 p,float radius,bool vertical)
        {
            Quaternion rotation=vertical?Quaternion.Euler(90,0,0):Quaternion.identity;var root=new GameObject("检修阀门与轮柄").transform;root.SetParent(parent,false);root.localPosition=p;root.localRotation=rotation;
            Tube("阀杆",root,new Vector3(0,-.24f,0),Vector3.zero,.031f,bronze);Torus("阀门手轮",root,Vector3.zero,radius,.018f,bronze);for(int spoke=0;spoke<5;spoke++){float angle=spoke*Mathf.PI*2/5;Tube("阀轮辐条",root,Vector3.zero,new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius),.013f,bronze);}Stud(root,Vector3.up*.025f,.052f,iron);
        }
        private void StreetLantern(Vector3 p,float height)
        {
            BankPost(p,height);
            Vector3 basePoint=p+Vector3.up*(height+.43f);
            Block("方灯旧铜下座",geometry,basePoint,new Vector3(.37f,.10f,.37f),bronze,2,.02f);
            Slab("暖纸灯腔",geometry,basePoint+Vector3.up*.28f,new Vector3(.255f,.46f,.255f),paper);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Slab("方灯四角木框",geometry,basePoint+new Vector3(x*.15f,.28f,z*.15f),new Vector3(.033f,.49f,.033f),wood);
            for(int side=-1;side<=1;side+=2){Slab("方灯前后竖棂",geometry,basePoint+new Vector3(0,.28f,side*.153f),new Vector3(.025f,.46f,.026f),bronze);Slab("方灯左右竖棂",geometry,basePoint+new Vector3(side*.153f,.28f,0),new Vector3(.026f,.46f,.025f),bronze);}
            LatheObject("灯顶重檐铜帽",geometry,basePoint+Vector3.up*.55f,new[]{new Vector2(0,0),new Vector2(.28f,0),new Vector2(.23f,.055f),new Vector2(.14f,.12f),new Vector2(.095f,.20f),new Vector2(.075f,.24f),new Vector2(0,.25f)},bronze,4);
            var core=Visuals.MeshObject("灯中微暖火芯",geometry,Disc(.056f,12),ember);core.transform.localPosition=basePoint+Vector3.up*.31f;core.transform.localRotation=Quaternion.Euler(90,0,0);
            var light=new GameObject("街岸暖灯",typeof(Light)).GetComponent<Light>();light.transform.SetParent(geometry,false);light.transform.localPosition=basePoint+Vector3.up*.30f;light.type=LightType.Point;light.color=new Color(1,.72f,.42f);light.intensity=.80f;light.range=3.2f;light.shadows=LightShadows.None;
        }
        private void Lantern(Vector3 position,float height)
        {
            if(position.x>0&&position.z<HalfDepth)position.x=Mathf.Max(position.x,HalfWidth+(position.y+height+1.2f)*(11f/13)+.5f);
            var source=Resources.Load<GameObject>("Environment/wooden_lantern_01/model");
            if(source){var root=new GameObject("CC0 木框灯笼").transform;root.SetParent(geometry,false);root.localPosition=position;var instance=Instantiate(source,root,false);var renderers=instance.GetComponentsInChildren<Renderer>();var bounds=new Bounds();bool first=true;var material=lanternSurface;
                foreach(var renderer in renderers){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>material).ToArray();}
                float scale=height/Mathf.Max(.01f,bounds.size.y);instance.transform.localScale*=scale;var center=root.InverseTransformPoint(bounds.center);instance.transform.localPosition-=new Vector3(center.x,center.y-bounds.size.y*.5f,center.z)*scale;
            }
            Tube("灯笼挂链",geometry,position+Vector3.up*(height*.85f),position+Vector3.up*(height+1.20f),.009f,iron);
            var flame=Visuals.MeshObject("灯笼内暖光",geometry,Disc(.11f,12),ember);flame.transform.localPosition=position+Vector3.up*(height*.40f);flame.transform.localRotation=Quaternion.Euler(90,0,0);
            var light=new GameObject("冷雾中的暖灯",typeof(Light)).GetComponent<Light>();light.transform.SetParent(geometry,false);light.transform.localPosition=position+Vector3.up*(height*.40f);light.type=LightType.Point;light.color=new Color(1,.76f,.48f);light.intensity=1.7f;light.range=4.5f;light.shadows=LightShadows.None;
        }
        private void Fog(Vector3 position)
        {
            var go=new GameObject("沿冷凝渠缓散的水雾",typeof(ParticleSystem));go.transform.SetParent(geometry,false);go.transform.localPosition=position;var system=go.GetComponent<ParticleSystem>();
            var main=system.main;main.loop=true;main.startLifetime=10;main.startSpeed=.045f;main.startSize=new ParticleSystem.MinMaxCurve(1.20f,2.10f);main.startColor=new Color(.64f,.76f,.81f,.32f);main.maxParticles=14;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=system.emission;emission.rateOverTime=1.1f;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(1.4f,.07f,.72f);
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=fog;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;system.Simulate(5,true,true);system.Play();mist.Add(system);
        }
        private void Update()
        {
            if(ReducedMotion==mistPaused)return;mistPaused=ReducedMotion;if(water)water.SetFloat("_Motion",ReducedMotion?0:1);foreach(var system in mist)if(system){if(ReducedMotion)system.Pause();else system.Play();}
        }
        private Texture2D MistTexture()
        {
            var texture=Own(new Texture2D(64,64,TextureFormat.RGBA32,false){name="原创柔雾粒子",wrapMode=TextureWrapMode.Clamp});
            var colors=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=(x-31.5f)/31.5f,dy=(y-31.5f)/31.5f;float radius=Mathf.Sqrt(dx*dx+dy*dy);float a=Mathf.Pow(Mathf.Max(0,1-radius),2)*( .70f+.30f*Mathf.PerlinNoise(x*.11f,y*.11f));colors[y*64+x]=new Color(1,1,1,a);}texture.SetPixels(colors);texture.Apply(false,true);return texture;
        }
        private Texture2D GroundInkTexture(bool wet)
        {
            // Low-alpha, feathered brush grain retains the actual paving underneath. The
            // irregular strokes deliberately have no filled circular silhouette like a pit.
            const int size=96;var texture=Own(new Texture2D(size,size,TextureFormat.RGBA32,false){name=wet?"原创浅冷凝水纹":"原创墨紫地面微纹",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear});
            var colors=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                float u=(x+.5f)/size,v=(y+.5f)/size,noise=Mathf.PerlinNoise(x*.087f,y*.087f);
                float edge=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v))/.18f));
                float wave=wet?(v+noise*.026f)*27+Mathf.Sin(u*9)*.50f:(u*.85f+v)*19+noise*1.9f;
                float stroke=Mathf.Pow(Mathf.Max(0,Mathf.Sin(wave)),wet?18:12);
                float alpha=edge*(.14f+.29f*noise+.27f*stroke);colors[y*size+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(colors);texture.Apply(false,true);return texture;
        }
        private Mesh GroundPlane(float width,float depth)
        {
            string key="ground-plane:"+Key(width)+":"+Key(depth);if(meshes.TryGetValue(key,out var cached))return cached;
            var result=Mesh("贴地透明笔纹",new List<Vector3>{new Vector3(-width*.5f,0,-depth*.5f),new Vector3(-width*.5f,0,depth*.5f),new Vector3(width*.5f,0,depth*.5f),new Vector3(width*.5f,0,-depth*.5f)},new List<Vector2>{Vector2.zero,Vector2.up,Vector2.one,Vector2.right},new List<int>{0,1,2,0,2,3});meshes[key]=result;return result;
        }
        private Mesh WaterPlane(float width,float depth)
        {
            string key="water-plane:"+Key(width)+":"+Key(depth);if(meshes.TryGetValue(key,out var cached))return cached;
            var mesh=TacticalSceneryMesh.Water(width,depth);meshes[key]=mesh;return mesh;
        }
        private GameObject Block(string name,Transform parent,Vector3 p,Vector3 size,Material material,int variant,float bevel=.075f)
        {
            variant=((variant%12)+12)%12;
            bool painted=material==stone||material==stoneWarm||material==stoneCool||material==masonry||material==mortar||material==jade||material==moss||material==soil;
            string key="carved-stone:"+Key(size.x)+":"+Key(size.y)+":"+Key(size.z)+":"+variant+":"+Key(bevel)+":"+painted;
            if(!meshes.TryGetValue(key,out var mesh)){mesh=TacticalSceneryMesh.Stone(size,variant,bevel,painted);meshes[key]=mesh;}
            var go=Visuals.MeshObject(name,parent,mesh,material);go.transform.localPosition=p;return go;
        }
        private GameObject Plank(string name,Transform parent,Vector3 p,Vector3 size,int variant)
        {
            variant=((variant%12)+12)%12;string key="timber-plank:"+Key(size.x)+":"+Key(size.y)+":"+Key(size.z)+":"+variant;
            if(!meshes.TryGetValue(key,out var mesh)){mesh=TacticalSceneryMesh.Plank(size,variant);meshes[key]=mesh;}
            var go=Visuals.MeshObject(name,parent,mesh,wood);go.transform.localPosition=p;return go;
        }
        private GameObject RoofTile(string name,Transform parent,Vector3 p,float width,float depth)
        {
            string key="clay-roof:"+Key(width)+":"+Key(depth);if(!meshes.TryGetValue(key,out var mesh)){mesh=TacticalSceneryMesh.RoofTile(width,depth);meshes[key]=mesh;}
            var go=Visuals.MeshObject(name,parent,mesh,roof);go.transform.localPosition=p;return go;
        }
        private GameObject Slab(string name,Transform parent,Vector3 p,Vector3 size,Material material)
        {
            string key="bevel:"+Key(size.x)+":"+Key(size.y)+":"+Key(size.z);float radius=Mathf.Min(.14f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.28f);
            if(!meshes.TryGetValue(key,out var mesh)){mesh=RoundedBox(size,radius);mesh.name=name;meshes[key]=mesh;}
            var go=Visuals.MeshObject(name,parent,mesh,material);go.transform.localPosition=p;return go;
        }
        private void Stud(Transform parent,Vector3 p,float radius,Material material,Quaternion? rotation=null)
        {var go=LatheObject("六角铜铆钉",parent,p,new[]{new Vector2(0,-.010f),new Vector2(radius,-.010f),new Vector2(radius,.010f),new Vector2(radius*.75f,.017f),new Vector2(0,.017f)},material,6);if(rotation.HasValue)go.transform.localRotation=rotation.Value;}
        private void Tube(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material material)
        {float height=Vector3.Distance(a,b),lip=Mathf.Min(radius*.16f,height*.12f);var go=LatheObject(name,parent,(a+b)*.5f,new[]{new Vector2(0,-height*.5f),new Vector2(radius*.84f,-height*.5f),new Vector2(radius,-height*.5f+lip),new Vector2(radius,height*.5f-lip),new Vector2(radius*.84f,height*.5f),new Vector2(0,height*.5f)},material,24);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private GameObject LatheObject(string name,Transform parent,Vector3 p,Vector2[] profile,Material material,int sides=32)
        {
            string key="lathe:"+sides+":"+string.Join(";",profile.Select(v=>Key(v.x)+","+Key(v.y)));
            if(!meshes.TryGetValue(key,out var mesh)){var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();for(int ring=0;ring<profile.Length;ring++)for(int side=0;side<=sides;side++){
                float angle=side*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(angle)*profile[ring].x,profile[ring].y,Mathf.Sin(angle)*profile[ring].x));uv.Add(new Vector2(side/(float)sides,ring/(float)(profile.Length-1)));
                if(ring<profile.Length-1&&side<sides){int a=ring*(sides+1)+side,b=a+1,c=a+sides+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}}
                mesh=Mesh(name,vertices,uv,triangles);meshes[key]=mesh;}
            var go=Visuals.MeshObject(name,parent,mesh,material);go.transform.localPosition=p;return go;
        }
        private void Torus(string name,Transform parent,Vector3 p,float radius,float tube,Material material,Quaternion? rotation=null)
        {
            int rings=radius>1?96:40,sides=12;string key="torus:"+radius+":"+tube+":"+rings;
            if(!meshes.TryGetValue(key,out var mesh)){var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++){
                float angle=ring*Mathf.PI*2/rings,cross=side*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(angle)*(radius+tube*Mathf.Cos(cross)),tube*Mathf.Sin(cross),Mathf.Sin(angle)*(radius+tube*Mathf.Cos(cross))));uv.Add(new Vector2(ring/(float)rings,side/(float)sides));
                if(ring<rings&&side<sides){int a=ring*(sides+1)+side,b=a+1,c=a+sides+1,d=c+1;triangles.AddRange(new[]{a,b,c,b,d,c});}}
                mesh=Mesh(name,vertices,uv,triangles);meshes[key]=mesh;}
            var go=Visuals.MeshObject(name,parent,mesh,material);go.transform.localPosition=p;if(rotation.HasValue)go.transform.localRotation=rotation.Value;
        }
        private Mesh Disc(float radius,int sides)
        {
            string key="disc:"+radius+":"+sides;if(meshes.TryGetValue(key,out var old))return old;
            var v=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{Vector2.one*.5f};var t=new List<int>();for(int i=0;i<=sides;i++){float angle=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));uv.Add(new Vector2(.5f+Mathf.Cos(angle)*.5f,.5f+Mathf.Sin(angle)*.5f));if(i<sides)t.AddRange(new[]{0,i+2,i+1});}var result=Mesh("水面柔圆",v,uv,t);meshes[key]=result;return result;
        }
        private void ClothPanel(string name,Transform parent,Vector3 p,float width,float height,Material material)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int columns=16,rows=12;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++){float u=x/(float)columns,v=y/(float)rows;float px=(u-.5f)*width;vertices.Add(new Vector3(px,(v-.5f)*height-Mathf.Sin(u*Mathf.PI)*.034f,Mathf.Sin(px*21)*.023f));uv.Add(new Vector2(u,v));if(x<columns&&y<rows){int a=y*(columns+1)+x,b=a+1,c=a+columns+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}}
            var panel=Mesh(name,vertices,uv,triangles);transientMeshes.Add(panel);var go=Visuals.MeshObject(name,parent,panel,material);go.transform.localPosition=p;
        }
        private static Mesh Mesh(string name,List<Vector3> vertices,List<Vector2> uv,List<int> triangles)
        {var mesh=new Mesh{name=name,indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}
        private static Mesh RoundedBox(Vector3 size,float radius)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();Vector3 half=size*.5f,inner=half-Vector3.one*radius;
            foreach(var normal in new[]{Vector3.up,Vector3.down,Vector3.right,Vector3.left,Vector3.forward,Vector3.back}){
                var right=Vector3.Cross(normal,Mathf.Abs(normal.y)>.9f?Vector3.forward:Vector3.up).normalized;var up=Vector3.Cross(right,normal);int offset=vertices.Count;
                float halfRight=Mathf.Abs(right.x)*half.x+Mathf.Abs(right.y)*half.y+Mathf.Abs(right.z)*half.z,halfUp=Mathf.Abs(up.x)*half.x+Mathf.Abs(up.y)*half.y+Mathf.Abs(up.z)*half.z;
                float halfNormal=Mathf.Abs(normal.x)*half.x+Mathf.Abs(normal.y)*half.y+Mathf.Abs(normal.z)*half.z;
                var xs=BevelSamples(halfRight,radius);var ys=BevelSamples(halfUp,radius);
                for(int y=0;y<ys.Length;y++)for(int x=0;x<xs.Length;x++){
                    var p=normal*halfNormal+right*xs[x]+up*ys[y];var nearest=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));var outward=(p-nearest).normalized;
                    vertices.Add(nearest+outward*radius);normals.Add(outward);uv.Add(new Vector2((xs[x]/halfRight+1)*.5f,(ys[y]/halfUp+1)*.5f));if(x<xs.Length-1&&y<ys.Length-1){int a=offset+y*xs.Length+x,b=a+1,c=a+xs.Length,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}
                }
            }
            var mesh=new Mesh{name="倒角建筑模块"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        private static float[] BevelSamples(float half,float radius)
        {float inner=half-radius;return new[]{-half,-inner-radius*.80f,-inner-radius*.38f,-inner,0,inner,inner+radius*.38f,inner+radius*.80f,half};}
        private static string Key(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
        private void CombineStatic()
        {
            ArchitecturalPartCount=geometry.GetComponentsInChildren<MeshFilter>().Length;
            var groups=new Dictionary<(Material material,int x,int z,bool casts),List<CombineInstance>>();var sources=new List<GameObject>();
            foreach(var filter in geometry.GetComponentsInChildren<MeshFilter>()){
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;var materials=renderer.sharedMaterials;
                Vector3 center=geometry.InverseTransformPoint(renderer.bounds.center);int sectorX=Mathf.FloorToInt(center.x/6),sectorZ=Mathf.FloorToInt(center.z/6);
                for(int index=0;index<materials.Length;index++){var material=materials[index];if(!material||index>=filter.sharedMesh.subMeshCount)continue;var key=(material,sectorX,sectorZ,renderer.shadowCastingMode!=ShadowCastingMode.Off&&material.renderQueue<3000);if(!groups.TryGetValue(key,out var items)){items=new List<CombineInstance>();groups[key]=items;}items.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=index,transform=geometry.worldToLocalMatrix*filter.transform.localToWorldMatrix});}
                sources.Add(filter.gameObject);
            }
            foreach(var group in groups){var combined=new Mesh{name="局部建筑合批 · "+group.Key.material.name+" · "+group.Key.x+","+group.Key.z,indexFormat=IndexFormat.UInt32};transientMeshes.Add(combined);combined.CombineMeshes(group.Value.ToArray(),true,true);var go=Visuals.MeshObject(combined.name,geometry,combined,group.Key.material);var renderer=go.GetComponent<MeshRenderer>();renderer.shadowCastingMode=group.Key.casts?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=true;}
            foreach(var source in sources){source.SetActive(false);Destroy(source);}
        }
        private void OnDestroy()
        {
            if(geometry){geometry.gameObject.SetActive(false);Destroy(geometry.gameObject);}
            foreach(var mesh in transientMeshes)if(mesh)Destroy(mesh);transientMeshes.Clear();
            foreach(var mesh in meshes.Values.Distinct())if(mesh)Destroy(mesh);meshes.Clear();
            foreach(var resource in resources)if(resource)Destroy(resource);resources.Clear();
        }
    }
}
