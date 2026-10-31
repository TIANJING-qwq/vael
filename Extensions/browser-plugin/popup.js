// ============================================================
// 默认值
// ============================================================
const DEFAULT_KEYWORDS = [
  "飞机", "客机", "民航", "航空", "机场", "航班", "飞行",
  "飞行员", "空姐", "空少", "登机", "值机", "起飞", "降落",
  "航线", "跑道", "塔台", "航站楼", "头等舱", "经济舱",
  "商务舱", "空中", "云端", "机翼", "机身", "尾翼",
  "驾驶舱", "黑匣子", "空中管制", "空管",

  "波音", "空客", "商飞", "庞巴迪", "巴航工业",
  "波音737", "波音747", "波音757", "波音767", "波音777",
  "波音787", "B737", "B747", "B757", "B767",
  "B777", "B787", "737MAX",

  "空客A320", "空客A330", "空客A340", "空客A350", "空客A380",
  "A320", "A330", "A340", "A350", "A380", "A320neo", "A350XWB",

  "C919", "C929", "ARJ21", "新舟60",

  "歼20", "歼35", "歼16", "歼15", "歼10", "运20", "运9",
  "轰6", "轰20", "直20", "直10", "空警2000", "空警500",
  "歼击机", "战斗机", "轰炸机", "运输机", "预警机",
  "侦察机", "加油机", "教练机", "武装直升机",

  "直升机", "私人飞机", "公务机", "湾流", "塞斯纳",
  "轻型飞机", "滑翔机", "热气球", "飞艇",

  "Flightradar", "飞常准", "航旅纵横", "民航局",
  "星空联盟", "天合联盟",

  "国航", "东航", "南航", "海航", "厦航", "川航", "深航",
  "吉祥航空", "春秋航空",

  "全日空", "大韩航空", "新加坡航空", "国泰航空",
  "阿联酋航空", "汉莎航空",

  "空难", "迫降", "航空事故", "飞行模拟", "模拟飞行"
];

const DEFAULT_XIYUE_ENDPOINT = "http://127.0.0.1:18520/notify";

const $ = (id) => document.getElementById(id);

function setStatus(msg, type) {
  const el = $("status");
  if (!el) return;
  el.className = "status show " + (type || "info");
  el.textContent = msg;

  if (type === "ok" || type === "err") {
    clearTimeout(el._timer);
    el._timer = setTimeout(() => {
      el.className = "status";
    }, 3000);
  }
}

function loadSettings() {
  try {
    chrome.storage.local.get(
      [
        "config",
        "webhookEnabled", "webhookUrl", "webhookFormat",
        "xiyueEnabled", "xiyueEndpoint"
      ],
      (result) => {
        if (chrome.runtime.lastError) {
          setStatus("读取失败：" + chrome.runtime.lastError.message, "err");
          return;
        }

        const cfg = result.config || {};

        $("enabled").checked = cfg.enabled !== false;
        $("notifyEnabled").checked = cfg.notifyEnabled !== false;
        $("webhookEnabled").checked = result.webhookEnabled === true;
        $("xiyueEnabled").checked = result.xiyueEnabled === true;

        const keywords = (Array.isArray(cfg.keywords) && cfg.keywords.length > 0)
          ? cfg.keywords
          : DEFAULT_KEYWORDS;
        $("keywords").value = keywords.join("\n");

        $("xiyueEndpoint").value =
          result.xiyueEndpoint || DEFAULT_XIYUE_ENDPOINT;

        $("webhookUrl").value = result.webhookUrl || "";
        $("webhookFormat").value = result.webhookFormat || "json";

        setStatus(`已加载：${keywords.length} 个关键词`, "info");
      }
    );
  } catch (e) {
    setStatus("初始化失败：" + e.message, "err");
    console.error(e);
  }
}

function saveSettings() {
  try {
    const keywords = $("keywords").value
      .split("\n")
      .map((s) => s.trim())
      .filter((s) => s.length > 0);

    if (keywords.length === 0) {
      setStatus("至少需要一个关键词", "err");
      return;
    }

    const config = {
      enabled: $("enabled").checked,
      notifyEnabled: $("notifyEnabled").checked,
      keywords: keywords
    };

    chrome.storage.local.set({
      config: config,
      webhookEnabled: $("webhookEnabled").checked,
      webhookUrl: $("webhookUrl").value.trim(),
      webhookFormat: $("webhookFormat").value,
      xiyueEnabled: $("xiyueEnabled").checked,
      xiyueEndpoint: $("xiyueEndpoint").value.trim() || DEFAULT_XIYUE_ENDPOINT
    }, () => {
      if (chrome.runtime.lastError) {
        setStatus("保存失败：" + chrome.runtime.lastError.message, "err");
        return;
      }
      setStatus(`✓ 已保存（${keywords.length} 关键词）`, "ok");
    });
  } catch (e) {
    setStatus("保存异常：" + e.message, "err");
    console.error(e);
  }
}

async function testXiyue() {
  try {
    const url = $("xiyueEndpoint").value.trim() || DEFAULT_XIYUE_ENDPOINT;

    setStatus("正在连接汐月...", "info");

    const resp = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        title: "测试通知",
        message: "来自浏览器插件的测试消息"
      })
    });

    if (resp.ok) {
      const data = await resp.json().catch(() => ({}));
      if (data && data.ok) {
        setStatus("✓ 汐月已接收测试消息", "ok");
      } else {
        setStatus("✓ 汐月响应 " + resp.status, "ok");
      }
    } else {
      setStatus("✗ 汐月返回 " + resp.status, "err");
    }
  } catch (e) {
    setStatus("✗ 无法连接汐月：" + e.message, "err");
  }
}

async function testWebhook() {
  try {
    const url = $("webhookUrl").value.trim();
    if (!url) {
      setStatus("请先填写 Webhook URL", "err");
      return;
    }

    const fmt = $("webhookFormat").value;
    let body;

    if (fmt === "text") {
      body = JSON.stringify({ text: "【测试】Bing 关键词演示" });
    } else if (fmt === "feishu") {
      body = JSON.stringify({
        msg_type: "text",
        content: { text: "【测试】Bing 关键词演示" }
      });
    } else {
      body = JSON.stringify({
        event: "test",
        message: "Bing 关键词演示扩展 Webhook 测试",
        timestamp: Date.now()
      });
    }

    setStatus("正在发送...", "info");

    const resp = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: body
    });

    if (resp.ok) {
      setStatus(`✓ Webhook 响应 ${resp.status}`, "ok");
    } else {
      setStatus(`✗ Webhook 失败 ${resp.status}`, "err");
    }
  } catch (e) {
    setStatus("✗ 网络错误：" + e.message, "err");
  }
}

function viewHistory() {
  chrome.storage.local.get(["history"], (result) => {
    const history = result.history || [];
    if (history.length === 0) {
      setStatus("暂无历史记录", "info");
      return;
    }

    const lines = history.slice(0, 15).map((h) => {
      const d = new Date(h.timestamp || Date.now());
      return `[${d.toLocaleString()}] ${h.keyword} ← "${h.query}"`;
    });

    const text = "最近 15 条记录：\n\n" + lines.join("\n");
    alert(text);
  });
}

function resetDefaults() {
  if (!confirm("恢复默认关键词？")) return;

  $("keywords").value = DEFAULT_KEYWORDS.join("\n");

  setStatus("已恢复默认，点击「保存设置」生效", "ok");
}

document.addEventListener("DOMContentLoaded", () => {
  try {
    $("save").addEventListener("click", saveSettings);
    $("testWebhook").addEventListener("click", testWebhook);
    $("testXiyue").addEventListener("click", testXiyue);
    $("viewHistory").addEventListener("click", viewHistory);
    $("resetDefaults").addEventListener("click", resetDefaults);

    loadSettings();
  } catch (e) {
    console.error("[popup] 初始化失败", e);
  }
});