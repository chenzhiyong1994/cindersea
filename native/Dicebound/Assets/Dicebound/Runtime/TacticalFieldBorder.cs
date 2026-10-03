using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>A quiet, chamfered ink keyline for the battlefield HUD.</summary>
    public sealed class TacticalFieldBorder : MaskableGraphic
    {
        public float Width=1.2f,Corner=5;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;float inset=Width*.5f;
            float left=rect.xMin+inset,right=rect.xMax-inset,bottom=rect.yMin+inset,top=rect.yMax-inset;
            float corner=Mathf.Min(Corner,Mathf.Min(right-left,top-bottom)*.5f);
            var points=new[]{new Vector2(left+corner,top),new Vector2(right-corner,top),new Vector2(right,top-corner),new Vector2(right,bottom+corner),new Vector2(right-corner,bottom),new Vector2(left+corner,bottom),new Vector2(left,bottom+corner),new Vector2(left,top-corner)};
            for(int i=0;i<points.Length;i++)
            {
                var a=points[i];var b=points[(i+1)%points.Length];var delta=b-a;if(delta.sqrMagnitude<.0001f)continue;
                var normal=new Vector2(-delta.y,delta.x).normalized*Width*.5f;int start=mesh.currentVertCount;
                mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);
                mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
