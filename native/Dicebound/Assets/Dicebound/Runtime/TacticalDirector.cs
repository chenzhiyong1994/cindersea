using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicebound.Tactics;
using Dicebound.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Story, input and presentation only. All battle and expedition changes go through TacticalRules.</summary>
    public sealed partial class TacticalDirector : MonoBehaviour
    {
        public static TacticalDirector Instance;
        public TacticalState State { get; private set; }
        public Canvas Canvas { get; private set; }
        public Soundscape Audio { get; private set; }
        public TacticalStage Stage { get; private set; }
        public string SaveDirectory { get; private set; }
        public string SaveError { get; private set; }
        public bool ReducedMotion { get; private set; }
        private TacticalMenuMotion menuMotion;
        private Material menuPortraitMotionMaterial;
        public bool Modal => overlay != null || ruleOverlay != null;
        public bool Busy { get; private set; }
        private TacticalStore store;
        private TacticalChronicle chronicle;
        private Transform page, overlay;
        private string selected, skill, throwObject;
        private string inspectedHero="sixuan";
        private TacticalRequest pending;
        private TacticalRequest hovered;
        public TacticalRequest PendingTarget => TacticalRangeSnapshot.Copy(pending);
        public TacticalRequest HoveredTarget => pending==null?TacticalRangeSnapshot.Copy(hovered):null;
        private Text preview, previewHeading, tilePreview, toast;
        private RectTransform previewPanel;
        private RectTransform toastPanel;
        private CanvasGroup toastGroup;
        private Button commit;
        private readonly List<string> party = new List<string> { "sixuan", "lingfeng", "cangling" };
        private int selectedTrial=2;
        private readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        private static readonly Vector2 Center = Vector2.one * .5f;
        private float toastUntil;
        private bool qa;
        private string view;
        private int windowWidth = 1600, windowHeight = 900;
        private int readingPage;
        private Coroutine presentation;
        private bool shuttingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Instance)
                new GameObject("Dicebound tactics").AddComponent<TacticalDirector>();
        }
        private void Awake()
        {
            Instance = this; Application.targetFrameRate = 60;
            var args = Environment.GetCommandLineArgs(); qa = args.Contains("--dicebound-tactical-verify") || args.Contains("--dicebound-ui-smoke") || args.Contains("--dicebound-experience-verify");
            Application.runInBackground = qa;
            SaveDirectory = Application.persistentDataPath;
            int index = Array.IndexOf(args, "--dicebound-save-dir");
            if (index >= 0 && index + 1 < args.Length) SaveDirectory = Path.GetFullPath(args[index + 1]);
            if (qa && index < 0) throw new InvalidOperationException("Tactical verification requires an isolated save directory.");
            store = new TacticalStore(SaveDirectory);
            Stage = gameObject.AddComponent<TacticalStage>(); Stage.Initialize(this);
            Audio = gameObject.AddComponent<Soundscape>(); Audio.Build();
            ReducedMotion = !qa && PlayerPrefs.GetInt("dicebound.tactics.motion", 0) == 1;
            menuMotion = gameObject.AddComponent<TacticalMenuMotion>();
            Audio.Music = qa ? .38f : PlayerPrefs.GetFloat("dicebound.tactics.music", .38f);
            // Automated player tests may silence the music track (--dicebound-music 0); SFX stay audible.
            int musicOverride=Array.IndexOf(args,"--dicebound-music");
            if(musicOverride>=0&&musicOverride+1<args.Length&&float.TryParse(args[musicOverride+1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var musicVolume))Audio.Music=Mathf.Clamp01(musicVolume);
            Audio.Effects = qa ? .7f : PlayerPrefs.GetFloat("dicebound.tactics.effects", .7f);
            Audio.Ambience = qa ? .28f : PlayerPrefs.GetFloat("dicebound.tactics.ambience", .28f);
            Canvas = Ui.Canvas("Tactical chronicle"); Ui.EventSystem();
            page = Ui.Rect("Chronicle page", Canvas.transform, Center, Vector2.zero, PaperViewport.DesignSize);
            toastPanel = TacticalArt.Surface("Notice panel", Canvas.transform, new Vector2(0, 400), new Vector2(680, 64), "panel-ink-v1").rectTransform;
            toastGroup = toastPanel.gameObject.AddComponent<CanvasGroup>(); toastGroup.blocksRaycasts = false; toastGroup.alpha = 0;
            toastPanel.GetComponent<Image>().raycastTarget=true;
            var dismissNotice=toastPanel.gameObject.AddComponent<Button>();dismissNotice.transition=Selectable.Transition.None;dismissNotice.onClick.AddListener(()=>Notify(""));
            toast = Ui.Label("Tactical notice", toastPanel, "", Center, Vector2.zero, new Vector2(632, 36), TacticalTheme.FontBody, TacticalTheme.OnDark, TextAnchor.MiddleCenter);
            bool loaded = store.TryLoad(out var saved, out var error); State = saved; SaveError = error;
            if (!loaded && error != null && store.TryRecoverPrevious(out var previous, out var recoveryError))
            {
                try { ProtectArchives(); State = previous; SaveError = "主档异常，已接续上一检查点；原文件已备份。"; }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { SaveError = "上一检查点可恢复，但备份失败：" + e.Message; }
            }
            Title();
            if(args.Contains("--dicebound-experience-verify"))gameObject.AddComponent<TacticalExperienceVerification>();
            else if (qa) gameObject.AddComponent<TacticalVerification>();
        }
        private void Update()
        {
            if (toast && toast.text.Length > 0)
            {
                float remaining = toastUntil - Time.unscaledTime;
                toastGroup.alpha = ReducedMotion ? (remaining > 0 ? 1 : 0) : Mathf.Clamp01(remaining / .25f);
                if (remaining <= 0) {toast.text = "";toastGroup.blocksRaycasts=false;}
            }
            if (Input.GetKeyDown(KeyCode.F11)) ToggleFullscreen();
            if (Busy) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            { CancelSelection(Input.GetKeyDown(KeyCode.Escape)); }
            if (Modal || view != "run" || State == null || State.phase != "battle") return;
            if (Input.GetKeyDown(KeyCode.Space) && pending != null) Commit();
            if (Input.GetKeyDown(KeyCode.E)) EndSelectedTurn();
            for (int i = 0; i < 3; i++) if (Input.GetKeyDown(KeyCode.F1 + i))
            { var ally = State.units.Where(u => u.team == "hero").ElementAtOrDefault(i); if (ally != null) Select(ally.id); }
        }
        public void CancelSelection(bool back=false)
        {
            if(Busy)return;
            if(ruleOverlay){CloseRulePopover();return;}
            if(Modal){CloseOverlay();return;}
            if(view=="run"&&State?.phase=="battle"){Stage.ExitGroundView();pending=null;hovered=null;skill=null;Render();}
            else if(back&&view=="party")Title();
        }
        private Texture2D Picture(string group, string id)
        {
            string key = group + "/" + id;
            if (!art.TryGetValue(key, out var value))
            { value = Resources.Load<Texture2D>("Art/Paper/" + key) ?? Resources.Load<Texture2D>("Art/Jinhai/" + key); art[key] = value; }
            return value;
        }
        private void ResetPage(string scene)
        {
            Audio.SetTheme(view=="run"?TacticalMusic.ThemeFor(State):"title");
            menuMotion.BeginPage();
            CloseOverlay(); Ui.Clear(page); preview = null; previewHeading = null; tilePreview = null; previewPanel = null; commit = null;
            bool battle = view == "run" && State != null && State.phase == "battle";
            Stage.SetVisible(battle);
            Shader.SetGlobalFloat("_TacticalGlassUseBackdrop",battle?0:1);
            if (battle) return;
            Ui.Panel("Ink ground", page, Center, Vector2.zero, PaperViewport.DesignSize, TacticalTheme.Navy);
            var image = Ui.Picture("Yaojing scene", page, Picture("Scenes", scene), Center, Vector2.zero, PaperViewport.DesignSize, TacticalTheme.SceneTint); Ui.Crop(image);
            Shader.SetGlobalTexture("_TacticalGlassBackdrop",image.texture);
            var crop=image.uvRect;
            Shader.SetGlobalVector("_TacticalGlassBackdropUV",new Vector4(crop.x,crop.y,crop.width,crop.height));
            Shader.SetGlobalColor("_TacticalGlassBackdropColor",TacticalTheme.SceneTint);
            Shader.SetGlobalColor("_TacticalGlassBackdropWash",TacticalTheme.NightWash);
            Ui.Panel("Night wash", page, Center, Vector2.zero, PaperViewport.DesignSize, TacticalTheme.NightWash);
            toastPanel.SetAsLastSibling();
        }
        private Text Label(string name, Transform parent, string text, float x, float y, float width, float height, int size = TacticalTheme.FontButton, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var tint=color??TacticalTheme.Ink;
            if(overlay&&parent.IsChildOf(overlay))
            {if(tint==TacticalTheme.Ink)tint=TacticalMenuArt.Text;else if(tint==TacticalTheme.FadedInk)tint=TacticalMenuArt.Muted;}
            return Ui.Label(name,parent,text,Center,new Vector2(x,y),new Vector2(width,height),size,tint,align);
        }
        private Button Button(string name, Transform parent, string text, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, bool primary = false, string icon = null)
            => TacticalUi.Button(name, parent, text, new Vector2(x, y), new Vector2(width, height), action, primary ? TacticalButtonVariant.Primary : TacticalButtonVariant.Secondary, icon);
        private Button Quiet(string name, Transform parent, string text, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, string icon = null, bool onPaper = true)
            => TacticalUi.Button(name, parent, text, new Vector2(x, y), new Vector2(width, height), action, TacticalButtonVariant.Quiet, icon, TacticalTheme.FontButton, onPaper ? TacticalSurfaceContext.Light : TacticalSurfaceContext.Dark);
        private Transform Panel(string name, float x, float y, float width, float height)
            => TacticalArt.Surface(name,page,new Vector2(x,y),new Vector2(width,height),"folio-ivory-v1").transform;
        public void StartRun(uint seed, IEnumerable<string> heroes) => StartRun(seed, heroes, 2);
        public void StartRun(uint seed, IEnumerable<string> heroes, int trial)
        {
            try
            {
                ProtectArchives();
                if (State != null) ArchiveFiles.Export(TacticalCodec.Encode(State), Path.Combine(SaveDirectory, "Tactical Backups"));
                var next = TacticalRules.NewAscent(seed, heroes, 4, trial);
                if (!store.TrySave(next, out var error)) { Notify("新旅程未开启：" + error); return; }
                State = next; SaveError = null; selected = null; skill = null; pending = null; hovered = null; lastOutcome = null; Notify(""); Render();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is ArgumentException) { Notify(e.Message); }
        }
        public void Request(TacticalRequest action)
        {
            if (Busy || State == null) return;
            try
            {
                var next = TacticalRules.Act(State, action);
                // Publish the rules result only after its checkpoint has been committed.
                if (!store.TrySave(next, out var error)) { SaveError = error; Notify("行动未提交：" + error); return; }
                var before = State; State = next; SaveError = null; pending = null; hovered = null;
                Stage.ExitGroundView();
                if (before.phase == "battle" && next.phase == "reward" && !before.sideBattle)
                {
                    if (TacticalChronicle.TryLoad(SaveDirectory, out chronicle, out var chronicleError) && chronicle.TryRecord(SaveDirectory, before.chapter, before.squad, out chronicleError)) { }
                    else Notify("旅程已保存，章节回顾暂未记录：" + chronicleError);
                }
                if (action.type == "endTurn") selected = TacticalRules.NextActiveHero(next, action.unitId)?.id;
                if (action.type != "skill") skill = null;
                Audio.Play(action.type == "move" ? "paper" : "select", action.type=="skill"?.3f:.7f);
                if (before.phase == "battle") presentation = StartCoroutine(Feedback(before, action));
                else { Render(); ShowOutcome(TacticalRules.DescribeOutcome(before,next,action)); }
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException) { RejectAction(e.Message); }
        }
        private void Header(string title, string subtitle)
        {
            var header = Panel("Chronicle header", 0, 458, 1810, 110);
            Label("Book identity", header, "烬海天阙\n结伴登阙 · 战棋", -722, 0, 300, 72, TacticalTheme.FontEmphasis);
            Ui.Heading("Page heading", header, title, Center, new Vector2(-44, 18), new Vector2(808, 48), TacticalTheme.FontSection, TacticalTheme.Ink);
            Label("Page subtitle", header, subtitle, -44, -29, 910,34, TacticalTheme.FontSecondary, TacticalTheme.FadedInk, TextAnchor.MiddleCenter);
            HeaderTools(header,false);
        }
        private void HeaderTools(Transform header,bool battle)
        {
            int count=battle?4:3;var positions=Ui.Row(count*120+(count-1)*8,8,count,out float width);
            float center=battle?660:718;
            int i=0;
            if(battle)
            {
                TacticalUi.FieldButton("Terrain guide",header,"地形",new Vector2(center+positions[i++],0),new Vector2(width,46),TerrainGuide,icon:"highground");
                TacticalUi.FieldButton("Guide",header,"操作",new Vector2(center+positions[i++],0),new Vector2(width,46),Help,icon:"help");
                TacticalUi.FieldButton("Preferences",header,"设置",new Vector2(center+positions[i++],0),new Vector2(width,46),Settings,icon:"settings");
                TacticalUi.FieldButton("Title",header,"标题",new Vector2(center+positions[i],0),new Vector2(width,46),Title,icon:"back");return;
            }
            Quiet("Guide",header,"操作",center+positions[i++],0,width,46,Help,"help",!battle);
            Quiet("Preferences",header,"设置",center+positions[i++],0,width,46,Settings,"settings",!battle);
            Quiet("Title",header,"标题",center+positions[i],0,width,46,Title,"back",!battle);
        }
        public void Render()
        {
            if (State == null) { Title(); return; }
            // Existing chapter checkpoints remain compatible, but their interstitial
            // story page is retired. Commit the ordinary begin action before showing play.
            if (State.phase == "story") { Request(new TacticalRequest { type = "begin" }); return; }
            view = "run";
            ResetPage(TacticalStory.Stage(State.chapter).sceneKey);
            switch (State.phase)
            {
                case "map": AscentMap(); break;
                case "shop": AscentShop(); break;
                case "battle": Battle(); break;
                case "reward": if(State.journeyMode=="ascent")AscentReward();else Reward(); break;
                case "route": Route(); break;
                case "event": if(State.journeyMode=="ascent")AscentEvent();else Event(); break;
                case "camp": if(State.journeyMode=="ascent"&&State.ascentRevision>=2)AscentCamp();else Camp(); break;
                case "victory": case "defeat": if(State.journeyMode=="ascent")AscentEnding();else Ending(); break;
                default: throw new InvalidOperationException("Unknown tactical page: " + State.phase);
            }
        }
        public void Select(string id)
        {
            var unit = TacticalRules.FindUnit(State, id); if (unit == null || unit.team != "hero" || unit.hp <= 0) return;
            Stage.ExitGroundView();selected = id; skillPage = 0; skill = null; pending = null; hovered = null; Render();
        }
        public void ChooseSkill(string id)
        {
            var unit=TacticalRules.FindUnit(State,selected);
            Stage.ExitGroundView();skill=id;pending=null;hovered=null;Render();
            if(unit==null)return;
            var availability=TacticalRules.SkillAvailability(State,selected,id);
            if(!availability.ok){if(id!=null&&TacticalContent.GetSkill(id)?.kind=="passive")Notify(availability.reason);else RejectAction(availability.reason);}
        }
        private static string ResourceName(string resource)=>resource=="charge"?"蓄势":(resource??"ap").ToUpperInvariant();
        private void RejectAction(string message){Audio.Play("deny",.85f);Notify(message);}
        private void EndSelectedTurn()
        {
            if(selected!=null)Request(new TacticalRequest{type="endTurn",unitId=selected});
        }
        public void CellPress(int x, int y)
        {
            if (Busy || Modal || view != "run" || State == null || State.phase != "battle") return;
            var occupant = State.units.FirstOrDefault(u => u.x == x && u.y == y && u.hp > 0);
            if (skill == null && occupant != null && occupant.team == "hero") { Select(occupant.id); return; }
            if (skill == null && occupant != null && occupant.team == "enemy") { InspectUnit(occupant.id); return; }
            if (selected == null) return;
            hovered = null;
            pending = new TacticalRequest { type = skill == null ? "move" : "skill", unitId = selected, skillId = skill, choice = skill == "throw" ? throwObject : null, x = x, y = y };
            RefreshPreview(); DrawSelection();
        }
        public void Hover(int x, int y)
        {
            if (pending != null || preview == null || State == null || State.phase != "battle" || Busy || Modal) return;
            if (selected != null)
            {
                hovered = new TacticalRequest { type = skill == null ? "move" : "skill", unitId = selected, skillId = skill, choice = skill == "throw" ? throwObject : null, x = x, y = y };
                RefreshPreview(); DrawSelection(); return;
            }
            SetPreviewExpanded(true);
            SetHudText(tilePreview,TileHoverSummary(x,y));
            SetHudText(preview,"请先选择一位可行动的同行者。");
        }
        public void HoverExit() { hovered=null;if(pending==null&&!Busy){RefreshPreview();DrawSelection();} }
        public void Commit()
        {
            if(pending==null)return;var check=TacticalRules.Preview(State,pending);
            if(check.ok)Request(pending);else RejectAction(check.reason);
        }


        private void EnemyIntentions()
        {
            var intents=TacticalRules.EnemyIntents(State).ToArray();var frame=Overlay("敌人下一步",1000,Mathf.Max(600,292+intents.Length*80));
            var rows=Ui.Column(Mathf.Max(600,292+intents.Length*80)*.5f-148,16,Enumerable.Repeat(64f,intents.Length).ToArray());
            for(int i=0;i<intents.Length;i++)
            {
                var intent=intents[i];var enemy=TacticalRules.FindUnit(State,intent.unitId);bool attack=intent.attacks;
                string effect=intent.text+(intent.damage>0?" · 预计伤害 "+intent.damage:"");
                Label("Enemy intent "+i,frame,enemy.name+"  →  "+effect,0,rows[i],856,64, TacticalTheme.FontEmphasis, attack?TacticalTheme.Vermilion:TacticalTheme.Ink);
            }
        }


        private void DrawSelection() {if(State!=null&&State.phase=="battle")Stage.Selection(State,selected,skill,pending??hovered,pending!=null);}

        private void SetPreviewExpanded(bool expanded)
        {
            if(preview==null||previewPanel==null)return;
            previewHeading.gameObject.SetActive(true);
        }
        private void SetMovePreview(bool expanded=true)
        {
            if(preview==null)return;
            SetPreviewExpanded(expanded);
            var available=TacticalRules.SkillAvailability(State,selected,null);
            string instructions=available.ok?"蓝色 · 可到达区域\n金色 · 实际移动路径\n\n点击地格预览\nSpace 确认":"暂不可用\n"+available.reason;
            SetHudText(preview,"移动\n1 AP · 最多 "+TacticalRules.MoveBudget(State)+" 步\n\n"+instructions);
        }
        private void SetHudText(Text target,string value)
        {
            if(!target||target.text==value)return;
            target.text=value;target.enabled=true;target.raycastTarget=false;
        }
        private void SetSkillPreview(TacticalSkill definition,TacticalUnit unit)
        {
            if(preview==null||unit==null)return;
            SetPreviewExpanded(true);
            var availability=TacticalRules.SkillAvailability(State,unit.id,definition.id);
            string reason=availability.ok?"点击目标预览\nSpace 确认":availability.reason;
            SetHudText(preview,definition.name+"  Lv."+TacticalRules.SkillLevel(unit,definition.id)+"\n"+SkillCost(definition)+" · 射程 "+TacticalRules.EffectiveSkillRange(State,unit,definition)+"\n\n"+TacticalBattleCopy.SkillPurpose(State,unit,definition)+"\n"+reason);
        }

        private string TileHoverSummary(int x,int y)
        {
            string terrain=TacticalRules.TerrainAt(State,x,y);
            string rule=terrain=="wall"?"不可通行 · 阻挡视线":terrain=="gap"?"不可通行 · 可越过射击":terrain=="rubble"?"通行 · 每格 2 步":terrain=="pipe"?"通行 · 每格 2 步\n敌方行动后 -2 生命":terrain=="cover"?"通行 · 每格 1 步\n受伤减半，向上取整":terrain=="high"?"通行 · 每格 1 步\n攻击低处增伤 2":terrain=="mist"?"通行 · 每格 1 步\n支持水雾施术与旧物":terrain=="shadow"?"通行 · 每格 1 步\n支持借影与阴影增伤":"通行 · 每格 1 步";
            string text=GridName(x,y)+" · "+TerrainName(terrain)+"\n"+rule;
            var unit=State.units.FirstOrDefault(u=>u.hp>0&&u.x==x&&u.y==y);
            if(unit!=null)text+="\n"+unit.name+"\n生命 "+unit.hp+"/"+unit.maxHp+(unit.block>0?" · 盾 "+unit.block:"");
            var item=TacticalRules.ObjectAt(State,x,y);
            if(unit==null&&item!=null)text+="\n"+(item.kind=="valve"?"蒸汽阀":item.kind=="canister"?"热罐":"木箱")+" · 耐久 "+item.hp;
            return text;
        }

        private void Medallion(string name,Transform parent,string icon,Vector2 position,float size)
        {
            float diameter=size+8;
            TacticalArt.Surface(name+" lens",parent,position,Vector2.one*diameter,"lens-jade-v1");
            if(TacticalContent.GetSkill(icon)!=null)TacticalArt.SkillSymbol(name,parent,icon,position,size);
            else TacticalArt.Symbol(name,parent,icon,position,Vector2.one*size);
        }

        private void RefreshPreview()
        {
            if (preview == null || commit == null) return;
            var target=pending??hovered;
            var active=TacticalRules.FindUnit(State,selected);
            if(tilePreview!=null)
            {
                int x=target?.x??active?.x??0,y=target?.y??active?.y??0;
                SetHudText(tilePreview,TileHoverSummary(x,y));
            }
            if (target == null)
            {
                SetMovePreview(false);
                if(skill!=null)SetSkillPreview(TacticalContent.GetSkill(skill),TacticalRules.FindUnit(State,selected));
                commit.interactable = false; return;
            }
            SetPreviewExpanded(true);
            var result = TacticalRules.Preview(State, target); commit.interactable = pending!=null&&result.ok;
            if(!result.ok){SetHudText(preview,(target.type=="move"?"无法移动":TacticalContent.GetSkill(target.skillId).name)+"\n\n"+result.reason);return;}
            string costs = result.cost + " " + (result.resource == "charge" ? "蓄势" : (result.resource ?? "AP").ToUpperInvariant());
            string effect = result.damage > 0 ? "预计生命损失 " + result.damage : result.heal > 0 ? "恢复 " + result.heal + " 生命" : result.block > 0 ? "获得 " + result.block + " 护盾" : "";
            if (!string.IsNullOrEmpty(result.objectId)) effect += "\n投掷场地物件";
            if (result.affected.Count > 1) effect += "\n波及 " + result.affected.Count + " 格";
            if (target.type == "move") effect = "路径 "+result.path.Count+" 格 · "+result.path.Sum(c=>TacticalRules.MoveCost(State,c.x,c.y))+" 步\n沿金色路径移动";
            string heading=target.type=="move"?"移动至 "+GridName(target.x,target.y):TacticalContent.GetSkill(target.skillId).name+" Lv."+TacticalRules.SkillLevel(active,target.skillId)+"\n目标 "+GridName(target.x,target.y);
            SetHudText(preview,heading+"\n消耗 "+costs+"\n\n"+effect+"\n"+(pending!=null?"Space 确认行动":"点击地格锁定目标"));
        }
        private void Route()
        {
            Header("穿过余热的路", "继续前进：选择战斗、补给或休息。");
            var columns=Ui.Row(State.routes.Count*508+(State.routes.Count-1)*48,48,State.routes.Count,out float width);
            for (int i = 0; i < State.routes.Count; i++)
            {
                var route = State.routes[i]; var frame = Panel("Route " + route.id, columns[i], -13, width, 672);
                TacticalArt.CropPicture("Route illustration " + i, frame, Picture("Scenes", route.kind == "camp" ? "dawn" : route.kind == "event" ? "archive" : "conduit"),new Vector2(0,176),new Vector2(width-64,256),20);
                TacticalArt.Surface("Route seal backing "+i,frame,new Vector2(0,56),Vector2.one*82,"panel-ink-v1");
                TacticalArt.Symbol("Route emblem "+i,frame,"route-"+route.kind,new Vector2(0,56),Vector2.one*62);
                Ui.Heading("Route title " + i, frame, route.name, Center, new Vector2(0, -24), new Vector2(408, 62), TacticalTheme.FontSection, TacticalTheme.Ink);
                Label("Route text " + i, frame, route.text, 0, -127, 408, 130, TacticalTheme.FontEmphasis, TacticalTheme.FadedInk);
                Button("Route choice " + route.id, frame, "走这条路", 0, -264, width-64, 64, () => Request(new TacticalRequest { type = "route", choice = route.id }), true,"confirm");
            }
            ExpeditionFooter();
        }
        private void Event()
        {
            var definition = TacticalStory.Events.First(e => e.id == State.eventId); Header(definition.title, "稍作停留，选择一份沿途帮助。");
            bool supplied = State.relics.Count == TacticalContent.Relics.Length;
            var frame = Panel("Encounter paper", 0, -7, 1452, 722);
            var choices=Ui.Row(1200,24,2,out float choiceWidth);
            Ui.Narrative("Encounter story", frame, definition.body, Center, new Vector2(0, 119), new Vector2(1260, 283), TacticalTheme.FontCard, TacticalTheme.Ink, TextAnchor.UpperLeft);
            Label("Event first consequence", frame, definition.resultA, choices[0], -110, choiceWidth, 120, TacticalTheme.FontButton, TacticalTheme.FadedInk);
            Label("Event second consequence", frame, supplied ? "随行的旧物已经齐全。工人递来补给，全队恢复 4 点生命。" : definition.resultB, choices[1], -110, choiceWidth, 120, TacticalTheme.FontButton, TacticalTheme.FadedInk);
            Button("Event A", frame, definition.choiceA, choices[0], -265, choiceWidth, 64, () => Request(new TacticalRequest { type = "event", choice = "A" }), true);
            Button("Event B", frame, supplied ? "接受补给 · 恢复体力" : definition.choiceB, choices[1], -265, choiceWidth, 64, () => Request(new TacticalRequest { type = "event", choice = "B" }));
        }
        private void Camp()
        {
            Header("交班灯下", "整理装备，恢复体力，再一起出发。");
            var frame = Panel("Camp paper", 0, 0, 1374, 688);
            var choices=Ui.Row(1172,24,2,out float choiceWidth);
            Ui.Narrative("Camp narrative", frame, "一盏暖灯照亮了歇脚处。大家喝下热汤，包扎伤口。\n\n同伴们修好器物，重新握紧兵器。前路还长，不过这次谁也不是独自前行。", Center, new Vector2(0, 87), new Vector2(1188, 306), TacticalTheme.FontCard, TacticalTheme.Ink, TextAnchor.UpperLeft);
            Button("Camp rest", frame, "喝汤包扎 · 恢复小队体力", choices[0], -234, choiceWidth, 68, () => Request(new TacticalRequest { type = "camp", choice = "rest" }), true);
            var study = new TacticalRequest { type = "camp", choice = "study" };
            var train = Button("Camp train", frame, "校准器物 · 带上新的领悟", choices[1], -234, choiceWidth, 68, () => Request(study)); train.interactable = TacticalRules.Preview(State, study).ok;
        }
        private void Ending()
        {
            bool won=State.phase=="victory";ResetPage(won?"dawn":"wheel");Header(won?"并肩守住曜京":"整装，再次出发",won?"敌人已被击退。谢谢每一位同行的伙伴。":"调整队伍与打法，下一次继续前进。");Audio.SetTheme(won?"victory":"defeat");
            var frame=Panel("Epilogue paper",0,-10,1564,727);
            TacticalArt.Symbol("Ending emblem",frame,won?"victory":"defeat",new Vector2(-680,212),Vector2.one*96);
            Ui.Narrative("Epilogue",frame,won?TacticalStory.Epilogue:TacticalStory.Failure,Center,new Vector2(65,196),new Vector2(1200,260), TacticalTheme.FontCard, TacticalTheme.Ink,TextAnchor.MiddleLeft);
            var people=Ui.Row(1299,24,State.squad.Count,out float personWidth);
            for(int i=0;i<State.squad.Count;i++)
            {
                string id=State.squad[i];var person=TacticalContent.GetHero(id);var card=TacticalArt.Surface("Ending companion "+id,frame,new Vector2(people[i],-42),new Vector2(personWidth,166),"folio-ivory-v1").transform;
                TacticalUi.Portrait("Ending portrait "+id,card,Picture("Heroes",id),id,new Vector2(-137,0),new Vector2(105,145),true);
                Label("Ending name "+id,card,person.name,58,29,218,46, TacticalTheme.FontCard, TacticalTheme.Ink);Label("Ending role "+id,card,TacticalUi.Role(id),58,-25,218,57, TacticalTheme.FontBody, TacticalTheme.FadedInk);
            }
            Label("Expedition record",frame,"此行收获："+(State.relics.Count==0?"与伙伴并肩战斗的经验":string.Join("、",State.relics.Select(id=>TacticalContent.GetRelic(id).name))),0,-178,1356,86, TacticalTheme.FontButton, TacticalTheme.FadedInk,TextAnchor.MiddleCenter);
            var choices=Ui.Row(1200,24,2,out float choiceWidth);
            Button("Restart",frame,"重新组队 · 再次出发",choices[0],-282,choiceWidth,64,SelectParty,true,"party");Button("Return title",frame,"返回标题",choices[1],-282,choiceWidth,64,Title,icon:"back");
        }
        private void ExpeditionFooter()
        {
            Label("Expedition companions", page, "同行者：" + string.Join("、", State.squad.Select(id => TacticalContent.GetHero(id).name)) + " · 旧物：" + (State.relics.Count == 0 ? "暂无" : string.Join("、", State.relics.Select(id => TacticalContent.GetRelic(id).name))), 0, -441, 1690, 72, TacticalTheme.FontButton, TacticalTheme.PaperLight, TextAnchor.MiddleCenter);
        }
        public void CloseOverlay() { CloseRulePopover();if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); overlay = null; } }
        private Transform Overlay(string title, float width = 1280, float height = 830)
        {
            Stage.ExitGroundView();CloseOverlay(); var veil = Ui.Panel("Modal shade", Canvas.transform, Center, Vector2.zero, PaperViewport.DesignSize, TacticalTheme.ModalShade, true); overlay = veil.transform;
            var frame = TacticalMenuArt.QuietPanel("Modal paper",overlay,Vector2.zero,new Vector2(width,height)).transform;
            TacticalMenuArt.Heading("Modal title",frame,title,new Vector2(0,height*.5f-78),new Vector2(width-240,78),TacticalTheme.FontPage,true).alignment=TextAnchor.MiddleCenter;
            TacticalMenuArt.Rule("Modal engraved rule",frame,new Vector2(0,height*.5f-128),width-180);
            Quiet("Close modal corner",frame,"×",width*.5f-48,height*.5f-48,44,44,CloseOverlay);
            Button("Close modal", frame, "关闭", 0, -height * .5f + 68, 410, 56, CloseOverlay); toastPanel.SetAsLastSibling(); return frame;
        }
        public void Help()
        {
            Reading("简单上手", "1. 召集三位同行者，沿十二层节点路线登阙。选择与当前地点相连的亮起节点；第 4、8 层是幕门守卫，第 5、9 层有商店与营火，最终击败顶层首领即可完成旅程。选人时点击头像只检视资料，加入队伍需点击独立按钮。属性与技能详解会显示真实初始数值。\n\n2. 战斗与精英节点进入战场；精英敌人更强。商店分别抽取本队三位同行者的招式，可花金币重随；技能自动归属对应角色，重复领悟提高等级和真实效果。营火节点可休整、研习或守夜。战斗中让三名同行者接连命中同一敌人可触发连携加成与溃散。战斗与领奖可随时查看队伍及行囊。事件节点让你作出选择。完成节点后回到路线图，按小队状况选择下一站。\n\n3. 战斗中左侧点击队员头像，或按 F1–F3 选人。底部选择移动或技能，悬停只查看预览，点击格子锁定目标，再按确认行动或 Space。右键或 Esc 取消；悬停不会代替已锁定目标。\n\n4. AP 支付移动与基础动作，SP 支付特技；移动按钮注明可走步数。击败全部敌人即可获胜，按 E 只结束当前队员，自动切到下一位；全部存活队员结束后敌方行动。墙体挡路挡视线，缺口可越射；碎石与热管每格耗 2 步。顶部「地形」可查看高地、掩体、水雾与阴影效果。\n\n5. 行动先保存，再播放演出；购买、事件选择和路线选择同样自动保存。继续旅程恢复上次进度；开启新旅程会备份当前档案。全队无人倒下地获胜可累积生还连胜，下一战全员获得额外蓄势与护盾。失败后可重新组队登阙。设置提供档案导入导出。\n\n6. 按住 Alt 或点击顶部「查看地面」可暂时隐藏人物主体，保留脚影与姓名，便于点选被人物遮住的格子；墙体与箱罐仍会遮挡。释放 Alt、再次点击、取消或提交行动会恢复人物。滚轮缩放，中键平移，Home 恢复视角，F11 全屏。设置可调整声音与减少动态效果。回水桥美术样板使用独立试玩档，不覆盖普通旅程。");
        }
        public void TerrainGuide()
        {
            var frame=Overlay("地形与路线",1390,868);
            Label("Terrain introduction",frame,"利用分路包抄与高地支援。墙体挡路也挡视线，缺口上方仍能越射。",0,284,1200,51, TacticalTheme.FontButton, TacticalTheme.FadedInk,TextAnchor.MiddleCenter);
            string[] kinds={"wall","gap","rubble","pipe","cover","high","mist","shadow"};
            string[] symbols={"wall","gap","rubble","pipe","cover","highground","mist","shadow"};
            var columns=Ui.Row(1294,32,3,out float width);var rows=Ui.Column(208,24,136,136,136);
            for(int i=0;i<kinds.Length;i++)
            {
                float x=columns[i%3],y=rows[i/3];string kind=kinds[i];
                TacticalArt.Symbol("Terrain emblem "+kind,frame,symbols[i],new Vector2(x-width*.5f+32,y+20),Vector2.one*52);
                TacticalMenuArt.Heading("Terrain name "+kind,frame,TerrainName(kind),new Vector2(x+48,y+32),new Vector2(width-96,48),TacticalTheme.FontEmphasis,true);
                Label("Terrain rule "+kind,frame,TerrainDescription(kind),x+48,y-26,width-96,90, TacticalTheme.FontBody, TacticalTheme.FadedInk);
            }
        }
        private void Reading(string title, string body) { readingPage = 0; ReadingPage(title, body); }
        private void ReadingPage(string title, string body)
        {
            var paragraphs = body.Split(new[] { "\n\n" }, StringSplitOptions.None); int count = (paragraphs.Length + 1) / 2;
            readingPage = Mathf.Clamp(readingPage, 0, count - 1); var frame = Overlay(title);
            var visible=paragraphs.Skip(readingPage*2).Take(2).ToArray();
            for(int i=0;i<visible.Length;i++)
            {
                var text=visible[i];int dot=text.IndexOf(". ");bool numbered=dot>0&&dot<3&&int.TryParse(text.Substring(0,dot),out _);
                float y=165-i*246;
                if(numbered)
                {
                    TacticalArt.Plate("Reading number backing "+i,frame,new Vector2(-522,y+85),new Vector2(52,54),new Color(.08f,.19f,.18f),3);
                    Label("Reading number "+i,frame,text.Substring(0,dot),-522,y+85,48,52,30,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
                    text=text.Substring(dot+2).Replace("无伤连胜","全员生还连胜");
                }
                Ui.Narrative("Reading text "+i,frame,text,Center,new Vector2(numbered?32:0,y),new Vector2(numbered?1000:1090,218),26,TacticalMenuArt.Text,TextAnchor.UpperLeft);
            }
            var previous = Button("Previous reading page", frame, "上一页", -363, -260, 245, 46, () => { readingPage--; ReadingPage(title, body); }); previous.interactable = readingPage > 0;
            Label("Reading page", frame, (readingPage + 1) + " / " + count, 0, -260, 300, 42, TacticalTheme.FontButton, TacticalTheme.FadedInk, TextAnchor.MiddleCenter);
            var next = Button("Next reading page", frame, "继续阅读", 363, -260, 245, 46, () => { readingPage++; ReadingPage(title, body); }); next.interactable = readingPage + 1 < count;
        }
        public void Chronicle()
        {
            if (!TacticalChronicle.TryLoad(SaveDirectory, out chronicle, out var error)) { Notify(error); return; }
            var frame = Overlay("旅程回顾", 1440, 856);
            Label("Chronicle meaning", frame, "回顾已完成的章节，重读与同行者一起走过的旅程。", 0, 285, 1260, 64, TacticalTheme.FontButton, TacticalTheme.FadedInk);
            var chapters=Ui.Column(219,24,56,56,56,56,56);
            for (int i = 0; i < 5; i++)
            {
                int chapter = i; var definition = TacticalStory.Stage(i);
                var button = Button("Chronicle chapter " + i, frame, chronicle.completed[i] ? definition.title + " · 阅读" : definition.title + " · 尚未完成", -290, chapters[i], 678, 56, () => Reading(definition.title, definition.outro)); button.interactable = chronicle.completed[i];
            }
            Label("Witness heading", frame, "曾到场的同行者", 417, 205, 392, 46, TacticalTheme.FontCard);
            var witnesses=Ui.Column(160,16,Enumerable.Repeat(48f,chronicle.witnesses.Count).ToArray());
            for (int i = 0; i < chronicle.witnesses.Count; i++)
            {
                string id = chronicle.witnesses[i];
                Button("Chronicle witness " + id, frame, TacticalContent.GetHero(id).name + " · 记录", 415, witnesses[i], 392, 48, () => Reading(TacticalContent.GetHero(id).name, TacticalStory.WitnessStatement(id)));
            }
        }
        public void Settings()
        {
            var frame = Overlay("声音、画面与旅程档案", 1240, 828);
            var rows=Ui.Column(265,32,56,56,56);
            Volume(frame, "音乐", rows[0], Audio.Music, value => Audio.Music = value);
            Volume(frame, "音效", rows[1], Audio.Effects, value => Audio.Effects = value);
            Volume(frame, "环境", rows[2], Audio.Ambience, value => Audio.Ambience = value);
            var motion=Button("Reduce motion", frame, "", 0, -30, 1020, 56, () => { ReducedMotion = !ReducedMotion; SaveSettings(); Settings(); });
            Label("Motion label",motion.transform,"减少动态效果",-250,0,440,36, TacticalTheme.FontButton, TacticalTheme.OnDark);
            Label("Motion status",motion.transform,ReducedMotion?"开启":"关闭",395,0,150,36, TacticalTheme.FontButton, ReducedMotion?TacticalTheme.Gold:TacticalTheme.OnDarkSub,TextAnchor.MiddleRight);
            var columns=Ui.Row(1020,24,2,out float width);
            Button("Export tactics", frame, "导出战棋档案", columns[0], -113, width, 56, Export,icon:"export");
            Button("Import tactics", frame, "导入战棋档案", columns[1], -113, width, 56, Import,icon:"import");
            Label("Archive information", frame, "继续旅程恢复上次进度；开启新旅程会备份当前档案。\n导入失败不会覆盖现有进度。", 0, -203, 1020, 87, TacticalTheme.FontButton, TacticalTheme.FadedInk);
            Button("Quit game", frame, "退出游戏", 390, -305, 237, 48, () => { SaveSettings(); Application.Quit(); });
        }
        private void Volume(Transform parent, string name, float y, float value, UnityEngine.Events.UnityAction<float> action)
        {
            Label("Volume name " + name, parent, name, -423, y, 175, 40, TacticalTheme.FontEmphasis);
            var root = Ui.Rect("Volume " + name, parent, Center, new Vector2(128, y), new Vector2(745, 44));
            var background = TacticalArt.Plate("Volume track",root,Vector2.zero,new Vector2(745,12),TacticalMenuArt.RaisedInk,4,true);
            var fillRoot = Ui.Rect("Volume fill area", root, Center, Vector2.zero, new Vector2(720, 8));
            var fill = TacticalArt.Plate("Volume fill",fillRoot,Vector2.zero,new Vector2(720,8),TacticalMenuArt.Accent,4);
            // Slider drives the anchors across fillRoot; fixed dimensions would be added twice.
            fill.rectTransform.sizeDelta=Vector2.zero;
            var handleRoot = Ui.Rect("Volume handle area", root, Center, Vector2.zero, new Vector2(720, 32));
            var handle = TacticalArt.Plate("Volume handle",handleRoot,Vector2.zero,new Vector2(32,32),TacticalTheme.Gold,16,true);
            TacticalArt.Plate("Volume handle pearl",handle.transform,Vector2.zero,new Vector2(22,22),TacticalTheme.PaperLight,11);
            // Slider stretches the handle across the parent vertically; zero delta keeps it circular.
            handle.rectTransform.sizeDelta=new Vector2(32,0);
            var slider = root.gameObject.AddComponent<Slider>(); slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.minValue = 0; slider.maxValue = 1; slider.value = value;
            background.raycastTarget = true; slider.onValueChanged.AddListener(v => { action(v); SaveSettings(); });
        }
        private void SaveSettings()
        {
            if (qa) return;
            PlayerPrefs.SetInt("dicebound.tactics.motion", ReducedMotion ? 1 : 0); PlayerPrefs.SetFloat("dicebound.tactics.music", Audio.Music); PlayerPrefs.SetFloat("dicebound.tactics.effects", Audio.Effects); PlayerPrefs.SetFloat("dicebound.tactics.ambience", Audio.Ambience); PlayerPrefs.Save();
        }
        private void Export()
        {
            if (State == null) { Notify("还没有可导出的战棋旅程。"); return; }
            try { ArchiveFiles.Export(TacticalCodec.Encode(State), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Dicebound Backups")); Notify("战棋档案已导出到文档中的 Dicebound Backups。"); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Notify(e.Message); }
        }
        private void Import()
        {
            string path = ArchiveFiles.ChooseImport(); if (path == null) return;
            try
            {
                if (new FileInfo(path).Length > 512000) { Notify("档案超过大小限制。"); return; }
                if (!TacticalCodec.TryDecode(File.ReadAllText(path), out var incoming, out var error)) { Notify(error); return; }
                ProtectArchives();
                if (State != null) ArchiveFiles.Export(TacticalCodec.Encode(State), Path.Combine(SaveDirectory, "Tactical Backups"));
                if (!store.TrySave(incoming, out error)) { Notify(error); return; }
                State = incoming; selected = null; pending = null; hovered = null; skill = null; lastOutcome = null; Render(); Notify("战棋旅程已接续。");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Notify(e.Message); }
        }
        public void Notify(string text)
        {
            if(!toast)return;
            if(string.IsNullOrEmpty(text)){toast.text="";toastGroup.alpha=0;toastGroup.blocksRaycasts=false;toastUntil=0;return;}
            toast.text=text;float width=Mathf.Clamp(toast.preferredWidth+48,360,1500);
            toast.rectTransform.sizeDelta=new Vector2(width-48,36);float height=Mathf.Max(36,toast.preferredHeight);
            toast.rectTransform.sizeDelta=new Vector2(width-48,height);toastPanel.sizeDelta=new Vector2(width,height+32);
            toastPanel.anchoredPosition=new Vector2(0,Mathf.Min(400,512-(height+32)*.5f));
            toastGroup.alpha=1;toastGroup.blocksRaycasts=true;toastUntil=Time.unscaledTime+3;toastPanel.SetAsLastSibling();
        }
        private void ProtectArchives()
        {
            var existing = new[] { store.SavePath, store.BackupPath }.Where(File.Exists).ToArray();
            if (existing.Length == 0) return;
            string directory = Path.Combine(SaveDirectory, "Tactical Backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(directory);
            foreach (string path in existing) File.Copy(path, Path.Combine(directory, Path.GetFileName(path)), false);
        }
        private IEnumerator Feedback(TacticalState before,TacticalRequest action)
        {
            Busy=true;var group=page.GetComponent<CanvasGroup>()??page.gameObject.AddComponent<CanvasGroup>();group.interactable=false;
            var stack=new Stack<IEnumerator>();stack.Push(Stage.Present(before,State,action,ReducedMotion));
            try
            {
                while(stack.Count>0)
                {
                    bool moved=false;object current=null;Exception failure=null;
                    try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                    catch(Exception e){failure=e;}
                    if(failure!=null){Debug.LogException(failure);Notify("演出已中断，恢复已保存的行动结果。");break;}
                    if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                    if(current is IEnumerator nested)stack.Push(nested);else yield return current;
                }
            }
            finally {while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();FinishPresentation();}
        }
        private void FinishPresentation()
        {
            if(!Busy)return;Busy=false;presentation=null;Stage.CancelEffects();
            var group=page? page.GetComponent<CanvasGroup>():null;if(group)group.interactable=true;
            if(!shuttingDown&&isActiveAndEnabled&&State!=null)Render();
        }
        public void SetPresentationPaused(bool paused)
        {
            if(menuMotion)menuMotion.SetSuspended(paused);
            if(paused)Stage.ExitGroundView();
            if(!paused||!Busy)return;
            if(presentation!=null)StopCoroutine(presentation);
            FinishPresentation();
        }
        private void OnApplicationFocus(bool focused) { if(!focused&&Stage)Stage.ExitGroundView();if (!qa) { AudioListener.pause = !focused; SetPresentationPaused(!focused); } }
        private void OnApplicationPause(bool paused) { if(paused&&Stage)Stage.ExitGroundView();if (!qa) { AudioListener.pause = paused; SetPresentationPaused(paused); } }
        private void ToggleFullscreen()
        {
            if (Screen.fullScreen) Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            else { windowWidth = Screen.width; windowHeight = Screen.height; var display = Screen.currentResolution; Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow); }
        }
        private void OnApplicationQuit() { shuttingDown=true; SaveSettings(); }
        private void OnDestroy()
        {
            shuttingDown=true;
            if(menuMotion)menuMotion.Dispose();
            if(menuPortraitMotionMaterial)Destroy(menuPortraitMotionMaterial);
            if(Instance==this)Instance=null;
            AudioListener.pause=false;
        }
        public static string GridName(int x, int y) => ((char)('A' + x)).ToString() + (y + 1);
        private static string TerrainName(string kind) => kind == "wall" ? "墙体" : kind == "gap" ? "缺口" : kind == "rubble" ? "碎石" : kind == "cover" ? "掩体" : kind == "mist" ? "水雾" : kind == "shadow" ? "阴影" : kind == "pipe" ? "热管" : kind == "high" ? "高台" : "地面";
        private static string TerrainDescription(string kind) => kind=="wall"?"不可通行，遮挡远程视线。":kind=="gap"?"不可通行；远程攻击可越过。":kind=="rubble"?"可以通行，每格消耗 2 步。":kind=="pipe"?"每格消耗 2 步，回合末受到 2 点伤害。":kind=="cover"?"受到的攻击伤害减半，向上取整。":kind=="high"?"攻击未站在高台的敌人，伤害增加 2。":kind=="mist"?"水雾支持沧泠施术与部分旧物。":kind=="shadow"?"阴影支持晏烛影换位与强化。":"普通地面，每格消耗 1 步。";
    }
}
