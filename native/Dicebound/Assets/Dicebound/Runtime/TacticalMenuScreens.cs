using System;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    public sealed partial class TacticalDirector
    {
        private bool companionSkills;
        private int companionSkillPage;
        private string companionSkillId;
        private static readonly string[] JadeRoster={"sixuan","lingfeng","cangling","yanzhuying","shangshuo"};

        public void Title()
        {
            view="title";pending=null;hovered=null;skill=null;
            ResetPage("street");Audio.SetTheme("title");
            var titleBackdrop=TacticalMenuArt.PlateBackdrop("Jade title companions",page,"title-companions-back-v2",.06f);
            menuMotion.Bind(titleBackdrop,"title-loop.mp4",glassBackdrop:true);
            var menu=Ui.Rect("Title pavilion",page,Center,new Vector2(-652,0),new Vector2(548,950));
            TacticalMenuArt.CraftImage("Game title calligraphy",menu,"title-logo",new Vector2(0,251),new Vector2(480,240));
            TacticalMenuArt.Rule("Subtitle left ornament",menu,new Vector2(-181,138),102);
            TacticalMenuArt.Rule("Subtitle right ornament",menu,new Vector2(181,138),102);
            var sub=TacticalMenuArt.Heading("Subtitle",menu,"结 伴 登 阙",new Vector2(0,138),new Vector2(252,66),36,true);sub.alignment=TextAnchor.MiddleCenter;
            TacticalMenuArt.MenuAction("Continue tactics",menu,"继续旅程",new Vector2(18,32),new Vector2(448,88),()=>{if(State!=null)Render();else SelectParty();},true);
            TacticalMenuArt.MenuAction("New tactics",menu,"开始新旅程",new Vector2(18,-58),new Vector2(448,88),SelectParty);
            TacticalMenuArt.MenuAction("Title settings",menu,"设置",new Vector2(18,-146),new Vector2(448,88),Settings);
            TacticalMenuArt.MenuAction("Exit game",menu,"退出",new Vector2(18,-234),new Vector2(448,88),Application.Quit);
            TacticalMenuArt.CraftImage("Hanging title jade",menu,"title-ornament",new Vector2(-183,-87),new Vector2(84,340));
            if(SaveError!=null)TacticalMenuArt.Label("Title save error",menu,SaveError,new Vector2(0,-362),new Vector2(480,88),23,TacticalTheme.Danger,TextAnchor.MiddleCenter);
            TacticalMenuArt.TextButton("Rescue chronicle",page,"同行纪事",new Vector2(787,-481),new Vector2(228,72),Chronicle,fontSize:29);
            TacticalMenuArt.Label("Version",page,"版本 "+Application.version,new Vector2(784,-523),new Vector2(220,30),22,TacticalMenuArt.Text,TextAnchor.MiddleRight);
        }

        public void SelectParty()
        {
            view="party";pending=null;hovered=null;skill=null;ResetPage("street");
            TacticalMenuArt.PlateBackdrop("Jade training pavilion",page,"party-plate",.06f);
            var heading=TacticalMenuArt.CraftImage("Roster heading",page,"secondary-button",new Vector2(-706,485),new Vector2(470,110));
            TacticalMenuArt.Decoration("Roster crest",heading.transform,"seal",new Vector2(-201,0),new Vector2(94,94));
            var title=TacticalMenuArt.Heading("Page heading",heading.transform,"选择同行者",new Vector2(20,0),new Vector2(368,72),46);title.alignment=TextAnchor.MiddleCenter;
            TacticalMenuArt.Label("Page subtitle",page,"三人同行 · 共赴天阙",new Vector2(-691,417),new Vector2(404,44),25,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
            var rosterPanel=TacticalMenuArt.QuietPanel("Companion roster",page,new Vector2(-614,-17),new Vector2(440,778));
            TacticalMenuArt.CraftImage("Companion jade tassel",page,"title-ornament",new Vector2(-906,-102),new Vector2(80,428));
            TacticalMenuArt.Heading("Companion pavilion verse",page,"玉\n璨\n山\n河\n\n人\n定\n三\n息",new Vector2(-902,151),new Vector2(58,485),30,true).alignment=TextAnchor.UpperCenter;
            for(int i=0;i<JadeRoster.Length;i++)
            {
                var hero=TacticalContent.GetHero(JadeRoster[i]);bool chosen=party.Contains(hero.id),inspected=inspectedHero==hero.id;
                float x=i==JadeRoster.Length-1&&JadeRoster.Length%2==1?0:i%2==0?-103:103;
                var position=new Vector2(x,250-i/2*244);
                var hit=Ui.Panel("Roster "+hero.id,rosterPanel.transform,Center,position,new Vector2(196,242),Color.clear,true);
                var inspect=hit.gameObject.AddComponent<Button>();inspect.targetGraphic=hit;inspect.transition=Selectable.Transition.None;
                inspect.onClick.AddListener(()=>InspectCompanion(hero.id));
                TacticalMenuArt.HeroPortrait("Portrait "+hero.id,hit.transform,hero.id,new Vector2(0,19),new Vector2(166,177));
                var rim=TacticalMenuArt.CraftImage("Portrait gold surround",hit.transform,"portrait-frame",Vector2.zero,new Vector2(206,256));
                if(inspected){var glow=rim.gameObject.AddComponent<Shadow>();glow.effectColor=new Color(1,.73f,.28f,.9f);glow.effectDistance=new Vector2(2,-2);rim.color=new Color(1.2f,1.12f,.93f);}
                var name=TacticalMenuArt.Heading("Hero name "+hero.id,hit.transform,hero.name,new Vector2(0,-88),new Vector2(164,42),31,inspected);name.alignment=TextAnchor.MiddleCenter;
                if(chosen)
                {
                    var badge=TacticalMenuArt.Decoration("Chosen badge "+hero.id,hit.transform,"seal",new Vector2(70,-51),new Vector2(49,49));
                    TacticalMenuArt.Label("Chosen label "+hero.id,badge.transform,"✓",new Vector2(0,1),new Vector2(35,35),29,TacticalMenuArt.Text,TextAnchor.MiddleCenter);
                }
            }
            var heroDetail=TacticalContent.GetHero(inspectedHero);
            if(heroDetail==null||!JadeRoster.Contains(heroDetail.id))heroDetail=TacticalContent.GetHero("sixuan");inspectedHero=heroDetail.id;
            // Extend beneath the detail panel so transparent ink edges cannot create a hard gap.
            var portraitPosition=new Vector2(-14,40);var portraitSize=new Vector2(760,920);
            TacticalArt.Plate("Companion portrait backing",page,portraitPosition,portraitSize,new Color(.025f,.065f,.064f,.72f),3);
            DrawMenuCharacter(page,heroDetail.id,portraitPosition,portraitSize);
            TacticalMenuArt.TextButton("Companion motion preview",page,"全画幅欣赏",new Vector2(-20,-373),new Vector2(228,50),()=>PreviewCompanionMotion(heroDetail.id),fontSize:24);
            var data=TacticalMenuArt.QuietPanel("Companion data",page,new Vector2(639,40),new Vector2(604,920));
            if(companionSkills)DrawCompanionSkills(data.transform,heroDetail);else DrawCompanionAttributes(data.transform,heroDetail);
            TacticalMenuArt.TextButton("Back to title",page,"‹  返回",new Vector2(-814,-477),new Vector2(224,82),Title,fontSize:33);
            bool inParty=party.Contains(heroDetail.id);
            TacticalMenuArt.TextButton("Choose "+heroDetail.id,page,inParty?"移出小队":"加入小队",new Vector2(165,-471),new Vector2(232,72),()=>
            {
                if(party.Contains(heroDetail.id))party.Remove(heroDetail.id);else if(party.Count<3)party.Add(heroDetail.id);else Notify("小队已有三人，请先移出一位。");
                SelectParty();
            },fontSize:27);
            // 新旅程使用 2–5 四档难度；旧旅程保留原难度。
            var trialRow=TacticalMenuArt.QuietPanel("Trial stepper",page,new Vector2(-268,-468),new Vector2(360,118)).transform;
            TacticalMenuArt.Label("Trial heading",trialRow,"登阙试炼",new Vector2(-96,26),new Vector2(150,40),26,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
            TacticalMenuArt.TextButton("Trial details",trialRow,"难度说明",new Vector2(91,29),new Vector2(148,34),TrialDetails,fontSize:20);
            TacticalMenuArt.TextButton("Trial down",trialRow,"‹",new Vector2(-150,-16),new Vector2(56,56),()=>{selectedTrial=Math.Max(2,selectedTrial-1);SelectParty();});
            TacticalMenuArt.Heading("Trial value",trialRow,TrialName(selectedTrial),new Vector2(-8,-16),new Vector2(142,56),32);
            TacticalMenuArt.TextButton("Trial up",trialRow,"›",new Vector2(120,-16),new Vector2(56,56),()=>{selectedTrial=Math.Min(5,selectedTrial+1);SelectParty();});
            var start=TacticalMenuArt.TextButton("Start tactics",page,"启 程   ›",new Vector2(652,-468),new Vector2(444,118),()=>StartRun(unchecked((uint)DateTime.UtcNow.Ticks),party.ToArray(),selectedTrial),true,fontSize:56);
            start.interactable=party.Count==3;
        }
        private void MenuBackdrop(){TacticalMenuArt.PlateBackdrop("Yaojing menu vista",page,"title-environment");}

        private void InspectCompanion(string id)
        {
            if(inspectedHero!=id){companionSkillId=null;companionSkillPage=0;}
            inspectedHero=id;SelectParty();
        }

        private void DrawMenuCharacter(Transform parent,string id,Vector2 position,Vector2 size)
        {
            var action=TacticalMenuMotion.CompanionPoster(id);
            if(!action){TacticalUi.Portrait("Full companion illustration",parent,Picture("Heroes",id),id,position,size,border:0,radius:3);return;}
            float aspect=(float)action.width/action.height;
            var image=TacticalArt.CropPicture("Full companion illustration",parent,action,position,size,3);
            // Calibrated against each source's face size. Keep the top edge to preserve hair;
            // the same UV applies to both the poster and the movie, without stretching.
            float height=1,eyesX=.75f;
            switch(id){
                case "sixuan":height=.86f;eyesX=.680f;break;
                case "cangling":eyesX=.727f;break;
                case "lingfeng":height=.95f;eyesX=.709f;break;
                case "shangshuo":eyesX=.755f;break;
            }
            float width=Mathf.Min(1,size.x/size.y/aspect*height);
            image.uvRect=new Rect(Mathf.Clamp(eyesX-width*.64f,0,1-width),1-height,width,height);
            string movie=TacticalMenuMotion.CompanionMovieFile(id);
            if(movie!=null)
            {
                if(!TacticalMenuMotion.CompanionUsesPackedMovie(id))
                {
                    menuMotion.Bind(image,movie);
                    return;
                }
                if(!menuPortraitMotionMaterial)
                {
                    var shader=Resources.Load<Shader>("Shaders/Tactics/MenuPackedVideo");
                    if(shader&&shader.isSupported)menuPortraitMotionMaterial=new Material(shader){name="Transparent companion motion"};
                }
                if(menuPortraitMotionMaterial)menuMotion.Bind(image,movie,material:menuPortraitMotionMaterial,packedColorAspect:aspect);
            }
        }

        private static string CompanionVerse(string id)
        {
            return id=="sixuan"?"以玉为衡，\n持律而行。":id=="lingfeng"?"刀向前路，\n赤心不退。":id=="cangling"?"听潮而行，\n万川同归。":id=="yanzhuying"?"月隐灯深，\n暗处留锋。":"长槊在前，\n身后无恙。";
        }
        private void DrawCompanionAttributes(Transform panel,TacticalHero hero)
        {
            TacticalMenuArt.Heading("Companion detail name",panel,hero.name,new Vector2(-133,368),new Vector2(244,98),77);
            TacticalMenuArt.Label("Companion role",panel,CompanionVerse(hero.id),new Vector2(136,371),new Vector2(205,94),25,TacticalMenuArt.Text,TextAnchor.MiddleLeft);
            TacticalMenuArt.Heading("Companion title",panel,hero.title,new Vector2(-67,301),new Vector2(374,62),34,true);
            TacticalMenuArt.Decoration("Companion hanging insignia",panel,"medallion",new Vector2(242,384),new Vector2(61,89));
            TacticalMenuArt.Rule("Companion heading divider",panel,new Vector2(0,268),508);
            string[] names={"♥  HP","✦  AP","SP"};int[] values={hero.maxHp,hero.maxAp,hero.maxSp};
            for(int i=0;i<3;i++)
            {
                var stat=TacticalMenuArt.QuietPanel("Companion stat "+i,panel,new Vector2((i-1)*175,197),new Vector2(164,108));
                TacticalMenuArt.Label("Companion stat label "+i,stat.transform,names[i],new Vector2(0,26),new Vector2(134,36),27,TacticalMenuArt.Text,TextAnchor.MiddleCenter);
                var number=TacticalMenuArt.Heading("Companion stat value "+i,stat.transform,values[i].ToString(),new Vector2(0,-19),new Vector2(130,66),46);number.font=Ui.DisplayFont;number.alignment=TextAnchor.MiddleCenter;
            }
            TacticalMenuArt.Heading("Initial skills heading",panel,"技能",new Vector2(-167,118),new Vector2(175,49),31);
            TacticalMenuArt.TextButton("Companion skills",panel,"详解 ›",new Vector2(183,118),new Vector2(152,50),()=>{companionSkills=true;SelectParty();},fontSize:25);
            var features=hero.skills.Take(3).Select(TacticalContent.GetSkill).ToArray();
            for(int i=0;i<features.Length;i++)
            {
                var definition=features[i];float y=43-i*109;
                var entry=TacticalMenuArt.QuietPanel("Initial skill "+definition.id,panel,new Vector2(0,y),new Vector2(508,106),true);
                var inspect=entry.gameObject.AddComponent<Button>();inspect.targetGraphic=entry;inspect.onClick.AddListener(()=>ShowCompanionSkill(hero.id,definition.id));
                TacticalArt.SkillSymbol("Menu skill icon "+definition.id,entry.transform,definition.id,new Vector2(-204,0),77);
                TacticalMenuArt.Heading("Menu skill label "+definition.id,entry.transform,definition.name,new Vector2(52,30),new Vector2(380,44),29);
                // 描述加宽到 380、字号 22：最长技能文本控制在两行内，避免在 106 高的条目里裁剪。
                TacticalMenuArt.Label("Menu skill description "+definition.id,entry.transform,definition.id=="step"?"3 格内换位，须借阴影；落影恢复 1 SP。":definition.text,new Vector2(52,-23),new Vector2(380,66),22,TacticalMenuArt.Text);
            }
            TacticalMenuArt.Heading("Party heading",panel,"同行者",new Vector2(-153,-245),new Vector2(190,44),29);
            TacticalMenuArt.Label("Party count",panel,party.Count+" / 3",new Vector2(186,-245),new Vector2(110,42),30,TacticalMenuArt.Text,TextAnchor.MiddleRight);
            for(int i=0;i<3;i++)
            {
                var frame=TacticalMenuArt.QuietPanel("Party slot "+i,panel,new Vector2((i-1)*148,-317),new Vector2(113,95));
                if(i<party.Count)TacticalMenuArt.HeroPortrait("Chosen portrait "+i,frame.transform,party[i],Vector2.zero,new Vector2(100,82));
                else TacticalMenuArt.Label("Empty companion "+i,frame.transform,"＋",Vector2.zero,new Vector2(90,60),32,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
            }
            TacticalMenuArt.Rule("Companion promise rule",panel,new Vector2(0,-425),396);
            TacticalMenuArt.Label("Companion promise",panel,"同看这曜京的天光。",new Vector2(0,-394),new Vector2(322,50),27,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
        }
        private TacticalSkill[] MenuSkills(TacticalHero hero)
        {return hero.skills.Concat(TacticalContent.Skills.Where(s=>string.IsNullOrEmpty(s.heroId)||s.heroId==hero.id).Select(s=>s.id)).Distinct().Select(TacticalContent.GetSkill).Where(s=>s!=null).ToArray();}

        private void ShowCompanionSkill(string heroId,string skillId)
        {
            inspectedHero=heroId;companionSkills=true;companionSkillId=skillId;
            var skills=MenuSkills(TacticalContent.GetHero(heroId));companionSkillPage=Mathf.Max(0,Array.FindIndex(skills,s=>s.id==skillId))/6;SelectParty();
        }

        private void DrawCompanionSkills(Transform panel,TacticalHero hero)
        {
            TacticalMenuArt.Heading("Companion detail name",panel,hero.name+" · 技能",new Vector2(-54,366),new Vector2(400,72),38,true);
            TacticalMenuArt.TextButton("Companion attributes",panel,"返回属性",new Vector2(164,366),new Vector2(172,54),()=>{companionSkills=false;SelectParty();},fontSize:23);
            var all=MenuSkills(hero);int pageCount=(all.Length+5)/6;companionSkillPage=Mathf.Clamp(companionSkillPage,0,pageCount-1);
            var visible=all.Skip(companionSkillPage*6).Take(6).ToArray();
            if(!visible.Any(s=>s.id==companionSkillId))companionSkillId=(visible.FirstOrDefault(s=>s.resource=="sp")??visible[0]).id;
            var previous=TacticalMenuArt.TextButton("Previous companion skills",panel,"‹ 上一页",new Vector2(-174,296),new Vector2(152,46),()=>{companionSkillPage--;companionSkillId=null;SelectParty();},fontSize:22);previous.interactable=companionSkillPage>0;
            var next=TacticalMenuArt.TextButton("Next companion skills",panel,"下一页 ›",new Vector2(174,296),new Vector2(152,46),()=>{companionSkillPage++;companionSkillId=null;SelectParty();},fontSize:22);next.interactable=companionSkillPage<pageCount-1;
            TacticalMenuArt.Label("Companion skills page",panel,$"{companionSkillPage+1} / {pageCount}",new Vector2(0,296),new Vector2(122,40),23,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
            for(int i=0;i<visible.Length;i++)
            {
                var item=visible[i];DrawMenuSkillChoice(panel,item,new Vector2((i%3-1)*171,199-i/3*120),new Vector2(158,108),item.id==companionSkillId,()=>ShowCompanionSkill(hero.id,item.id));
            }
            var definition=TacticalContent.GetSkill(companionSkillId);
            TacticalMenuArt.Heading("Companion skill name",panel,definition.name,new Vector2(0,-32),new Vector2(500,58),34,true);
            TacticalMenuArt.Label("Companion skill availability",panel,hero.skills.Contains(definition.id)?"初始掌握":"旅途中可领悟",new Vector2(0,-87),new Vector2(500,38),22,TacticalMenuArt.Muted);
            string cost=definition.cost+" "+(definition.resource=="charge"?"蓄势":definition.resource.ToUpperInvariant());
            TacticalMenuArt.Label("Companion skill stats",panel,definition.kind=="passive"?"被动 · 自身 · 常驻":$"{cost}    射程 {definition.range} 格    冷却 {definition.cooldown} 轮",new Vector2(0,-137),new Vector2(500,48),23,TacticalMenuArt.Accent);
            var previewUnit=new TacticalUnit{id=hero.id,heroId=hero.id,name=hero.name,team="hero"};previewUnit.skills.Add(definition.id);
            RuleBody("Companion skill effect",panel,SkillEffectText(previewUnit,definition,false),new Vector2(0,-267),new Vector2(500,204),25,context:previewUnit);
            TacticalMenuArt.Label("Companion skill detail note",panel,"战场预览会计入地形、领悟与旧物加成。",new Vector2(0,-382),new Vector2(500,60),22,TacticalMenuArt.Muted);
        }

        private void DrawMenuSkillChoice(Transform parent,TacticalSkill definition,Vector2 position,Vector2 size,bool selected,UnityEngine.Events.UnityAction action)
        {
            var button=TacticalMenuArt.TextButton("Inspect skill "+definition.id,parent,"",position,size,action,selected:selected,fontSize:22);
            TacticalArt.SkillSymbol("Menu skill icon "+definition.id,button.transform,definition.id,new Vector2(0,18),58);
            TacticalMenuArt.Label("Menu skill label "+definition.id,button.transform,definition.name,new Vector2(0,-34),new Vector2(size.x-12,34),22,selected?TacticalMenuArt.Gold:TacticalMenuArt.Text,TextAnchor.MiddleCenter);
        }
    }
}
