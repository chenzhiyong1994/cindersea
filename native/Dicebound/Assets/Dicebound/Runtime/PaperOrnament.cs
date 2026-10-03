using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    // Original line engraving: paired rules, broken corners and workshop seal marks.
    public sealed class PaperOrnament:MaskableGraphic
    {
        public bool Corners=true;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float x=r.width*.5f,y=r.height*.5f;
            void Line(Vector2 a,Vector2 b,float width=.8f){Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int i=vh.currentVertCount;vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddVert(a-n,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
            for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2){
                float inset=Corners?15:4;Line(new Vector2(sx*x,sy*(y-inset)),new Vector2(sx*x,0));Line(new Vector2(sx*(x-inset),sy*y),new Vector2(0,sy*y));
                if(Corners){Line(new Vector2(sx*(x-4),sy*(y-23)),new Vector2(sx*(x-4),sy*(y-4)));Line(new Vector2(sx*(x-4),sy*(y-4)),new Vector2(sx*(x-23),sy*(y-4)));Line(new Vector2(sx*(x-11),sy*y),new Vector2(sx*x,sy*(y-11)));Line(new Vector2(sx*(x-9),sy*(y-9)),new Vector2(sx*(x-17),sy*(y-9)));Line(new Vector2(sx*(x-9),sy*(y-9)),new Vector2(sx*(x-9),sy*(y-17)));}
            }
            if(Corners&&r.width>250)for(int s=-1;s<=1;s+=2){float yy=s*y;Line(new Vector2(-6,yy),new Vector2(0,yy-3*s));Line(new Vector2(0,yy-3*s),new Vector2(6,yy));}
        }
    }
}
