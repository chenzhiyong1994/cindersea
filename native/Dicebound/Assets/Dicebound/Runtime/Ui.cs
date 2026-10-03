using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Dicebound.Presentation
{
    public static class Ui
    {
        public static readonly Color Paper=new Color(.89f,.845f,.735f);
        public static readonly Color PaperLight=new Color(.965f,.935f,.84f);
        public static readonly Color Ink=new Color(.20f,.18f,.155f);
        public static readonly Color FadedInk=new Color(.43f,.385f,.315f);
        public static readonly Color Copper=new Color(.51f,.39f,.255f);
        public static readonly Color Vermilion=new Color(.56f,.20f,.15f);
        public static readonly Color Moss=new Color(.26f,.36f,.30f);
        private static Font font,displayFont;
        private static Sprite rounded;
        public static Font Font {get {if(!font)font=Resources.Load<Font>("Fonts/LXGWWenKai-Regular")??Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular")??DisplayFont;return font;}}
        public static Font DisplayFont {get {if(!displayFont)displayFont=Resources.Load<Font>("Fonts/NotoSerifCJKsc-Regular");return displayFont;}}
        public static Sprite Rounded
        {
            get {
                if(rounded)return rounded;
                const int size=64;const float radius=6;
                var texture=Visuals.Own(new Texture2D(size,size,TextureFormat.RGBA32,false){name="Soft interface corners",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp});
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                    var point=new Vector2(Mathf.Max(Mathf.Abs(x+.5f-size*.5f)-(size*.5f-radius),0),Mathf.Max(Mathf.Abs(y+.5f-size*.5f)-(size*.5f-radius),0));
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius-point.magnitude+.5f));
                }
                texture.SetPixels(pixels);texture.Apply();rounded=Visuals.Own(Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16)));return rounded;
            }
        }
        public static Canvas Canvas(string name,RenderMode mode=RenderMode.ScreenSpaceOverlay)
        {
            var o=new GameObject(name,typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=o.GetComponent<Canvas>();canvas.renderMode=mode;
            if(mode!=RenderMode.WorldSpace){var scaler=o.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=PaperViewport.DesignSize;scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;o.AddComponent<PaperViewportCanvas>().Build();}
            return canvas;
        }
        public static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Vector2? pivot=null)
        {
            if(parent&&parent.TryGetComponent<PaperViewportCanvas>(out var viewport))parent=viewport.Content;
            var o=new GameObject(name,typeof(RectTransform));var r=o.GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=anchor;r.pivot=pivot??new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;
        }
        /// <summary>Element centres descending from a top edge, separated by the requested gap.</summary>
        public static float[] Column(float top,float gap,params float[] heights)
        {
            if(heights==null)throw new System.ArgumentNullException(nameof(heights));
            if(float.IsNaN(top)||float.IsInfinity(top))throw new System.ArgumentOutOfRangeException(nameof(top));
            if(gap<0||float.IsNaN(gap)||float.IsInfinity(gap))throw new System.ArgumentOutOfRangeException(nameof(gap));
            var positions=new float[heights.Length];float edge=top;
            for(int i=0;i<heights.Length;i++)
            {
                if(heights[i]<0||float.IsNaN(heights[i])||float.IsInfinity(heights[i]))throw new System.ArgumentOutOfRangeException(nameof(heights));
                positions[i]=edge-heights[i]*.5f;edge-=heights[i]+gap;
            }
            return positions;
        }
        /// <summary>Equal-width element centres within a container centred at zero.</summary>
        public static float[] Row(float totalWidth,float gap,int count,out float itemWidth)
        {
            itemWidth=0;if(count<0)throw new System.ArgumentOutOfRangeException(nameof(count));
            if(count==0)return new float[0];
            if(totalWidth<=0||float.IsNaN(totalWidth)||float.IsInfinity(totalWidth))throw new System.ArgumentOutOfRangeException(nameof(totalWidth));
            if(gap<0||float.IsNaN(gap)||float.IsInfinity(gap))throw new System.ArgumentOutOfRangeException(nameof(gap));
            itemWidth=(totalWidth-gap*(count-1))/count;
            if(itemWidth<=0)throw new System.ArgumentOutOfRangeException(nameof(totalWidth),"Container must fit all items and gaps.");
            var positions=new float[count];float first=-totalWidth*.5f+itemWidth*.5f;
            for(int i=0;i<count;i++)positions[i]=first+i*(itemWidth+gap);
            return positions;
        }
        public static Image Panel(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color,bool raycast=false)
        {
            var r=Rect(name,parent,anchor,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;
        }
        public static Image Surface(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color,bool raycast=false)
        {
            var image=Panel(name,parent,anchor,pos,size,color,raycast);image.sprite=Rounded;image.type=Image.Type.Sliced;return image;
        }
        public static Image Frame(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color? fill=null)
        {
            var outer=Surface(name,parent,anchor,pos,size,fill??Paper);
            var grain=outer.gameObject.AddComponent<UiGradient>();grain.Bottom=new Color(.91f,.89f,.85f);grain.Top=Color.white;
            Ornament(outer.transform,size,Copper);
            return outer;
        }
        public static void Ornament(Transform parent,Vector2 size,Color color,bool corners=true)
        {
            var r=Rect("Engraved keyline",parent,Vector2.one*.5f,Vector2.zero,size-Vector2.one*12);
            var ornament=r.gameObject.AddComponent<PaperOrnament>();ornament.color=color;ornament.Corners=corners;ornament.raycastTarget=false;
        }
        public static Text Label(string name,Transform parent,string value,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            // CJK ascent/descent and fractional canvas scaling must fit one complete line.
            size.y=Mathf.Max(size.y,Mathf.Ceil(fontSize*1.5f));
            var r=Rect(name,parent,anchor,pos,size);var text=r.gameObject.AddComponent<Text>();text.font=Font;text.text=value;text.fontSize=fontSize;text.color=color;text.alignment=alignment;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.supportRichText=false;text.lineSpacing=1f;return text;
        }
        public static Text Narrative(string name,Transform parent,string value,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var text=Label(name,parent,value,anchor,pos,size,fontSize,color,alignment);
            text.gameObject.AddComponent<PaperNarrativeText>().SetText(value);return text;
        }
        public static Button Button(string name,Transform parent,string value,Vector2 anchor,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,bool primary=false)
        {
            var image=Surface(name,parent,anchor,pos,size,primary?Vermilion:PaperLight,true);
            var gradient=image.gameObject.AddComponent<UiGradient>();gradient.Bottom=new Color(.87f,.85f,.80f);Ornament(image.transform,size,primary?new Color(.85f,.68f,.49f):Copper,false);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.10f,1.07f,1.02f);colors.pressedColor=new Color(.83f,.76f,.65f);colors.disabledColor=new Color(.72f,.69f,.62f,.72f);colors.fadeDuration=.14f;button.colors=colors;
            Heading("Label",image.transform,value,new Vector2(.5f,.5f),Vector2.zero,size-new Vector2(20,4),primary?27:22,primary?PaperLight:Ink);
            image.gameObject.AddComponent<UiButtonMotion>();
            button.onClick.AddListener(action);return button;
        }
        public static Button QuietButton(string name,Transform parent,string value,Vector2 anchor,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var image=Surface(name,parent,anchor,pos,size,new Color(.70f,.62f,.48f,.12f),true);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.7f,1.8f,1.8f,1);colors.pressedColor=new Color(.7f,.8f,.8f,1);colors.disabledColor=new Color(.65f,.7f,.75f,.5f);colors.fadeDuration=.12f;button.colors=colors;
            Label("Label",image.transform,value,Vector2.one*.5f,Vector2.zero,size-new Vector2(16,4),20,Ink);
            image.gameObject.AddComponent<UiButtonMotion>();button.onClick.AddListener(action);return button;
        }
        public static Image Fade(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color,bool horizontal=false,bool reverse=false)
        {
            var panel=Panel(name,parent,anchor,pos,size,color);
            var gradient=panel.gameObject.AddComponent<UiGradient>();gradient.Horizontal=horizontal;
            gradient.Bottom=reverse?Color.white:new Color(1,1,1,0);gradient.Top=reverse?new Color(1,1,1,0):Color.white;
            return panel;
        }
        public static Text Heading(string name,Transform parent,string value,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize,Color color)
        {
            var label=Label(name,parent,value,anchor,pos,size,fontSize,color);label.font=DisplayFont;return label;
        }
        public static void Icon(string kind,Transform parent,Vector2 position,float size,Color color)
        {var root=Rect(kind+" icon",parent,Vector2.one*.5f,position,Vector2.one*size);var icon=root.gameObject.AddComponent<UiIcon>();icon.Kind=kind;icon.color=color;icon.raycastTarget=false;}
        public static void Fill(RectTransform rect) {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        public static RawImage Picture(string name,Transform parent,Texture texture,Vector2 anchor,Vector2 pos,Vector2 size,Color color)
        {
            var r=Rect(name,parent,anchor,pos,size);var image=r.gameObject.AddComponent<RawImage>();image.texture=texture;image.color=color;image.raycastTarget=false;return image;
        }
        public static Texture2D PaperArt(string category,string key,Texture2D fallback=null)
        {return Resources.Load<Texture2D>("Art/Paper/"+category+"/"+key)??fallback;}
        public static void Crop(RawImage image)
        {
            if(!image.texture)return;float target=image.rectTransform.sizeDelta.x/image.rectTransform.sizeDelta.y;float source=(float)image.texture.width/image.texture.height;
            image.uvRect=source>target?new Rect((1-target/source)*.5f,0,target/source,1):new Rect(0,(1-source/target)*.5f,1,source/target);
        }
        public static void Clear(Transform parent)
        {
            // Destroy is deferred until the end of the frame; retire old controls immediately.
            for(int i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i).gameObject;child.SetActive(false);Object.Destroy(child);}
        }
        public static void EventSystem()
        {
            if(!UnityEngine.EventSystems.EventSystem.current)new GameObject("Input",typeof(EventSystem),typeof(StandaloneInputModule));
        }
    }
    public sealed class UiGradient:BaseMeshEffect
    {
        public Color Bottom=Color.white,Top=Color.white;public bool Horizontal;
        public override void ModifyMesh(VertexHelper helper)
        {
            if(!IsActive())return;var rect=((RectTransform)transform).rect;var v=new UIVertex();
            for(int i=0;i<helper.currentVertCount;i++){helper.PopulateUIVertex(ref v,i);float t=Horizontal?Mathf.InverseLerp(rect.xMin,rect.xMax,v.position.x):Mathf.InverseLerp(rect.yMin,rect.yMax,v.position.y);v.color=(Color)v.color*Color.Lerp(Bottom,Top,t);helper.SetUIVertex(v,i);}
        }
    }
    public sealed class UiBorder:BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper helper)
        {
            if(!IsActive())return;helper.Clear();var rect=((RectTransform)transform).rect;Color c=graphic.color;
            var outer=new[]{new Vector2(rect.xMin,rect.yMin+7),new Vector2(rect.xMin+7,rect.yMin),new Vector2(rect.xMax-7,rect.yMin),new Vector2(rect.xMax,rect.yMin+7),new Vector2(rect.xMax,rect.yMax-7),new Vector2(rect.xMax-7,rect.yMax),new Vector2(rect.xMin+7,rect.yMax),new Vector2(rect.xMin,rect.yMax-7)};
            for(int i=0;i<8;i++){Vector2 a=outer[i],b=outer[(i+1)%8],n=new Vector2(-(b-a).y,(b-a).x).normalized*.6f;int o=helper.currentVertCount;helper.AddVert(a+n,c,Vector2.one*.5f);helper.AddVert(b+n,c,Vector2.one*.5f);helper.AddVert(b-n,c,Vector2.one*.5f);helper.AddVert(a-n,c,Vector2.one*.5f);helper.AddTriangle(o,o+1,o+2);helper.AddTriangle(o,o+2,o+3);}
        }
    }
    public sealed class UiButtonMotion:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
    {
        public float HoverScale=1.012f,PressedScale=.985f;
        private bool hover,held;private Button button;private CanvasGroup group;
        private void Awake(){button=GetComponent<Button>();group=gameObject.AddComponent<CanvasGroup>();}
        public void OnPointerEnter(PointerEventData e){hover=true;}public void OnPointerExit(PointerEventData e){hover=held=false;}
        public void OnPointerDown(PointerEventData e){held=true;}public void OnPointerUp(PointerEventData e){held=false;}
        private void Update(){float scale=button.interactable?(held?PressedScale:hover?HoverScale:1):1;if(TacticalDirector.Instance&&TacticalDirector.Instance.ReducedMotion)scale=1;var target=Vector3.one*scale;if((transform.localScale-target).sqrMagnitude>.000001f)transform.localScale=Vector3.Lerp(transform.localScale,target,1-Mathf.Exp(-Time.unscaledDeltaTime*20));else if(transform.localScale!=target)transform.localScale=target;if(group.alpha!=1)group.alpha=1;}
    }
    public sealed class UiIcon:MaskableGraphic
    {
        public string Kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float unit=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
            void Line(Vector2 a,Vector2 b,float width=.085f){a*=unit;b*=unit;Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*unit*width*.5f;int o=vh.currentVertCount;vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddVert(a-n,color,Vector2.zero);vh.AddTriangle(o,o+1,o+2);vh.AddTriangle(o,o+2,o+3);}
            void Path(params Vector2[] points){for(int i=1;i<points.Length;i++)Line(points[i-1],points[i]);}
            void Circle(float radius,int sides=40){for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;Line(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,.06f);}}
            void Dot(Vector2 point){int center=vh.currentVertCount;vh.AddVert(point*unit,color,Vector2.zero);for(int i=0;i<12;i++){float a=i*Mathf.PI/6;vh.AddVert((point+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.105f)*unit,color,Vector2.zero);}for(int i=0;i<12;i++)vh.AddTriangle(center,center+1+i,center+1+(i+1)%12);}
            if(Kind=="move"){Path(new Vector2(-.72f,-.43f),new Vector2(.55f,.66f));Path(new Vector2(-.05f,.64f),new Vector2(.60f,.70f),new Vector2(.63f,.05f));Line(new Vector2(-.69f,-.68f),new Vector2(-.28f,-.33f));}
            else if(Kind=="heal"){Circle(.74f);Line(new Vector2(-.42f,0),new Vector2(.42f,0),.17f);Line(new Vector2(0,-.42f),new Vector2(0,.42f),.17f);}
            else if(Kind=="water"){for(int row=-1;row<=1;row++)for(int i=0;i<18;i++){float x=-.78f+i*.086f,next=x+.086f;Line(new Vector2(x,row*.38f+Mathf.Sin(x*5)*.11f),new Vector2(next,row*.38f+Mathf.Sin(next*5)*.11f),.075f);}}
            else if(Kind=="throw"){Path(new Vector2(-.72f,-.7f),new Vector2(-.24f,-.7f),new Vector2(-.24f,-.22f),new Vector2(-.72f,-.22f),new Vector2(-.72f,-.7f));for(int i=0;i<16;i++){float t=i/16f,n=(i+1)/16f;Line(new Vector2(-.5f+1.15f*t,-.02f+Mathf.Sin(t*Mathf.PI)*.70f),new Vector2(-.5f+1.15f*n,-.02f+Mathf.Sin(n*Mathf.PI)*.70f));}Path(new Vector2(.25f,.08f),new Vector2(.68f,-.06f),new Vector2(.76f,.38f));}
            else if(Kind=="shield")Path(new Vector2(-.65f,.65f),new Vector2(0,.82f),new Vector2(.65f,.65f),new Vector2(.52f,-.20f),new Vector2(0,-.8f),new Vector2(-.52f,-.2f),new Vector2(-.65f,.65f));
            else if(Kind=="sword"){Path(new Vector2(-.58f,-.68f),new Vector2(.53f,.57f),new Vector2(.65f,.8f),new Vector2(.35f,.70f),new Vector2(-.67f,-.48f));Line(new Vector2(-.65f,-.13f),new Vector2(-.11f,-.65f));}
            else if(Kind=="moon"){for(int i=0;i<32;i++){float a=(i/32f*1.65f+.18f)*Mathf.PI,b=((i+1)/32f*1.65f+.18f)*Mathf.PI;Line(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.72f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.72f,.08f);}Path(new Vector2(.14f,.56f),new Vector2(-.13f,.20f),new Vector2(-.10f,-.22f),new Vector2(.31f,-.55f));}
            else if(Kind=="undo"||Kind=="reroll"){for(int i=0;i<30;i++){float a=(i/30f*1.60f-.55f)*Mathf.PI,b=((i+1)/30f*1.60f-.55f)*Mathf.PI;Line(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.65f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.65f);}Path(new Vector2(-.82f,.22f),new Vector2(-.64f,-.25f),new Vector2(-.18f,-.15f));}
            else if(Kind=="settings"){Circle(.38f);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.52f,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.77f,.18f);}}
            else if(Kind=="die"){Path(new Vector2(-.62f,-.62f),new Vector2(.62f,-.62f),new Vector2(.62f,.62f),new Vector2(-.62f,.62f),new Vector2(-.62f,-.62f));Dot(new Vector2(-.28f,.28f));Dot(Vector2.zero);Dot(new Vector2(.28f,-.28f));}
            else {Path(new Vector2(0,.82f),new Vector2(.20f,.2f),new Vector2(.82f,0),new Vector2(.2f,-.2f),new Vector2(0,-.82f),new Vector2(-.2f,-.2f),new Vector2(-.82f,0),new Vector2(-.2f,.2f),new Vector2(0,.82f));}
        }
    }
}
