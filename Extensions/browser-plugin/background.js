const TRIGGER_COOLDOWN_MS = 30 * 1000;
const DEFAULT_XIYUE_ENDPOINT = "http://127.0.0.1:18520/notify";

const lastTrigger = {};
let lastCriticalUrlTrigger = 0;

chrome.runtime.onMessage.addListener((msg) => {
  // ===== 关键网址：seatmaps.com，按“网址”命中，格式与关键词一致 =====
  if (msg?.type === "critical-url-detected") {
    const now = Date.now();
    if (now - lastCriticalUrlTrigger < 5000) return;
    lastCriticalUrlTrigger = now;

    const payload = {
      keyword: msg.keyword || "网址",
      query: msg.url,
      pageUrl: msg.pageUrl,
      timestamp: msg.timestamp
    };

    // 通知汐月：格式与关键词触发完全一致
    notifyXiYue(payload);

    // Webhook：event 和字段与关键词触发一致，只是 keyword 为“网址”
    sendWebhook({
      event: "keyword-detected",
      keyword: payload.keyword,
      query: payload.query,
      pageUrl: payload.pageUrl,
      timestamp: payload.timestamp,
      detectedAt: new Date(payload.timestamp).toISOString()
    });

    // 历史记录
    saveHistory({
      keyword: payload.keyword,
      query: payload.query,
      timestamp: payload.timestamp
    });

    return;
  }

  // ===== Bing 关键词检测 =====
  if (msg?.type !== "keyword-detected") return;

  const {
    keyword, query, notifyEnabled, pageUrl, timestamp
  } = msg;

  const now = Date.now();
  if (lastTrigger[keyword] && now - lastTrigger[keyword] < TRIGGER_COOLDOWN_MS) {
    return;
  }
  lastTrigger[keyword] = now;

  if (notifyEnabled !== false) {
    try {
      chrome.notifications.create("kw-" + now, {
        type: "basic",
        iconUrl: "icon.png",
        title: "关键词触发",
        message: `检测到「${keyword}」\n搜索：${query}`,
        priority: 1
      });
    } catch (e) { }
  }

  notifyXiYue({ keyword, query, pageUrl, timestamp });

  sendWebhook({
    event: "keyword-detected",
    keyword,
    query,
    pageUrl,
    timestamp,
    detectedAt: new Date(timestamp).toISOString()
  });

  saveHistory({ keyword, query, timestamp });
});

// ============================================================
// 发送给汐月：保持原格式，title 为“关键词命中：<keyword>”
// ============================================================
function notifyXiYue(payload) {
  chrome.storage.local.get(["xiyueEnabled", "xiyueEndpoint"], (result) => {
    if (result.xiyueEnabled !== true) return;

    const endpoint = (result.xiyueEndpoint || DEFAULT_XIYUE_ENDPOINT).trim();
    if (!endpoint) return;

    const body = JSON.stringify({
      title: `关键词命中：${payload.keyword}`,
      message:
        `搜索：${payload.query}\n` +
        `来源：${payload.pageUrl}`
    });

    fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body
    })
      .then((r) => r.json())
      .then((d) => {
        if (d?.ok) console.log("[汐月] 已接收通知");
        else console.warn("[汐月] 返回异常", d);
      })
      .catch((err) => {
        console.warn("[汐月] 连接失败（汐月可能未启动）", err);
      });
  });
}

// ============================================================
// Webhook：保持原格式
// ============================================================
function sendWebhook(payload) {
  chrome.storage.local.get(["webhookUrl", "webhookEnabled", "webhookFormat"], (result) => {
    const { webhookUrl, webhookEnabled, webhookFormat } = result;
    if (!webhookEnabled || !webhookUrl) return;

    let body;
    const text = `【关键词触发】${payload.keyword}\n搜索：${payload.query}\n时间：${payload.detectedAt}`;

    if (webhookFormat === "text") {
      body = JSON.stringify({ text });
    } else if (webhookFormat === "feishu") {
      body = JSON.stringify({
        msg_type: "text",
        content: { text }
      });
    } else {
      body = JSON.stringify(payload);
    }

    fetch(webhookUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body
    }).catch((err) => console.error("[Bing演示] Webhook 失败", err));
  });
}

// ============================================================
// 历史记录
// ============================================================
function saveHistory(entry) {
  chrome.storage.local.get(["history"], (result) => {
    const history = result.history || [];
    history.unshift(entry);
    if (history.length > 100) history.length = 100;
    chrome.storage.local.set({ history });
  });
}

// ============================================================
// 安装初始化
// ============================================================
chrome.runtime.onInstalled.addListener(() => {
  chrome.storage.local.get(["config"], (result) => {
    if (!result.config) {
      chrome.storage.local.set({
        config: {
          enabled: true,
          notifyEnabled: true
        },
        xiyueEnabled: true,
        xiyueEndpoint: DEFAULT_XIYUE_ENDPOINT
      });
    }
  });
});