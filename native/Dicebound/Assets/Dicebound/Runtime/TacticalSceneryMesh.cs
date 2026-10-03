using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dicebound.Presentation
{
    /// <summary>Original metre-scale stonework and botanical geometry. No rule or collider data.</summary>
    internal static class TacticalSceneryMesh
    {
        internal static float Noise(int seed,int salt)
        {
            uint value=unchecked((uint)(seed*374761393+salt*668265263));
            value=(value^(value>>13))*1274126177u;value^=value>>16;
            return (value&0xffffff)/16777215f;
        }
        internal static Mesh Stone(Vector3 size,int variant,float bevel,bool paintedStone=true)
        {
            float hx=size.x*.5f,hz=size.z*.5f,hy=size.y*.5f;
            float corner=Mathf.Min(bevel,Mathf.Min(hx,hz)*.32f);
            var corners=new[]{new Vector2(-hx+corner,-hz),new Vector2(hx-corner,-hz),new Vector2(hx,-hz+corner),new Vector2(hx,hz-corner),new Vector2(hx-corner,hz),new Vector2(-hx+corner,hz),new Vector2(-hx,hz-corner),new Vector2(-hx,-hz+corner)};
            var outline=new Vector2[16];
            for(int i=0;i<16;i++){
                var q=i%2==0?corners[i/2]:Vector2.Lerp(corners[i/2],corners[(i/2+1)%8],.42f+Noise(variant,i)*.16f);
                float chip=(.06f+Noise(variant,i)*.38f)*corner;
                // Every chipped corner is inside the original footprint. Logical floors
                // and blockers keep their existing coordinates and exact picking colliders.
                q.x-=Mathf.Sign(q.x)*chip;q.y-=Mathf.Sign(q.y)*chip;outline[i]=q;
                if(paintedStone&&i%2==1&&Noise(variant,i+201)>.68f){
                    // Occasional eroded edge recesses interrupt a ruler-straight seam.
                    // Every top vertex retains the exact same y and stays inside its
                    // original footprint; the flat walk surface/collider never changes.
                    float notch=Mathf.Min(.065f,Mathf.Min(hx,hz)*.10f)*(.35f+Noise(variant,i+211)*.65f);
                    if(Mathf.Abs(q.x)/hx>Mathf.Abs(q.y)/hz)q.x-=Mathf.Sign(q.x)*notch;else q.y-=Mathf.Sign(q.y)*notch;
                    outline[i]=q;
                }
            }
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            float lip=Mathf.Min(corner*.88f,hy*.44f);
            var heights=new[]{-hy,-hy+lip,hy-lip,hy};
            var insets=new[]{corner*.32f,0f,0f,corner*.66f};
            for(int ring=0;ring<4;ring++)for(int side=0;side<16;side++){
                int next=(side+1)%16;
                Vector3 a=Point(outline[side],heights[ring],insets[ring]);
                Vector3 b=Point(outline[next],heights[ring],insets[ring]);
                if(ring<3){
                    Vector3 c=Point(outline[side],heights[ring+1],insets[ring+1]);
                    Vector3 d=Point(outline[next],heights[ring+1],insets[ring+1]);
                    float length=Vector3.Distance(a,b),u=Noise(variant,71+side)*.9f;
                    Quad(vertices,uv,triangles,a,c,b,d,new Vector2(u,heights[ring]+hy),new Vector2(u,heights[ring+1]+hy),new Vector2(u+length,heights[ring]+hy),new Vector2(u+length,heights[ring+1]+hy));
                }
            }
            Vector2 topOffset=Vector2.one*Noise(variant,82)*.3f;
            int top=vertices.Count;vertices.Add(new Vector3(0,hy,0));uv.Add(new Vector2(hx,hz)+topOffset);
            for(int i=0;i<16;i++){var p=Point(outline[i],hy,insets[3]);vertices.Add(p);uv.Add(new Vector2(p.x+hx,p.z+hz)+topOffset);}
            for(int i=0;i<16;i++)triangles.AddRange(new[]{top,top+1+(i+1)%16,top+1+i});
            int bottom=vertices.Count;vertices.Add(new Vector3(0,-hy,0));uv.Add(Vector2.one*.5f);
            for(int i=0;i<16;i++){vertices.Add(Point(outline[i],-hy,insets[0]));uv.Add(Vector2.zero);}
            for(int i=0;i<16;i++)triangles.AddRange(new[]{bottom,bottom+1+i,bottom+1+(i+1)%16});
            if(paintedStone){
                // A single broad mineral field spans roughly two metres. Deterministic
                // offset and quarter turns prevent the same rock grain repeating per flag.
                float frequency=.53f+Noise(variant,171)*.06f;
                var offset=new Vector2(Noise(variant,172),Noise(variant,173))*.83f;
                for(int i=0;i<uv.Count;i++){
                    var p=uv[i]*frequency;
                    if(variant%4==1)p=new Vector2(-p.y,p.x);else if(variant%4==2)p=-p;else if(variant%4==3)p=new Vector2(p.y,-p.x);
                    uv[i]=p+offset;
                }
            }
            var mesh=Finish("圆蚀破边厚砌石 "+variant,vertices,uv,triangles);
            var colors=new List<Color>();float pigment=.92f+Noise(variant,135)*.08f;
            Color tint=paintedStone?StonePigment(variant):Color.white;
            foreach(var v in vertices){float foot=Mathf.SmoothStep(.80f,1,Mathf.InverseLerp(-hy,hy,v.y));colors.Add(new Color(tint.r*pigment*foot,tint.g*pigment*foot,tint.b*pigment*foot,1));}
            mesh.SetColors(colors);return mesh;
        }
        private static Color StonePigment(int variant)
        {
            switch((variant%12+12)%12){
                case 0:case 5:case 9:return new Color(1.18f,1.14f,1.05f,1); // fresh warm ivory
                case 1:case 7:return new Color(1.08f,1.12f,1.16f,1); // pale sky-lit limestone
                case 2:case 10:return new Color(.91f,.98f,1.07f,1); // cool older grey
                case 3:case 8:return new Color(1.12f,1.02f,.87f,1); // mellow old rice pigment
                case 4:return new Color(1.02f,.95f,.84f,1);
                default:return new Color(1.12f,1.15f,1.10f,1);
            }
        }
        private static Vector3 Point(Vector2 p,float y,float inset)
        {return new Vector3(p.x-Mathf.Sign(p.x)*inset,y,p.y-Mathf.Sign(p.y)*inset);}

        internal static Mesh Plank(Vector3 size,int variant)
        {
            var mesh=Stone(size,variant,Mathf.Min(.035f,size.z*.12f),false);
            var vertices=mesh.vertices;var uv=new Vector2[vertices.Length];float offset=Noise(variant,94)*.73f;
            // Long cut ends and restrained bowing keep the top surface usable at y=0.
            for(int i=0;i<vertices.Length;i++){
                var v=vertices[i];float t=(v.x/size.x+.5f);
                bool vertical=size.y>size.x&&size.y>size.z;
                if(!vertical){v.z+=Mathf.Sin(t*Mathf.PI)*(.006f+Noise(variant,23)*.011f);if(v.y<size.y*.3f)v.y-=Mathf.Sin(t*Mathf.PI)*.013f;}vertices[i]=v;
                // Grain follows the timber's real long axis, including upright house posts
                // and crate corners. Their fibres must not appear cut horizontally.
                uv[i]=vertical?new Vector2(v.x+v.z+offset,v.y+size.y*.5f+offset):new Vector2(v.z+size.z*.5f+offset,v.x+size.x*.5f+offset);
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.name="原木榫板 "+variant;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        internal static Mesh LeafCluster(int variant,bool grass)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            int leaves=grass?14:19;
            for(int leaf=0;leaf<leaves;leaf++){
                float angle=Noise(variant,leaf*11)*Mathf.PI*2;
                float height=(grass?.20f:.27f)+Noise(variant,leaf*11+1)*(grass?.36f:.31f);
                float reach=(grass?.08f:.15f)+Noise(variant,leaf*11+2)*.27f;
                float width=(grass?.015f:.038f)+Noise(variant,leaf*11+3)*(grass?.020f:.075f);
                Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 side=Vector3.Cross(Vector3.up,direction)*width;
                Vector3 root=direction*(Noise(variant,leaf*11+4)*.13f);
                for(int segment=0;segment<3;segment++){
                    float a=segment/3f,b=(segment+1)/3f;
                    Vector3 p=root+direction*(reach*a*a)+Vector3.up*(height*Mathf.Sin(a*Mathf.PI*.6f));
                    Vector3 q=root+direction*(reach*b*b)+Vector3.up*(height*Mathf.Sin(b*Mathf.PI*.6f));
                    float wa=Mathf.Sin(a*Mathf.PI)*.90f+.12f,wb=b>=1?0:Mathf.Sin(b*Mathf.PI)*.90f+.12f;
                    Quad(vertices,uv,triangles,p-side*wa,q-side*wb,p+side*wa,q+side*wb,new Vector2(0,a),new Vector2(0,b),new Vector2(1,a),new Vector2(1,b),true);
                }
            }
            return Finish(grass?"实体弯叶草簇":"实体水岸叶丛",vertices,uv,triangles);
        }
        internal static Mesh Branch(Vector3[] points,float radius)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int sides=7;
            for(int ring=0;ring<points.Length;ring++){
                Vector3 tangent=ring+1<points.Length?(points[ring+1]-points[ring]).normalized:(points[ring]-points[ring-1]).normalized;
                Vector3 right=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized,up=Vector3.Cross(right,tangent);
                float r=radius*Mathf.Lerp(1,.24f,ring/(float)(points.Length-1));
                for(int side=0;side<=sides;side++){
                    float angle=side*Mathf.PI*2/sides;vertices.Add(points[ring]+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*r);uv.Add(new Vector2(side/(float)sides,ring*.35f));
                    if(ring>0&&side<sides){int a=(ring-1)*(sides+1)+side,b=a+1,c=a+sides+1,d=c+1;triangles.AddRange(new[]{a,b,c,b,d,c});}
                }
            }
            return Finish("水岸曲折枝干",vertices,uv,triangles);
        }
        internal static Mesh Ribbon(Vector3[] points,float width)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<points.Length-1;i++){
                Vector3 direction=(points[i+1]-points[i]).normalized;
                Vector3 side=Vector3.Cross(direction,Mathf.Abs(direction.y)>.85f?Vector3.forward:Vector3.up).normalized*width*.5f;
                float a=i/(float)(points.Length-1),b=(i+1)/(float)(points.Length-1);
                Quad(vertices,uv,triangles,points[i]-side,points[i+1]-side,points[i]+side,points[i+1]+side,new Vector2(0,a),new Vector2(0,b),new Vector2(1,a),new Vector2(1,b),true);
            }
            return Finish("水流与细波纹",vertices,uv,triangles);
        }
        internal static Mesh RoofTile(float width,float depth)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int columns=8,rows=4;
            for(int layer=0;layer<2;layer++)for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++){
                float u=x/(float)columns,v=y/(float)rows;
                vertices.Add(new Vector3((u-.5f)*width,Mathf.Sin(u*Mathf.PI)*width*.15f+(1-v)*(1-v)*.065f-layer*.042f,(v-.5f)*depth));uv.Add(new Vector2(u,v));
                if(x<columns&&y<rows){int a=layer*(columns+1)*(rows+1)+y*(columns+1)+x,b=a+1,c=a+columns+1,d=c+1;triangles.AddRange(layer==0?new[]{a,c,b,b,c,d}:new[]{a,b,c,b,d,c});}
            }
            for(int x=0;x<columns;x++){
                int a=x,b=x+1,c=x+(columns+1)*(rows+1),d=c+1;triangles.AddRange(new[]{a,b,c,b,d,c});
                a=rows*(columns+1)+x;b=a+1;c=a+(columns+1)*(rows+1);d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            return Finish("厚唇卷边曲面陶瓦",vertices,uv,triangles);
        }
        internal static Mesh Arch(float innerRadius,float thickness,float depth,int variant)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();const int pieces=11;
            for(int piece=0;piece<pieces;piece++){
                float a=piece*Mathf.PI/pieces+.006f,b=(piece+1)*Mathf.PI/pieces-.006f;
                float outer=innerRadius+thickness*(.92f+Noise(variant,piece)*.12f);
                var ia=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*innerRadius;var ib=new Vector2(Mathf.Cos(b),Mathf.Sin(b))*innerRadius;
                var oa=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*outer;var ob=new Vector2(Mathf.Cos(b),Mathf.Sin(b))*outer;
                Vector3[] p={new Vector3(ia.x,ia.y,-depth*.5f),new Vector3(ib.x,ib.y,-depth*.5f),new Vector3(oa.x,oa.y,-depth*.5f),new Vector3(ob.x,ob.y,-depth*.5f),new Vector3(ia.x,ia.y,depth*.5f),new Vector3(ib.x,ib.y,depth*.5f),new Vector3(oa.x,oa.y,depth*.5f),new Vector3(ob.x,ob.y,depth*.5f)};
                Quad(v,uv,t,p[0],p[1],p[2],p[3],Vector2.zero,Vector2.up,Vector2.right,Vector2.one);
                Quad(v,uv,t,p[4],p[6],p[5],p[7],Vector2.zero,Vector2.up,Vector2.right,Vector2.one);
                Quad(v,uv,t,p[0],p[4],p[1],p[5],Vector2.zero,Vector2.up,Vector2.right,Vector2.one);
                Quad(v,uv,t,p[2],p[3],p[6],p[7],Vector2.zero,Vector2.up,Vector2.right,Vector2.one);
                Quad(v,uv,t,p[0],p[2],p[4],p[6],Vector2.zero,Vector2.up,Vector2.right,Vector2.one);
                Quad(v,uv,t,p[1],p[5],p[3],p[7],Vector2.zero,Vector2.up,Vector2.right,Vector2.one);
            }
            return Finish("错缝石券真实拱洞",v,uv,t);
        }
        internal static Mesh Water(float width,float depth)
        {
            var mesh=Finish("渠道有界水面",new List<Vector3>{new Vector3(-width*.5f,0,-depth*.5f),new Vector3(-width*.5f,0,depth*.5f),new Vector3(width*.5f,0,depth*.5f),new Vector3(width*.5f,0,-depth*.5f)},new List<Vector2>{Vector2.zero,Vector2.up,Vector2.one,Vector2.right},new List<int>{0,1,2,0,2,3});
            // Per-face metric dimensions survive static combining, unlike property blocks.
            mesh.SetUVs(1,new List<Vector2>{new Vector2(width,depth),new Vector2(width,depth),new Vector2(width,depth),new Vector2(width,depth)});return mesh;
        }
        internal static Mesh Bush(int variant)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var colors=new List<Color>();
            for(int leaf=0;leaf<96;leaf++){
                float a=Noise(variant,leaf*7)*Mathf.PI*2,r=.10f+Noise(variant,leaf*7+1)*.57f;
                float height=.16f+Mathf.Sqrt(Mathf.Max(0,1-r*r/.49f))*.55f+Noise(variant,leaf*7+2)*.16f;
                Vector3 center=new Vector3(Mathf.Cos(a)*r,height,Mathf.Sin(a)*r);
                Vector3 direction=new Vector3(Mathf.Cos(a),.28f+Noise(variant,leaf*7+3)*.58f,Mathf.Sin(a)).normalized;
                Vector3 side=Vector3.Cross(Vector3.up,direction).normalized;
                float length=.15f+Noise(variant,leaf*7+4)*.15f,width=.053f+Noise(variant,leaf*7+5)*.051f;
                Vector3 basePoint=center-direction*length*.52f,ridge=center+Vector3.up*.024f,tip=center+direction*length*.55f;
                float tone=.82f+Noise(variant,leaf*7+6)*.25f;int before=v.Count;
                Quad(v,uv,t,basePoint,center-side*width,ridge,tip,new Vector2(.5f,0),new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(.5f,1),true);
                Quad(v,uv,t,basePoint,ridge,center+side*width,tip,new Vector2(.5f,0),new Vector2(.5f,.5f),new Vector2(1,.5f),new Vector2(.5f,1),true);
                for(int i=before;i<v.Count;i++){float shade=(i-before)%8>=4?.72f:1;colors.Add(new Color(tone*shade,tone*shade,tone*.95f*shade,1));}
            }
            var result=Finish("阔叶叠生自然灌木 "+variant,v,uv,t);result.SetColors(colors);return result;
        }
        internal static Mesh Flowers(int variant)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var colors=new List<Color>();
            for(int bloom=0;bloom<12;bloom++){
                float a=Noise(variant,bloom*3)*Mathf.PI*2,r=Noise(variant,bloom*3+1)*.40f,h=.24f+Noise(variant,bloom*3+2)*.32f;
                Vector3 root=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),head=root+new Vector3(.025f,h,.035f);
                Vector3 stem=new Vector3(.007f,0,0);
                Quad(v,uv,t,root-stem,head-stem,root+stem,head+stem,Vector2.zero,Vector2.up,Vector2.right,Vector2.one,true);
                while(colors.Count<v.Count)colors.Add(new Color(.24f,.64f,.53f,1));
                for(int petal=0;petal<5;petal++){
                    float angle=petal*Mathf.PI*2/5,scale=.045f+Noise(variant,bloom+petal)*.010f;
                    Vector3 d=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),side=Vector3.Cross(Vector3.up,d)*scale*.46f;
                    Quad(v,uv,t,head,head+d*scale*1.1f+Vector3.up*.008f-side,head+d*scale*1.1f+Vector3.up*.008f+side,head+d*scale*1.65f-Vector3.up*.009f,Vector2.zero,Vector2.up,Vector2.right,Vector2.one,true);
                    while(colors.Count<v.Count)colors.Add(new Color(1,.94f,.70f,1));
                }
                // A dark ochre centre remains part of the flower mesh, not twelve tiny props.
                Quad(v,uv,t,head+new Vector3(-.016f,.012f,-.016f),head+new Vector3(-.016f,.012f,.016f),head+new Vector3(.016f,.012f,-.016f),head+new Vector3(.016f,.012f,.016f),Vector2.zero,Vector2.up,Vector2.right,Vector2.one,true);
                while(colors.Count<v.Count)colors.Add(new Color(.60f,.47f,.35f,1));
            }
            var result=Finish("野花团簇与曲茎 "+variant,v,uv,t);result.SetColors(colors);return result;
        }
        internal static Mesh Gable(float width,float depth,float rise)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            float x=width*.5f,z=depth*.5f;
            foreach(int side in new[]{-1,1}){
                int first=v.Count;v.AddRange(new[]{new Vector3(side*x,0,-z),new Vector3(side*x,rise,0),new Vector3(side*x,0,z)});uv.AddRange(new[]{Vector2.zero,new Vector2(.5f,1),Vector2.right});t.AddRange(side<0?new[]{first,first+2,first+1}:new[]{first,first+1,first+2});
            }
            Quad(v,uv,t,new Vector3(-x,0,-z),new Vector3(-x,rise,0),new Vector3(x,0,-z),new Vector3(x,rise,0),Vector2.zero,Vector2.up,Vector2.right,Vector2.one);
            Quad(v,uv,t,new Vector3(x,0,z),new Vector3(x,rise,0),new Vector3(-x,0,z),new Vector3(-x,rise,0),Vector2.zero,Vector2.up,Vector2.right,Vector2.one);
            return Finish("完整木石屋山墙",v,uv,t);
        }
        private static void Quad(List<Vector3> v,List<Vector2> uv,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud,bool doubleSided=false)
        {int first=v.Count;v.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{ua,ub,uc,ud});t.AddRange(new[]{first,first+1,first+2,first+2,first+1,first+3});if(doubleSided){first=v.Count;v.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{ua,ub,uc,ud});t.AddRange(new[]{first+2,first+1,first,first+3,first+1,first+2});}}
        private static Mesh Finish(string name,List<Vector3> v,List<Vector2> uv,List<int> t)
        {var mesh=new Mesh{name=name,indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}
    }
}
