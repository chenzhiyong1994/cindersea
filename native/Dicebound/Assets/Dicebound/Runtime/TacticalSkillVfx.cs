using System;
using System.Collections;
using System.Collections.Generic;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dicebound.Presentation
{
    /// <summary>Presentation of an already saved action. Shapes and timing never determine hits.</summary>
    public sealed class TacticalSkillVfx : MonoBehaviour
    {
        public bool Active => activeRoot;
        public string CurrentSkill { get; private set; }
        public string CurrentHero { get; private set; }
        /// <summary>Effect archetype actually rendered after the ultimate remap (ultimates render as oath).</summary>
        public string RenderedSkill { get; private set; }
        public float Progress { get; private set; }
        public int PresentedCount { get; private set; }
        public int ActiveRendererCount {get {int count=0;for(int i=0;i<pieces.Count;i++)if(pieces[i].renderer&&pieces[i].renderer.enabled&&pieces[i].renderer.gameObject.activeInHierarchy)count++;return count;}}
        /// <summary>Verification diagnostics: schedule summary of the active cast's pieces.</summary>
        public string PieceSummary()
        {
            var parts=new System.Collections.Generic.List<string>();
            for(int i=0;i<pieces.Count&&i<6;i++){var p=pieces[i];parts.Add((p.renderer==null?"null":p.renderer.enabled?"on":"off")+":"+p.born.ToString("F2")+"+"+p.life.ToString("F2"));}
            return "skill="+CurrentSkill+" hero="+CurrentHero+" pieces="+pieces.Count+" root="+(activeRoot==null?"none":activeRoot.gameObject.activeInHierarchy?"on":"off")+(parts.Count>0?" ["+string.Join(",",parts)+"]":"");
        }
        public int CleaveCount { get; private set; }
        public int BalanceCount { get; private set; }
        public int TideCount { get; private set; }
        public int UltimateCount { get; private set; }
        public bool PreviewHeld { get; private set; }
        public float ImpactProgress { get; private set; }
        public float LastImpactRealtime { get; private set; }
        public int ImpactCount { get; private set; }
        public const float SkillDuration = .62f;
        public const float UltimateDuration = .78f;
        private sealed class Piece
        {
            public Transform transform;
            public Renderer renderer;
            public MaterialPropertyBlock properties = new MaterialPropertyBlock();
            public Color color;
            public Vector3 start, velocity;
            public float born, life, scaleFrom=1, scaleTo=1, reveal;
            public float fadeIn=.045f, fadeOut=.45f, moveFinish=.72f, gravity, spinFrom, spinTo, erase, sweepAxis;
            public bool projectile;
            public bool painted;
            public Vector4 region;
            public Vector2 flow;
            public Vector2 groundFade;
            public float emission,facet;
        }
        private readonly List<Piece> pieces=new List<Piece>(64);
        private readonly List<Mesh> meshes=new List<Mesh>(32);
        private readonly List<Material> materials=new List<Material>(9);
        private Material ribbon,particle,wash;
        private Material slashPaint,crystalPaint,waterPaint,moonPaint,anchorPaint,threadPaint;
        private sealed class LightCue { public Light light; public float intensity, born, life; }
        private readonly List<LightCue> localLights=new List<LightCue>();
        private Transform activeRoot,world;
        private Camera viewCamera;
        private int generation;
        private static readonly int Tint=Shader.PropertyToID("_Tint"),Reveal=Shader.PropertyToID("_Reveal"),Dissolve=Shader.PropertyToID("_Dissolve");
        private static readonly int AtlasRect=Shader.PropertyToID("_AtlasRect"),Emission=Shader.PropertyToID("_Emission"),Phase=Shader.PropertyToID("_Phase"),Flow=Shader.PropertyToID("_Flow"),Facet=Shader.PropertyToID("_Facet");
        private static readonly int GroundFade=Shader.PropertyToID("_GroundFade");
        private static readonly int Tail=Shader.PropertyToID("_Tail"),SweepAxis=Shader.PropertyToID("_SweepAxis");
        // Actual alpha bounds from the original 1254px RGBA atlases, including a transparent border.
        private static readonly Vector4[] SlashRegions={UV(49,698,550,422),UV(670,753,540,283),UV(65,81,536,451),UV(732,94,444,433)};
        private static readonly Vector4[] CrystalRegions={UV(173,696,294,485),UV(650,796,560,212),UV(69,74,489,492),UV(836,47,229,547)};
        private static readonly Vector4[] WaterRegions={UV(47,710,535,405),UV(695,689,511,441),UV(57,93,531,400),UV(716,90,477,452)};
        private static readonly Vector4[] MoonRegions={UV(84,787,479,251),UV(759,718,400,405),UV(82,97,465,434),UV(760,115,412,401)};
        private static readonly Vector4[] AnchorRegions={UV(101,705,443,423),UV(794,653,313,530),UV(95,117,442,444),UV(706,116,466,418)};
        private static readonly Vector4[] ThreadRegions={UV(102,782,470,265),UV(690,726,468,411),UV(100,66,450,473),UV(729,105,411,383)};
        private static Vector4 UV(float x,float y,float width,float height)=>new Vector4(x/1254f,y/1254f,width/1254f,height/1254f);
        private static readonly Color Gold=new Color(1,.69f,.20f), Silver=new Color(.62f,.84f,1), Water=new Color(.15f,.73f,.88f), Foam=new Color(.80f,.98f,1);

        /// <summary>Read-only production atlas artwork for the six signature skill icons.</summary>
        public static bool TryGetSkillIcon(string id,out Texture2D texture,out Rect pixelRect)
        {
            texture=null;pixelRect=default;Vector4 region;
            switch(id){
                case "cleave":region=SlashRegions[0];break;
                case "balance":region=CrystalRegions[0];break;
                case "tide":region=WaterRegions[0];break;
                case "moon":region=MoonRegions[1];break;
                case "anchor":region=AnchorRegions[0];break;
                case "stitch":region=ThreadRegions[1];break;
                default:return false;
            }
            texture=Resources.Load<Texture2D>("Tactics/Vfx/"+id+"-painted-v1");if(!texture)return false;
            pixelRect=new Rect(Mathf.RoundToInt(region.x*texture.width),Mathf.RoundToInt(region.y*texture.height),Mathf.RoundToInt(region.z*texture.width),Mathf.RoundToInt(region.w*texture.height));return true;
        }

        public void Initialize(Camera view,Transform parent)
        {
            viewCamera=view;world=parent;
            var shader=Resources.Load<Shader>("Effects/PaperStroke");
            var noise=Resources.Load<Texture2D>("Effects/Pierre/FractalNoise_1");
            var mask=Resources.Load<Texture2D>("Effects/Pierre/Particle_Soft");
            if(!shader||!noise||!mask)throw new InvalidOperationException("战棋技能演出缺少 PaperStroke Shader 或已登记的 Pierre CC0 贴图。");
            ribbon=CreateMaterial(shader,noise,mask,0);particle=CreateMaterial(shader,noise,mask,2);wash=CreateMaterial(shader,noise,mask,1);
            var painted=Resources.Load<Shader>("Tactics/Vfx/PaintedSkill");
            if(!painted)throw new InvalidOperationException("战棋技能演出缺少 PaintedSkill Shader。");
            slashPaint=CreatePainted(painted,noise,"cleave");crystalPaint=CreatePainted(painted,noise,"balance");waterPaint=CreatePainted(painted,noise,"tide");
            moonPaint=CreatePainted(painted,noise,"moon");anchorPaint=CreatePainted(painted,noise,"anchor");threadPaint=CreatePainted(painted,noise,"stitch");
        }
        private Material CreatePainted(Shader shader,Texture noise,string id)
        {
            var texture=Resources.Load<Texture2D>("Tactics/Vfx/"+id+"-painted-v1");
            if(!texture)throw new InvalidOperationException("缺少正式绘制技能图集："+id);
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Trilinear;
            var material=new Material(shader){name="Original painted skill · "+id,renderQueue=3060};
            material.SetTexture("_MainTex",texture);material.SetTexture("_NoiseTex",noise);material.SetFloat("_Reveal",1);materials.Add(material);return material;
        }
        private Material CreateMaterial(Shader shader,Texture noise,Texture mask,int mode)
        {
            var material=new Material(shader){name="Tactical skill pigment "+mode,renderQueue=3060};
            material.SetTexture("_NoiseTex",noise);material.SetTexture("_MainTex",mask);material.SetFloat("_Mode",mode);material.SetFloat("_Reveal",1);materials.Add(material);return material;
        }
        public IEnumerator Play(string skillId,string heroId,Vector3 source,Vector3 target,IReadOnlyList<Vector3> affected,bool reduced,Action<float> pose=null,Action onImpact=null)
        {
            if(!ribbon)throw new InvalidOperationException("TacticalSkillVfx.Initialize 必须先于 Play。");
            Cancel();int ticket=generation;CurrentSkill=skillId;CurrentHero=heroId;PresentedCount++;
            var definition=TacticalContent.GetSkill(skillId);
            if(definition!=null&&definition.resource=="charge")skillId="oath";
            else if(definition!=null&&!string.IsNullOrEmpty(definition.heroId)&&skillId!="balance"&&skillId!="moon"&&skillId!="step"&&skillId!="cleave"&&skillId!="tide"&&skillId!="anchor"&&skillId!="stitch")
            {
                heroId=definition.heroId;
                skillId=definition.kind=="heal"?"mend":definition.kind=="guard"||definition.kind=="shield"?"ward":definition.kind=="haste"?"haste":heroId=="sixuan"?"balance":heroId=="yanzhuying"?"moon":heroId=="lingfeng"?"cleave":heroId=="cangling"?"tide":"anchor";
            }
            ImpactProgress=skillId=="tide"||((skillId=="strike"||skillId=="line"||skillId=="oath")&&heroId=="cangling")?.43f:
                skillId=="anchor"||skillId=="brace"||skillId=="ward"||skillId=="mend"||skillId=="stitch"||skillId=="haste"||skillId=="step"?.30f:.34f;
            RenderedSkill=skillId;
            if(skillId=="cleave")CleaveCount++;if(skillId=="balance")BalanceCount++;if(skillId=="tide")TideCount++;if(skillId=="oath")UltimateCount++;
            activeRoot=new GameObject("Skill VFX · "+skillId+" · "+heroId).transform;activeRoot.SetParent(world,false);
            Vector3 direction=target-source;direction.y=0;direction=direction.sqrMagnitude<.001f?Vector3.forward:direction.normalized;
            float duration=(skillId=="oath"?UltimateDuration:SkillDuration)*(reduced?.22f:1);
            bool impacted=false;
            float impactHold=0;
            try
            {
                Build(skillId,heroId,source,target,direction,affected);
                for(float elapsed=0;ticket==generation&&(elapsed<duration||!impacted);)
                {
                    if(!PreviewHeld)
                    {
                        if(impactHold>0)impactHold=Mathf.Max(0,impactHold-Time.unscaledDeltaTime);
                        else
                        {
                            float next=Mathf.Clamp01(elapsed/duration);
                            bool impactFrame=!impacted&&next>=ImpactProgress;
                            // A slow frame still visits the authored contact frame. Only this
                            // presentation clock pauses; saves, UI and Time.timeScale are untouched.
                            Progress=impactFrame?ImpactProgress:next;Render(Progress,reduced);pose?.Invoke(Progress);
                            if(impactFrame)
                            {
                                impacted=true;ImpactCount++;LastImpactRealtime=Time.realtimeSinceStartup;onImpact?.Invoke();
                                elapsed=ImpactProgress*duration;
                                bool firmStrike=skillId=="cleave"||skillId=="moon"||skillId=="shove"||((skillId=="strike"||skillId=="line"||skillId=="oath")&&heroId!="cangling");
                                if(!reduced&&firmStrike)impactHold=.036f;
                            }
                            else elapsed+=Time.unscaledDeltaTime;
                        }
                    }
                    yield return null;
                }
                if(ticket==generation&&!PreviewHeld)
                {
                    if(!impacted){Progress=ImpactProgress;Render(Progress,reduced);pose?.Invoke(Progress);impacted=true;ImpactCount++;LastImpactRealtime=Time.realtimeSinceStartup;onImpact?.Invoke();}
                    Progress=1;Render(1,reduced);pose?.Invoke(1);
                }
            }
            finally {if(ticket==generation)Cancel();}
        }
        /// <summary>Freeze a real action's presentation at a normalized time for a screenshot; no rule fields change.</summary>
        public void SetPreviewProgress(float progress)
        {if(!Active)throw new InvalidOperationException("只能暂停正在播放的技能演出。");PreviewHeld=true;Progress=Mathf.Clamp01(progress);Render(Progress,false);}
        public void ReleasePreview(){PreviewHeld=false;}
        private void Build(string id,string hero,Vector3 source,Vector3 target,Vector3 direction,IReadOnlyList<Vector3> affected)
        {
            switch(id)
            {
                case "cleave": Slash(source,target,direction,1.5f);break;
                case "balance": Boundary(source,target,1,Silver);break;
                case "tide": Tide(source,target,direction,1);break;
                case "moon": Needle(source,target,1,true);break;
                case "stitch": Threads(source,target,true);break;
                case "anchor": Shields(target,affected,"shangshuo");break;
                case "step": ShadowFoot(source);ShadowFoot(target);break;
                case "hook": Threads(source,target,false);break;
                case "line": HeroStrike(hero,source,target,direction,1.05f);break;
                case "burst": Dust(target,affected);break;
                case "brace": case "ward": Shields(target,affected,hero);break;
                case "mend": Threads(source,target,true,.9f);break;
                case "haste": Ticks(target);break;
                case "shove": Push(source,target,direction);break;
                case "throw": Dust(target,affected);break;
                case "oath":
                    if(hero=="lingfeng")Slash(source,target,direction,2.1f);
                    else if(hero=="sixuan")Boundary(source,target,1.35f,Silver);
                    else if(hero=="cangling")Tide(source,target,direction,1.4f);
                    else if(hero=="yanzhuying")Needle(source,target,1.4f,true);
                    else if(hero=="ruanzhuo")Threads(source,target,false,1.35f);
                    else AnchorThrust(source,target,1.35f);
                    // Oath accents are detached motes, preserving Ruanzhuo's exactly two physical threads.
                    var accent=Accent(hero);
                    if(affected!=null)for(int i=0;i<affected.Count;i++)BurstSparks(affected[i]+Vector3.up*.65f,direction,accent,7,ImpactProgress);
                    break;
                default: HeroStrike(hero,source,target,direction,.75f);break;
            }
        }
        public static Color Accent(string hero)
        {switch(hero){case "lingfeng":return Gold;case "sixuan":return Silver;case "cangling":return Water;case "yanzhuying":return new Color(.76f,.29f,.44f);case "ruanzhuo":return new Color(.97f,.58f,.28f);default:return new Color(.71f,.60f,.35f);}}
        private static Color Glow(Color color,float intensity)=>new Color(color.r*intensity,color.g*intensity,color.b*intensity,color.a);
        private void HeroStrike(string hero,Vector3 source,Vector3 target,Vector3 direction,float size)
        {
            switch(hero){
                case "lingfeng":Slash(source,target,direction,1.5f*size);break;
                case "sixuan":CrystalThrust(source,target,size);break;
                case "cangling":WaterBolt(source,target,direction,size);break;
                case "yanzhuying":Needle(source,target,size,false);break;
                case "ruanzhuo":Threads(source,target,false,size);break;
                default:AnchorThrust(source,target,size);break;
            }
        }
        private void Slash(Vector3 source,Vector3 target,Vector3 forward,float radius)
        {
            float size=radius/1.5f;
            Vector3 right=Vector3.ProjectOnPlane(forward,viewCamera.transform.forward).normalized;
            if(right.sqrMagnitude<.01f)right=DirectedRight(forward);
            Vector3 up=Vector3.Cross(viewCamera.transform.forward,right).normalized;
            var center=Vector3.Lerp(source,target,.50f)+Vector3.up*.54f;
            Curve("Brief blade anticipation",source+Vector3.up*.63f,center,.028f,new Color(.89f,.54f,.22f,.46f),.04f,.27f,.07f);
            var wake=PaintedPanel("Painted black-crimson blade wake",center-forward*.13f+viewCamera.transform.forward*.025f,right,up,2.70f*size,1.34f*size,slashPaint,SlashRegions[1],new Color(1,1,1,.50f),.25f,.42f,.08f);
            wake.velocity=forward*.16f;wake.spinFrom=-9;wake.spinTo=8;wake.erase=.48f;
            wake.groundFade=new Vector2(source.y+.025f,.20f);
            var blade=PaintedPanel("Fast vermilion cutting edge",center,right,up,2.56f*size,1.46f*size,slashPaint,SlashRegions[0],new Color(1,1,1,.92f),.21f,.31f,.55f);
            blade.reveal=.40f;blade.erase=.52f;blade.spinFrom=-16;blade.spinTo=6;blade.scaleFrom=.96f;
            blade.groundFade=wake.groundFade;
            var contact=PaintedPanel("Concentrated blade contact",target+Vector3.up*.61f-viewCamera.transform.forward*.035f,right,up,.92f*size,.62f*size,slashPaint,SlashRegions[2],new Color(1,1,1,.93f),ImpactProgress,.18f,.75f);
            contact.fadeIn=0;contact.fadeOut=.72f;contact.scaleFrom=.92f;contact.scaleTo=1.06f;
            var embers=PaintedPanel("Separated gold ember aftermath",target+Vector3.up*.76f,right,up,.72f*size,.68f*size,slashPaint,SlashRegions[3],new Color(1,1,1,.56f),ImpactProgress+.035f,.42f,.45f);
            embers.velocity=forward*.38f+Vector3.up*.14f;embers.gravity=.17f;embers.scaleTo=.84f;
            BurstSparks(target+Vector3.up*.68f,forward,Gold,10,ImpactProgress);
            LocalLight(target+Vector3.up*.65f,new Color(1,.40f,.14f),.85f,2.1f,ImpactProgress,.22f);
        }
        private void Boundary(Vector3 source,Vector3 target,float size,Color color)
        {
            Curve("Fine balance casting link",source+Vector3.up*.8f,target+Vector3.up*.8f,.026f,new Color(color.r,color.g,color.b,.64f),.035f,.31f);
            float r=.84f*size,height=1.20f*size;var corners=new[]{new Vector3(-r,0,-r),new Vector3(-r,0,r),new Vector3(r,0,r),new Vector3(r,0,-r)};
            var seal=PaintedPanel("Painted square rune foundation",target+Vector3.up*.036f,Vector3.right,Vector3.forward,2.05f*size,2.05f*size,crystalPaint,CrystalRegions[2],new Color(1,1,1,.48f),.15f,.72f,.26f);
            seal.scaleFrom=.98f;seal.fadeIn=.15f;seal.fadeOut=.36f;
            for(int i=0;i<4;i++)
            {
                Vector3 bottom=target+corners[i]+Vector3.up*.08f;
                Vector3 next=target+corners[(i+1)%4]+Vector3.up*.08f,edge=(next-bottom).normalized;
                var post=PaintedPanel("Rising crystalline boundary",bottom+Vector3.up*height*.5f,viewCamera.transform.right,Vector3.up,.23f*size,height,crystalPaint,CrystalRegions[3],new Color(1,1,1,.70f),.22f+i*.013f,.49f,.35f);
                post.reveal=.18f;post.sweepAxis=1;post.erase=.78f;
                for(int layer=0;layer<2;layer++)
                    PaintedPanel("Fine rune boundary edge",(bottom+next)*.5f+Vector3.up*(layer==0?.01f:height),edge,Vector3.up,r*2,.15f*size,crystalPaint,CrystalRegions[1],new Color(1,1,1,layer==0?.55f:.32f),.26f+layer*.05f,.52f,.28f);
                FacetedCrystal("Dimensional blue boundary crystal",bottom+Vector3.up*(height+.15f*size),.29f*size,.49f*size,ImpactProgress,.48f);
            }
            BurstSparks(target+Vector3.up*.18f,Vector3.up,color,6,ImpactProgress);
            LocalLight(target+Vector3.up*.60f,new Color(.22f,.59f,1),.82f,2.4f,ImpactProgress,.27f);
        }
        private void Tide(Vector3 source,Vector3 target,Vector3 direction,float size)
        {
            Vector3 side=Vector3.Cross(Vector3.up,direction);
            // The continuous wave follows source/target height without determining any hit.
            Vector3 distance=target-source;
            var body=WaveSurface("Advancing painted water body",source+Vector3.up*.075f,direction,side,2.52f*size,1.90f*size,waterPaint,WaterRegions[1],new Color(1,1,1,.74f),.045f,.76f,.12f);
            SetTravel(body,distance);body.scaleFrom=.93f;body.scaleTo=1.03f;body.flow=new Vector2(.014f,.009f);body.fadeOut=.47f;
            var foam=WaveSurface("Painted white foam wake",source-direction*.08f+Vector3.up*.11f,direction,side,2.35f*size,1.83f*size,waterPaint,WaterRegions[2],new Color(1,1,1,.54f),.09f,.74f,.15f);
            SetTravel(foam,distance);foam.scaleFrom=.94f;foam.flow=new Vector2(.009f,.012f);foam.fadeOut=.49f;
            Vector3 crestRight=Vector3.Dot(side,viewCamera.transform.right)<0?-side:side;
            crestRight=Vector3.Slerp(crestRight,viewCamera.transform.right,.42f).normalized;
            var crest=PaintedPanel("Moving translucent wave crest",source+Vector3.up*.35f,crestRight,Vector3.up,2.30f*size,.94f*size,waterPaint,WaterRegions[0],new Color(1,1,1,.76f),.085f,.64f,.15f);
            SetTravel(crest,distance);crest.scaleFrom=.94f;crest.flow=new Vector2(.008f,.010f);crest.fadeOut=.56f;
            var splash=PaintedPanel("Short foamy contact splash",target+Vector3.up*.49f,DirectedRight(direction),viewCamera.transform.up,1.25f*size,1.02f*size,waterPaint,WaterRegions[3],new Color(1,1,1,.80f),ImpactProgress,.34f,.24f);
            splash.fadeIn=0;splash.scaleFrom=.92f;splash.scaleTo=1.09f;splash.velocity=direction*.15f;
            for(int i=0;i<8;i++)
            {
                Vector3 start=target+side*(i%5-2)*.16f*size+Vector3.up*.30f;
                Dot("Ballistic water droplets",start,direction*.30f+side*Mathf.Sin(i*2.4f)*.24f+Vector3.up*(.26f+i%3*.06f),(.035f+i%3*.012f)*size,Foam,ImpactProgress+i%3*.017f,.40f);
            }
            LocalLight(target+Vector3.up*.36f,new Color(.26f,.71f,.83f),.36f,2.2f,ImpactProgress,.23f);
        }
        private Vector3 DirectedRight(Vector3 direction)=>viewCamera.transform.right*(Vector3.Dot(direction,viewCamera.transform.right)<0?-1:1);
        private Piece DirectedPanel(string name,Vector3 from,Vector3 to,float width,Material material,Vector4 region,Color color,float born,float life,float emission,bool verticalTexture=false)
        {
            Vector3 delta=to-from;float length=Mathf.Max(.45f,delta.magnitude);Vector3 axis=delta.sqrMagnitude>.001f?delta.normalized:viewCamera.transform.right;
            Vector3 across=Vector3.Cross(viewCamera.transform.forward,axis).normalized;if(across.sqrMagnitude<.01f)across=viewCamera.transform.up;
            // Texture +X (or +Y for a pillar) follows the actual source-to-target axis.
            var piece=PaintedPanel(name,(from+to)*.5f,verticalTexture?across:axis,verticalTexture?axis:across,verticalTexture?width:length,verticalTexture?length:width,material,region,color,born,life,emission);
            piece.reveal=.44f;piece.sweepAxis=verticalTexture?1:0;piece.erase=.53f;return piece;
        }
        private void Needle(Vector3 source,Vector3 target,float size,bool crescent)
        {
            Vector3 delta=target-source;
            DirectedPanel("Quick ruby-silver needle thrust",source+Vector3.up*.85f,target+Vector3.up*.78f,.39f*size,moonPaint,MoonRegions[0],new Color(1,1,1,.92f),.20f,.30f,.36f);
            if(crescent){var arc=PaintedPanel("Broken ruby crescent echo",target+Vector3.up*.66f+viewCamera.transform.forward*.02f,DirectedRight(delta),viewCamera.transform.up,1.37f*size,1.20f*size,moonPaint,MoonRegions[1],new Color(1,1,1,.50f),.28f,.42f,.20f);arc.spinFrom=-15;arc.spinTo=10;arc.erase=.57f;}
            var contact=PaintedPanel("Ruby-silver contact shards",target+Vector3.up*.75f,DirectedRight(delta),viewCamera.transform.up,.78f*size,.71f*size,moonPaint,MoonRegions[2],new Color(1,1,1,.86f),ImpactProgress,.21f,.48f);contact.fadeIn=0;
            var motes=PaintedPanel("Reflective ruby detached motes",target+Vector3.up*.84f,viewCamera.transform.right,viewCamera.transform.up,.72f*size,.64f*size,moonPaint,MoonRegions[3],new Color(1,1,1,.53f),ImpactProgress+.04f,.42f,.26f);motes.velocity=delta.normalized*.31f+Vector3.up*.16f;motes.gravity=.13f;
            LocalLight(target+Vector3.up*.60f,new Color(.82f,.12f,.25f),.44f,1.8f,ImpactProgress,.18f);
        }
        private void CrystalThrust(Vector3 source,Vector3 target,float size)
        {
            DirectedPanel("Blue crystalline sword impulse",source+Vector3.up*.82f,target+Vector3.up*.76f,.26f*size,crystalPaint,CrystalRegions[3],new Color(1,1,1,.85f),.20f,.30f,.34f,true);
            FacetedCrystal("Blue crystal thrust contact",target+Vector3.up*.78f,.23f*size,.46f*size,ImpactProgress,.30f);
            var seal=PaintedPanel("Refined blue contact seal",target+Vector3.up*.60f,viewCamera.transform.right,viewCamera.transform.up,.69f*size,.69f*size,crystalPaint,CrystalRegions[2],new Color(1,1,1,.44f),ImpactProgress,.23f,.30f);seal.fadeIn=0;
            BurstSparks(target+Vector3.up*.74f,(target-source).normalized,Silver,5,ImpactProgress);
        }
        private void WaterBolt(Vector3 source,Vector3 target,Vector3 direction,float size)
        {
            var surge=PaintedPanel("Travelling aqua flute impulse",source+Vector3.up*.62f,DirectedRight(direction),viewCamera.transform.up,.88f*size,.58f*size,waterPaint,WaterRegions[0],new Color(1,1,1,.83f),.055f,.61f,.16f);SetTravel(surge,target-source);surge.fadeOut=.72f;
            var contact=PaintedPanel("Aqua spray contact",target+Vector3.up*.58f,DirectedRight(direction),viewCamera.transform.up,.83f*size,.75f*size,waterPaint,WaterRegions[3],new Color(1,1,1,.78f),ImpactProgress,.28f,.25f);contact.fadeIn=0;contact.scaleFrom=.94f;contact.scaleTo=1.05f;
        }
        private void AnchorThrust(Vector3 source,Vector3 target,float size)
        {
            DirectedPanel("Firm bronze-gold spear impulse",source+Vector3.up*.78f,target+Vector3.up*.72f,.30f*size,anchorPaint,AnchorRegions[1],new Color(1,1,1,.84f),.20f,.30f,.30f,true);
            var impact=PaintedPanel("Painted amber spear contact",target+Vector3.up*.57f,DirectedRight(target-source),viewCamera.transform.up,.95f*size,.73f*size,anchorPaint,AnchorRegions[3],new Color(1,1,1,.79f),ImpactProgress,.25f,.38f);impact.fadeIn=0;impact.scaleFrom=.94f;impact.scaleTo=1.06f;
            BurstSparks(target+Vector3.up*.62f,(target-source).normalized,Accent("shangshuo"),6,ImpactProgress);
            LocalLight(target+Vector3.up*.50f,new Color(1,.62f,.22f),.42f,1.8f,ImpactProgress,.22f);
        }
        private void Threads(Vector3 source,Vector3 target,bool healing,float size=1)
        {
            Vector3 from=source+Vector3.up*.77f,to=target+Vector3.up*.60f;
            if((target-source).sqrMagnitude<.025f){from+=viewCamera.transform.right*.48f;to-=viewCamera.transform.right*.12f;}
            // This SINGLE painted strip contains exactly two separate unbranched
            // source strands. Do not add a third curve or subdivide the topology.
            var strands=PaintedThreadBand(from,to,.55f*size,.10f,.58f);strands.reveal=(ImpactProgress-.10f)/.58f;strands.erase=.61f;
            var fabric=PaintedPanel(healing?"Warm woven healing veil":"Copper thread capture fabric",target+Vector3.up*.62f+viewCamera.transform.forward*.018f,DirectedRight(target-source),viewCamera.transform.up,1.13f*size,1.02f*size,threadPaint,ThreadRegions[1],new Color(1,1,1,healing?.46f:.40f),ImpactProgress-.04f,.53f,.21f);fabric.scaleFrom=.97f;fabric.scaleTo=1.03f;fabric.velocity=Vector3.up*.13f;fabric.fadeIn=.13f;
            var flare=PaintedPanel(healing?"Ivory healing contact bloom":"Copper thread contact",target+Vector3.up*.53f,viewCamera.transform.right,viewCamera.transform.up,.87f*size,.87f*size,threadPaint,ThreadRegions[healing?2:3],new Color(1,1,1,.63f),ImpactProgress,.31f,.33f);flare.fadeIn=0;flare.scaleFrom=.95f;flare.scaleTo=1.06f;
            LocalLight(target+Vector3.up*.58f,new Color(1,.54f,.22f),healing?.42f:.30f,1.9f,ImpactProgress,.29f);
        }
        private void Shields(Vector3 target,IReadOnlyList<Vector3> affected,string hero)
        {
            Material material=hero=="sixuan"?crystalPaint:hero=="cangling"?waterPaint:anchorPaint;
            Vector4 pane=hero=="sixuan"?CrystalRegions[2]:hero=="cangling"?WaterRegions[0]:AnchorRegions[0];
            Vector4 seal=hero=="sixuan"?CrystalRegions[2]:hero=="cangling"?WaterRegions[2]:AnchorRegions[2];
            int count=affected==null||affected.Count==0?1:affected.Count;
            for(int j=0;j<count;j++){
                Vector3 point=affected==null||affected.Count==0?target:affected[j];
                var shell=PaintedShieldShell("Curved painted guardian veil",point+Vector3.up*.70f,1.36f,1.42f,material,pane,new Color(1,1,1,.39f),.13f+j*.012f,.70f,.20f);shell.scaleFrom=.98f;shell.fadeIn=.20f;shell.fadeOut=.44f;
                var ground=PaintedPanel("Anchored guardian foundation",point+Vector3.up*.047f,Vector3.right,Vector3.forward,1.45f,1.45f,material,seal,new Color(1,1,1,.44f),.12f,.73f,.24f);ground.scaleFrom=.96f;ground.fadeIn=.18f;
                if(hero!="cangling")for(int k=-1;k<=1;k+=2){var post=PaintedPanel("Guardian boundary pillar",point+viewCamera.transform.right*k*.55f+Vector3.up*.70f,viewCamera.transform.right,Vector3.up,.20f,1.40f,material,hero=="sixuan"?CrystalRegions[3]:AnchorRegions[1],new Color(1,1,1,.53f),.17f,.62f,.28f);post.reveal=.21f;post.sweepAxis=1;}
            }
            LocalLight(target+Vector3.up*.62f,hero=="sixuan"?new Color(.24f,.54f,1):hero=="cangling"?Water:new Color(1,.63f,.23f),.39f,2.1f,ImpactProgress,.32f);
        }
        private void ShadowFoot(Vector3 point)
        {
            var shadow=PaintedPanel("Painted borrowed eclipse shadow",point+Vector3.up*.045f,Vector3.right,Vector3.forward,1.28f,1.28f,moonPaint,MoonRegions[1],new Color(.45f,.38f,.52f,.48f),.08f,.58f,.10f);shadow.scaleFrom=1.03f;shadow.scaleTo=.76f;
            var shards=PaintedPanel("Ruby shadow wisps",point+Vector3.up*.42f,viewCamera.transform.right,viewCamera.transform.up,.64f,.85f,moonPaint,MoonRegions[3],new Color(.65f,.57f,.72f,.38f),.19f,.55f,.16f);shards.velocity=Vector3.up*.24f;
        }
        private void Push(Vector3 source,Vector3 target,Vector3 direction)
        {
            DirectedPanel("Short forward kinetic impulse",source+Vector3.up*.63f,target+direction*.18f+Vector3.up*.63f,.37f,anchorPaint,AnchorRegions[1],new Color(1,.94f,.83f,.65f),.20f,.30f,.23f,true);
            var contact=PaintedPanel("Amber kinetic contact dust",target+Vector3.up*.58f,DirectedRight(direction),viewCamera.transform.up,1.02f,.78f,anchorPaint,AnchorRegions[3],new Color(1,.93f,.80f,.61f),ImpactProgress,.29f,.30f);contact.fadeIn=0;contact.velocity=direction*.28f;
        }
        private void Ticks(Vector3 target)
        {
            var cloth=PaintedPanel("Painted changeover woven light",target+Vector3.up*.72f,viewCamera.transform.right,viewCamera.transform.up,.92f,1.14f,threadPaint,ThreadRegions[1],new Color(1,.94f,.83f,.46f),.13f,.64f,.23f);cloth.velocity=Vector3.up*.25f;cloth.scaleFrom=.97f;cloth.fadeIn=.16f;
            var contact=PaintedPanel("Changeover restored-action bloom",target+Vector3.up*.46f,viewCamera.transform.right,viewCamera.transform.up,.73f,.73f,threadPaint,ThreadRegions[2],new Color(1,1,1,.57f),ImpactProgress,.33f,.29f);contact.fadeIn=0;
        }
        private void Dust(Vector3 target,IReadOnlyList<Vector3> affected)
        {
            int count=affected==null||affected.Count==0?1:affected.Count;
            for(int j=0;j<count;j++){
                Vector3 point=affected==null||affected.Count==0?target:affected[j];
                var impact=PaintedPanel("Painted stone-fragment contact",point+Vector3.up*.55f,viewCamera.transform.right,viewCamera.transform.up,1.27f,1.05f,anchorPaint,AnchorRegions[3],new Color(1,.94f,.82f,.78f),ImpactProgress+j*.01f,.31f,.34f);impact.fadeIn=0;impact.scaleFrom=.93f;impact.scaleTo=1.08f;
                for(int i=0;i<3;i++)Dot("Settling local pipe dust",point+Vector3.up*.25f,new Vector3(Mathf.Sin(i*2.4f)*.30f,.22f,Mathf.Cos(i*2.4f)*.30f),.17f,new Color(.66f,.58f,.45f,.26f),ImpactProgress+i*.021f,.50f,true);
                BurstSparks(point+Vector3.up*.40f,Vector3.up,Gold,6,ImpactProgress);
            }
        }
        private void BurstSparks(Vector3 center,Vector3 direction,Color color,int count,float born)
        {
            for(int i=0;i<count;i++)
            {
                Vector3 spread=viewCamera.transform.right*Mathf.Sin(i*2.399f)+viewCamera.transform.up*(.25f+Mathf.Cos(i*2.399f)*.65f);
                Vector3 velocity=direction*.37f+spread*(.21f+i%3*.065f);
                Spark(center,velocity,.10f+i%3*.025f,color,born+(i%3)*.012f,.27f+i%3*.045f);
            }
        }
        private void Spark(Vector3 center,Vector3 velocity,float length,Color color,float born,float life)
        {
            Vector3 axis=Vector3.ProjectOnPlane(velocity,viewCamera.transform.forward).normalized;
            if(axis.sqrMagnitude<.01f)axis=viewCamera.transform.up;
            Vector3 side=Vector3.Cross(viewCamera.transform.forward,axis)*.012f;
            axis*=length*.5f;
            var mesh=new Mesh{name="Tapered directional painted chip"};
            mesh.vertices=new[]{-axis,side,axis,-side};mesh.uv=new[]{new Vector2(0,.5f),new Vector2(.5f,0),new Vector2(1,.5f),new Vector2(.5f,1)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();meshes.Add(mesh);
            var p=Add("Ballistic directional contact chip",mesh,ribbon,center,color,born,life);p.velocity=velocity;p.gravity=.24f;p.moveFinish=1;p.scaleFrom=1;p.scaleTo=.24f;p.fadeIn=0;p.fadeOut=.28f;
        }
        private void SetTravel(Piece piece,Vector3 displacement)
        {piece.velocity=displacement;piece.moveFinish=Mathf.Clamp01((ImpactProgress-piece.born)/piece.life);piece.projectile=true;}
        private void Curve(string name,Vector3 from,Vector3 to,float width,Color color,float born,float life,float bend=.12f)
        {
            var points=new Vector3[25];for(int i=0;i<points.Length;i++){float t=i/(float)(points.Length-1);points[i]=Vector3.Lerp(from,to,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*bend;}
            Ribbon(name,points,width,color,born,life,.28f);
        }
        private Piece Ribbon(string name,Vector3[] points,float width,Color color,float born,float life,float reveal,bool ground=false)
        {
            // The ribbon is a real curved mesh, rather than a camera-facing cylinder or another impact ring.
            var vertices=new Vector3[points.Length*2];var uv=new Vector2[vertices.Length];var triangles=new int[(points.Length-1)*6];Vector3 center=Vector3.zero;
            for(int i=0;i<points.Length;i++)center+=points[i];center/=points.Length;
            for(int i=0;i<points.Length;i++)
            {
                Vector3 tangent=points[Math.Min(i+1,points.Length-1)]-points[Math.Max(0,i-1)];Vector3 side=Vector3.Cross(tangent,ground?Vector3.up:viewCamera.transform.forward).normalized;
                if(side.sqrMagnitude<.01f)side=Vector3.right;
                // Straight boundaries have no interior sample; tapering both endpoints
                // would shrink the whole segment to 12.5% of its intended width.
                float taper=points.Length==2?1:Mathf.Sin(Mathf.PI*(.04f+.92f*i/(points.Length-1)));Vector3 offset=side*width*.5f*taper;
                vertices[i*2]=points[i]-center-offset;vertices[i*2+1]=points[i]-center+offset;uv[i*2]=new Vector2(i/(float)(points.Length-1),0);uv[i*2+1]=new Vector2(i/(float)(points.Length-1),1);
                if(i==points.Length-1)continue;int t=i*6,v=i*2;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2;
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();meshes.Add(mesh);
            var piece=Add(name,mesh,ribbon,center,color,born,life);piece.reveal=reveal;return piece;
        }
        private Piece PaintedPanel(string name,Vector3 center,Vector3 right,Vector3 up,float width,float height,Material material,Vector4 region,Color color,float born,float life,float emission)
        {
            right*=width*.5f;up*=height*.5f;
            var mesh=new Mesh{name=name};mesh.vertices=new[]{-right-up,right-up,right+up,-right+up};mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();meshes.Add(mesh);
            return AddPainted(name,mesh,material,center,region,color,born,life,emission);
        }
        private Piece PaintedThreadBand(Vector3 from,Vector3 to,float width,float born,float life)
        {
            const int segments=32;Vector3 center=(from+to)*.5f,delta=to-from;
            var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];
            for(int i=0;i<=segments;i++){
                float t=i/(float)segments;Vector3 point=Vector3.Lerp(from,to,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.18f;
                Vector3 tangent=delta+Vector3.up*(Mathf.Cos(t*Mathf.PI)*Mathf.PI*.18f),side=Vector3.Cross(viewCamera.transform.forward,tangent).normalized;
                if(side.sqrMagnitude<.01f)side=viewCamera.transform.up;
                vertices[i*2]=point-center-side*width*.5f;vertices[i*2+1]=point-center+side*width*.5f;uv[i*2]=new Vector2(t,0);uv[i*2+1]=new Vector2(t,1);
                if(i==segments)continue;int v=i*2,index=i*6;triangles[index]=v;triangles[index+1]=v+1;triangles[index+2]=v+2;triangles[index+3]=v+1;triangles[index+4]=v+3;triangles[index+5]=v+2;
            }
            var mesh=new Mesh{name="Exactly two painted dream strands"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();meshes.Add(mesh);
            return AddPainted("Exactly two painted dream strands",mesh,threadPaint,center,ThreadRegions[0],new Color(1,1,1,.79f),born,life,.27f);
        }
        private Piece PaintedShieldShell(string name,Vector3 center,float width,float height,Material material,Vector4 region,Color color,float born,float life,float emission)
        {
            const int columns=24,rows=6;var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[columns*rows*6];
            Vector3 toward=Vector3.ProjectOnPlane(-viewCamera.transform.forward,Vector3.up).normalized;
            for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++){
                float u=col/(float)columns,v=row/(float)rows;int index=row*(columns+1)+col;
                vertices[index]=viewCamera.transform.right*((u-.5f)*width)+Vector3.up*((v-.5f)*height)+toward*Mathf.Cos((u-.5f)*1.65f)*.24f;uv[index]=new Vector2(u,v);
                if(row==rows||col==columns)continue;int t=(row*columns+col)*6;triangles[t]=index;triangles[t+1]=index+columns+1;triangles[t+2]=index+1;triangles[t+3]=index+1;triangles[t+4]=index+columns+1;triangles[t+5]=index+columns+2;
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();meshes.Add(mesh);
            return AddPainted(name,mesh,material,center,region,color,born,life,emission);
        }
        private Piece WaveSurface(string name,Vector3 center,Vector3 direction,Vector3 side,float width,float length,Material material,Vector4 region,Color color,float born,float life,float emission)
        {
            const int columns=32,rows=14;var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[columns*rows*6];
            for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++)
            {
                float u=col/(float)columns,v=row/(float)rows,ridge=Mathf.Pow(Mathf.Max(0,1-Mathf.Abs(u-.73f)*5.8f),2),cross=1-Mathf.Pow(v*2-1,2);
                int index=row*(columns+1)+col;vertices[index]=direction*((u-.5f)*length)+side*((v-.5f)*width)+Vector3.up*(.015f+ridge*cross*.25f*(width/3.3f));uv[index]=new Vector2(u,v);
                if(row==rows||col==columns)continue;int t=(row*columns+col)*6;triangles[t]=index;triangles[t+1]=index+columns+1;triangles[t+2]=index+1;triangles[t+3]=index+1;triangles[t+4]=index+columns+1;triangles[t+5]=index+columns+2;
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();meshes.Add(mesh);
            return AddPainted(name,mesh,material,center,region,color,born,life,emission);
        }
        private void FacetedCrystal(string name,Vector3 center,float width,float height,float born,float life)
        {
            Vector3 right=viewCamera.transform.right*width*.5f,front=Vector3.Cross(viewCamera.transform.right,Vector3.up).normalized*width*.35f;
            var ring=new[]{right,front,-right,-front};var vertices=new Vector3[24];var uv=new Vector2[24];var triangles=new int[24];var colors=new Color[24];
            for(int half=0;half<2;half++)for(int side=0;side<4;side++)
            {
                int v=(half*4+side)*3;vertices[v]=Vector3.up*height*(half==0?.5f:-.5f);vertices[v+1]=ring[side];vertices[v+2]=ring[(side+1)%4];
                uv[v]=new Vector2(.5f,half==0?1:0);uv[v+1]=new Vector2(0,.5f);uv[v+2]=new Vector2(1,.5f);
                triangles[v]=v;triangles[v+1]=half==0?v+1:v+2;triangles[v+2]=half==0?v+2:v+1;
                var color=side%2==0?Color.white:new Color(.66f,.82f,1,1);colors[v]=colors[v+1]=colors[v+2]=color;
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.colors=colors;mesh.RecalculateBounds();meshes.Add(mesh);
            var piece=AddPainted(name,mesh,crystalPaint,center,CrystalRegions[0],new Color(1,1,1,.79f),born,life,.35f);piece.facet=1;piece.velocity=Vector3.up*.045f;piece.fadeIn=0;
        }
        private Piece AddPainted(string name,Mesh mesh,Material material,Vector3 position,Vector4 region,Color color,float born,float life,float emission)
        {
            mesh.RecalculateNormals();if(mesh.colors.Length==0){var colors=new Color[mesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;mesh.colors=colors;}
            var piece=Add(name,mesh,material,position,color,born,life);piece.painted=true;piece.region=region;piece.emission=emission;return piece;
        }
        private void LocalLight(Vector3 position,Color color,float intensity,float range,float born,float life)
        {
            var light=new GameObject("Transient painted spell bounce",typeof(Light)).GetComponent<Light>();light.transform.SetParent(activeRoot,false);light.transform.position=position;
            light.type=LightType.Point;light.color=color;light.range=range;light.shadows=LightShadows.None;light.intensity=0;localLights.Add(new LightCue{light=light,intensity=intensity,born=born,life=life});
        }
        private void Dot(string name,Vector3 point,Vector3 velocity,float size,Color color,float born,float life,bool smoke=false)
        {
            Vector3 right=viewCamera.transform.right*size,up=viewCamera.transform.up*size;var mesh=new Mesh{name=name};mesh.vertices=new[]{-right-up,right-up,right+up,-right+up};mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();meshes.Add(mesh);
            var p=Add(name,mesh,smoke?wash:particle,point,color,born,life);p.velocity=velocity;p.scaleTo=smoke?1.65f:.28f;p.gravity=smoke?.03f:.32f;p.moveFinish=1;p.fadeIn=smoke?.12f:0;
        }
        private Piece Add(string name,Mesh mesh,Material material,Vector3 position,Color color,float born,float life)
        {
            var root=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(activeRoot,false);root.transform.position=position;root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
            var p=new Piece{transform=root.transform,renderer=renderer,start=position,color=color,born=born,life=life};pieces.Add(p);return p;
        }
        private void Render(float time,bool reduced)
        {
            foreach(var glow in localLights)if(glow.light)
            {
                float t=(time-glow.born)/glow.life;
                glow.light.intensity=reduced||t<0||t>=1?0:glow.intensity*Mathf.Pow(1-t,2.5f);
            }
            for(int i=0;i<pieces.Count;i++)
            {
                var p=pieces[i];float t=(time-p.born)/p.life;bool show=t>=0&&t<1&&(!reduced||p.painted);p.renderer.enabled=show;if(!show)continue;
                float travel=Mathf.Clamp01(t/Mathf.Max(.01f,p.moveFinish));
                float motion=p.projectile?Mathf.SmoothStep(0,1,travel):1-Mathf.Pow(1-travel,3);
                // Reduced motion keeps the contact artwork still and omits flying
                // chips, light pulses and the local strike pause.
                p.transform.position=p.start+p.velocity*(reduced?1:motion)-Vector3.up*(reduced?0:p.gravity*t*t);
                float scaleTime=p.painted?1-Mathf.Pow(1-Mathf.Clamp01(t/.30f),3):t;
                p.transform.localScale=Vector3.one*Mathf.Lerp(p.scaleFrom,p.scaleTo,reduced?.5f:scaleTime);
                p.transform.rotation=Quaternion.AngleAxis(reduced?0:Mathf.Lerp(p.spinFrom,p.spinTo,1-Mathf.Pow(1-t,3)),viewCamera.transform.forward);
                float opacity=(p.fadeIn<=0?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(t/p.fadeIn)))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-p.fadeOut)/(1-p.fadeOut))));
                float written=p.reveal>0?Mathf.SmoothStep(0,1,Mathf.Clamp01(t/p.reveal)):1;
                Color color=p.color;color.a*=opacity;p.properties.SetColor(Tint,color);p.properties.SetFloat(Reveal,reduced?1:written);
                p.properties.SetFloat(Tail,!reduced&&p.erase>0?Mathf.SmoothStep(0,1,Mathf.Clamp01((t-p.erase)/(1-p.erase))):0);
                p.properties.SetFloat(Dissolve,Mathf.Clamp01((t-.56f)/.44f));
                if(p.painted){p.properties.SetVector(AtlasRect,p.region);p.properties.SetFloat(Emission,p.emission*(.55f+.45f*Mathf.Pow(1-t,2)));p.properties.SetFloat(Phase,reduced?0:t);p.properties.SetVector(Flow,new Vector4(p.flow.x,p.flow.y,0,0));p.properties.SetFloat(Facet,p.facet);p.properties.SetFloat(SweepAxis,p.sweepAxis);p.properties.SetVector(GroundFade,new Vector4(p.groundFade.x,p.groundFade.y,0,0));}
                p.renderer.SetPropertyBlock(p.properties);
            }
        }
        public void Cancel()
        {
            generation++;PreviewHeld=false;if(activeRoot){activeRoot.gameObject.SetActive(false);Destroy(activeRoot.gameObject);}activeRoot=null;pieces.Clear();localLights.Clear();
            foreach(var mesh in meshes)if(mesh)Destroy(mesh);meshes.Clear();
        }
        private void OnDisable(){Cancel();}
        private void OnDestroy(){Cancel();foreach(var material in materials)if(material)Destroy(material);materials.Clear();}
    }
}
