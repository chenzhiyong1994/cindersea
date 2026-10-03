using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Soft outer shadow rendered before the surface in the same Graphic; no extra hit target.</summary>
    public sealed class TacticalSurfaceShadow:BaseMeshEffect
    {
        public float Radius=24,Blur=12,Drop=5,Strength=.25f;
        private const int CornerSteps=10,Points=(CornerSteps+1)*4,Rings=9;
        private readonly List<UIVertex> body=new List<UIVertex>();
        public override void ModifyMesh(VertexHelper helper)
        {
            // A masking surface must describe only its body, never the expanded shadow region.
            var mask=GetComponent<Mask>();
            if(!IsActive()||Strength<=0||mask&&mask.enabled)return;
            body.Clear();helper.GetUIVertexStream(body);helper.Clear();
            var rect=((RectTransform)transform).rect;float radius=Mathf.Min(Radius,Mathf.Min(rect.width,rect.height)*.5f);
            for(int ring=0;ring<=Rings;ring++)
            {
                float t=ring/(float)Rings,expand=Blur*(1-t),alpha=Strength*t*t*graphic.color.a;
                float halfX=rect.width*.5f+expand,halfY=rect.height*.5f+expand,r=radius+expand;
                int first=helper.currentVertCount;
                for(int corner=0;corner<4;corner++)for(int step=0;step<=CornerSteps;step++)
                {
                    float angle=(corner+step/(float)CornerSteps)*Mathf.PI*.5f;
                    float cx=(corner==0||corner==3)?halfX-r:-halfX+r;
                    float cy=corner<2?halfY-r:-halfY+r;
                    var point=new Vector2(cx+Mathf.Cos(angle)*r,cy+Mathf.Sin(angle)*r-Drop)+rect.center;
                    AddShadowVertex(helper,point,alpha);
                }
                if(ring>0)for(int i=0;i<Points;i++)
                {
                    int j=(i+1)%Points,outer=first-Points;
                    helper.AddTriangle(outer+i,outer+j,first+i);helper.AddTriangle(outer+j,first+j,first+i);
                }
            }
            int centre=helper.currentVertCount;AddShadowVertex(helper,rect.center+new Vector2(0,-Drop),Strength*graphic.color.a);
            int inner=centre-Points;for(int i=0;i<Points;i++)helper.AddTriangle(centre,inner+i,inner+(i+1)%Points);
            // Append explicitly so the surface always draws after the shadow, independent of native stream behaviour.
            int offset=helper.currentVertCount;for(int i=0;i<body.Count;i++)helper.AddVert(body[i]);
            for(int i=0;i<body.Count;i+=3)helper.AddTriangle(offset+i,offset+i+1,offset+i+2);
        }
        private static void AddShadowVertex(VertexHelper helper,Vector2 point,float alpha)
        {
            var vertex=UIVertex.simpleVert;vertex.position=point;vertex.color=new Color(0,0,0,alpha);vertex.uv0=Vector2.one*.5f;vertex.uv1=new Vector4(0,0,0,-1);helper.AddVert(vertex);
        }
    }
}
