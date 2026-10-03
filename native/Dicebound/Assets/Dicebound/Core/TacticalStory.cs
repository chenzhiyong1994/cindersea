using System;

namespace Dicebound.Tactics
{
    public sealed class TacticalEnemyDefinition
    {public string id,name,rank,text;public int range,move;}
    public sealed class StoryChapter
    {
        public string title, location, sceneKey, objective, intro, outro;
        public string[] interjections;

        public StoryChapter(string title, string location, string sceneKey, string objective,
            string intro, string outro, string[] interjections)
        {
            this.title = title;
            this.location = location;
            this.sceneKey = sceneKey;
            this.objective = objective;
            this.intro = intro;
            this.outro = outro;
            this.interjections = interjections;
        }
    }

    public sealed class StoryEvent
    {
        public string id, title, body, choiceA, choiceB, resultA, resultB;

        public StoryEvent(string id, string title, string body, string choiceA, string choiceB,
            string resultA, string resultB)
        {
            this.id = id;
            this.title = title;
            this.body = body;
            this.choiceA = choiceA;
            this.choiceB = choiceB;
            this.resultA = resultA;
            this.resultB = resultB;
        }
    }

    // 现行战棋采用简明的城市守护故事。地方、敌障和对白为游戏原创，
    // 六人的身份、装备与能力边界沿用正式人设。三人开路，其余伙伴接应。
    public static class TacticalStory
    {
        public static readonly TacticalEnemyDefinition[] Enemies={
            Enemy("sluice","失控闸卫","normal","重阀近击：近身攻击。铜铁闸板构装，沿街巷追击。",1,3),
            Enemy("loom","缚梦织偶","normal","梦线牵射：2 格内攻击。注意分开站位与掩体。",2,3),
            Enemy("echo","回声渡影","normal","回声扑袭：近身攻击。水影会沿桥面接近。",1,3),
            Enemy("seal","封签校卫","normal","铜签飞射：2 格内攻击。借高台与柜墙交叉射击。",2,3),
            Enemy("afterimage","昼轮蜕影","normal","蜕膜刃：近身攻击。薄膜构装会追赶受伤同行者。",1,3),
            Enemy("steamwarden","炉阀巨卫","elite","重阀锤击：近身攻击；每第三轮改为炉阀震荡，连目标邻格同行者一并受到伤害。分散站位或提前定身。",1,2),
            Enemy("mirrorweaver","折光织主","elite","折光蚀心：3 格内攻击并施加 1 轮削弱，削弱使攻击伤害降低 3。可用掩体阻断射线。",3,2),
            Enemy("tidejudge","沉潮裁官","elite","沉潮枷锁：2 格内攻击并使目标下轮定身。先拆除枷锁来源，避免被困在热管或包围中。",2,2),
            Enemy("sunwheel","曜轮天阙","boss","天阙灼线：奇数轮远射；曜轮横扫：偶数轮攻击目标与邻格并施加 1 轮易伤（承受攻击 +3）。每第三轮获得 8 护盾。射程 3，移动 1；分散站位，以穿盾或集中输出应对轮盾。",3,1)
        };
        static TacticalEnemyDefinition Enemy(string id,string name,string rank,string text,int range,int move)
        {return new TacticalEnemyDefinition{id=id,name=name,rank=rank,text=text,range=range,move=move};}
        public static TacticalEnemyDefinition GetEnemy(string id){return Array.Find(Enemies,e=>e.id==id);}
        public static string EnemyDescription(string id){return GetEnemy(id)?.text??"构装敌障，将沿可通行地块接近同行者。";}

        public const string Prologue =
            "昼轮检修时暗了三息。再亮起时，失控的构装堵住了曜京的街巷。\n\n" +
            "司玄、晏烛影、凌风、沧泠、商朔和阮灼结伴出发。三人开路，伙伴接应，把安全的街道还给大家。";

        public const string Epilogue =
            "最后一片蜕影散去，曜京的道路重新通畅。居民挥手迎接队伍，食堂的热汤已经摆好。\n\n" +
            "凌风端碗的手还在发抖，商朔让他坐下。沧泠在掌心写下‘路通了’，晏烛影把旧账收起：‘先歇一会儿。’\n\n" +
            "阮灼笑着招呼大家，司玄揉了揉发麻的右手。昼轮仍亮着，明天的路，他们还会一起走。";

        public const string Failure =
            "小队暂时挡不住这波敌人，伙伴把伤者接回安全处。\n\n" +
            "阮灼：‘先包扎，休息好了再出发。’重新召集三人，就能开始新的旅程。";

        private static readonly StoryChapter[] Chapters =
        {
            new StoryChapter(
                "第一幕 · 巷口开路", "曜京下城 · 灰灯巷", "street",
                "击败全部敌人。",
                "灰灯巷里，铁架和铜阀盘拼成闸卫，堵住了街口。\n\n" +
                "凌风拔出刀：‘先把路打通！’司玄点头，伙伴们并肩迎了上去。",
                "闸卫散成铁条，街口重新通畅。药车驶过，居民向小队挥手。凌风笑着说：‘走，下一条路！’",
                new[]
                {
                    "凌风：‘一起上，别落下伙伴！’",
                    "司玄：‘看准敌人，稳住脚步。’",
                    "商朔：‘我在后面，放心往前。’",
                    "晏烛影：‘檐下有影，我来绕过去。’"
                }),
            new StoryChapter(
                "第二幕 · 同行向前", "废热管网 · 汇流廊", "conduit",
                "击败全部敌人。",
                "热管长廊冒着蒸汽，黑帘和木梭缠成的织偶迎面扑来。\n\n" +
                "阮灼举起缝夜梭：‘这条路我熟，跟紧我！’沧泠吹响骨笛，压住扑来的乱流。",
                "织偶的梦线松开，长廊安静下来。阮灼拍了拍伙伴的肩：‘配合得不错！前面就是回水桥。’",
                new[]
                {
                    "阮灼：‘借这里的余热，跟我上！’",
                    "沧泠在掌心写：有水雾，我能帮忙。",
                    "商朔：‘站到我身后，别硬挨。’",
                    "凌风：‘你们开路，我来挡住它！’"
                }),
            new StoryChapter(
                "第三幕 · 守住回水桥", "下城冷凝渠 · 回水桥", "bridge",
                "击败全部敌人。",
                "回水桥上，空衣般的水影拦住了去路。桥下水雾翻涌，居民正在远处避难。\n\n" +
                "商朔横槊护住队伍：‘一起把它们打散。’沧泠的笛声从雾里传来。",
                "水影散去，回水桥恢复安静。伙伴们从桥头走过，沧泠在掌心写：都跟上了。",
                new[]
                {
                    "商朔：‘靠紧些，我护住这边。’",
                    "沧泠抬起骨笛，水雾在脚边散开。",
                    "凌风：‘看我的刀，把它们打散！’",
                    "司玄：‘大家都在，继续向前。’"
                }),
            new StoryChapter(
                "第四幕 · 旧档房开路", "昼轮检修署 · 旧档房", "archive",
                "击败全部敌人。",
                "旧档房前，木夹、铜印和纸签拼成的校卫突然扑出，挡住了队伍。\n\n" +
                "晏烛影轻抬针剑：‘碍路的东西，拆了便是。’司玄向前一步，招呼伙伴一起上。",
                "铜印落地，纸签散开。司玄放低剑：‘这条路也通了。’伙伴们走向昼轮底座。",
                new[]
                {
                    "晏烛影：‘借这片影子，换个位置。’",
                    "司玄：‘跟上我，别独自迎敌。’",
                    "阮灼：‘热管还在，支援交给我。’",
                    "商朔：‘我挡住它，你们跟上。’"
                }),
            new StoryChapter(
                "第五幕 · 曜京的明天", "昼轮底座 · 冷却环廊", "wheel",
                "击败全部敌人。",
                "昼轮底座的冷却雾中，灰白蜕影张开薄膜，挡住最后一段道路。\n\n" +
                "凌风握紧刀：‘打败它，大家就能回家！’司玄举起裁云剑，伙伴们并肩冲向前方。",
                "最后的蜕影碎进冷却水，道路终于畅通。阮灼向大家挥手：‘回去喝热汤！’伙伴们笑着收起器物。",
                new[]
                {
                    "凌风：‘最后一场，一起拿下！’",
                    "司玄：‘看准这一击，大家跟上。’",
                    "商朔：‘我守住后面，放心进攻。’",
                    "晏烛影：‘光太亮，靠着维修台走。’"
                })
        };

        public static StoryChapter Stage(int stage)
        {
            return Chapters[Math.Max(0, Math.Min(Chapters.Length - 1, stage))];
        }

        // 保留既有档案接口，只展示简短的同行者话语。
        public static string WitnessStatement(string heroId)
        {
            switch (heroId)
            {
                case "sixuan": return "下一段路，我和你们一起走。";
                case "yanzhuying": return "旧账还在。这一路，先把碍事的东西清开。";
                case "lingfeng": return "有我这把刀，大家一起往前！";
                case "cangling": return "沧泠在掌心写：都跟上了。";
                case "shangshuo": return "你们放心向前，我护住后面。";
                case "ruanzhuo": return "歇够了就上路，记得带一碗热汤。";
                default: return string.Empty;
            }
        }

        public static readonly StoryEvent[] Events =
        {
            new StoryEvent("witness", "路边热汤",
                "街边食堂支起汤锅，老板向小队挥手。\n\n" +
                "‘喝碗汤再走，或者带件合用的旧物。’",
                "喝热汤 · 全队恢复 4 生命", "拿旧物 · 获得 1 件遗物",
                "热汤下肚，大家缓过一口气。全队恢复 4 点生命。",
                "老板递来一件合用的旧物。获得 1 件遗物。"),
            new StoryEvent("dream", "帘下歇脚",
                "黑帘下有三张空凳，工人递来热饼和修好的旧物。\n\n" +
                "‘歇一会儿再走，或者挑件合用的东西。’",
                "吃饼歇脚 · 全队恢复 4 生命", "拿旧物 · 获得 1 件遗物",
                "小队吃完热饼，重新站起身。全队恢复 4 点生命。",
                "工人挑出一件修好的旧物交给小队。获得 1 件遗物。"),
            new StoryEvent("water", "渠边补给",
                "回水渠边摆着药布和工具箱。\n\n" +
                "医者朝小队招手：‘要包扎，还是带件旧物？’",
                "包扎伤口 · 全队恢复 4 生命", "拿旧物 · 获得 1 件遗物",
                "医者为大家包扎好伤口。全队恢复 4 点生命。",
                "小队带上一件合用的旧物，继续出发。获得 1 件遗物。"),
            // —— 深化 P1 事件 ——
            new StoryEvent("dreamshop", "补梦铺的旧梦",
                "补梦铺的柜台上摆着一只旧枕头，老板娘压低声音：\n\n" +
                "‘陪它坐一会儿，或者花 25 金币听个故事——故事里有本事。’",
                "听故事 · 25 金币换一次领悟", "收下枕头 · 接下来两场战斗后各恢复 6",
                "大家听完故事，有人默默记下了新的手法。随机领悟一项技能。",
                "枕头送给了最能睡的同伴。接下来两场战斗胜利后全队恢复 6 生命。"),
            new StoryEvent("vendor", "落闸的货郎",
                "货郎的车轮卡在落下的闸轨里，满头大汗地朝小队招手。\n\n" +
                "‘搭把手！酬金从袋子里拿，别客气！’",
                "帮推车 · 全队 -4 生命，获得 45 金币", "绕行赶路 · 相安无事",
                "大家一起把车抬过闸轨。货郎数出 45 金币，全队累得喘气（-4 生命）。",
                "闸轨太高，货郎自己想办法去了。小队继续赶路。"),
            new StoryEvent("potshare", "灯下同盟的分锅",
                "互助食堂的灶前支着一口大锅，香气飘出半条街。\n\n" +
                "‘捐 15 金币添柴，锅里管够；不捐也有一份。’",
                "添柴 · 15 金币，全队恢复 12 生命", "吃一份 · 全队恢复 8 生命",
                "灶火烧旺，大家喝到锅底见天。全队恢复 12 生命。",
                "阮灼笑着给每人盛了一份：‘慢点喝，管够。’全队恢复 8 生命。"),
            new StoryEvent("darkrest", "检修暗息",
                "前方的街灯忽然熄了三息——昼轮检修的例行暗息。\n\n" +
                "黑暗里有人提议：歇口气，还是趁机赶段路？",
                "静默三息 · 全队获得 15 蓄势", "趁暗行事 · 获得 30 金币",
                "黑暗中大家调匀了呼吸，睁眼时蓄势满满。全队获得 15 蓄势。",
                "借着黑暗的掩护，小队快步穿过了关卡。获得 30 金币。"),
            new StoryEvent("valveevent", "失控的闸阀",
                "一只闸阀嘶嘶地喷着蒸汽，阀轮眼看要崩开。\n\n" +
                "‘关得住它有赏；关不住可要挨烫。’",
                "冒险关闭 · 七成得稀有旧物，三成全队 -8 生命", "呼叫工班 · 获得 10 金币",
                "阀轮被合力压住，阀门后藏着一件事先封存的稀有旧物！",
                "蒸汽烫到了几个人（全队 -8 生命），但没人受重伤。工班赶到后接手了。"),
            new StoryEvent("pearlecho", "沉珠的回声",
                "渠水深处传来规律的回声，只有沧泠听得清。\n\n" +
                "‘回声里有后面几层的动静——想听，还是先上路？’",
                "听潮备战 · 下一战全队 +5 护盾、+10 蓄势", "拾起沉珠 · 获得 20 金币",
                "沧泠听出前方敌障的脚步，提醒伙伴们提前备战。",
                "沉珠被收进行囊，换成了实实在在的盘缠。获得 20 金币。"),
            new StoryEvent("moonclue", "旧账线索",
                "晏烛影在灯下认出了半页熟悉的账迹。\n\n" +
                "‘追下去能拿回欠款；不追，这一程我出蓄势。’",
                "追账 · 获得 60 金币与一件诅咒旧物", "放走 · 晏烛影接下来三场开局 +20 蓄势",
                "欠款连本带利追了回来——和那本不祥的旧账一起。获得 60 金币与一件诅咒旧物。",
                "晏烛影收起针剑：‘这一程，我出力。’接下来三场她开局获得 20 蓄势。"),
            new StoryEvent("grainhaul", "烈山送粮",
                "凌风的同门押着粮车停在巷口，看见他就喊：\n\n" +
                "‘师兄！搭把手，或者给我们指个路？’",
                "亲自扛粮 · 凌风 -6 生命，获得一件普通旧物", "托人送 · 获得 20 金币",
                "凌风扛起粮袋一路小跑，顺路从车上拿了件旧物（凌风 -6 生命）。",
                "粮车有人接手，同门塞了些盘缠。获得 20 金币。"),
            new StoryEvent("archiveseal", "天衡封存",
                "司玄认出了现场一件需要封存的证物。\n\n" +
                "‘封存要花时间核校；先放行，署里的酬金照发。’",
                "封存核校 · 一项已学技能提升一级", "先放行 · 获得 15 金币",
                "核校完毕，封存的卷宗里夹着一条早已失传的手法记录。随机一项已学技能提升一级。",
                "证物随队放行，署里的酬金很快送到。获得 15 金币。"),
            new StoryEvent("nailreturn", "界钉归还",
                "商朔认出界钉匣里混进了一枚早年借出的旧钉。\n\n" +
                "‘物归原主能换些人情；留下也自有用处。’",
                "物归原主 · 获得一件稀有旧物", "留作纪念 · 获得 15 金币",
                "旧钉物归原主，对方回赠了一件珍藏的稀有旧物。",
                "旧钉留在了匣里。获得 15 金币。"),
            new StoryEvent("dicestall", "灰灯巷骰摊",
                "巷口的骰摊围着几个下工的工人，庄家朝小队扬扬下巴。\n\n" +
                "‘押 20 金币，猜大小。不玩也能捡点漏。’",
                "押一把 · 一半翻倍，一半输光", "捡漏 · 获得 5 金币",
                "骰盅揭开——赢了！20 金币变成了 40。",
                "骰盅揭开——输了。小队摇摇头离开，顺路捡到 5 金币。"),
            new StoryEvent("pipeleak", "热管裂口",
                "热管裂口喷着细汽，检修口旁放着一只用旧的烬芯炉。\n\n" +
                "‘引走蒸汽的人可以拿走它；封堵也有工钱。’",
                "引导蒸汽 · 获得烬芯炉", "封堵裂口 · 获得 15 金币",
                "蒸汽被引进废管，烬芯炉完好无损地留在了大家手里。",
                "裂口封好了。工头数出 15 金币工钱。"),
            new StoryEvent("kite", "孩子的纸鸢",
                "一只纸鸢挂在管廊高处，主人是个眼眶发红的孩子。\n\n" +
                "‘爬上去取会蹭伤；不取，孩子会记很久。’",
                "取回纸鸢 · 全队 -3 生命，接下来三场开局 +5 护盾", "婉拒离开 · 获得 10 金币",
                "纸鸢回到了孩子手里。大家的胳膊蹭伤了（全队 -3 生命），接下来三场战斗开局获得 5 护盾。",
                "孩子被安抚着回家了。工棚主塞来 10 金币谢意。"),
            new StoryEvent("oldxu", "拾荒的老许",
                "老许在废料堆里翻拣，头也不抬：\n\n" +
                "‘想打听前面几层的门道？老规矩，10 金币。’",
                "打听门道 · 支付 10 金币；下一战全队 +4 护盾、+10 蓄势", "点头致意 · 相安无事",
                "老许把前面几层的门道一五一十说了。情报花得值。",
                "老许摆摆手继续翻他的废料堆。小队继续赶路。"),
            new StoryEvent("genealogy", "缺页的祖谱",
                "凌风在旧书摊上翻到一页刀法手记，摊主认出了他的佩刀。\n\n" +
                "‘有缘就抄一份；若急着赶路，带些干粮也好。’",
                "抄录手记 · 凌风领悟或升级「引燎」", "收起 · 获得 20 金币",
                "凌风照着缺页练了一遍，刀势里多了些火候。他领悟了「引燎」。",
                "祖谱放回原处。书摊主人送的干粮值 20 金币。")
        };
        // The original v3/revision-1 seed contract used exactly these three entries.
        // Keep that pool independent of additions to the modern event catalogue.
        public static readonly StoryEvent[] OriginalEvents =
        {
            Array.Find(Events,e=>e.id=="witness"),
            Array.Find(Events,e=>e.id=="dream"),
            Array.Find(Events,e=>e.id=="water")
        };
        // 人物事件需要对应角色在场；进节点时按小队替换为普通事件。
        public static string EventGate(string eventId)
        {
            switch(eventId)
            {
                case "pearlecho":return "cangling";
                case "moonclue":return "yanzhuying";
                case "grainhaul":case "genealogy":return "lingfeng";
                case "archiveseal":return "sixuan";
                case "nailreturn":return "shangshuo";
                default:return "";
            }
        }
    }
}
