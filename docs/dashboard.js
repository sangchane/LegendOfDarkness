(function () {
  "use strict";
  var data = window.LOD_DASHBOARD_DATA;
  var model = window.LodDashboardModel;
  var snapshot = model.isValidDashboardSnapshot(window.LOD_DASHBOARD_SNAPSHOT) ? window.LOD_DASHBOARD_SNAPSHOT : null;
  var labels = { overview: "대시보드", system: "시스템 지도", flows: "기능 흐름", delivery: "개발·지식 그래프", world: "월드 지도", abilities: "기술·마법", knowledge: "게임 데이터", operations: "운영·유지보수", prototypes: "화면 실험실" };
  var category = "all";
  var searchQuery = "";
  function select(selector, host) { return (host || document).querySelector(selector); }
  function selectAll(selector, host) { return Array.prototype.slice.call((host || document).querySelectorAll(selector)); }
  function currentViewFromUrl() { return model.normalizeView(new URLSearchParams(window.location.search).get("view")); }
  function showView(view, updateHistory) {
    var normalized = model.normalizeView(view);
    selectAll("[data-view]").forEach(function (panel) {
      var active = panel.getAttribute("data-view") === normalized;
      panel.hidden = !active; panel.classList.toggle("is-active", active);
    });
    selectAll("[data-view-target]").forEach(function (button) {
      var active = button.getAttribute("data-view-target") === normalized;
      button.classList.toggle("is-active", active);
      if (active) { button.setAttribute("aria-current", "page"); } else { button.removeAttribute("aria-current"); }
    });
    select("#view-label").textContent = labels[normalized];
    document.title = labels[normalized] + " · LOD 개발 대시보드";
    if (updateHistory) { var url = new URL(window.location.href); url.searchParams.set("view", normalized); window.history.pushState({ view: normalized }, "", url); }
    document.body.classList.remove("menu-open"); select("#menu-toggle").setAttribute("aria-expanded", "false"); select("#menu-toggle").setAttribute("aria-label", "메뉴 열기"); if (updateHistory) { select("#workspace").focus({ preventScroll: true }); }
  }
  function createTextElement(tag, className, value) { var element = document.createElement(tag); if (className) { element.className = className; } element.textContent = value; return element; }
  function renderKnowledge() {
    var entries = model.filterKnowledge(data.knowledge, category, searchQuery);
    var list = select("#knowledge-list"); list.replaceChildren();
    entries.forEach(function (entry) {
      var article = document.createElement("article"); article.className = "knowledge-row";
      var body = document.createElement("div"); body.appendChild(createTextElement("h2", "", entry.title)); body.appendChild(createTextElement("p", "", entry.summary));
      var meta = document.createElement("div"); meta.className = "knowledge-meta"; meta.append(createTextElement("span", "", "출처"), createTextElement("code", "", entry.source), createTextElement("span", "", "다음"), createTextElement("strong", "", entry.next)); body.appendChild(meta);
      article.append(createTextElement("span", "knowledge-type", entry.label), body, createTextElement("span", "badge badge-" + entry.status, entry.statusLabel)); list.appendChild(article);
    });
    select("#knowledge-count").textContent = "총 " + entries.length + "개 항목"; select("#knowledge-empty").hidden = entries.length !== 0;
  }
  function renderSnapshot() {
    if (!snapshot) { return; }
    var current = snapshot.roadmap.current[0];
    select("#snapshot-current-title").textContent = current.title;
    select("#snapshot-current-detail").textContent = current.detail || current.area + " 작업";
    select("#snapshot-current-area").textContent = current.area;
    select("#snapshot-board-title").textContent = current.title;
    select("#snapshot-board-detail").textContent = current.detail || "현재 NEXT-ACTION";
    select("#snapshot-generated-date").textContent = snapshot.generatedAt.slice(0, 10);
    select("#snapshot-branch").textContent = snapshot.git.branch;

    var verification = snapshot.verification;
    var verificationTitle = verification.status === "passed"
      ? verification.passed + "개 검사 통과"
      : verification.status === "failed" ? verification.failed + "개 검사 실패" : "검증 미실행";
    select("#snapshot-verification-title").textContent = verificationTitle;
    select("#snapshot-verification-detail").textContent = verification.command || "스냅샷 생성 시 --verify로 갱신";

    var graphify = snapshot.graphify;
    var formatCount = function (value) { return Number.isInteger(value) ? value.toLocaleString("ko-KR") : "미확인"; };
    select("#snapshot-graphify-nodes").textContent = formatCount(graphify.nodes);
    select("#snapshot-graphify-links").textContent = formatCount(graphify.links);
    select("#snapshot-graphify-communities").textContent = formatCount(graphify.communities);
    select("#snapshot-obsidian-notes").textContent = formatCount(graphify.obsidian.notes);
    select("#snapshot-graphify-date").textContent = snapshot.generatedAt.slice(0, 10) + " 스냅샷";
    select("#snapshot-graphify-mode").textContent = graphify.nodes > 5000 ? "대시보드: 커뮤니티 집약 뷰 · 전체 노드: Obsidian" : "대시보드: 전체 노드 뷰";
    var graphState = select("#snapshot-graphify-state");
    var graphReady = graphify.status === "available" && graphify.graphHtml;
    graphState.textContent = graphReady ? "그래프 연결됨" : "생성 결과 없음";
    graphState.className = "badge " + (graphReady ? "badge-done" : "badge-risk");
  }
  function showGraphUnavailable() {
    select("#graphify-frame-shell").hidden = true;
    select("#graphify-unavailable").hidden = false;
    ["#snapshot-graphify-nodes", "#snapshot-graphify-links", "#snapshot-graphify-communities", "#snapshot-obsidian-notes"].forEach(function (selector) { select(selector).textContent = "미확인"; });
    var graphState = select("#snapshot-graphify-state");
    graphState.textContent = "그래프 파일 없음";
    graphState.className = "badge badge-risk";
  }
  function verifyGraphAsset() {
    if (!snapshot || !snapshot.graphify.graphHtml) { showGraphUnavailable(); return; }
    if (window.location.protocol === "file:") { return; }
    window.fetch(new URL("../graphify-out/graph.html", window.location.href), { method: "HEAD", cache: "no-store" })
      .then(function (response) { if (!response.ok) { showGraphUnavailable(); } })
      .catch(showGraphUnavailable);
  }
  function renderComponents() {
    var host = select("#component-catalog");
    data.components.forEach(function (entry) {
      var article = document.createElement("article"); article.className = "component-card";
      var heading = document.createElement("header");
      var title = document.createElement("div"); title.append(createTextElement("small", "", entry.kind), createTextElement("h2", "", entry.name));
      heading.append(title, createTextElement("span", "badge badge-" + entry.status, entry.lifecycle));
      var dependencies = entry.dependsOn.length ? entry.dependsOn.join(" · ") : "없음";
      article.append(heading, createTextElement("p", "", entry.responsibility));
      var meta = document.createElement("dl");
      meta.append(createTextElement("dt", "", "위치"), createTextElement("dd", "", entry.source), createTextElement("dt", "", "의존"), createTextElement("dd", "", dependencies));
      article.appendChild(meta); host.appendChild(article);
    });
  }
  function renderFlowTabs() {
    var host = select("#flow-tabs");
    data.flows.forEach(function (flow, index) {
      var button = createTextElement("button", "", flow.label); button.id = "flow-tab-" + flow.id; button.type = "button"; button.setAttribute("role", "tab"); button.setAttribute("aria-controls", "flow-panel"); button.setAttribute("data-flow", flow.id); button.setAttribute("aria-selected", String(index === 0)); button.tabIndex = index === 0 ? 0 : -1; host.appendChild(button);
    });
  }
  function showFlow(id) {
    var flow = model.findFlow(data.flows, id); if (!flow) { return; }
    select("#flow-panel").setAttribute("aria-labelledby", "flow-tab-" + flow.id);
    select("#flow-label").textContent = flow.label; select("#flow-summary").textContent = flow.summary;
    var status = select("#flow-status"); status.className = "badge badge-" + flow.status; status.textContent = flow.status === "verified" ? "확인됨" : flow.status === "active" ? "검증 중" : flow.status === "partial" ? "부분 확인" : "경로 없음";
    var steps = select("#flow-steps"); steps.replaceChildren();
    flow.steps.forEach(function (step, index) {
      var item = document.createElement("li");
      item.append(createTextElement("span", "flow-number", String(index + 1).padStart(2, "0")), createTextElement("strong", "", step.component), createTextElement("h3", "", step.action));
      var meta = document.createElement("dl"); meta.append(createTextElement("dt", "", "위치"), createTextElement("dd", "", step.source), createTextElement("dt", "", "검증"), createTextElement("dd", "", step.evidence), createTextElement("dt", "", "운영 신호"), createTextElement("dd", "", step.signal)); item.appendChild(meta); steps.appendChild(item);
    });
    selectAll("[data-flow]").forEach(function (tab) { var active = tab.getAttribute("data-flow") === flow.id; tab.setAttribute("aria-selected", String(active)); tab.tabIndex = active ? 0 : -1; });
  }
  function showToast(message) { var toast = select("#toast"); toast.textContent = message; toast.classList.add("is-visible"); window.setTimeout(function () { toast.classList.remove("is-visible"); }, 1800); }
  function showDemoScreen(screen) {
    select("#inline-game-preview").setAttribute("data-screen", screen);
    selectAll("[data-demo-screen]").forEach(function (tab) { var active = tab.getAttribute("data-demo-screen") === screen; tab.setAttribute("aria-selected", String(active)); tab.tabIndex = active ? 0 : -1; });
    selectAll("[data-screen-layer]").forEach(function (layer) { layer.hidden = layer.getAttribute("data-screen-layer") !== screen; });
  }
  function copyText(value) {
    if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") { return navigator.clipboard.writeText(value); }
    return Promise.reject(new Error("Clipboard API unavailable"));
  }
  selectAll("[data-view-target]").forEach(function (button) { button.addEventListener("click", function () { showView(button.getAttribute("data-view-target"), true); }); });
  selectAll("[data-view-jump]").forEach(function (button) { button.addEventListener("click", function () { showView(button.getAttribute("data-view-jump"), true); }); });
  selectAll("[data-category]").forEach(function (button) { button.addEventListener("click", function () { category = button.getAttribute("data-category"); selectAll("[data-category]").forEach(function (chip) { var active = chip === button; chip.classList.toggle("is-active", active); chip.setAttribute("aria-pressed", String(active)); }); renderKnowledge(); }); });
  select("#knowledge-search").addEventListener("input", function (event) { searchQuery = event.target.value; renderKnowledge(); });
  selectAll("[data-demo-screen]").forEach(function (button, index) { button.tabIndex = index === 0 ? 0 : -1; button.addEventListener("click", function () { showDemoScreen(button.getAttribute("data-demo-screen")); }); button.addEventListener("keydown", function (event) { if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") { return; } var tabs = selectAll("[data-demo-screen]"); var offset = event.key === "ArrowRight" ? 1 : -1; var next = tabs[(tabs.indexOf(button) + offset + tabs.length) % tabs.length]; event.preventDefault(); showDemoScreen(next.getAttribute("data-demo-screen")); next.focus(); }); });
  selectAll("[data-hud-toggle]").forEach(function (button) { button.addEventListener("click", function () { var pressed = button.getAttribute("aria-pressed") !== "true"; var name = button.getAttribute("data-hud-toggle"); button.setAttribute("aria-pressed", String(pressed)); select("#inline-hud-preview").classList.toggle("show-" + name, pressed); }); });
  var hudPosition = { x: 0, y: 0 };
  var targetSelected = false;
  var targetHp = 30;
  selectAll("[data-hud-move]").forEach(function (button) { button.addEventListener("click", function () { var direction = button.getAttribute("data-hud-move"); var delta = { up: [0, -12], left: [-12, 0], center: [0, 0], right: [12, 0], down: [0, 12] }[direction]; hudPosition = direction === "center" ? { x: 0, y: 0 } : { x: hudPosition.x + delta[0], y: hudPosition.y + delta[1] }; var avatar = select(".hud-avatar"); avatar.style.setProperty("--avatar-x", hudPosition.x + "px"); avatar.style.setProperty("--avatar-y", hudPosition.y + "px"); select("#hud-feedback").textContent = direction === "center" ? "위치 초기화" : button.getAttribute("aria-label"); }); });
  select("[data-hud-target]").addEventListener("click", function (event) { targetSelected = !targetSelected; event.currentTarget.setAttribute("aria-pressed", String(targetSelected)); select("#hud-feedback").textContent = targetSelected ? "벌을 대상으로 선택" : "대상 선택 해제"; });
  selectAll("[data-hud-action]").forEach(function (button) { button.addEventListener("click", function () { var action = button.getAttribute("data-hud-action"); if ((action === "attack" || action === "skill") && !targetSelected) { select("#hud-feedback").textContent = "먼저 대상을 선택하세요"; return; } if (action === "attack" || action === "skill") { targetHp = Math.max(0, targetHp - (action === "attack" ? 6 : 10)); select("#hud-target-hp").textContent = String(targetHp); select("#hud-feedback").textContent = button.textContent + " 적중 · 벌 HP " + targetHp; } else { select("#hud-feedback").textContent = "물약 사용 · HP 회복"; } button.disabled = true; button.classList.add("is-cooling"); window.setTimeout(function () { button.disabled = false; button.classList.remove("is-cooling"); }, action === "skill" ? 900 : 600); }); });
  select("#flow-tabs").addEventListener("click", function (event) { var tab = event.target.closest("[data-flow]"); if (tab) { showFlow(tab.getAttribute("data-flow")); } });
  select("#flow-tabs").addEventListener("keydown", function (event) { if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") { return; } var tabs = selectAll("[data-flow]"); var current = tabs.indexOf(document.activeElement); var offset = event.key === "ArrowRight" ? 1 : -1; var next = tabs[(current + offset + tabs.length) % tabs.length]; event.preventDefault(); showFlow(next.getAttribute("data-flow")); next.focus(); });
  select("#menu-toggle").addEventListener("click", function (event) { var open = !document.body.classList.contains("menu-open"); document.body.classList.toggle("menu-open", open); event.currentTarget.setAttribute("aria-expanded", String(open)); event.currentTarget.setAttribute("aria-label", open ? "메뉴 닫기" : "메뉴 열기"); if (open) { select(".primary-nav .nav-item.is-active").focus(); } else { event.currentTarget.focus(); } });
  selectAll("[data-copy-command]").forEach(function (button) { button.addEventListener("click", function () { copyText(button.getAttribute("data-copy-command")).then(function () { showToast("명령을 복사했습니다."); }, function () { showToast("복사 기능을 쓸 수 없습니다. 명령을 직접 선택해 주세요."); }); }); });
  select("#graphify-frame").addEventListener("error", showGraphUnavailable);
  window.addEventListener("popstate", function () { showView(currentViewFromUrl(), false); });
  document.addEventListener("keydown", function (event) { if (event.key === "Escape" && document.body.classList.contains("menu-open")) { document.body.classList.remove("menu-open"); select("#menu-toggle").setAttribute("aria-expanded", "false"); select("#menu-toggle").setAttribute("aria-label", "메뉴 열기"); select("#menu-toggle").focus(); } });
  renderSnapshot(); verifyGraphAsset(); renderKnowledge(); renderComponents(); renderFlowTabs(); showFlow(data.flows[0].id); showView(currentViewFromUrl(), false);
})();
