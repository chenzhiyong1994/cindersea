using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Finite cut-in accents driven by the presentation timeline, never by battle state.</summary>
    public sealed class TacticalCutInImpact : MaskableGraphic
    {
        public string HeroId;
        public Color Accent=Color.white;
        public bool Foreground;
        public int VisibleVertexCount {get;private set;}
        private float phase;
        private bool reduced;
        private Vector2 meshScale,meshCentre;

        public void Render(float progress,bool reduce)
        {
            phase=Mathf.Clamp01(progress);reduced=reduce;SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();VisibleVertexCount=0;
            if(reduced||phase<=0||phase>=1)return;
            Rect bounds=rectTransform.rect;meshScale=new Vector2(bounds.width/1920f,bounds.height/1080f);meshCentre=bounds.center;
            float arrival=Rise(phase,0,.09f)*(1-Rise(phase,.19f,.43f));
            float strike=Rise(phase,.105f,.16f)*(1-Rise(phase,.19f,.39f));
            float afterglow=Rise(phase,.15f,.23f)*(1-Rise(phase,.35f,.78f));
            float depart=Rise(phase,.79f,.85f)*(1-Rise(phase,.91f,1));
            SpeedLines(mesh,arrival,depart);
            if(!Foreground)EntryBands(mesh,arrival,depart);
            HeroAccent(mesh,strike,afterglow);
            if(!Foreground)ImpactArc(mesh,strike);
            VisibleVertexCount=mesh.currentVertCount;
        }

        private void SpeedLines(VertexHelper mesh,float arrival,float depart)
        {
            float strength=arrival+depart*.72f;if(strength<.002f)return;
            float slide=phase<.79f?Mathf.Lerp(-460,460,Mathf.Clamp01(phase/.36f)):Mathf.Lerp(-220,920,(phase-.79f)/.21f);
            int count=Foreground?18:30;
            for(int i=0;i<count;i++)
            {
                float a=Hash(i+2),b=Hash(i+53),c=Hash(i+103);
                // Upper/lower rails and the open left side carry the acceleration.
                float y=i%3==0?390+b*128:i%3==1?-490+b*160:-230+b*530;
                float x=(i%3==2?-1030+a*940:-1150+a*1890)+slide;
                float length=(Foreground?90:180)+c*(Foreground?210:370);
                Vector2 tip=new Vector2(x,y),tail=tip-new Vector2(length,length*.13f);
                Color tint=Color.Lerp(Accent,Color.white,Foreground?.67f:.12f);
                float width=(Foreground?1.1f:3.2f)+b*(Foreground?1.7f:4);
                Ribbon(mesh,tail,tip,.2f,width,tint,strength*(Foreground?.67f:.22f),true);
                if(i%5==0)Ribbon(mesh,tail,tip,1,width*6,Accent,strength*.07f,true);
            }
        }

        private void EntryBands(VertexHelper mesh,float arrival,float depart)
        {
            float strength=arrival+depart*.6f;if(strength<.002f)return;
            float sweep=phase<.79f?Mathf.Lerp(-76,34,Mathf.Clamp01(phase/.3f)):Mathf.Lerp(34,170,(phase-.79f)/.21f);
            for(int i=0;i<2;i++)
            {
                float y=(i==0?-390:320)+sweep;
                Vector2 from=new Vector2(-930,y-90),to=new Vector2(920,y+135);
                Ribbon(mesh,from,to,12,64,Accent,strength*.15f,false);
                Ribbon(mesh,from,to,1,2.5f,Color.Lerp(Accent,Color.white,.65f),strength*.52f,true);
            }
        }

        private void HeroAccent(VertexHelper mesh,float strike,float afterglow)
        {
            float force=strike+(Foreground?.1f:.17f)*afterglow;
            if(force<.002f)return;
            float travel=Mathf.Clamp01((phase-.105f)/.32f);
            Vector2 origin=new Vector2(-275,-32);
            switch(HeroId)
            {
                case "sixuan":
                    if(!Foreground)
                    {
                        Crystal(mesh,origin,220+travel*210,.57f,.1f,Accent,force*.4f);
                        Crystal(mesh,origin,146+travel*160,.57f,.1f,Color.Lerp(Accent,Color.white,.7f),force*.58f);
                    }
                    for(int i=0;i<(Foreground?7:12);i++)
                    {
                        float angle=100+i*24+Hash(i+77)*13;
                        Vector2 position=origin+Direction(angle)*(130+travel*(240+Hash(i+9)*350));
                        Crystal(mesh,position,12+Hash(i+3)*29,.35f,angle,Accent,force*(Foreground?.7f:.45f));
                    }
                    break;
                case "lingfeng":
                    for(int i=0;i<(Foreground?2:4);i++)
                    {
                        float y=-390+i*160+travel*70;
                        Vector2 start=new Vector2(-930,y-90),end=new Vector2(110+i*95,y+225);
                        Color tint=i%2==0?new Color(1,.3f,.11f):Color.Lerp(Accent,Color.white,.38f);
                        Ribbon(mesh,start,end,Foreground?1:3,Foreground?3:23,tint,force*(Foreground?.72f:.3f),true);
                    }
                    Arc(mesh,origin,340+travel*270,1,.7f,118,285,Foreground?2:10,Accent,force*.46f,20);
                    break;
                case "cangling":
                    for(int i=0;i<(Foreground?2:3);i++)
                    {
                        float radius=240+i*112+travel*190;
                        Arc(mesh,origin+new Vector2(-55,-80),radius,1,.57f,100,303,Foreground?3:12,Accent,force*(.48f-i*.07f),28);
                        if(!Foreground)Arc(mesh,origin+new Vector2(-55,-66),radius,1,.57f,112,292,2,Color.Lerp(Accent,Color.white,.7f),force*.55f,28);
                    }
                    break;
                case "yanzhuying":
                    Arc(mesh,origin+new Vector2(125,30),285+travel*240,1,.97f,96,298,Foreground?4:23,Accent,force*.68f,32);
                    Arc(mesh,origin+new Vector2(151,39),258+travel*240,1,.97f,105,293,Foreground?1.3f:3,Color.Lerp(Accent,Color.white,.8f),force*.8f,30);
                    if(Foreground)Ribbon(mesh,new Vector2(-880,-330),new Vector2(60,130+travel*140),.5f,3,Accent,force*.73f,true);
                    break;
                case "shangshuo":
                    Shield(mesh,origin+new Vector2(-30,25),235+travel*230,force*(Foreground?.45f:.64f));
                    if(!Foreground)Shield(mesh,origin+new Vector2(-30,25),184+travel*183,force*.34f);
                    break;
                default:
                    Arc(mesh,origin,260+travel*300,1,.7f,98,293,3,Accent,force*.5f,28);
                    break;
            }
            // A small, one-shot spray connects the signature silhouette to the shared impact.
            for(int i=0;i<(Foreground?10:18);i++)
            {
                float angle=80+Hash(i+87)*255;
                Vector2 radial=Direction(angle);
                float distance=90+travel*(280+Hash(i+40)*320);
                Vector2 tip=origin+radial*distance;
                float length=18+Hash(i+2)*65;
                Ribbon(mesh,tip-radial*length,tip,.3f,Foreground?1.5f:3,Color.Lerp(Accent,Color.white,.62f),force*(Foreground?.66f:.4f),true);
            }
        }

        private void ImpactArc(VertexHelper mesh,float force)
        {
            if(force<.002f)return;
            float expansion=Rise(phase,.11f,.39f);
            Vector2 centre=new Vector2(-235,-40);
            Arc(mesh,centre,180+expansion*620,1,.66f,40,330,6,Accent,force*.23f,40);
            Arc(mesh,centre,146+expansion*600,1,.66f,80,300,1.5f,Color.Lerp(Accent,Color.white,.7f),force*.5f,32);
        }

        private void Shield(VertexHelper mesh,Vector2 centre,float radius,float opacity)
        {
            Color gold=Color.Lerp(Accent,new Color(1,.78f,.31f),.55f);
            Vector2 previous=ShieldPoint(5,centre,radius);
            for(int i=0;i<6;i++)
            {
                Vector2 next=ShieldPoint(i,centre,radius);
                if(Foreground)Ribbon(mesh,previous,next,12,12,gold,opacity*.22f,false);
                Ribbon(mesh,previous,next,Foreground?3.5f:10,Foreground?3.5f:10,gold,opacity,false);
                if(!Foreground)Ribbon(mesh,previous,next,1.8f,1.8f,Color.Lerp(gold,Color.white,.65f),opacity*.9f,false);
                previous=next;
            }
        }

        private static Vector2 ShieldPoint(int i,Vector2 centre,float radius)
        {
            float angle=90+i*60;
            Vector2 point=Direction(angle);
            if(i==3)point.y*=1.18f;
            return centre+new Vector2(point.x*.9f,point.y)*radius;
        }

        private void Crystal(VertexHelper mesh,Vector2 centre,float size,float narrow,float angle,Color tint,float opacity)
        {
            Vector2 up=Direction(angle+90)*size,side=Direction(angle)*size*narrow;
            Vector2 top=centre+up,left=centre-side,bottom=centre-up,right=centre+side;
            if(Foreground&&Protected(centre-Vector2.one*size,centre+Vector2.one*size,0))return;
            Face(mesh,top,left,centre,tint,opacity*.3f);
            Face(mesh,left,bottom,centre,tint,opacity*.15f);
            Face(mesh,bottom,right,centre,tint,opacity*.38f);
            Face(mesh,right,top,centre,Color.Lerp(tint,Color.white,.65f),opacity*.4f);
            Ribbon(mesh,top,left,1,1,tint,opacity,false);Ribbon(mesh,left,bottom,1,1,tint,opacity,false);
            Ribbon(mesh,bottom,right,1,1,tint,opacity,false);Ribbon(mesh,right,top,1,1,tint,opacity,false);
        }

        private void Arc(VertexHelper mesh,Vector2 centre,float radius,float wide,float tall,float from,float to,float width,Color tint,float opacity,int segments)
        {
            Vector2 prior=centre+Vector2.Scale(Direction(from),new Vector2(wide,tall))*radius;
            for(int i=1;i<=segments;i++)
            {
                float t=i/(float)segments;
                Vector2 point=centre+Vector2.Scale(Direction(Mathf.Lerp(from,to,t)),new Vector2(wide,tall))*radius;
                float taper=Mathf.Max(0,Mathf.Sin(t*Mathf.PI));
                Ribbon(mesh,prior,point,width*(.15f+taper),width*(.15f+taper),tint,opacity*Mathf.Sqrt(taper),false);
                prior=point;
            }
        }

        private void Ribbon(VertexHelper mesh,Vector2 start,Vector2 end,float startWidth,float endWidth,Color tint,float opacity,bool tailFade)
        {
            if(opacity<.003f)return;
            float padding=Mathf.Max(startWidth,endWidth);
            if(Foreground&&Protected(start,end,padding))return;
            if(!Clip(ref start,ref end,945-padding,525-padding))return;
            Vector2 direction=(end-start).normalized,normal=new Vector2(-direction.y,direction.x);
            int first=mesh.currentVertCount;
            Color edge=tint;edge.a=0;
            Color core=tint;core.a=Mathf.Clamp01(opacity)*color.a;
            Color tail=core;if(tailFade)tail.a*=.04f;
            Vertex(mesh,start-normal*startWidth,edge);Vertex(mesh,start-normal*startWidth*.25f,tail);
            Vertex(mesh,start+normal*startWidth*.25f,tail);Vertex(mesh,start+normal*startWidth,edge);
            Vertex(mesh,end-normal*endWidth,edge);Vertex(mesh,end-normal*endWidth*.25f,core);
            Vertex(mesh,end+normal*endWidth*.25f,core);Vertex(mesh,end+normal*endWidth,edge);
            for(int i=0;i<3;i++){mesh.AddTriangle(first+i,first+4+i,first+i+1);mesh.AddTriangle(first+i+1,first+4+i,first+5+i);}
        }

        private void Face(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Color tint,float opacity)
        {
            if(Mathf.Abs(a.x)>950||Mathf.Abs(a.y)>530||Mathf.Abs(b.x)>950||Mathf.Abs(b.y)>530)return;
            int first=mesh.currentVertCount;tint.a=Mathf.Clamp01(opacity)*color.a;
            Vertex(mesh,a,tint);Vertex(mesh,b,tint);Vertex(mesh,c,tint);mesh.AddTriangle(first,first+1,first+2);
        }

        private void Vertex(VertexHelper mesh,Vector2 point,Color tint)
        {mesh.AddVert(meshCentre+Vector2.Scale(point,meshScale),tint,Vector2.zero);}

        private static bool Protected(Vector2 a,Vector2 b,float padding)
        {
            float left=Mathf.Min(a.x,b.x)-padding,right=Mathf.Max(a.x,b.x)+padding;
            float bottom=Mathf.Min(a.y,b.y)-padding,top=Mathf.Max(a.y,b.y)+padding;
            return (right>175&&left<785&&top>15&&bottom<460)||(right>315&&left<960&&top> -530&&bottom< -145);
        }

        private static bool Clip(ref Vector2 a,ref Vector2 b,float width,float height)
        {
            Vector2 delta=b-a;float from=0,to=1;
            if(!ClipSide(-delta.x,a.x+width,ref from,ref to)||!ClipSide(delta.x,width-a.x,ref from,ref to)||
                !ClipSide(-delta.y,a.y+height,ref from,ref to)||!ClipSide(delta.y,height-a.y,ref from,ref to))return false;
            b=a+delta*to;a+=delta*from;return (b-a).sqrMagnitude>.01f;
        }

        private static bool ClipSide(float direction,float distance,ref float from,ref float to)
        {
            if(Mathf.Abs(direction)<.00001f)return distance>=0;
            float ratio=distance/direction;
            if(direction<0){if(ratio>to)return false;from=Mathf.Max(from,ratio);}
            else {if(ratio<from)return false;to=Mathf.Min(to,ratio);}
            return true;
        }

        private static float Rise(float value,float start,float end)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,value));
        private static Vector2 Direction(float degrees){float angle=degrees*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));}
        private static float Hash(int seed){float value=Mathf.Sin(seed*127.13f+5.7f)*4387.19f;return value-Mathf.Floor(value);}
    }
}
