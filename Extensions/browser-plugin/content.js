(() => {
  // ===== 关键网址检测：seatmaps.com，命中词为“网址” =====
  const host = location.hostname.toLowerCase();
  if (host === "seatmaps.com" || host.endsWith(".seatmaps.com")) {
    if (!window.__criticalUrlTriggered) {
      window.__criticalUrlTriggered = true;
      try {
        chrome.runtime.sendMessage({
          type: "critical-url-detected",
          keyword: "网址",
          url: location.href,
          host: host,
          pageUrl: location.href,
          timestamp: Date.now()
        });
      } catch (e) {
        console.warn("[Bing演示] 关键网址消息发送失败", e);
      }
    }
    return;
  }

  // ===== Bing 关键词逻辑 =====
  let config = null;
  let lastHandledUrl = "";
  let keywordsLoaded = false;

  const DEFAULT_CONFIG = {
    enabled: true,
    keywords: [
      "飞机", "客机", "民航", "航空", "机场", "航班", "飞行",
      "飞行员", "空姐", "空少", "登机", "值机", "起飞", "降落",
      "航线", "跑道", "塔台", "航站楼", "头等舱", "经济舱",
      "商务舱", "空中", "云端", "机翼", "机身", "尾翼",
      "驾驶舱", "黑匣子", "空中管制", "空管", "塔台",

      "波音", "空客", "商飞", "庞巴迪", "巴航工业", "Embraer",
      "麦道", "麦道公司",

      "波音737", "波音747", "波音757", "波音767", "波音777",
      "波音787", "波音797", "B737", "B747", "B757", "B767",
      "B777", "B787", "737MAX", "737max",

      "空客A320", "空客A330", "空客A340", "空客A350", "空客A380",
      "空客A220", "A320", "A330", "A340", "A350", "A380", "A220",
      "A320neo", "A350XWB",

      "C919", "C929", "C939", "ARJ21", "新舟60", "新舟700",

      "歼20", "歼35", "歼16", "歼15", "歼10", "运20", "运9",
      "轰6", "轰20", "直20", "直10", "武直10", "空警2000",
      "空警500", "歼击机", "战斗机", "轰炸机", "运输机",
      "预警机", "侦察机", "加油机", "教练机", "武装直升机",

      "直升机", "私人飞机", "公务机", "湾流", "塞斯纳", "小飞机",
      "轻型飞机", "滑翔机", "热气球", "飞艇",

      "Flightradar", "Flightradar24", "飞常准", "航旅纵横",
      "民航局", "CAAC", "国际航空", "星空联盟", "天合联盟",
      "寰宇一家",

      "国航", "东航", "南航", "海航", "厦航", "川航", "深航",
      "吉祥航空", "春秋航空", "山东航空", "天津航空", "河北航空",
      "中国国际航空", "东方航空", "南方航空",

      "波音客服", "空客客服", "美联航", "达美航空", "美航",
      "全日空", "日本航空", "大韩航空", "韩亚航空", "新加坡航空",
      "国泰航空", "阿联酋航空", "卡塔尔航空", "汉莎航空",
      "法航", "荷航", "英航", "澳航",

      "空难", "迫降", "备降", "航空事故", "飞机失事", "航空安全",
      "驾驶", "飞行模拟", "飞行游戏", "模拟飞行", "FSX", "X-Plane",
      "微软模拟飞行",

      "涡扇发动机", "涡桨发动机", "喷气发动机", "涡轮",
      "普惠", "通用电气", "罗罗", "GE航空", "罗尔斯罗伊斯"
    ],
    notifyEnabled: true
  };

  function normalizeConfig(raw) {
    const cfg = { ...DEFAULT_CONFIG, ...raw };
    if (!Array.isArray(cfg.keywords)) cfg.keywords = DEFAULT_CONFIG.keywords;
    return cfg;
  }

  function loadConfig(cb) {
    chrome.storage.local.get(["config"], (result) => {
      let stored = result.config;

      if (!stored) {
        chrome.storage.local.get(["rules", "enabled"], (old) => {
          if (old.rules) {
            const keywords = Object.keys(old.rules);
            stored = {
              enabled: old.enabled !== false,
              keywords: keywords.length ? keywords : DEFAULT_CONFIG.keywords,
              notifyEnabled: true
            };
          } else {
            stored = DEFAULT_CONFIG;
          }
          chrome.storage.local.set({ config: stored });
          config = normalizeConfig(stored);
          keywordsLoaded = true;
          if (cb) cb();
        });
        return;
      }

      config = normalizeConfig(stored);
      keywordsLoaded = true;
      if (cb) cb();
    });
  }

  function checkAndTrigger() {
    if (!config || !config.enabled) return;

    const q = new URL(location.href).searchParams.get("q") || "";
    if (!q) return;

    if (lastHandledUrl === location.href) return;

    const query = q.toLowerCase();
    for (const keyword of config.keywords) {
      if (!keyword) continue;
      if (query.includes(keyword.toLowerCase())) {
        lastHandledUrl = location.href;

        try {
          chrome.runtime.sendMessage({
            type: "keyword-detected",
            keyword: keyword,
            query: q,
            notifyEnabled: config.notifyEnabled,
            pageUrl: location.href,
            timestamp: Date.now()
          });
        } catch (e) {
          console.warn("[Bing演示] 发送消息失败", e);
        }
        return;
      }
    }
  }

  loadConfig(() => checkAndTrigger());

  let lastHref = location.href;
  const observer = new MutationObserver(() => {
    if (location.href !== lastHref) {
      lastHref = location.href;
      setTimeout(() => {
        if (keywordsLoaded) checkAndTrigger();
        else loadConfig(() => checkAndTrigger());
      }, 300);
    }
  });
  observer.observe(document.documentElement, { childList: true, subtree: true });

  window.addEventListener("popstate", () => {
    setTimeout(() => {
      if (keywordsLoaded) checkAndTrigger();
    }, 300);
  });
})();