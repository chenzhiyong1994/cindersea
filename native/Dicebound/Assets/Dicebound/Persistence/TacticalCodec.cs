using System;
using System.IO;
using System.Linq;
using Dicebound.Tactics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Dicebound.Persistence
{
    public static class TacticalCodec
    {
        static readonly JsonSerializerSettings Settings=new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,MaxDepth=32,MissingMemberHandling=MissingMemberHandling.Ignore};
        public static string Encode(TacticalState state)
        {
            if(!TacticalValidation.Valid(state))throw new InvalidDataException("战棋档案状态不符合 v3 规则。");
            return JsonConvert.SerializeObject(new{format="dicebound-tactical",version=3,run=state.Clone()},Settings);
        }
        public static bool TryDecode(string text,out TacticalState state,out string error)
        {
            state=null;error="无法读取战棋 v3 档案，原文件已保留。";
            if(string.IsNullOrWhiteSpace(text)||text.Length>256000)return false;
            try {
                using(var reader=new JsonTextReader(new StringReader(text)){MaxDepth=32,DateParseHandling=DateParseHandling.None}){
                    var root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read())return false;
                    if((string)root["format"]!="dicebound-tactical"||!Integer(root["version"])||(int)root["version"]!=3||!(root["run"] is JObject run))return false;
                    if(!Types(run))return false;
                    var decoded=run.ToObject<TacticalState>(JsonSerializer.Create(Settings));if(!TacticalValidation.Valid(decoded))return false;
                    TacticalRules.RefreshShopOfferPresentation(decoded);state=decoded;error=null;return true;
                }
            }catch(Exception e) when(e is JsonException||e is ArgumentException||e is InvalidCastException||e is FormatException||e is OverflowException){return false;}
        }
        static bool Types(JObject s)
        {
            // New combat/shop fields are optional only for an original v3 checkpoint.
            // When present, reject coerced strings, decimals and partial revision/reroll groups.
            if((s["ascentRevision"]==null)!=(s["shopRerolls"]==null))return false;
            if(s["ascentRevision"]!=null&&(!Integer(s["ascentRevision"])||!Integer(s["shopRerolls"])))return false;
            bool currentAscent=s["ascentRevision"]!=null&&(int)s["ascentRevision"]>=1;
            bool depthAscent=s["ascentRevision"]!=null&&(int)s["ascentRevision"]>=2;
            bool expandedAscent=s["ascentRevision"]!=null&&(int)s["ascentRevision"]>=3;
            if(s["shopRevision"]!=null&&!Integer(s["shopRevision"]))return false;
            if(s["ascentRevision"]!=null&&(int)s["ascentRevision"]>=4&&!Integer(s["shopRevision"]))return false;
            // Depth revision 2 checkpoints must carry the combo/drench and streak fields in full.
            if(depthAscent&&new[]{"flawlessStreak","pendingShield","pendingCharge"}.Any(k=>!Integer(s[k])))return false;
            if(new[]{"flawlessStreak","pendingShield","pendingCharge"}.Any(k=>s[k]!=null&&!Integer(s[k])))return false;
            // P1 fields stay optional so earlier revision-2 checkpoints keep decoding; bounds live in validation.
            if(new[]{"trial","pillowBattles","zhangBattles","kiteBattles","immunityUsed"}.Any(k=>s[k]!=null&&!Integer(s[k])))return false;
            if(expandedAscent&&new[]{"trial","pillowBattles","zhangBattles","kiteBattles","immunityUsed"}.Any(k=>!Integer(s[k])))return false;
            // Missing ascent fields are original v3 checkpoints, with legacy defaults.
            string[] ascentFields={"journeyMode","currentNodeId","floor","gold","mapNodes","shopOffers"};
            if(ascentFields.Any(k=>s[k]!=null)){
                if(s["journeyMode"]?.Type!=JTokenType.String||s["currentNodeId"]?.Type!=JTokenType.String||!Integer(s["floor"])||!Integer(s["gold"])||!(s["mapNodes"] is JArray nodes)||!(s["shopOffers"] is JArray offers))return false;
                foreach(var value in nodes){if(!(value is JObject n)||new[]{"id","kind","name","text","eventId"}.Any(k=>n[k]?.Type!=JTokenType.String)||new[]{"floor","lane","chapter"}.Any(k=>!Integer(n[k]))||n["visited"]?.Type!=JTokenType.Boolean||n["completed"]?.Type!=JTokenType.Boolean||!(n["next"] is JArray next)||next.Any(v=>v.Type!=JTokenType.String))return false;}
                foreach(var value in offers){if(!(value is JObject o)||new[]{"id","kind","name","text","skillId","relicId"}.Any(k=>o[k]?.Type!=JTokenType.String)||(currentAscent||o["heroId"]!=null)&&o["heroId"]?.Type!=JTokenType.String||!Integer(o["price"])||o["sold"]?.Type!=JTokenType.Boolean)return false;}
            }
            // Original v3 saves omitted both dimensions. Partial or coerced dimensions are corruption.
            if((s["boardWidth"]==null)!=(s["boardHeight"]==null))return false;
            if(s["boardWidth"]!=null&&(!Integer(s["boardWidth"])||!Integer(s["boardHeight"])))return false;
            if(new[]{"version","revision","chapter","round","objectiveX","objectiveY","objectiveProgress","objectiveRequired","surviveRounds","objectiveTurn","seed","rng"}.Any(k=>!Integer(s[k])))return false;
            if(new[]{"phase","nodeKind","eventId"}.Any(k=>s[k]?.Type!=JTokenType.String)||s["sideBattle"]?.Type!=JTokenType.Boolean)return false;
            if(new[]{"squad","relics","log"}.Any(k=>!(s[k] is JArray a)||a.Any(v=>v.Type!=JTokenType.String)))return false;
            if(!(s["terrain"] is JArray terrain)||terrain.Any(v=>!(v is JObject c)||!Integer(c["x"])||!Integer(c["y"])||c["kind"]?.Type!=JTokenType.String))return false;
            if(!(s["objects"] is JArray objects)||objects.Any(v=>!(v is JObject o)||new[]{"id","kind"}.Any(k=>o[k]?.Type!=JTokenType.String)||new[]{"x","y","hp"}.Any(k=>!Integer(o[k]))))return false;
            if(!(s["units"] is JArray units))return false;
            foreach(var value in units){if(!(value is JObject u))return false;
                if(depthAscent&&new[]{"drenched","comboMask"}.Any(k=>!Integer(u[k])))return false;
                if(new[]{"drenched","comboMask"}.Any(k=>u[k]!=null&&!Integer(u[k])))return false;
                if(u["affixes"]!=null&&u["affixes"].Type!=JTokenType.String)return false;
                if(u["tauntedBy"]!=null&&u["tauntedBy"].Type!=JTokenType.String)return false;
                if(new[]{"shellUsed","affixCounter","concealed"}.Any(k=>u[k]!=null&&!Integer(u[k])))return false;
                if(expandedAscent&&(u["affixes"]?.Type!=JTokenType.String||u["tauntedBy"]?.Type!=JTokenType.String||new[]{"shellUsed","affixCounter","concealed"}.Any(k=>!Integer(u[k]))))return false;
                if(currentAscent&&(u["turnEnded"]?.Type!=JTokenType.Boolean||u["enemyType"]?.Type!=JTokenType.String||new[]{"weakened","vulnerable","regeneration","damageBonus","retaliation","scorched","attackPower","attackRange","moveRange"}.Any(k=>!Integer(u[k]))))return false;
                if(u["turnEnded"]!=null&&u["turnEnded"].Type!=JTokenType.Boolean||u["enemyType"]!=null&&u["enemyType"].Type!=JTokenType.String)return false;
                if(new[]{"weakened","vulnerable","regeneration","damageBonus","retaliation","scorched","attackPower","attackRange","moveRange"}.Any(k=>u[k]!=null&&!Integer(u[k])))return false;
                if(new[]{"id","heroId","name","team"}.Any(k=>u[k]?.Type!=JTokenType.String)||new[]{"x","y","hp","maxHp","ap","maxAp","sp","maxSp","charge","block"}.Any(k=>!Integer(u[k]))||u["rooted"]?.Type!=JTokenType.Boolean)return false;
                if(!(u["skills"] is JArray skills)||skills.Any(v=>v.Type!=JTokenType.String)||!(u["cooldowns"] is JObject cd)||cd.Properties().Any(p=>!Integer(p.Value))||!(u["skillLevels"] is JObject levels)||levels.Properties().Any(p=>!Integer(p.Value)))return false;
            }
            if(!(s["routes"] is JArray routes)||routes.Any(v=>!(v is JObject r)||new[]{"id","kind","name","text"}.Any(k=>r[k]?.Type!=JTokenType.String)))return false;
            if(!(s["rewards"] is JArray rewards)||rewards.Any(v=>!(v is JObject r)||new[]{"id","kind","name","text","skillId","relicId"}.Any(k=>r[k]?.Type!=JTokenType.String)||(r["amount"]!=null&&!Integer(r["amount"]))))return false;
            return true;
        }
        static bool Integer(JToken token){return token!=null&&token.Type==JTokenType.Integer;}
    }
}
