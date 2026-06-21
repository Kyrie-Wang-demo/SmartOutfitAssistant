const state = { mood: "开心", wardrobe: [], history: [], settings: {} };
const $ = (s) => document.querySelector(s);
const $$ = (s) => Array.from(document.querySelectorAll(s));
const esc = (v) => String(v ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;");
const fmt = (v) => new Intl.DateTimeFormat("zh-CN", { month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" }).format(new Date(v));
const split = (v) => String(v || "").split(/[,，;；\s]+/).map(x => x.trim()).filter(Boolean);
function toast(msg) { const el = $("#toast"); el.textContent = msg; el.classList.add("show"); clearTimeout(toast.t); toast.t = setTimeout(() => el.classList.remove("show"), 2600); }
async function api(url, opt = {}) { const r = await fetch(url, opt); if (!r.ok) { let m = `请求失败 ${r.status}`; try { m = (await r.json()).error || m; } catch { } throw new Error(m); } return r.json(); }
function swatch(c) { return /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(c || "") ? `<span class="chip"><i style="width:12px;height:12px;border-radius:50%;background:${c};border:1px solid rgba(0,0,0,.12)"></i>${esc(c)}</span>` : `<span class="chip">${esc(c)}</span>`; }

async function loadSummary() {
  const s = await api("/api/summary");
  $("#metricWardrobe").textContent = s.wardrobeCount;
  $("#metricFav").textContent = s.favoriteWardrobeCount;
  $("#metricHistory").textContent = s.historyCount;
  $("#metricReuse").textContent = s.ownedPiecesUsed;
  $("#wardrobeCountHero").textContent = s.wardrobeCount;
}

async function loadSettings() {
  const s = state.settings = await api("/api/settings");
  $("#setDisplayName").value = s.displayName || "";
  $("#setDefaultOccasion").value = s.defaultOccasion || "日常";
  $("#setStylePreference").value = s.stylePreference || "";
  $("#setFitPreference").value = s.fitPreference || "舒适";
  $("#setPreferredColors").value = (s.preferredColors || []).join(", ");
  $("#setAvoidColors").value = (s.avoidColors || []).join(", ");
  $("#setOnlineImages").checked = !!s.enableOnlineImages;
  $("#setPreferWardrobe").checked = !!s.preferWardrobe;
  $("#occasion").value = s.defaultOccasion || "日常";
  $("#fitPreference").value = s.fitPreference || "舒适";
  $("#preferWardrobe").checked = !!s.preferWardrobe;
}

async function loadWardrobe() {
  const q = encodeURIComponent($("#wardrobeSearch")?.value || "");
  const c = encodeURIComponent($("#wardrobeFilter")?.value || "");
  state.wardrobe = await api(`/api/wardrobe?q=${q}&category=${c}`);
  $("#wardrobeEmpty").style.display = state.wardrobe.length ? "none" : "block";
  $("#wardrobeGrid").innerHTML = state.wardrobe.map(i => `
    <article class="wardrobe-item">
      <div class="card-actions"><button class="icon-btn" data-edit="${esc(i.id)}">编辑</button><button class="icon-btn" data-delete="${esc(i.id)}">删除</button></div>
      <img class="wardrobe-img" src="${esc(i.imagePath)}" alt="${esc(i.name)}" loading="lazy"/>
      <div class="wardrobe-body">
        <h3>${i.favorite ? "★ " : ""}${esc(i.name)}</h3>
        <p>${esc(i.category)} · ${esc(i.material)} · ${esc(i.season)} · ${esc(i.occasion)}</p>
        <div class="meta">${swatch(i.color)}${(i.tags || []).slice(0, 4).map(t => `<span class="chip">#${esc(t)}</span>`).join("")}<span class="chip">${fmt(i.updatedAt)}</span></div>
      </div>
    </article>`).join("");
}

async function loadHistory() {
  state.history = await api("/api/history");
  $("#historyEmpty").style.display = state.history.length ? "none" : "block";
  $("#historyList").innerHTML = state.history.map(h => `
    <article class="history-item">
      <div class="history-top">
        <div><p class="eyebrow">${esc(h.mood)} · ${esc(h.occasion)} · ${fmt(h.createdAt)}</p><h3>${h.favorite ? "★ " : ""}${esc(h.styleName)}</h3><p>${h.weather.temperature}℃ ${esc(h.weather.condition)}，衣柜 ${h.ownedCount} 件，推荐 ${h.recommendedCount} 件</p></div>
        <div class="actions-row"><button class="btn subtle" data-replay="${esc(h.id)}">查看</button><button class="btn subtle" data-fav-history="${esc(h.id)}">收藏</button></div>
      </div>
      <div class="history-pieces">${h.pieces.map(p => `<span class="chip">${esc(p.slot)}：${esc(p.color)}${esc(p.category)}</span>`).join("")}</div>
    </article>`).join("");
}

function renderResult(data) {
  const pieces = data.pieces.map(p => {
    const fallback = "https://loremflickr.com/720/960/fashion,outfit";
    const img = p.imagePath ? `<img class="wardrobe-img" src="${esc(p.imagePath)}" alt="${esc(p.name)}" loading="lazy" referrerpolicy="no-referrer" onerror="this.onerror=null;this.src='${fallback}'" />` : `<div class="placeholder-img">✦</div>`;
    const meta = p.source === "衣柜" ? "" : `<div class="online-meta"><span>${esc(p.imageCredit || "联网参考图")}</span>${p.searchUrl ? `<a href="${esc(p.searchUrl)}" target="_blank" rel="noopener noreferrer">查看搜索</a>` : ""}</div>`;
    return `<article class="piece-card">${img}<div class="piece-body"><div class="piece-slot">${esc(p.slot)}</div><h3>${esc(p.name)}</h3><p>${esc(p.category)} · ${esc(p.color)} · ${esc(p.material)}</p><p class="source ${p.source === "衣柜" ? "own" : "buy"}">${p.source === "衣柜" ? "来自我的衣柜" : "推荐购买 · 已联网找图"}</p>${meta}<p>${esc(p.note)}</p></div></article>`;
  }).join("");
  $("#result").className = "result";
  $("#result").innerHTML = `<div class="result-header"><div><p class="eyebrow">${esc(data.mood)} · ${esc(data.occasion)} · ${esc(data.weather.condition)} · ${data.weather.temperature}℃</p><h2>${esc(data.styleName)}</h2><p>${esc(data.styleDescription)}</p></div><div class="stats"><span class="chip">衣柜 ${data.ownedCount} 件</span><span class="chip">补充 ${data.recommendedCount} 件</span><span class="chip">风 ${data.weather.windLevel} 级</span></div></div><div class="pieces-grid">${pieces}</div><div class="info-columns"><div class="info-box"><h3>搭配理由</h3><ul>${data.reasons.map(x => `<li>${esc(x)}</li>`).join("")}</ul></div><div class="info-box"><h3>天气调整</h3><ul>${data.weatherAdjustments.map(x => `<li>${esc(x)}</li>`).join("")}</ul></div><div class="info-box"><h3>购买建议</h3><ul>${data.shoppingTips.map(x => `<li>${esc(x)}</li>`).join("")}</ul></div></div>`;
  $("#todayTitle").textContent = data.styleName;
  $("#todaySummary").textContent = `${data.mood} · ${data.weather.temperature}℃ ${data.weather.condition} · 衣柜 ${data.ownedCount} 件`;
  $("#todayChips").innerHTML = data.pieces.slice(0, 5).map(x => `<span class="chip">${esc(x.slot)}</span>`).join("");
}

async function extractDominantColor(file) {
  return new Promise(resolve => {
    const img = new Image(), url = URL.createObjectURL(file);
    img.onload = () => {
      const c = document.createElement("canvas"), ctx = c.getContext("2d", { willReadFrequently: true });
      c.width = c.height = 64; ctx.drawImage(img, 0, 0, 64, 64);
      const d = ctx.getImageData(0, 0, 64, 64).data; let r = 0, g = 0, b = 0, n = 0;
      for (let i = 0; i < d.length; i += 16) { if (d[i + 3] < 128) continue; if (d[i] > 242 && d[i + 1] > 242 && d[i + 2] > 242) continue; r += d[i]; g += d[i + 1]; b += d[i + 2]; n++; }
      URL.revokeObjectURL(url); resolve(n ? `#${[r / n, g / n, b / n].map(v => Math.round(v).toString(16).padStart(2, "0")).join("")}` : "");
    };
    img.onerror = () => resolve(""); img.src = url;
  });
}

function openEdit(i) {
  $("#editId").value = i.id; $("#editName").value = i.name; $("#editCategory").value = i.category; $("#editColor").value = i.color; $("#editMaterial").value = i.material; $("#editSeason").value = i.season; $("#editOccasion").value = i.occasion; $("#editTags").value = (i.tags || []).join(", "); $("#editNotes").value = i.notes || ""; $("#editFavorite").checked = !!i.favorite; $("#editDialog").showModal();
}

function wireEvents() {
  $$(".mood").forEach(b => b.onclick = () => { state.mood = b.dataset.mood; $$(".mood").forEach(x => x.classList.toggle("active", x === b)); });
  $("#fillRainy").onclick = () => { $("#temperature").value = 18; $("#condition").value = "小雨"; $("#windLevel").value = 4; $("#humidity").value = 78; toast("已填入雨天示例"); };
  $("#fillCold").onclick = () => { $("#temperature").value = 3; $("#condition").value = "多云"; $("#windLevel").value = 5; $("#humidity").value = 48; toast("已填入低温示例"); };
  $("#recommendForm").onsubmit = async e => {
    e.preventDefault();
    try {
      const payload = { mood: state.mood, customMood: $("#customMood").value.trim(), occasion: $("#occasion").value, fitPreference: $("#fitPreference").value, preferWardrobe: $("#preferWardrobe").checked, weather: { temperature: +$("#temperature").value, condition: $("#condition").value, windLevel: +$("#windLevel").value, humidity: +$("#humidity").value } };
      toast("正在生成并搜索参考图...");
      const r = await api("/api/outfits/recommend", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
      renderResult(r); await Promise.all([loadHistory(), loadSummary()]); toast("搭配已生成");
    } catch (err) { toast(err.message); }
  };
  $("#photos").onchange = async e => { const files = [...(e.target.files || [])]; $("#preview").innerHTML = files.slice(0, 9).map(f => `<img src="${URL.createObjectURL(f)}" alt="预览"/>`).join(""); if (files[0] && !$("#itemColor").value.trim()) { const c = await extractDominantColor(files[0]); if (c) $("#itemColor").value = c; } };
  $("#uploadForm").onsubmit = async e => {
    e.preventDefault(); const files = $("#photos").files; if (!files.length) return toast("请先选择图片");
    const fd = new FormData(); [...files].forEach(f => fd.append("photos", f));
    ["Name", "Category", "Color", "Material", "Season", "Occasion", "Tags", "Notes", "Favorite"].forEach(k => { const el = $("#item" + k); if (el) fd.append(k.toLowerCase(), el.type === "checkbox" ? el.checked : el.value); });
    try { await api("/api/wardrobe/upload", { method: "POST", body: fd }); e.target.reset(); $("#preview").innerHTML = ""; await Promise.all([loadWardrobe(), loadSummary()]); toast("已保存到衣柜"); } catch (err) { toast(err.message); }
  };
  $("#wardrobeSearch").oninput = () => { clearTimeout(wireEvents.t); wireEvents.t = setTimeout(loadWardrobe, 250); };
  $("#wardrobeFilter").onchange = loadWardrobe;
  $("#wardrobeGrid").onclick = async e => {
    const del = e.target.closest("[data-delete]"), edit = e.target.closest("[data-edit]");
    if (edit) { const item = state.wardrobe.find(x => x.id === edit.dataset.edit); if (item) openEdit(item); }
    if (del && confirm("确定删除这件衣物吗？")) { try { await api(`/api/wardrobe/${del.dataset.delete}`, { method: "DELETE" }); await Promise.all([loadWardrobe(), loadSummary()]); toast("已删除"); } catch (err) { toast(err.message); } }
  };
  $("#editForm").onsubmit = async e => {
    e.preventDefault(); if (e.submitter?.value !== "save") { $("#editDialog").close(); return; }
    const id = $("#editId").value, payload = { name: $("#editName").value, category: $("#editCategory").value, color: $("#editColor").value, material: $("#editMaterial").value, season: $("#editSeason").value, occasion: $("#editOccasion").value, tags: split($("#editTags").value), notes: $("#editNotes").value, favorite: $("#editFavorite").checked };
    try { await api(`/api/wardrobe/${id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) }); $("#editDialog").close(); await Promise.all([loadWardrobe(), loadSummary()]); toast("已更新衣物"); } catch (err) { toast(err.message); }
  };
  $("#historyList").onclick = async e => {
    const replay = e.target.closest("[data-replay]"), fav = e.target.closest("[data-fav-history]");
    if (replay) { const h = state.history.find(x => x.id === replay.dataset.replay); if (h) { renderResult(h); $("#recommend").scrollIntoView({ behavior: "smooth" }); } }
    if (fav) { await api(`/api/history/${fav.dataset.favHistory}/favorite`, { method: "POST" }); await loadHistory(); toast("已切换收藏"); }
  };
  $("#clearHistory").onclick = async () => { if (!state.history.length) return toast("暂无历史"); if (confirm("确定清空历史？")) { await api("/api/history", { method: "DELETE" }); await Promise.all([loadHistory(), loadSummary()]); toast("历史已清空"); } };
  $("#exportData").onclick = () => { location.href = "/api/export"; };
  $("#settingsForm").onsubmit = async e => {
    e.preventDefault();
    const s = { displayName: $("#setDisplayName").value, defaultOccasion: $("#setDefaultOccasion").value, stylePreference: $("#setStylePreference").value, fitPreference: $("#setFitPreference").value, preferredColors: split($("#setPreferredColors").value), avoidColors: split($("#setAvoidColors").value), enableOnlineImages: $("#setOnlineImages").checked, preferWardrobe: $("#setPreferWardrobe").checked };
    try { await api("/api/settings", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(s) }); await loadSettings(); toast("设置已保存"); } catch (err) { toast(err.message); }
  };
  $("#refreshAll").onclick = () => loadAll().then(() => toast("已刷新"));
  window.addEventListener("online", updateOnlineState); window.addEventListener("offline", updateOnlineState);
}

function updateOnlineState() { const el = $("#onlineState"); if (!el) return; el.textContent = navigator.onLine ? "网络可用" : "离线模式"; el.style.background = navigator.onLine ? "#edf7f1" : "#fff4e7"; }
function wirePwa() { let deferred = null; const btn = $("#installApp"); window.addEventListener("beforeinstallprompt", e => { e.preventDefault(); deferred = e; btn.hidden = false; }); btn?.addEventListener("click", async () => { if (!deferred) return toast("可在浏览器菜单中选择安装应用"); deferred.prompt(); await deferred.userChoice; deferred = null; btn.hidden = true; }); window.addEventListener("appinstalled", () => { btn.hidden = true; toast("已安装为 App"); }); if ("serviceWorker" in navigator) navigator.serviceWorker.register("/sw.js").catch(console.debug); }
async function loadAll() { await Promise.all([loadSettings(), loadWardrobe(), loadHistory(), loadSummary()]); updateOnlineState(); }
document.addEventListener("DOMContentLoaded", async () => { wireEvents(); wirePwa(); try { await loadAll(); } catch (err) { toast(err.message); } });
