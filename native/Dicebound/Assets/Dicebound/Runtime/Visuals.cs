using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dicebound.Presentation
{
    public static class Palette
    {
        public static readonly Color Ink=new Color(.038f,.062f,.085f);
        public static readonly Color Panel=new Color(.045f,.065f,.085f,.97f);
        public static readonly Color Bone=new Color(.93f,.92f,.85f);
        public static readonly Color Gold=new Color(.78f,.69f,.48f);
        public static readonly Color Muted=new Color(.57f,.67f,.73f);
        public static readonly Color Wine=new Color(.46f,.115f,.14f);
        public static readonly Color Teal=new Color(.25f,.65f,.64f);
        public static Color Kind(string kind) {return kind=="attack"?new Color(.67f,.27f,.25f):kind=="trick"?new Color(.44f,.44f,.72f):Teal;}
    }
    public static class Visuals
    {
        private static readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        public static T Own<T>(T value) where T:UnityEngine.Object {owned.Add(value);return value;}
        public static void Release() {foreach(var o in owned)if(o)UnityEngine.Object.Destroy(o);owned.Clear();}
        public static Material Material(Color color,float metallic=0,float smooth=.35f,bool unlit=false)
        {
            var m=Own(new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")));
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
            if(color.a<1) {
                m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);
                m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_Cull",(int)CullMode.Off);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;
            }
            return m;
        }
        public static Material Picture(Texture texture,Color tint,bool transparent=false)
        {
            var m=Material(tint,0,0,true);m.SetTexture("_BaseMap",texture);
            m.SetInt("_Cull",(int)CullMode.Off);
            if(transparent) {m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.02f);m.EnableKeyword("_ALPHATEST_ON");}
            return m;
        }
        public static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material material,bool collision=false)
        {
            var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(parent,false);
            o.transform.localPosition=pos;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision)UnityEngine.Object.Destroy(o.GetComponent<Collider>());return o;
        }
        public static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            var o=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(parent,false);
            o.GetComponent<MeshFilter>().sharedMesh=mesh;o.GetComponent<MeshRenderer>().sharedMaterial=material;return o;
        }
        public static Mesh Panel(float width,float height,int columns=1,int rows=1)
        {
            var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++) {
                int n=y*(columns+1)+x;uv[n]=new Vector2((float)x/columns,(float)y/rows);
                vertices[n]=new Vector3((uv[n].x-.5f)*width,(uv[n].y-.5f)*height,0);
                if(x<columns&&y<rows) {int a=n,b=n+1,c=n+columns+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}
            }
            var mesh=Own(new Mesh{name="Painted panel"});mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Mesh RoundedBox(Vector3 size,float radius)
        {
            var points=new[]{-1f,-.86f,-.5f,0,.5f,.86f,1f};var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            var directions=new[]{Vector3.up,Vector3.down,Vector3.right,Vector3.left,Vector3.forward,Vector3.back};
            foreach(var normal in directions) {
                var right=Vector3.Cross(normal,Mathf.Abs(normal.y)>.9f?Vector3.forward:Vector3.up).normalized;var up=Vector3.Cross(right,normal);
                int offset=vertices.Count;
                for(int y=0;y<points.Length;y++)for(int x=0;x<points.Length;x++) {
                    Vector3 cube=normal+right*points[x]+up*points[y];
                    var p=Vector3.Scale(cube,size*.5f);var inner=size*.5f-Vector3.one*radius;
                    var nearest=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    var outward=(p-nearest).normalized;vertices.Add(nearest+outward*radius);normals.Add(outward);uv.Add(new Vector2((points[x]+1)/2,(points[y]+1)/2));
                    if(x<points.Length-1&&y<points.Length-1) {int a=offset+y*points.Length+x,b=a+1,c=a+points.Length,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}
                }
            }
            var mesh=Own(new Mesh{name="Rounded solid"});mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        public static LineRenderer Line(string name,Transform parent,IEnumerable<Vector3> points,Color color,float width=.018f,bool loop=false)
        {
            var o=new GameObject(name,typeof(LineRenderer));o.transform.SetParent(parent,false);var line=o.GetComponent<LineRenderer>();
            line.sharedMaterial=Material(color,0,0,true);line.useWorldSpace=false;line.loop=loop;line.startWidth=line.endWidth=width;line.numCornerVertices=4;line.numCapVertices=4;
            var list=new List<Vector3>(points);line.positionCount=list.Count;line.SetPositions(list.ToArray());return line;
        }
        public static LineRenderer Ring(string name,Transform parent,float radius,Color color,float y=0)
        {
            var points=new List<Vector3>();for(int i=0;i<96;i++){float t=i*Mathf.PI*2/96;points.Add(new Vector3(Mathf.Cos(t)*radius,y,Mathf.Sin(t)*radius));}
            return Line(name,parent,points,color,.015f,true);
        }
        public static float Smooth(float t) {return t*t*(3-2*t);}
        public static IEnumerator Tween(float seconds,Action<float> sample)
        {
            if(seconds<=0){sample(1);yield break;}
            for(float time=0;time<seconds;time+=Time.unscaledDeltaTime){sample(Mathf.Clamp01(time/seconds));yield return null;}sample(1);
        }
    }
}
