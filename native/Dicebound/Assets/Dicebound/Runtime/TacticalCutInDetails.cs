using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Gold leaf coloration for the licensed brush lettering; text remains real localized UI text.</summary>
    public sealed class TacticalGoldLettering : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive())return;
            Rect bounds=((RectTransform)transform).rect;var vertex=new UIVertex();
            for(int i=0;i<mesh.currentVertCount;i++)
            {
                mesh.PopulateUIVertex(ref vertex,i);float t=Mathf.InverseLerp(bounds.yMin,bounds.yMax,vertex.position.y);
                Color shade=Color.Lerp(new Color(.71f,.42f,.15f),new Color(1,.92f,.66f),t);
                shade.a=vertex.color.a/255f;vertex.color=shade;mesh.SetUIVertex(vertex,i);
            }
        }
    }

    public sealed class TacticalCutInFlourish : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();float width=rectTransform.rect.width;
            Segment(mesh,new Vector2(-width*.5f,-4),new Vector2(width*.5f,4),1.2f,color);
            Segment(mesh,new Vector2(-width*.4f,-7),new Vector2(width*.44f,1),.65f,new Color(color.r,color.g,color.b,.55f));
            Vector2 centre=new Vector2(width*.28f,2);float r=9;
            var pts=new[]{centre+Vector2.up*r,centre+Vector2.right*r,centre-Vector2.up*r,centre-Vector2.right*r};
            for(int i=0;i<4;i++)Segment(mesh,pts[i],pts[(i+1)%4],1.2f,color);
            Segment(mesh,new Vector2(-width*.5f,-4),new Vector2(-width*.46f,7),1.4f,color);
        }
        internal static void Segment(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
        {
            Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;int i=mesh.currentVertCount;
            mesh.AddVert(a-n,tint,Vector2.zero);mesh.AddVert(a+n,tint,Vector2.up);
            mesh.AddVert(b+n,tint,Vector2.one);mesh.AddVert(b-n,tint,Vector2.right);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
    }

    /// <summary>Deterministic, finite foreground particles. Phase-driven so pause and QA keyframes are identical.</summary>
    public sealed class TacticalCutInMotes : MaskableGraphic
    {
        public Color Accent=Color.white;
        public bool Tide;
        private float phase;
        private bool reduced;
        public void Render(float progress,bool reduce){phase=progress;reduced=reduce;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(reduced)return;
            float envelope=Mathf.SmoothStep(0,1,phase/.2f)*(1-Mathf.SmoothStep(.76f,1,phase));
            for(int i=0;i<48;i++)
            {
                float seed=Frac(Mathf.Sin(i*127.13f+5.7f)*4387.19f);
                float seed2=Frac(Mathf.Sin(i*45.41f+21.7f)*5183.47f);
                float travel=phase*(220+seed*400);
                float x=-930+seed*1660+travel,y=-390+seed2*710+travel*.25f;
                // Keep the face and the name free of foreground noise.
                if(x>260&&x<660&&y>-30&&y<410||x>350&&y< -175)continue;
                float length=(i%8==0?26:4)+seed2*12,width=i%8==0?2.6f:1.25f;
                var a=new Vector2(x,y);var b=a+new Vector2(length,length*(Tide?.35f:.7f));
                var glow=Accent;glow.a=envelope*(.14f+seed*.14f);
                TacticalCutInFlourish.Segment(mesh,a,b,width*4,glow);
                var hot=Color.Lerp(Accent,Color.white,.72f);hot.a=envelope*(.34f+seed*.4f);
                TacticalCutInFlourish.Segment(mesh,a,b,width,hot);
            }
        }
        private static float Frac(float v)=>v-Mathf.Floor(v);
    }
}
