using System;
using System.Collections.Generic;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dicebound.Presentation
{
    /// <summary>Persistent floor features read the saved terrain, independently of skill ranges and painted plates.</summary>
    public sealed class TacticalTerrainFeatures : MonoBehaviour
    {
        public int FeatureCellCount { get; private set; }
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<ParticleSystem> vapour = new List<ParticleSystem>();
        private readonly Dictionary<Material, Batch> batches = new Dictionary<Material, Batch>();
        private sealed class Batch { public readonly List<Vector3> vertices=new List<Vector3>(); public readonly List<Vector2> uv=new List<Vector2>(); public readonly List<int> triangles=new List<int>(); }
        private Transform features;
        private TacticalEnvironment environment;
        private bool paused;
        private Material shade, shadeLine, wet, ripple, copper, copperDark, heat, rubble, crack, wood, metal, mist;

        public void Build(TacticalEnvironment owner, TacticalState state)
        {
            Release();environment=owner;
            features=new GameObject("常态地形 · 阴影水雾热网与落脚特征").transform;features.SetParent(owner.StaticGeometry,false);
            var brush=BrushTexture();var cloud=CloudTexture();
            shade=Ink("黑帘投下的可借阴影",new Color(.11f,.105f,.20f,.69f),brush);
            shadeLine=Ink("斜落的帘影纹",new Color(.12f,.12f,.21f,.42f));
            wet=Ink("清青冷凝水迹",new Color(.13f,.48f,.53f,.64f),brush);
            ripple=Ink("冷凝水面细白波",new Color(.68f,.94f,.96f,.70f));
            copper=Ink("可通行的铜热管",new Color(.67f,.38f,.16f,.94f));
            copperDark=Ink("热管背光与榫接缝",new Color(.20f,.125f,.08f,.91f));
            heat=Ink("余热管缝橙色微光",new Color(1,.48f,.11f,.62f));
            rubble=Ink("破裂灰石的亮断面",new Color(.57f,.56f,.50f,.90f));
            crack=Ink("碎石与高台边缘暗缝",new Color(.14f,.17f,.19f,.76f));
            wood=Ink("可借掩护的低木撑",new Color(.45f,.30f,.17f,.90f));
            metal=Ink("高台踏缘与掩护铜角",new Color(.80f,.67f,.42f,.87f));
            mist=Ink("贴地冷凝水汽",new Color(.73f,.88f,.92f,.22f),cloud);
            foreach(var cell in state.terrain)
            {
                if(cell.kind=="wall"||cell.kind=="gap"||cell.kind=="plain")continue;
                var p=features.InverseTransformPoint(owner.CellWorld(cell.x,cell.y))+Vector3.up*(owner.PaintedActive?.065f:.023f);
                FeatureCellCount++;
                if(cell.kind=="shadow")
                {
                    // Feathered patch and diagonal bars read as cast shadow, never a hole.
                    Quad(shade,p,new Vector2(1.15f,1.09f),-19);
                    for(int i=0;i<4;i++)Quad(shadeLine,p+new Vector3((i-1.5f)*.20f,.004f,(i-1.5f)*.07f),new Vector2(.055f,.80f),31);
                }
                else if(cell.kind=="mist")
                {
                    Quad(wet,p,new Vector2(1.12f,1.01f),13);
                    Arc(ripple,p+new Vector3(-.08f,.008f,-.03f),.28f,.16f,-25,225,.016f);
                    Arc(ripple,p+new Vector3(.17f,.010f,.15f),.36f,.19f,28,174,.014f);
                    Fog(p,cell.x,cell.y);
                }
                else if(cell.kind=="pipe")
                {
                    for(int i=0;i<2;i++)
                    {
                        var q=p+new Vector3(0,0,(i-.5f)*.31f);
                        Quad(copperDark,q,new Vector2(1.13f,.15f));Quad(copper,q+Vector3.up*.004f,new Vector2(1.07f,.087f));
                        Quad(heat,q+new Vector3(0,.008f,-.027f),new Vector2(.96f,.018f));
                        for(int joint=-1;joint<=1;joint+=2)Quad(copperDark,q+new Vector3(joint*.38f,.010f,0),new Vector2(.052f,.19f));
                    }
                    Arc(copper,p+new Vector3(.18f,.013f,0),.105f,.105f,0,360,.031f);
                    Quad(heat,p+new Vector3(.18f,.017f,0),new Vector2(.15f,.025f),40);
                }
                else if(cell.kind=="rubble")
                {
                    for(int i=0;i<7;i++)
                    {
                        var q=p+new Vector3((i%3-1)*.29f,.004f,(i/3-1)*.30f);
                        Quad(crack,q,new Vector2(.31f,.25f),i*37);Quad(rubble,q+new Vector3(-.024f,.004f,-.023f),new Vector2(.23f,.16f),i*37);
                    }
                    Strip(crack,p,new[]{new Vector2(-.43f,.41f),new Vector2(-.12f,.08f),new Vector2(.08f,.14f),new Vector2(.42f,-.40f)},.023f);
                }
                else if(cell.kind=="cover")
                {
                    // Low edge braces signal cover while keeping the centre visibly walkable.
                    for(int i=0;i<3;i++){var q=p+new Vector3(0,.010f,.24f+i*.10f);Quad(crack,q,new Vector2(1.06f,.096f));Quad(wood,q+Vector3.up*.004f,new Vector2(1.01f,.061f));}
                    for(int side=-1;side<=1;side+=2)Quad(metal,p+new Vector3(side*.46f,.025f,.34f),new Vector2(.067f,.37f));
                }
                else if(cell.kind=="high")
                {
                    Quad(crack,p+new Vector3(0,0,-.49f),new Vector2(1.13f,.065f));
                    Quad(metal,p+new Vector3(0,.006f,-.455f),new Vector2(1.08f,.029f));
                    for(int i=0;i<3;i++)Strip(metal,p+new Vector3((i-1)*.24f,.010f,-.26f),new[]{new Vector2(-.07f,-.045f),new Vector2(0,.03f),new Vector2(.07f,-.045f)},.026f);
                }
            }
            foreach(var entry in batches)
            {
                var mesh=new Mesh{name=entry.Key.name+" · 全战场特征合批"};owned.Add(mesh);mesh.SetVertices(entry.Value.vertices);mesh.SetUVs(0,entry.Value.uv);mesh.SetColors(Enumerable.Repeat(Color.white,entry.Value.vertices.Count).ToList());mesh.SetTriangles(entry.Value.triangles,0);mesh.RecalculateBounds();
                var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(features,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=entry.Key;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            paused=false;RefreshMotion();
            Debug.Log("DICEBOUND_TERRAIN_FEATURES cells="+FeatureCellCount+" batches="+batches.Count+" painted="+owner.PaintedActive+" vapour="+vapour.Count);
        }
        private Material Ink(string name,Color color,Texture texture=null)
        {
            var shader=Shader.Find("Dicebound/TacticalSprite");if(!shader)throw new InvalidOperationException("地形特征缺少 TacticalSprite shader。");
            var material=new Material(shader){name=name,renderQueue=2995};owned.Add(material);material.SetColor("_Color",color);material.SetFloat("_Cutoff",.003f);material.SetFloat("_LightInfluence",0);material.SetFloat("_ZWrite",0);if(texture)material.SetTexture("_MainTex",texture);return material;
        }
        private void Polygon(Material material,Vector3[] points,Vector2[] uv=null)
        {
            if(!batches.TryGetValue(material,out var batch)){batch=new Batch();batches.Add(material,batch);}int first=batch.vertices.Count;
            batch.vertices.AddRange(points);batch.uv.AddRange(uv??Enumerable.Repeat(Vector2.one*.5f,points.Length));
            for(int i=1;i<points.Length-1;i++)batch.triangles.AddRange(new[]{first,first+i+1,first+i});
        }
        private void Quad(Material material,Vector3 p,Vector2 size,float angle=0)
        {
            var rotation=Quaternion.Euler(0,angle,0);float x=size.x*.5f,z=size.y*.5f;
            Polygon(material,new[]{p+rotation*new Vector3(-x,0,-z),p+rotation*new Vector3(x,0,-z),p+rotation*new Vector3(x,0,z),p+rotation*new Vector3(-x,0,z)},new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
        }
        private void Strip(Material material,Vector3 p,Vector2[] points,float width)
        {
            for(int i=1;i<points.Length;i++){Vector2 a=points[i-1],b=points[i],side=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Polygon(material,new[]{p+new Vector3(a.x+side.x,0,a.y+side.y),p+new Vector3(b.x+side.x,0,b.y+side.y),p+new Vector3(b.x-side.x,0,b.y-side.y),p+new Vector3(a.x-side.x,0,a.y-side.y)});}
        }
        private void Arc(Material material,Vector3 p,float radiusX,float radiusY,float from,float to,float width)
        {
            var points=new Vector2[19];for(int i=0;i<points.Length;i++){float a=Mathf.Lerp(from,to,i/(float)(points.Length-1))*Mathf.Deg2Rad;points[i]=new Vector2(Mathf.Cos(a)*radiusX,Mathf.Sin(a)*radiusY);}Strip(material,p,points,width);
        }
        private Texture2D BrushTexture()
        {
            const int n=96;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="原创地形湿影羽化笔触",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};owned.Add(texture);var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){float u=(x+.5f)/n,v=(y+.5f)/n,grain=Mathf.PerlinNoise(x*.075f,y*.075f);float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));float a=Mathf.SmoothStep(0,1,edge/.14f)*(.62f+.32f*grain);pixels[y*n+x]=new Color(1,1,1,a);}texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        private Texture2D CloudTexture()
        {
            const int n=64;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="原创低水汽软边",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};owned.Add(texture);var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){float u=(x-31.5f)/31.5f,v=(y-31.5f)/31.5f;pixels[y*n+x]=new Color(1,1,1,Mathf.Pow(Mathf.Max(0,1-u*u-v*v),1.35f));}texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        private void Fog(Vector3 point,int x,int y)
        {
            var go=new GameObject("水雾 · "+x+","+y,typeof(ParticleSystem));go.transform.SetParent(features,false);go.transform.localPosition=point+Vector3.up*.075f;
            var system=go.GetComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);system.useAutoRandomSeed=false;system.randomSeed=(uint)(17+x*181+y*37);
            var main=system.main;main.loop=true;main.startLifetime=5;main.startSpeed=.012f;main.startSize=new ParticleSystem.MinMaxCurve(.43f,.70f);main.startColor=new Color(1,1,1,.73f);main.maxParticles=4;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=system.emission;emission.rateOverTime=.7f;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(.78f,.025f,.68f);
            var color=system.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.23f),new GradientAlphaKey(.68f,.65f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mist;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            system.Simulate(5,true,true);system.Play();vapour.Add(system);
        }
        private void RefreshMotion()
        {
            if(!environment)return;bool reduced=environment.ReducedMotion;if(paused==reduced)return;paused=reduced;foreach(var system in vapour)if(system){if(reduced)system.Pause();else system.Play();}
        }
        private void Update(){RefreshMotion();}
        public void SetVisible(bool visible){if(features)features.gameObject.SetActive(visible);}
        public void Release()
        {
            if(features){features.gameObject.SetActive(false);Destroy(features.gameObject);}features=null;foreach(var resource in owned)if(resource)Destroy(resource);owned.Clear();vapour.Clear();batches.Clear();environment=null;FeatureCellCount=0;
        }
        private void OnDestroy(){Release();}
    }
}
