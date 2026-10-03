using Dicebound.Tactics;

namespace Dicebound.Presentation
{
    /// <summary>Presentation-only score routing; never changes or persists journey state.</summary>
    public static class TacticalMusic
    {
        public static readonly string[] Keys={"title","travel","battle","elite","boss","victory","defeat"};

        public static string ThemeFor(TacticalState state)
        {
            if(state==null)return "title";
            switch(state.phase)
            {
                case "battle":
                    // Chapters include ordinary encounters near the summit. Only the node is a Boss.
                    if(state.journeyMode=="ascent")return state.nodeKind=="boss"?"boss":state.nodeKind=="elite"?"elite":"battle";
                    return state.chapter==4&&!state.sideBattle?"boss":state.nodeKind=="elite"?"elite":"battle";
                case "reward":case "victory":return "victory";
                case "defeat":return "defeat";
                default:return "travel";
            }
        }

        public static string TitleFor(string key)
        {
            switch(key)
            {
                case "title":return "江湖启程";
                case "travel":return "晴岚行路";
                case "battle":return "回水交锋";
                case "elite":return "强敌临阵";
                case "boss":return "天阙决战";
                case "victory":return "同袍凯旋";
                case "defeat":return "余烬再行";
                default:return "";
            }
        }
    }
}
