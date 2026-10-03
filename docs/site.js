"use strict";

(() => {
  const english = {
    skip: "Skip to content", brandSub: "烬海天阙", brandHome: "Dicebound home", navLabel: "Main navigation", navJourney: "The ascent", navCompanions: "Companions", navBeta: "About the beta",
    heroTag: "FIRST BETA · OPEN-SOURCE TACTICAL ROGUELIKE", heroLine: "Three companions. Twelve floors. One shared ascent.",
    heroDescription: "Enter Yaojing, a city of mountain kingdoms and ancient machinery. Choose your companions. Turn terrain, skills and relics into a winning strategy. Every floor brings a different possibility.",
    source: "Explore the source", download: "Download Windows Beta", portable: "Portable ZIP", checksums: "SHA-256 checksums", releaseNotes: "Release notes", downloadLinksLabel: "Other downloads and release information", singlePlayer: "Single-player · Chinese UI", scroll: "Discover the journey", factsLabel: "Beta content",
    factParty: "Five companions. A party of three.", factFloors: "Floors of branching routes", factContent: "Relics and journey events", factDifficulty: "Ascent difficulties",
    journeyKicker: "01 / THE ASCENT", journeyTitle: "Every step shapes the battle.", journeyIntro: "Read the field. Find your opening.<br>Bring tactics and builds together on every ascent.",
    battleAlt: "Development gameplay: three companions face mechanical enemies on a stone bridge battlefield, with skill cards along the bottom.", battleCaption: "Development gameplay · The interface may differ from the current beta",
    tacticTitle: "Make terrain part of your strategy", tacticText: "Use elevation, mist and shadow. Read enemy intentions before choosing your position and attack order. End each companion’s turn individually to coordinate the whole party.", momentum: "Momentum",
    buildTitle: "Grow a build of your own", buildText: "Learn character-specific skills and level them up through repeated acquisitions. Shop rerolls, relic combinations and event choices let the same party find new ways to fight.", skillsTag: "Skill progression", relicsTag: "Relic synergies",
    ascentTitle: "Choose the path you climb", ascentText: "Navigate battles, elites, shops, events and campfires before the final confrontation. Four trials await: Mistwalker, Barrierbreaker, Against the Tide and Skyward Ascent.", routeTag: "Branching routes", bossTag: "Distinct elites & bosses",
    companionsKicker: "02 / COMPANIONS", companionsTitle: "Five strengths. Three paths entwined.", companionsIntro: "Distinct skills. Different roles.<br>Select a companion to discover their place in the party.", selectCompanion: "Select a companion",
    nameSixuan: "Si Xuan", nameLingfeng: "Ling Feng", nameCangling: "Cang Ling", nameYanzhuying: "Yan Zhuying", nameShangshuo: "Shang Shuo",
    roleSixuan: "Control · Protection", roleLingfeng: "Melee · Disruption", roleCangling: "Healing · Tides", roleYanzhuying: "Shadows · Ambush", roleShangshuo: "Guard · Retaliation", ultimateLabel: "SIGNATURE ULTIMATE",
    betaKicker: "03 / FIRST BETA", betaTitle: "A first step.<br>An open invitation.", betaIntro: "This is Dicebound’s first open-source beta. Help shape its tactical feel, explore its builds, and refine the details of this hand-painted world.", feedback: "Share feedback or report an issue",
    platformLabel: "CURRENT PLATFORM", platformValue: "Windows x64 · Single-player · Simplified Chinese game UI", packageLabel: "INSTALL & PLAY", packageValue: "Neither package requires Unity. The executables are not code-signed; Windows may display a publisher or reputation warning.", engineLabel: "DEVELOPMENT STACK", statusLabel: "BETA SCOPE", statusValue: "Balance, interfaces and effects are still evolving. Full regression testing and broad device compatibility checks have not been completed.", licenseLabel: "SOURCE & LICENSES", licenseValue: "See the repository’s licenses and provenance notes for the terms applying to code, artwork and third-party content.",
    closingTitle: "The next step is yours.", readBuild: "Read the build guide →", footerNote: "Together, toward the sky.", assetSources: "Website asset sources"
  };

  const companions = {
    sixuan: {
      zh: ["天衡玉律", "以衡域约束敌人，以玉律护契守护同伴。控制、护盾与裁断相互衔接，为队伍创造从容出手的机会。", "天衡裁云", "范围定身 · 全队护盾"],
      en: ["Keeper of the Jade Law", "Bind enemies and protect allies. Control, shields and decisive strikes combine to give the party room to act.", "Heaven’s Verdict", "Area bind · Party shields"]
    },
    lingfeng: {
      zh: ["赤心刀", "用击退打乱敌阵，以刀势与余焰持续压迫。抓住残血目标的破绽，为同行者劈开前路。", "赤心燎原", "范围重击 · 余焰伤害"],
      en: ["The Crimson Blade", "Break enemy formations with knockback and keep the pressure on with sweeping strikes and lingering embers. Finish weakened foes to open the way.", "Crimson Wildfire", "Heavy area strike · Lingering embers"]
    },
    cangling: {
      zh: ["听潮王嗣", "借水雾施展潮汐之力，治疗同伴、清除负面状态。兼顾压制与补给，让队伍在长战中站稳脚步。", "静海回潮", "全队治疗 · 净化与护盾"],
      en: ["Heir of the Listening Tide", "Channel the mist to heal allies and cleanse harmful effects. Blend support with suppression to keep the party steady through a long battle.", "Still Sea’s Return", "Party healing · Cleanse & shields"]
    },
    yanzhuying: {
      zh: ["蚀月遗姬", "借阴影换位，以针刃寻找敌人的破绽。灵活切入，穿盾削弱，再为下一次奇袭留下余地。", "蚀月封喉", "穿盾突袭 · 削弱敌群"],
      en: ["Scion of the Eclipsed Moon", "Move through shadows and find an opening with needle and blade. Slip into position, pierce shields and weaken enemies for the next ambush.", "Eclipsing Strike", "Shield-piercing strike · Area weaken"]
    },
    shangshuo: {
      zh: ["镇渊玄戍", "以界钉定住敌人，以厚重护盾守住阵线。将承受的压力化作反击，为队伍撑起可靠的屏障。", "镇渊玄戍", "全队厚盾 · 近身反击"],
      en: ["Warden of the Deep", "Pin enemies in place and hold the line behind heavy shields. Turn incoming pressure into retaliation and give your companions a reliable defense.", "Warden’s Bulwark", "Heavy party shields · Melee retaliation"]
    }
  };

  const elements = [...document.querySelectorAll("[data-i18n]")];
  const originals = new Map(elements.map(element => [element, element.innerHTML]));
  const attributes = [...document.querySelectorAll("[data-i18n-aria], [data-i18n-alt]")].map(element => {
    const attr = element.hasAttribute("data-i18n-aria") ? "aria-label" : "alt";
    const key = element.dataset.i18nAria || element.dataset.i18nAlt;
    return {element, attr, key, original: element.getAttribute(attr)};
  });
  const tabs = [...document.querySelectorAll(".character")];
  const languageButton = document.getElementById("language");
  let language = new URLSearchParams(location.search).get("lang") === "en" ? "en" : "zh";
  let selected = "sixuan";

  function showCompanion(id) {
    if (!companions[id]) return;
    selected = id;
    const text = companions[id][language];
    tabs.forEach((tab, index) => {
      const active = tab.dataset.hero === id;
      tab.classList.toggle("active", active);
      tab.setAttribute("aria-selected", String(active));
      tab.tabIndex = active ? 0 : -1;
      if (active) document.getElementById("detail-number").textContent = String(index + 1).padStart(2, "0");
    });
    document.getElementById("character-detail").setAttribute("aria-labelledby", `tab-${id}`);
    ["detail-title", "detail-description", "detail-ultimate", "detail-ultimate-description"].forEach((id, index) => {
      document.getElementById(id).textContent = text[index];
    });
  }

  function translate() {
    document.documentElement.lang = language === "en" ? "en" : "zh-CN";
    elements.forEach(element => {
      element.innerHTML = language === "en" ? (english[element.dataset.i18n] || originals.get(element)) : originals.get(element);
    });
    attributes.forEach(({element, attr, key, original}) => element.setAttribute(attr, language === "en" ? english[key] : original));
    languageButton.textContent = language === "en" ? "中文" : "EN";
    languageButton.lang = language === "en" ? "zh-CN" : "en";
    languageButton.setAttribute("aria-label", language === "en" ? "切换至中文" : "Switch to English");
    document.title = language === "en" ? "Dicebound · 烬海天阙 — First Beta" : "烬海天阙 · Dicebound — First Beta";
    document.querySelector('meta[name="description"]').content = language === "en" ? "Three companions. Twelve floors. Dicebound is an open-source single-player tactical roguelike in a hand-painted fantasy world. First beta for Windows, with Simplified Chinese game UI." : "三人同行，十二层登阙。烬海天阙 Dicebound 是一款以手绘东方幻想世界为舞台的单人战棋 roguelike，首个 Beta 已开源。";
    showCompanion(selected);
  }

  languageButton.addEventListener("click", () => {
    language = language === "en" ? "zh" : "en";
    const url = new URL(location.href);
    if (language === "en") url.searchParams.set("lang", "en");
    else url.searchParams.delete("lang");
    history.replaceState(null, "", url);
    translate();
  });

  tabs.forEach((tab, index) => {
    tab.addEventListener("click", () => showCompanion(tab.dataset.hero));
    tab.addEventListener("keydown", event => {
      let next;
      if (event.key === "ArrowRight") next = (index + 1) % tabs.length;
      else if (event.key === "ArrowLeft") next = (index + tabs.length - 1) % tabs.length;
      else if (event.key === "Home") next = 0;
      else if (event.key === "End") next = tabs.length - 1;
      else return;
      event.preventDefault();
      showCompanion(tabs[next].dataset.hero);
      tabs[next].focus({preventScroll: true});
      tabs[next].scrollIntoView({block: "nearest", inline: "nearest", behavior: "instant"});
    });
  });
  translate();
})();
