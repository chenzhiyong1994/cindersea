using System;
using System.Collections.Generic;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dicebound.Presentation
{
    /// <summary>Six retained meshes, no per-hover GameObject rebuild; rules supply every cell and path.</summary>
    public sealed class TacticalRangeRenderer : MonoBehaviour
    {
        public const float BorderPixels=3f;
        public int VisibleCellCount {get;private set;}
        public int ProtectedLabelCount {get;private set;}
        private sealed class Part
        {
            public Mesh mesh;public Material material;public MeshRenderer renderer;
            public readonly List<Vector3> vertices=new List<Vector3>(512),directions=new List<Vector3>(512);
            public readonly List<Vector2> uv=new List<Vector2>(512);
            public readonly List<int> triangles=new List<int>(768);
            public void Clear(){vertices.Clear();directions.Clear();uv.Clear();triangles.Clear();}
            public void Upload(){mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(directions);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.18f);mesh.bounds=bounds;renderer.enabled=vertices.Count>0;}
        }
        private readonly Part[] parts=new Part[6];
        private readonly List<RectTransform> labels=new List<RectTransform>(16);
        private readonly Vector4[] protectedRects=new Vector4[32];
        private readonly Vector3[] corners=new Vector3[4];
        private readonly HashSet<int> emphasized=new HashSet<int>();
        private Camera viewCamera;private TacticalEnvironment environment;
        private static readonly int ProtectedRectsId=Shader.PropertyToID("_TacticalRangeProtectedRects"),ProtectedCountId=Shader.PropertyToID("_TacticalRangeProtectedCount");
        private static readonly Color Cyan=new Color(.015f,.74f,.91f),Amber=new Color(1,.63f,.15f),Red=new Color(.97f,.115f,.055f),Green=new Color(.06f,.83f,.49f);
        public void Initialize(Camera view,TacticalEnvironment board,Transform world)
        {
            viewCamera=view;environment=board;var shader=Resources.Load<Shader>("Tactics/Range/TacticalRange");
            if(!shader||!shader.isSupported)throw new InvalidOperationException("Missing or unsupported tactical range shader.");
            var root=new GameObject("Depth-tested tactical range overlay").transform;root.SetParent(world,false);
            for(int i=0;i<parts.Length;i++)
            {
                var item=new GameObject(new[]{"Legal cells","Selected unit","Actual affected cells","Focused target","Rule path","Invalid focus X"}[i]);item.transform.SetParent(root,false);
                var part=new Part{mesh=new Mesh{name=item.name+" shared range mesh"},material=new Material(shader){name=item.name+" range ink",renderQueue=3100+i*10}};
                item.AddComponent<MeshFilter>().sharedMesh=part.mesh;part.renderer=item.AddComponent<MeshRenderer>();part.renderer.sharedMaterial=part.material;
                part.renderer.shadowCastingMode=ShadowCastingMode.Off;part.renderer.receiveShadows=false;part.renderer.enabled=false;
                part.material.SetFloat("_BorderPixels",BorderPixels);part.material.SetFloat("_LinePixels",i==4?7f:i==5?3.8f:3.2f);part.material.SetFloat("_PathOutline",i==4?1:0);part.material.SetFloat("_Shape",i>=4?1:0);parts[i]=part;
            }
        }
        public void Protect(IEnumerable<Transform> roots)
        {labels.Clear();foreach(var root in roots)if(root is RectTransform rect)labels.Add(rect);}
        private void LateUpdate()
        {
            if(!viewCamera)return;int count=0;
            for(int i=0;i<labels.Count&&count<protectedRects.Length;i++)
            {
                var label=labels[i];if(!label||!label.gameObject.activeInHierarchy)continue;
                label.GetWorldCorners(corners);float x0=1,y0=1,x1=0,y1=0;
                for(int j=0;j<4;j++){var p=viewCamera.WorldToViewportPoint(corners[j]);x0=Mathf.Min(x0,p.x);y0=Mathf.Min(y0,p.y);x1=Mathf.Max(x1,p.x);y1=Mathf.Max(y1,p.y);}
                float pad=2f/Mathf.Max(1,viewCamera.pixelHeight);protectedRects[count++]=new Vector4(x0-pad,y0-pad,x1+pad,y1+pad);
            }
            ProtectedLabelCount=count;Shader.SetGlobalInt(ProtectedCountId,count);Shader.SetGlobalVectorArray(ProtectedRectsId,protectedRects);
        }
        private void Style(int index,Color color,float alpha){parts[index].material.SetColor("_Color",color);parts[index].material.SetFloat("_FillAlpha",alpha);}
        public void Show(TacticalState state,string unitId,string skillId,TacticalRangeSnapshot snapshot)
        {
            if(parts[0]==null)return;for(int i=0;i<parts.Length;i++)parts[i].Clear();emphasized.Clear();VisibleCellCount=0;
            var unit=TacticalRules.FindUnit(state,unitId);bool move=skillId==null;
            Style(0,move?Cyan:Amber,move?.20f:.28f);Style(1,Cyan,0);Style(2,snapshot.semantic==TacticalRangeSemantic.Support?Green:snapshot.semantic==TacticalRangeSemantic.Move?Amber:Amber,.40f);Style(3,Amber,0);Style(4,new Color(1,.82f,.32f),1);Style(5,Red,1);
            foreach(var cell in snapshot.affected)emphasized.Add(cell.y*environment.BoardWidth+cell.x);
            if(snapshot.request!=null)emphasized.Add(snapshot.request.y*environment.BoardWidth+snapshot.request.x);
            foreach(var cell in snapshot.legal)if(!emphasized.Contains(cell.y*environment.BoardWidth+cell.x)){Cell(parts[0],cell.x,cell.y,.028f);VisibleCellCount++;}
            if(unit!=null&&unit.hp>0)Cell(parts[1],unit.x,unit.y,.034f);
            if(snapshot.request!=null&&TacticalRules.Inside(state,snapshot.request.x,snapshot.request.y))
            {
                int x=snapshot.request.x,y=snapshot.request.y;
                if(snapshot.semantic==TacticalRangeSemantic.Invalid)
                {
                    var p=environment.CellWorld(x,y)+Vector3.up*.072f;
                    Segment(parts[5],p+new Vector3(-.31f,0,-.31f),p+new Vector3(.31f,0,.31f));Segment(parts[5],p+new Vector3(-.31f,0,.31f),p+new Vector3(.31f,0,-.31f));
                }
                else
                {
                    foreach(var cell in snapshot.affected){Cell(parts[2],cell.x,cell.y,.038f);VisibleCellCount++;}
                    Cell(parts[3],x,y,.045f);
                    if(unit!=null&&snapshot.path.Count>0)
                    {
                        var from=environment.CellWorld(unit.x,unit.y)+Vector3.up*.085f;
                        foreach(var cell in snapshot.path)
                        {
                            var to=environment.CellWorld(cell.x,cell.y)+Vector3.up*.085f;Segment(parts[4],from,to);
                            var direction=(to-from).normalized;var side=Vector3.Cross(Vector3.up,direction);var tip=Vector3.Lerp(from,to,.72f);
                            Segment(parts[4],tip-direction*.20f+side*.14f,tip);Segment(parts[4],tip-direction*.20f-side*.14f,tip);from=to;
                        }
                    }
                    if(!string.IsNullOrEmpty(snapshot.preview?.objectId))
                    {
                        var item=state.objects.Find(o=>o.id==snapshot.preview.objectId);
                        if(item!=null){Style(4,Amber,1);var a=environment.CellWorld(item.x,item.y)+Vector3.up*.45f;var b=environment.CellWorld(x,y)+Vector3.up*.35f;var from=a;for(int j=1;j<=24;j++){float t=j/24f;var to=Vector3.Lerp(a,b,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*1.7f;Segment(parts[4],from,to);from=to;}}
                    }
                }
            }
            for(int i=0;i<parts.Length;i++)parts[i].Upload();
        }
        private void Cell(Part part,int x,int y,float lift)
        {
            var p=environment.CellWorld(x,y)+Vector3.up*lift;int i=part.vertices.Count;const float r=.588f;
            part.vertices.Add(p+new Vector3(-r,0,-r));part.vertices.Add(p+new Vector3(-r,0,r));part.vertices.Add(p+new Vector3(r,0,r));part.vertices.Add(p+new Vector3(r,0,-r));
            part.uv.Add(Vector2.zero);part.uv.Add(Vector2.up);part.uv.Add(Vector2.one);part.uv.Add(Vector2.right);for(int j=0;j<4;j++)part.directions.Add(Vector3.up);Quad(part,i);
        }
        private void Segment(Part part,Vector3 a,Vector3 b)
        {
            if((b-a).sqrMagnitude<.000001f)return;int i=part.vertices.Count;var direction=(b-a).normalized;
            part.vertices.Add(a);part.vertices.Add(a);part.vertices.Add(b);part.vertices.Add(b);
            part.uv.Add(new Vector2(-.5f,0));part.uv.Add(new Vector2(.5f,0));part.uv.Add(new Vector2(.5f,1));part.uv.Add(new Vector2(-.5f,1));for(int j=0;j<4;j++)part.directions.Add(direction);Quad(part,i);
        }
        private static void Quad(Part part,int i){part.triangles.Add(i);part.triangles.Add(i+1);part.triangles.Add(i+2);part.triangles.Add(i);part.triangles.Add(i+2);part.triangles.Add(i+3);}
        public void Clear(){VisibleCellCount=0;for(int i=0;i<parts.Length;i++)if(parts[i]!=null){parts[i].Clear();parts[i].Upload();}}
        private void OnDestroy(){for(int i=0;i<parts.Length;i++)if(parts[i]!=null){Destroy(parts[i].mesh);Destroy(parts[i].material);}Shader.SetGlobalInt(ProtectedCountId,0);}
    }
}
