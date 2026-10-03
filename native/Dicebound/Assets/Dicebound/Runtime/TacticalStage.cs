using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>World presentation consumes committed states; animation never mutates rules.</summary>
    public sealed class TacticalStage : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public TacticalEnvironment Environment { get; private set; }
        public TacticalSkillVfx SkillVfx { get; private set; }
        public TacticalUltimateCutIn UltimateCutIn { get; private set; }
        public TacticalRangeRenderer RangeRenderer { get; private set; }
        public TacticalCombatNumbers CombatNumbers { get; private set; }
        public int ContactCount { get; private set; }
        public int CameraImpulseCount { get; private set; }
        public float CameraImpulsePixels { get; private set; }
        public TacticalRangeSnapshot RangeSnapshot { get; private set; }
        public float RangeBorderPixels => TacticalRangeRenderer.BorderPixels;
        public int ActorCount => actors.Count;
        public int AnimatedActorCount => actors.Values.Count(a => a.HasAnimationFrames && a.FrameCount == 12);
        public int SceneModelCount => Environment ? Environment.SceneModelCount : 0;
        public int ArchitecturalPartCount => Environment ? Environment.ArchitecturalPartCount : 0;
        public int PresentedActions { get; private set; }
        public int EnemyTurnPresentations { get; private set; }
        public int BoardWidth => Environment?Environment.BoardWidth:8;
        public int BoardHeight => Environment?Environment.BoardHeight:8;
        public float DefaultOrthographicSize {get;private set;}=5.9f;
        public Rect BoardSafeRect => boardSafeOverride??TacticalTheme.BoardSafeRect;
        public bool GroundViewActive {get;private set;}
        private readonly Dictionary<string,TacticalActor> actors = new Dictionary<string,TacticalActor>();
        private readonly Dictionary<string,Transform> badges = new Dictionary<string,Transform>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Dictionary<Color,Material> ink = new Dictionary<Color,Material>();
        private Material contactMaterial;
        private readonly Dictionary<TacticalActor,Vector3> recoilOrigins = new Dictionary<TacticalActor,Vector3>();
        private readonly Dictionary<TacticalActor,int> recoilTickets = new Dictionary<TacticalActor,int>();
        private int recoilGeneration;
        private Transform world, actorRoot, markings, effects;
        private Rect? boardSafeOverride;
        private TacticalDirector director;
        private TacticalState shown;
        private string scene;
        private bool visible;
        private bool groundViewLatched,groundViewHeld,groundAltBlocked;
        private Vector3 focus = new Vector3(0,.15f,0);
        private bool actionFramed;
        private Vector3 playerFocus;
        private float playerViewSize;
        private Vector3 defaultFocus;
        private float impulseStarted, impulseDuration, impulseStrength;
        private Vector2 impulseDirection;
        private Vector3 dragOrigin;
        private int hoverX = -1, hoverY = -1;
        private static Color Gold => TacticalTheme.Gold;
        private static Color Teal => TacticalTheme.Ally;
        private static Color Red => TacticalTheme.Enemy;

        public void Initialize(TacticalDirector owner)
        {
            director=owner;
            world = Root("Yaojing tactical world", transform);
            Environment=gameObject.AddComponent<TacticalEnvironment>();
            Camera=Root("Tactical camera",transform).gameObject.AddComponent<Camera>();
            Camera.tag="MainCamera"; Camera.gameObject.AddComponent<AudioListener>();
            Camera.orthographic=true; Camera.orthographicSize=5.9f; Camera.nearClipPlane=.1f; Camera.farClipPlane=100;
            Camera.backgroundColor=new Color(.22f,.29f,.33f); Camera.clearFlags=CameraClearFlags.SolidColor;
            Camera.allowHDR=true; Camera.allowMSAA=true;
            Camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            SkillVfx=gameObject.AddComponent<TacticalSkillVfx>();SkillVfx.Initialize(Camera,world);
            UltimateCutIn=gameObject.AddComponent<TacticalUltimateCutIn>();UltimateCutIn.Initialize();
            RangeRenderer=gameObject.AddComponent<TacticalRangeRenderer>();RangeRenderer.Initialize(Camera,Environment,world);
            QualitySettings.shadows=UnityEngine.ShadowQuality.All; QualitySettings.shadowResolution=UnityEngine.ShadowResolution.High;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.25f,.34f,.46f);
            RenderSettings.ambientEquatorColor=new Color(.15f,.21f,.31f);
            RenderSettings.ambientGroundColor=new Color(.09f,.14f,.22f);
            // A view-depth haze separates the rear houses from the close stonework.
            // Tactical floors remain before its start; actor projection stays unchanged.
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.31f,.42f,.49f); RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=31;RenderSettings.fogEndDistance=49;
            // Side light makes the low walls' cast shadows visible instead of hiding
            // almost the entire shadow behind each wall in this isometric camera.
            Light("Warm dawn through the eaves",new Vector3(34,35,0),new Color(1,.95f,.83f),1.82f,true);
            Light("Cool canal shadow bounce",new Vector3(35,138,0),new Color(.54f,.72f,.94f),.14f,false);
            Light("Warm frontage fill",new Vector3(32,55,0),new Color(1,.92f,.78f),.03f,false);
            var studio=Resources.Load<Cubemap>("Tactics/Environment/studio_small_09_1k");
            if(!studio)throw new InvalidOperationException("Missing tactical studio reflection cubemap.");
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=studio;RenderSettings.reflectionIntensity=.12f;
            var probe=Root("Subtle canal reflections",world).gameObject.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=studio;probe.boxProjection=false;probe.size=new Vector3(150,80,150);probe.center=Vector3.zero;probe.intensity=.12f;probe.blendDistance=0;
            var volume=Root("Yaojing colour grade",world).gameObject.AddComponent<Volume>(); volume.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile); volume.sharedProfile=profile;
            var tone=profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var adjustment=profile.Add<ColorAdjustments>(); adjustment.postExposure.Override(.10f); adjustment.contrast.Override(2); adjustment.saturation.Override(1);
            var bloom=profile.Add<Bloom>(); bloom.intensity.Override(.10f); bloom.threshold.Override(1.5f);
            // Gaussian far blur starts beyond the complete tactical rectangle; neither
            // sprite faces nor the nearest legal cells enter this optical transition.
            var depthOfField=profile.Add<DepthOfField>();depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
            depthOfField.gaussianStart.Override(34);depthOfField.gaussianEnd.Override(46);depthOfField.gaussianMaxRadius.Override(1.55f);depthOfField.highQualitySampling.Override(true);
            SetVisible(false); UpdateCamera();
        }
        private Transform Root(string name,Transform parent) { var root=new GameObject(name).transform;root.SetParent(parent,false);return root; }
        private Material Material(Color color,bool unlit)
        {
            var material=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",color); if(!unlit){material.SetFloat("_Metallic",.65f);material.SetFloat("_Smoothness",.4f);}
            if(color.a<.999f){material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_Cull",(int)CullMode.Off);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3005;}
            owned.Add(material);return material;
        }
        private void Light(string name,Vector3 rotation,Color color,float intensity,bool shadows)
        {var item=Root(name,world);item.localEulerAngles=rotation;var light=item.gameObject.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.shadows=shadows?LightShadows.Soft:LightShadows.None;light.shadowStrength=.88f;light.shadowBias=.022f;light.shadowNormalBias=.04f;var data=light.gameObject.AddComponent<UniversalAdditionalLightData>();data.usePipelineSettings=false;data.softShadowQuality=SoftShadowQuality.High;if(shadows)RenderSettings.sun=light;}
        public void SetVisible(bool value) {visible=value;if(!value){ClearHover();CancelEffects();}if(world)world.gameObject.SetActive(value);if(Environment)Environment.SetVisible(value);}
        public void SetGroundView(bool value)
        {
            groundViewLatched=value&&visible&&shown!=null&&!director.Busy&&!director.Modal;
            if(!value){groundViewHeld=false;groundAltBlocked=Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);}
            RefreshGroundView();
        }
        public void ExitGroundView() {groundViewLatched=groundViewHeld=false;groundAltBlocked=Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);RefreshGroundView();}
        private void RefreshGroundView()
        {
            bool active=visible&&(groundViewLatched||groundViewHeld)&&shown!=null&&!director.Busy&&!director.Modal;
            if(active==GroundViewActive)return;GroundViewActive=active;
            foreach(var pair in actors)
            {
                pair.Value.SetBodyVisible(!active);
                if(!badges.TryGetValue(pair.Key,out var badge)||!badge)continue;
                badge.position=pair.Value.VisualPoint(active?.26f:pair.Value.StandingHeight+.22f);
                foreach(Transform child in badge)child.gameObject.SetActive(!active||child.name=="Name");
            }
            ClearHover();director.RefreshGroundViewControl();
        }
        public void Synchronize(TacticalState state)
        {
            CancelEffects();
            // An imported archive may share a seed and chapter but have taken a different route.
            // Terrain is part of the rendered board identity; stale tiles must never survive an import.
            string key=state.seed+":"+state.chapter+":"+state.sideBattle+":"+state.nodeKind+":"+TacticalRules.BoardWidth(state)+"x"+TacticalRules.BoardHeight(state)+":"+
                string.Join("|",state.terrain.OrderBy(c=>c.y).ThenBy(c=>c.x).Select(c=>c.x+","+c.y+"="+c.kind));
            bool rebuilt=scene!=key;
            // A valve or rusted enemy can change terrain without starting a new
            // battle. Rebuilding those tiles must not reset every actor's facing.
            bool sameBattle=TacticalBattlePresentation.KeepFacingOnRebuild(shown,state);
            var previousFacing=rebuilt&&sameBattle?actors.ToDictionary(pair=>pair.Key,pair=>pair.Value.transform.rotation):null;
            if(scene!=key)
            {
                scene=key;ClearHover();Clear(actorRoot); Clear(markings); Clear(effects); actors.Clear();badges.Clear();
                actorRoot=Root("Companions and constructs",world);markings=Root("Tactical markings",world);effects=Root("Committed action effects",world);
                Environment.Build(state,world);
            }
            shown=state; Environment.RefreshObjects(state);
            foreach(string id in actors.Keys.Where(id=>!state.units.Any(u=>u.id==id)).ToArray())
            {Clear(actors[id].transform);actors.Remove(id);if(badges.TryGetValue(id,out var badge))Clear(badge);badges.Remove(id);}
            foreach(var unit in state.units)
            {
                if(!actors.TryGetValue(unit.id,out var actor))
                {
                    actor=Root("Actor "+unit.id,actorRoot).gameObject.AddComponent<TacticalActor>();
                    actor.Build(unit.heroId,unit.team!="hero");actors.Add(unit.id,actor);
                    var hit=actor.gameObject.AddComponent<TacticalActorHit>();hit.UnitId=unit.id;
                    // Sprite silhouette picking follows its tilted camera plane; a vertical
                    // capsule would select transparent space or miss the displayed head.
                    var collider=actor.GetComponent<CapsuleCollider>();if(collider)collider.enabled=false;
                    actor.transform.rotation=previousFacing!=null&&previousFacing.TryGetValue(unit.id,out var facing)?facing:Quaternion.Euler(0,unit.team=="hero"?30:210,0);
                }
                actor.gameObject.SetActive(unit.hp>0);
                actor.transform.position=Environment.CellWorld(unit.x,unit.y); actor.ResetMotion(); actor.Idle();
                if(badges.TryGetValue(unit.id,out var previous))Clear(previous);
                if(unit.hp>0) badges[unit.id]=Badge(unit);
            }
            RangeRenderer.Protect(badges.Values);
            if(rebuilt)FitBoardView();SetVisible(true);Physics.SyncTransforms();UpdateCamera();
        }
        private Transform Badge(TacticalUnit unit)
        {
            var root=new GameObject("Status "+unit.id,typeof(RectTransform),typeof(Canvas)).transform;root.SetParent(actorRoot,false);root.position=actors[unit.id].VisualPoint(actors[unit.id].StandingHeight+.22f);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var rect=(RectTransform)root;rect.sizeDelta=new Vector2(176,82);rect.localScale=Vector3.one*.0082f;
            var name=Ui.Label("Name",root,unit.name,Vector2.one*.5f,new Vector2(0,16),new Vector2(164,32),21,TacticalTheme.OnDark);
            var outline=name.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.025f,.032f,.04f,.95f);outline.effectDistance=new Vector2(1.5f,-1.5f);
            Ui.Panel("Health dark",root,Vector2.one*.5f,new Vector2(0,-11),new Vector2(132,14),new Color(.02f,.025f,.028f,.95f));
            float capacity=Mathf.Max(1,unit.maxHp,unit.hp+unit.block),health=unit.hp/capacity,shield=unit.block/capacity;
            Color life=unit.hp<=unit.maxHp*.3f?TacticalTheme.Danger:unit.team=="hero"?new Color(.49f,.82f,.27f):new Color(.94f,.27f,.23f);
            Ui.Panel("Health",root,Vector2.one*.5f,new Vector2(-62+62*health,-11),new Vector2(124*health,8),life);
            Ui.Panel("Health upper glint",root,Vector2.one*.5f,new Vector2(-62+62*health,-8),new Vector2(124*health,1),Color.Lerp(life,Color.white,.48f));
            Ui.Panel("Shield",root,Vector2.one*.5f,new Vector2(-62+124*health+62*shield,-11),new Vector2(124*shield,8),TacticalArt.ShieldMeterColor);
            var boundary=Ui.Panel("Health shield boundary",root,Vector2.one*.5f,new Vector2(-62+124*health,-11),new Vector2(3,12),new Color(.035f,.055f,.10f));
            var boundaryGlint=Ui.Panel("Health shield boundary glint",root,Vector2.one*.5f,new Vector2(-62+124*health,-11),new Vector2(1,10),new Color(.94f,.96f,1));
            boundary.gameObject.SetActive(unit.hp>0&&unit.block>0);boundaryGlint.gameObject.SetActive(unit.hp>0&&unit.block>0);
            var shieldText=Ui.Label("Shield amount",root,unit.block>0?"盾 "+unit.block:"",Vector2.one*.5f,new Vector2(0,-30),new Vector2(156,25),18,TacticalArt.ShieldLabelColor,TextAnchor.MiddleCenter);
            var shieldOutline=shieldText.gameObject.AddComponent<Outline>();shieldOutline.effectColor=new Color(.015f,.025f,.04f,.95f);shieldOutline.effectDistance=new Vector2(1,-1);
            root.rotation=Camera.transform.rotation;return root;
        }
        public Vector2 TargetScreenPoint(int x,int y)
        {
            if(TryTargetScreenPoint(x,y,out var point))return point;
            throw new InvalidOperationException("目标 "+TacticalDirector.GridName(x,y)+" 没有稳定的可点击区域"+(GroundViewActive?"，真实墙体或界面仍遮挡该格。":"；请按住 Alt 或点击「查看地面」后选择。"));
        }
        public bool TryTargetScreenPoint(int x,int y,out Vector2 result)
        {
            result=Vector2.zero;if(shown==null||!TacticalRules.Inside(shown,x,y))return false;
            var occupied=shown.units.FirstOrDefault(u=>u.hp>0&&u.x==x&&u.y==y);
            if(!GroundViewActive&&occupied!=null&&actors.TryGetValue(occupied.id,out var actor))foreach(float height in new[]{1.35f,1.05f,.75f,.45f,.18f})foreach(float dx in new[]{0f,-.12f,.12f,-.25f,.25f})
            {Vector2 point=Camera.WorldToScreenPoint(actor.VisualPoint(height)+Camera.transform.right*dx);if(StableTargetPatch(point,x,y)){result=point;return true;}}
            foreach(float height in new[]{.015f})
            foreach(float dx in new[]{0f,-.2f,.2f,-.5f,.5f})foreach(float dz in new[]{0f,-.2f,.2f,-.5f,.5f})
            {Vector2 point=Camera.WorldToScreenPoint(Environment.CellWorld(x,y)+new Vector3(dx,height,dz));if(StableTargetPatch(point,x,y)){result=point;return true;}}
            // Walls can leave a broad oblique strip between the coarse world offsets.
            // Search only this actual tile's projected footprint, preserving the same
            // opaque raycast and four-frame silhouette checks as ordinary candidates.
            var centre=Environment.CellWorld(x,y)+Vector3.up*.015f;float half=TacticalEnvironment.TileSize*.5f;
            var corners=new[]{new Vector3(-half,0,-half),new Vector3(-half,0,half),new Vector3(half,0,-half),new Vector3(half,0,half)}.Select(p=>(Vector2)Camera.WorldToScreenPoint(centre+p)).ToArray();
            var visiblePixels=new List<Vector2>();int candidates=0;
            for(int sy=Mathf.CeilToInt(corners.Min(p=>p.y));sy<=Mathf.FloorToInt(corners.Max(p=>p.y));sy++)
            for(int sx=Mathf.CeilToInt(corners.Min(p=>p.x));sx<=Mathf.FloorToInt(corners.Max(p=>p.x));sx++)
            {var point=new Vector2(sx,sy);candidates++;if(StableTargetPoint(point,x,y))visiblePixels.Add(point);}
            Vector2 projectedCentre=Camera.WorldToScreenPoint(centre);
            foreach(var point in visiblePixels.OrderBy(p=>(p-projectedCentre).sqrMagnitude))if(StableTargetPatch(point,x,y))
            {
                result=point;
                Debug.Log("DICEBOUND_TILE_PICK_SCAN cell="+TacticalDirector.GridName(x,y)+" ground="+GroundViewActive+" testedPixels="+candidates+" stableVisiblePixels="+visiblePixels.Count+" contiguousPixels=81 screen="+point);
                return true;
            }
            Debug.Log("DICEBOUND_TILE_PICK_SCAN cell="+TacticalDirector.GridName(x,y)+" ground="+GroundViewActive+" testedPixels="+candidates+" stableVisiblePixels="+visiblePixels.Count+" contiguousPixels=0");
            return false;
        }
        private bool StableTargetPatch(Vector2 point,int x,int y)
        {
            // Reject the border quickly, then verify every pixel of the 9x9 area.
            foreach(float dx in new[]{-4f,0,4f})foreach(float dy in new[]{-4f,0,4f})
                if(!StableTargetPoint(point+new Vector2(dx,dy),x,y))return false;
            for(int dx=-4;dx<=4;dx++)for(int dy=-4;dy<=4;dy++)
                if(!StableTargetPoint(point+new Vector2(dx,dy),x,y))return false;
            return true;
        }
        private bool StableTargetPoint(Vector2 point,int x,int y)
        {
            if(OverInterface(point)||!PointCell(point,out int px,out int py)||px!=x||py!=y)return false;
            var ray=Camera.ScreenPointToRay(point);float groundDistance=float.PositiveInfinity;int gx=-1,gy=-1;
            foreach(var hit in Physics.RaycastAll(ray,100).OrderBy(h=>h.distance))
            {
                if(hit.collider.GetComponentInParent<TacticalActorHit>())continue;
                var item=hit.collider.GetComponentInParent<TacticalObjectMarker>();var tile=hit.collider.GetComponent<TacticalTileHit>();
                if(!item&&!tile)continue;gx=item?item.X:tile.X;gy=item?item.Y:tile.Y;groundDistance=hit.distance;break;
            }
            float cutoff=gx==x&&gy==y?groundDistance:float.PositiveInfinity;
            foreach(var actor in actors.Values)
            {var unit=TacticalRules.FindUnit(shown,actor.GetComponent<TacticalActorHit>().UnitId);if(unit!=null&&unit.hp>0&&unit.x==x&&unit.y==y&&actor.TryHitIdleFrames(ray,out float distance,out bool all)&&all)cutoff=Mathf.Min(cutoff,distance);}
            if(float.IsPositiveInfinity(cutoff)||cutoff>groundDistance)return false;
            foreach(var actor in actors.Values)
            {var unit=TacticalRules.FindUnit(shown,actor.GetComponent<TacticalActorHit>().UnitId);if(unit!=null&&unit.hp>0&&(unit.x!=x||unit.y!=y)&&actor.TryHitIdleFrames(ray,out float distance,out _)&&distance<cutoff)return false;}
            return true;
        }
        public bool PointCell(Vector2 point,out int x,out int y)
        {
            x=y=-1;if(!visible||shown==null||!PaperViewport.Contains(point))return false;var ray=Camera.ScreenPointToRay(point);float actorDistance=float.PositiveInfinity;
            foreach(var actor in actors.Values)if(actor.TryHit(ray,out float distance)&&distance<actorDistance){var unit=TacticalRules.FindUnit(shown,actor.GetComponent<TacticalActorHit>().UnitId);if(unit!=null&&unit.hp>0){x=unit.x;y=unit.y;actorDistance=distance;}}
            foreach(var hit in Physics.RaycastAll(ray,100).OrderBy(h=>h.distance))
            {
                if(hit.collider.GetComponentInParent<TacticalActorHit>())continue;
                if(actorDistance<hit.distance)return true;
                var item=hit.collider.GetComponentInParent<TacticalObjectMarker>();
                if(item){x=item.X;y=item.Y;return true;}
                var tile=hit.collider.GetComponent<TacticalTileHit>();if(tile){x=tile.X;y=tile.Y;return true;}
            }
            if(!float.IsPositiveInfinity(actorDistance))return true;
            return Environment.CellFromHit(ray,out x,out y);
        }
        public bool PressAt(Vector2 point)
        {if(OverInterface(point)||!PointCell(point,out var x,out var y))return false;director.CellPress(x,y);return true;}
        private bool OverInterface(Vector2 point)
        {
            if(!EventSystem.current)return false;
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);return hits.Count>0;
        }
        private void Update()
        {
            UpdateCamera();
            if(Environment)Environment.ReducedMotion=director.ReducedMotion;
            bool alt=Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);if(!alt)groundAltBlocked=false;
            groundViewHeld=alt&&!groundAltBlocked&&Application.isFocused;RefreshGroundView();
            if(!visible||director.Busy||director.Modal){ClearHover();return;}
            // Keep the last board target while reading HUD summaries or opening its inspector.
            Vector2 mouse=Input.mousePosition;if(OverInterface(mouse))return;
            if(Input.GetMouseButtonDown(0))PressAt(mouse);
            if(PointCell(mouse,out int x,out int y)){if(x!=hoverX||y!=hoverY){hoverX=x;hoverY=y;director.Hover(x,y);}}
            else ClearHover();
            float scroll=Input.mouseScrollDelta.y;if(Mathf.Abs(scroll)>.01f)ZoomView(scroll);
            if(Input.GetMouseButtonDown(2))dragOrigin=Input.mousePosition;
            if(Input.GetMouseButton(2))
            {var delta=Input.mousePosition-dragOrigin;dragOrigin=Input.mousePosition;PanView(delta);}
            if(Input.GetKeyDown(KeyCode.Home))ResetView();
        }
        public void SetBoardSafeRect(Rect designRect,bool refit=true)
        {
            if(designRect.width<=0||designRect.height<=0||float.IsNaN(designRect.x)||float.IsNaN(designRect.y)||float.IsNaN(designRect.width)||float.IsNaN(designRect.height)||float.IsInfinity(designRect.x)||float.IsInfinity(designRect.y)||float.IsInfinity(designRect.width)||float.IsInfinity(designRect.height))throw new ArgumentOutOfRangeException(nameof(designRect));
            if(BoardSafeRect==designRect)return;boardSafeOverride=designRect;if(refit&&shown!=null)FitBoardView();
        }
        public void FitBoardView()
        {
            if(!Camera||!Environment)return;Rect safe=BoardSafeRect;
            // HUD layout supplies the available design-space rectangle, including variable
            // skill-tray height. The camera must not maintain its own copy of those edges.
            Vector3 right=Camera.transform.right,up=Camera.transform.up;
            float halfX=Mathf.Abs(right.x)*Environment.HalfWidth+Mathf.Abs(right.z)*Environment.HalfDepth;
            float halfY=Mathf.Abs(up.x)*Environment.HalfWidth+Mathf.Abs(up.z)*Environment.HalfDepth;
            float horizontal=actors.Count==0?.8f:Mathf.Max(.8f,actors.Values.Max(actor=>actor.HorizontalExtent));
            float top=Mathf.Max(TacticalActor.VisibleHeight+.56f,actors.Count==0?0:actors.Values.Max(actor=>actor.FrameHeight))+.25f*up.y;
            Vector2 design=PaperViewport.DesignSize;
            DefaultOrthographicSize=Mathf.Max(5.9f,Mathf.Max((halfX+horizontal+.12f)/(PaperViewport.Aspect*(safe.width/design.x)),(halfY+top*.5f+.16f)/(safe.height/design.y)));
            defaultFocus=up*(top*.5f)-right*(DefaultOrthographicSize*PaperViewport.Aspect*(safe.center.x/(design.x*.5f)))-up*(DefaultOrthographicSize*(safe.center.y/(design.y*.5f)));
            ResetView();
        }
        public void ResetView() {focus=defaultFocus;Camera.orthographicSize=DefaultOrthographicSize;UpdateCamera();}
        public void ZoomView(float wheelDelta)
        {
            if(float.IsNaN(wheelDelta)||float.IsInfinity(wheelDelta))throw new ArgumentOutOfRangeException(nameof(wheelDelta));
            if(!Camera)return;Camera.orthographicSize=Mathf.Clamp(Camera.orthographicSize-wheelDelta*.45f,3.8f,DefaultOrthographicSize*1.3f);UpdateCamera();
        }
        public void PanView(Vector2 deltaPixels)
        {
            if(float.IsNaN(deltaPixels.x)||float.IsInfinity(deltaPixels.x)||float.IsNaN(deltaPixels.y)||float.IsInfinity(deltaPixels.y))throw new ArgumentOutOfRangeException(nameof(deltaPixels));
            if(!Camera||!Environment)return;var right=Camera.transform.right;var forward=Vector3.ProjectOnPlane(Camera.transform.up,Vector3.up).normalized;
            float pixelWorld=Camera.orthographicSize*2/Mathf.Max(1,PaperViewport.Pixels.height);focus-=right*deltaPixels.x*pixelWorld+forward*deltaPixels.y*pixelWorld;
            focus.x=Mathf.Clamp(focus.x,defaultFocus.x-Environment.HalfWidth*.65f,defaultFocus.x+Environment.HalfWidth*.65f);focus.z=Mathf.Clamp(focus.z,defaultFocus.z-Environment.HalfDepth*.65f,defaultFocus.z+Environment.HalfDepth*.65f);UpdateCamera();
        }
        private void UpdateCamera()
        {
            if(!Camera)return;PaperViewport.Apply(Camera);var offset=new Vector3(11,13,-14);
            if(Environment&&Environment.PaintedActive){float size=Camera.orthographicSize;var position=Environment.PaintedScene.ClampView(focus+offset,Camera.aspect,ref size);focus=position-offset;Camera.orthographicSize=size;}
            Camera.transform.position=focus+offset;Camera.transform.LookAt(focus);
            // Apply a short damped screen-space translation after the normal camera fit.
            // Player focus/zoom and the painted camera clamp remain the unshaken baseline.
            CameraImpulsePixels=0;
            float age=Time.unscaledTime-impulseStarted;
            if(age<impulseDuration&&impulseDuration>0&&!director.ReducedMotion)
            {
                float t=age/impulseDuration,decay=Mathf.Pow(1-t,3);
                Vector2 pixels=(impulseDirection*Mathf.Cos(t*Mathf.PI*3.5f)+new Vector2(-impulseDirection.y,impulseDirection.x)*Mathf.Sin(t*Mathf.PI*5)*.24f)*impulseStrength*decay;
                CameraImpulsePixels=pixels.magnitude;
                float worldPerPixel=Camera.orthographicSize*2/Mathf.Max(1,PaperViewport.Pixels.height);
                Camera.transform.position+=(Camera.transform.right*pixels.x+Camera.transform.up*pixels.y)*worldPerPixel;
            }
            foreach(var pair in badges)if(pair.Value)
            {
                pair.Value.rotation=Camera.transform.rotation;
                if(actors.TryGetValue(pair.Key,out var actor))pair.Value.position=actor.VisualPoint(GroundViewActive?.26f:actor.StandingHeight+.22f);
            }
        }
        public void Selection(TacticalState state,string unitId,string skill,TacticalRequest target,bool locked=true)
        {
            if(!markings)return;ClearChildren(markings);
            RangeSnapshot=TacticalRangeSnapshot.Build(state,unitId,skill,target,locked);
            RangeRenderer.Show(state,unitId,skill,RangeSnapshot);
            if(target==null)hoverX=hoverY=-1;
        }
        private Material InkMaterial(Color color) {if(!ink.TryGetValue(color,out var material)){material=Material(color,true);ink[color]=material;}return material;}
        private static Color WithAlpha(Color color,float alpha) {color.a=alpha;return color;}
        private void ClearHover()
        {bool active=hoverX>=0||hoverY>=0;hoverX=hoverY=-1;if(active&&director)director.HoverExit();}
        private LineRenderer Line(string name,Vector3[] points,Color color,float width,Transform parent,bool loop=false)
        {var line=Root(name,parent).gameObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.sharedMaterial=InkMaterial(color);line.startWidth=line.endWidth=width;line.positionCount=points.Length;line.SetPositions(points);line.loop=loop;line.numCapVertices=3;line.numCornerVertices=3;return line;}
        private LineRenderer Ring(string name,Vector3 center,float radius,Color color,Transform parent,float width=.02f)
        {return Line(name,Enumerable.Range(0,48).Select(i=>center+new Vector3(Mathf.Cos(i*Mathf.PI/24)*radius,0,Mathf.Sin(i*Mathf.PI/24)*radius)).ToArray(),color,width,parent,true);}
        private static Vector3 Arc(Vector3 a,Vector3 b,float t)=>Vector3.Lerp(a,b,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*1.7f;
        public IEnumerator Present(TacticalState before,TacticalState after,TacticalRequest request,bool reduced)
        {
            ExitGroundView();
            if(!CombatNumbers){CombatNumbers=gameObject.AddComponent<TacticalCombatNumbers>();CombatNumbers.Initialize(Camera,director.Canvas,this);}
            PresentedActions++;ClearChildren(markings);RangeSnapshot=null;RangeRenderer.Clear();
            foreach(var badge in badges.Values)if(badge)
            {badge.gameObject.SetActive(true);var name=badge.Find("Name");if(name)name.gameObject.SetActive(false);}
            float factor=reduced?.12f:1f;
            var receipts=new HashSet<string>();
            bool resolvedEnemies=request.type=="endTurn"&&(after.round>before.round||after.phase!="battle"&&before.units.Any(u=>u.team=="enemy"&&u.hp>0)&&after.units.Where(u=>u.team=="hero"&&u.hp>0).All(u=>u.turnEnded));
            if(resolvedEnemies)
            {
                EnemyTurnPresentations++;
                var intents=TacticalRules.EnemyIntents(before).ToArray();
                for(int i=0;i<intents.Length;i++)
                {
                    var intent=intents[i];
                    var old=TacticalRules.FindUnit(before,intent.unitId);var next=TacticalRules.FindUnit(after,intent.unitId);
                    if(old==null||!actors.TryGetValue(old.id,out var enemy))continue;
                    if(next!=null&&(old.x!=next.x||old.y!=next.y))yield return Travel(enemy,new[]{Environment.CellWorld(old.x,old.y)}.Concat(intent.path.Select(c=>Environment.CellWorld(c.x,c.y))).ToArray(),factor);
                    if(intent.attacks&&actors.TryGetValue(intent.targetId??"",out var target))
                    {
                        Face(enemy,target.transform.position);director.Audio.PlayTactical("windup","",.65f);
                        yield return Tween(.10f*factor,t=>enemy.Pose("attack",t*.30f));
                        enemy.Pose("attack",.55f);
                        Contact(target,enemy.transform.position,Red,1,intent.damage==0,reduced);
                        // A turn is one committed state transition. If several enemies hit
                        // the same ally, show its exact net receipt once at the last contact.
                        if(!intents.Skip(i+1).Any(n=>n.attacks&&n.targetId==intent.targetId))
                            PresentReceipt(before,after,intent.targetId,enemy.transform.position,"",false,reduced,receipts,false);
                        yield return new WaitForSecondsRealtime(reduced?.012f:.042f);
                        yield return Tween(.14f*factor,t=>enemy.Pose("attack",.55f+t*.45f));enemy.Idle();
                    }
                }
            }
            else if((request.type=="move"||request.type=="skill")&&actors.TryGetValue(request.unitId??"",out var actor))
            {
                var plan=TacticalRules.Preview(before,request);
                var target=Environment.CellWorld(request.x,request.y);
                if(request.type=="move")
                {
                    var path=new List<Vector3>{actor.transform.position};path.AddRange(plan.path.Select(c=>Environment.CellWorld(c.x,c.y)));yield return Travel(actor,path.ToArray(),factor);
                }
                else if(request.type=="skill")
                {
                    var definition=TacticalContent.GetSkill(request.skillId);
                    bool magic=definition.resource!="ap"||definition.kind=="guard"||definition.kind=="heal";
                    var unit=TacticalRules.FindUnit(before,request.unitId);
                    // Self-targeted wards keep the existing direction. Movement
                    // faces each real path segment in Travel; ending a turn has no target.
                    if(TacticalBattlePresentation.ShouldFaceTarget(unit,request))Face(actor,target);
                    if(!reduced&&definition.resource!="ap")yield return FrameAction(actor.transform.position,target);
                    if(definition.resource=="charge")
                    {
                        actor.Pose("cast",.30f);
                        yield return UltimateCutIn.Play(unit.heroId,reduced);
                    }
                    if(!string.IsNullOrEmpty(plan.objectId))
                    {
                        yield return Pose(actor,"attack",.24f*factor);
                        var item=before.objects.First(o=>o.id==plan.objectId);var original=Environment.ObjectVisual(item.id);if(original)original.gameObject.SetActive(false);
                        var thrown=original?Instantiate(original,effects):null;
                        if(thrown){thrown.gameObject.SetActive(true);var a=actor.transform.position+Vector3.up*1.3f;yield return Tween(.5f*factor,t=>{thrown.position=Arc(a,target+Vector3.up*.3f,t);thrown.rotation=Quaternion.Euler(t*360,t*280,0);});Clear(thrown);}
                    }
                    var affected=plan.affected.Count>0?plan.affected.Select(cell=>Environment.CellWorld(cell.x,cell.y)).ToArray():new[]{target};
                    bool heavy=definition.resource!="ap"||definition.id=="throw";
                    var origin=actor.transform.position;
                    director.Audio.PlayTactical(magic?"cast":"windup",unit.heroId,.75f);
                    yield return SkillVfx.Play(definition.id,unit.heroId,origin,target,affected,reduced,
                        p=>actor.Pose(magic?"cast":"attack",p<SkillVfx.ImpactProgress?Mathf.Lerp(0,.48f,p/Mathf.Max(.01f,SkillVfx.ImpactProgress)):Mathf.Lerp(.55f,1,(p-SkillVfx.ImpactProgress)/Mathf.Max(.01f,1-SkillVfx.ImpactProgress))),
                        ()=>{
                            int contactsBefore=ContactCount;
                            foreach(var previous in before.units)
                                PresentReceipt(before,after,previous.id,origin,unit.heroId,heavy,reduced,receipts,true);
                            var objectTarget=TacticalRules.ObjectAt(before,request.x,request.y);
                            if(ContactCount==contactsBefore&&objectTarget!=null)
                            {
                                var remaining=after.objects.FirstOrDefault(o=>o.id==objectTarget.id);
                                if(remaining==null||remaining.hp<objectTarget.hp)
                                    ContactAt(target+Vector3.up*.45f,target-origin,JinhaiArt.Accent(unit.heroId),heavy?1.3f:.85f,false,reduced,unit.heroId);
                            }
                            if(definition.kind=="heal")director.Audio.PlayTactical("heal",unit.heroId,.7f);
                            else if(definition.kind=="guard")director.Audio.PlayTactical("guard",unit.heroId,.7f);
                        });
                    if(definition.kind=="step")yield return Travel(actor,new[]{actor.transform.position,target},factor*.5f);
                }
            }
            // Animate every displacement and health delta from the committed result, including collision, explosions and pipes.
            foreach(var previous in before.units)
            {
                var next=TacticalRules.FindUnit(after,previous.id);if(next==null||!actors.TryGetValue(previous.id,out var actor)||previous.hp<=0)continue;
                // Victory applies camp recovery and resets the squad's grid positions.
                // Those bookkeeping changes are not hits, heals or battlefield travel.
                if(after.phase!="battle"&&previous.team=="hero")continue;
                EndRecoil(actor);
                var destination=Environment.CellWorld(next.x,next.y);
                if(Vector3.Distance(actor.transform.position,destination)>.05f)
                {var from=actor.transform.position;yield return Tween(.22f*factor,t=>{actor.transform.position=Vector3.Lerp(from,destination,1-Mathf.Pow(1-t,3));actor.Pose("hit",t);});}
                PresentReceipt(before,after,previous.id,destination-Vector3.forward,"",false,reduced,receipts,true);
                if(next.hp<=0)
                {
                    director.Audio.PlayTactical("down",previous.heroId,.62f);
                    var landing=actor.transform.position;
                    yield return Tween(reduced?.04f:.22f,t=>{actor.FadeOut(t);if(!reduced)actor.transform.position=landing+Vector3.down*(t*.10f);});
                    actor.gameObject.SetActive(false);actor.transform.position=landing;
                    if(badges.TryGetValue(previous.id,out var fallen)&&fallen)fallen.gameObject.SetActive(false);
                }
                actor.Idle();
            }
            // Let receipts finish their hold/fade instead of cutting them off during Render.
            while(CombatNumbers.ActiveCount>0)yield return null;
            yield return new WaitForSecondsRealtime(.06f*factor);ClearChildren(effects);
            if(actionFramed)yield return RestoreActionView();
        }
        private IEnumerator FrameAction(Vector3 source,Vector3 target)
        {
            playerFocus=focus;playerViewSize=Camera.orthographicSize;actionFramed=true;
            float spread=Mathf.Abs(Vector3.Dot(target-source,Camera.transform.right))*.5f;
            float size=Mathf.Min(playerViewSize,Mathf.Max(4.5f,DefaultOrthographicSize*.63f,(spread+2.2f)/1.05f));
            Rect safe=BoardSafeRect;Vector2 design=PaperViewport.DesignSize;
            Vector3 centre=(source+target)*.5f+Vector3.up*.8f;
            Vector3 frame=centre-Camera.transform.right*(size*PaperViewport.Aspect*safe.center.x/(design.x*.5f))-Camera.transform.up*(size*safe.center.y/(design.y*.5f));
            yield return Tween(.20f,t=>{float s=t*t*(3-2*t);focus=Vector3.Lerp(playerFocus,frame,s);Camera.orthographicSize=Mathf.Lerp(playerViewSize,size,s);UpdateCamera();});
        }
        private IEnumerator RestoreActionView()
        {
            Vector3 start=focus;float size=Camera.orthographicSize;
            yield return Tween(.20f,t=>{float s=t*t*(3-2*t);focus=Vector3.Lerp(start,playerFocus,s);Camera.orthographicSize=Mathf.Lerp(size,playerViewSize,s);UpdateCamera();});
            ResetActionView();
        }
        private void ResetActionView()
        {if(!actionFramed)return;focus=playerFocus;if(Camera)Camera.orthographicSize=playerViewSize;actionFramed=false;UpdateCamera();}
        public void CancelEffects()
        {
            ExitGroundView();
            StopAllCoroutines();if(SkillVfx)SkillVfx.Cancel();if(UltimateCutIn)UltimateCutIn.Cancel();if(CombatNumbers)CombatNumbers.Cancel();ClearChildren(effects);
            foreach(var actor in recoilOrigins.Keys.ToArray())EndRecoil(actor);
            impulseDuration=impulseStrength=CameraImpulsePixels=0;
            if(director&&director.Audio)director.Audio.CancelTactical();
            RangeSnapshot=null;if(RangeRenderer)RangeRenderer.Clear();
            ResetActionView();
            UpdateCamera();
            foreach(var actor in actors.Values)if(actor){actor.ResetMotion();actor.Idle();}
            foreach(var badge in badges.Values)if(badge)
            {
                badge.gameObject.SetActive(true);var name=badge.Find("Name");if(name)name.gameObject.SetActive(true);
                var trail=badge.Find("Committed health trail");if(trail)Clear(trail);
            }
        }
        private void Face(TacticalActor actor,Vector3 point){var v=point-actor.transform.position;v.y=0;if(v.sqrMagnitude>.01f)actor.transform.rotation=Quaternion.LookRotation(v);}
        private IEnumerator Pose(TacticalActor actor,string pose,float duration){yield return Tween(duration,t=>actor.Pose(pose,t));}
        private IEnumerator Travel(TacticalActor actor,Vector3[] path,float factor)
        {
            // Walk frames advance continuously across the path. Resetting four frames
            // every short tile tween produced a 16 fps shuffle and abrupt foot changes.
            float elapsed=0;int lastContact=-1;
            for(int i=1;i<path.Length;i++)
            {
                var a=actor.transform.position;var b=path[i];float distance=Vector3.Distance(a,b);if(distance<.03f)continue;Face(actor,b);
                float duration=Mathf.Clamp(distance/TacticalEnvironment.TileSize*actor.SecondsPerTile,.18f,.9f)*factor;
                float started=elapsed;
                yield return Tween(duration,t=>
                {
                    elapsed=started+duration*t;actor.transform.position=Vector3.Lerp(a,b,t);
                    // Six drawings per second gives alternating deliberate steps; the
                    // saved world path owns translation, without bobbing the foot anchor.
                    actor.Pose("run",director.ReducedMotion?0:Mathf.Repeat(elapsed*1.5f,1));
                    int contact=Mathf.FloorToInt(elapsed*3);if(contact!=lastContact){lastContact=contact;director.Audio.PlayTactical("step","",.58f);}
                });
            }
            actor.Idle();
        }
        private void PresentReceipt(TacticalState before,TacticalState after,string id,Vector3 source,string hero,bool heavy,bool reduced,HashSet<string> presented,bool contact)
        {
            if(presented.Contains(id)||!actors.TryGetValue(id,out var actor))return;
            var previous=TacticalRules.FindUnit(before,id);var next=TacticalRules.FindUnit(after,id);
            if(previous==null||next==null||previous.hp<=0)return;
            if(after.phase!="battle"&&previous.team=="hero")return;
            int delta=next.hp-previous.hp,shield=next.block-previous.block;
            if(delta==0&&shield==0)return;
            presented.Add(id);
            StartCoroutine(HealthReceipt(id,previous,next,reduced));
            Vector3 point=actor.VisualPoint(TacticalActor.VisibleHeight+.10f);
            bool damaging=delta<0||shield<0;
            bool weight=heavy||-delta>=10;
            if(damaging&&contact)Contact(actor,source,string.IsNullOrEmpty(hero)?Red:JinhaiArt.Accent(hero),weight?1.3f:.85f,shield<0,reduced,hero);
            if(delta<0)CombatNumbers.Show(id,point,-delta,"damage",weight,reduced);
            else if(delta>0)CombatNumbers.Show(id,point,delta,"heal",false,reduced);
            if(shield>0)CombatNumbers.Show(id,point,shield,"shield",false,reduced);
            else if(shield<0)
            {
                CombatNumbers.Show(id,point,-shield,"block",false,reduced);
                director.Audio.PlayTactical(next.block==0?"break":"guard",hero,.48f);
            }
        }
        private IEnumerator HealthReceipt(string id,TacticalUnit previous,TacticalUnit next,bool reduced)
        {
            if(!badges.TryGetValue(id,out var badge)||!badge)yield break;
            var fill=badge.Find("Health") as RectTransform;var glint=badge.Find("Health upper glint") as RectTransform;
            if(!fill||!glint)yield break;
            float capacity=Mathf.Max(1,next.maxHp,next.hp+next.block),from=Mathf.Clamp01(previous.hp/capacity),to=next.hp/capacity,shield=next.block/capacity;
            var shieldFill=badge.Find("Shield") as RectTransform;
            if(shieldFill){shieldFill.sizeDelta=new Vector2(124*shield,8);shieldFill.anchoredPosition=new Vector2(-62+124*to+62*shield,-11);}
            var boundary=badge.Find("Health shield boundary") as RectTransform;
            var boundaryGlint=badge.Find("Health shield boundary glint") as RectTransform;
            foreach(var divider in new[]{boundary,boundaryGlint})if(divider){divider.anchoredPosition=new Vector2(-62+124*to,-11);divider.gameObject.SetActive(next.hp>0&&next.block>0);}
            var shieldText=badge.Find("Shield amount")?.GetComponent<Text>();if(shieldText)shieldText.text=next.block>0?"盾 "+next.block:"";
            // The actual result arrives at contact. A restrained ivory trail then
            // drains the lost segment without hiding the remaining health.
            fill.sizeDelta=new Vector2(124*to,8);fill.anchoredPosition=new Vector2(-62+62*to,-11);
            glint.sizeDelta=new Vector2(124*to,1);glint.anchoredPosition=new Vector2(-62+62*to,-8);
            if(reduced||to>=from)yield break;
            var trail=Ui.Panel("Committed health trail",badge,Vector2.one*.5f,new Vector2(-62+124*to+62*(from-to),-11),new Vector2(124*(from-to),8),new Color(1,.88f,.68f,.9f));
            if(boundary)boundary.SetAsLastSibling();if(boundaryGlint)boundaryGlint.SetAsLastSibling();
            yield return Tween(.42f,t=>{
                if(!trail)return;float length=(from-to)*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.24f)/.76f)));
                trail.rectTransform.sizeDelta=new Vector2(124*length,8);trail.rectTransform.anchoredPosition=new Vector2(-62+124*to+62*length,-11);
                var color=trail.color;color.a=.9f*(1-t*.4f);trail.color=color;
            });
            if(trail)Clear(trail.transform);
        }
        private void Contact(TacticalActor actor,Vector3 source,Color tint,float strength,bool blocked,bool reduced,string hero="")
        {
            actor.HitFeedback(strength,blocked?new Color(.57f,.79f,1):tint,reduced);
            Vector3 position=actor.VisualPoint(.88f),direction=actor.transform.position-source;direction.y=0;
            if(direction.sqrMagnitude<.01f)direction=Camera.transform.right;direction.Normalize();
            StartCoroutine(Recoil(actor,direction,strength,reduced));
            ContactAt(position,direction,tint,strength,blocked,reduced,hero);
        }
        private void ContactAt(Vector3 position,Vector3 direction,Color tint,float strength,bool blocked,bool reduced,string hero)
        {
            ContactCount++;direction.y=0;if(direction.sqrMagnitude<.01f)direction=Camera.transform.right;direction.Normalize();
            director.Audio.PlayTactical(blocked?"guard":strength>1?"heavy":"hit",hero,Mathf.Clamp(strength,.6f,1.25f));
            StartCoroutine(Impact(position,direction,blocked?new Color(.63f,.87f,1):tint,strength,reduced));
            if(!reduced)
            {
                Vector2 projected=new Vector2(Vector3.Dot(direction,Camera.transform.right),Vector3.Dot(direction,Camera.transform.up));
                impulseDirection=projected.sqrMagnitude>.01f?projected.normalized:Vector2.right;
                impulseStarted=Time.unscaledTime;impulseDuration=strength>1?.18f:.13f;
                impulseStrength=Mathf.Max(impulseStrength*.35f,blocked?1.3f:strength>1?3.6f:2.1f);
                CameraImpulseCount++;UpdateCamera();
            }
        }
        private IEnumerator Recoil(TacticalActor actor,Vector3 direction,float strength,bool reduced)
        {
            EndRecoil(actor);
            Vector3 original=actor.transform.position;
            int ticket=++recoilGeneration;recoilOrigins[actor]=original;recoilTickets[actor]=ticket;
            float duration=reduced?.10f:.20f;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                if(!actor||!recoilTickets.TryGetValue(actor,out var current)||current!=ticket)yield break;
                float t=Mathf.Clamp01(elapsed/duration);actor.Pose("hit",t);
                if(!reduced){float kick=t<.22f?1-Mathf.Pow(1-t/.22f,3):Mathf.Pow(1-(t-.22f)/.78f,3);actor.transform.position=original+direction*(kick*.065f*strength);}
                yield return null;
            }
            if(recoilTickets.TryGetValue(actor,out var active)&&active==ticket)EndRecoil(actor);
        }
        private void EndRecoil(TacticalActor actor)
        {
            if(!recoilOrigins.TryGetValue(actor,out var original))return;
            if(actor){actor.transform.position=original;actor.Idle();}
            recoilOrigins.Remove(actor);recoilTickets.Remove(actor);
        }
        private IEnumerator Impact(Vector3 position,Vector3 direction,Color tint,float strength,bool reduced)
        {
            if(!contactMaterial)
            {
                var shader=Resources.Load<Shader>("Tactics/Vfx/ContactSpark");
                if(!shader)throw new InvalidOperationException("Missing contact spark shader.");
                contactMaterial=new Material(shader);owned.Add(contactMaterial);
            }
            var root=Root("Directional contact sparks",effects);
            Vector3 right=Camera.transform.right,up=Camera.transform.up;
            float angle=Mathf.Atan2(Vector3.Dot(direction,up),Vector3.Dot(direction,right));
            int count=reduced?3:7;var rays=new LineRenderer[count];var vectors=new Vector3[count];
            Color hot=Color.Lerp(tint,new Color(1,.95f,.80f),.72f);
            for(int i=0;i<count;i++)
            {
                float a=angle+(i%2==0?0:Mathf.PI)+(i*.67f-.8f);
                vectors[i]=(right*Mathf.Cos(a)+up*Mathf.Sin(a))*(.65f+(i%3)*.16f);
                rays[i]=Line("Tapered contact "+i,new[]{position,position},Color.white,.035f,root);
                rays[i].sharedMaterial=contactMaterial;
                rays[i].alignment=LineAlignment.View;rays[i].numCapVertices=1;rays[i].endWidth=0;
            }
            yield return Tween(reduced?.085f:.19f,t=>{
                float distance=reduced?.08f:Mathf.Lerp(.04f,.55f,1-Mathf.Pow(1-t,3))*strength;
                for(int i=0;i<count;i++)if(rays[i])
                {
                    rays[i].SetPosition(0,position+vectors[i]*distance);
                    rays[i].SetPosition(1,position+vectors[i]*(distance+(.16f*(1-t)+.025f)*strength));
                    rays[i].startWidth=(i<2?.055f:.026f)*Mathf.Pow(1-t,2);rays[i].endWidth=0;
                    Color color=i<2?new Color(1,.98f,.90f):hot;color.a=Mathf.Pow(1-t,1.5f);rays[i].startColor=rays[i].endColor=color;
                }
            });Clear(root);
        }
        private IEnumerator Tween(float seconds,Action<float> action)
        {for(float elapsed=0;elapsed<seconds;elapsed+=Time.unscaledDeltaTime){action(Mathf.Clamp01(elapsed/seconds));yield return null;}action(1);}
        private void Clear(Transform root){if(root){root.gameObject.SetActive(false);Destroy(root.gameObject);}}
        private void ClearChildren(Transform root){if(!root)return;foreach(Transform item in root)Clear(item);}
        private void OnDisable(){CancelEffects();}
        private void OnApplicationFocus(bool focused){if(!focused)ExitGroundView();}
        private void OnApplicationPause(bool paused){if(paused)ExitGroundView();}
        private void OnDestroy(){CancelEffects();foreach(var item in owned)if(item)Destroy(item);}
    }
    public sealed class TacticalActorHit : MonoBehaviour { public string UnitId; }
}
