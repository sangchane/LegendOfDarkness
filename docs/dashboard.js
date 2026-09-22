(function () {
  "use strict";
  var data = window.LOD_DASHBOARD_DATA;
  var model = window.LodDashboardModel;
  var snapshot = model.isValidDashboardSnapshot(window.LOD_DASHBOARD_SNAPSHOT) ? window.LOD_DASHBOARD_SNAPSHOT : null;
  var labels = {
    overview: "지금 되는 것", monsters: "괴물 도감", abilities: "기술·마법", items: "아이템 도감",
    world: "지도·워프", changes: "원작과 달라진 것", knowledge: "게임 데이터", delivery: "지식 그래프",
  };
  var category = "all";
  var searchQuery = "";
  // 화면이 보이게 된 순간에만 할 수 있는 일이 있다 — 숨은 요소는 크기를 잴 수 없어서
  // 워프 연결도의 선을 못 그린다. 그 화면들이 여기에 이름을 걸어 둔다.
  var watchers = [];

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
    if (updateHistory) {
      var url = new URL(window.location.href);
      url.searchParams.set("view", normalized);
      window.history.pushState({ view: normalized }, "", url);
    }
    document.body.classList.remove("menu-open");
    select("#menu-toggle").setAttribute("aria-expanded", "false");
    select("#menu-toggle").setAttribute("aria-label", "메뉴 열기");
    if (updateHistory) { select("#workspace").focus({ preventScroll: true }); }
    watchers.forEach(function (watcher) { watcher(normalized); });
  }

  function createTextElement(tag, className, value) {
    var element = document.createElement(tag);
    if (className) { element.className = className; }
    element.textContent = value;
    return element;
  }

  function renderKnowledge() {
    var entries = model.filterKnowledge(data.knowledge, category, searchQuery);
    var list = select("#knowledge-list"); list.replaceChildren();
    entries.forEach(function (entry) {
      var article = document.createElement("article"); article.className = "knowledge-row";
      var body = document.createElement("div");
      body.appendChild(createTextElement("h2", "", entry.title));
      body.appendChild(createTextElement("p", "", entry.summary));
      var meta = document.createElement("div"); meta.className = "knowledge-meta";
      meta.append(createTextElement("span", "", "출처"), createTextElement("code", "", entry.source),
        createTextElement("span", "", "다음"), createTextElement("strong", "", entry.next));
      body.appendChild(meta);
      article.append(createTextElement("span", "knowledge-type", entry.label), body,
        createTextElement("span", "badge badge-" + entry.status, entry.statusLabel));
      list.appendChild(article);
    });
    select("#knowledge-count").textContent = "총 " + entries.length + "개 항목";
    select("#knowledge-empty").hidden = entries.length !== 0;
  }

  function renderSnapshot() {
    if (!snapshot) {
      select("#snapshot-generated-date").textContent = "스냅샷 없음";
      select("#snapshot-current-title").textContent = "스냅샷이 없습니다. node scripts/generate-dashboard-snapshot.js 로 만드세요.";
      return;
    }
    select("#snapshot-generated-date").textContent = snapshot.generatedAt.slice(0, 10);
    select("#snapshot-branch").textContent = snapshot.git.branch;

    var current = snapshot.roadmap.current[0];
    var body = select("#snapshot-current-title");
    body.replaceChildren();
    body.append(createTextElement("strong", "", current.title));
    if (current.detail) { body.append(document.createTextNode(" — " + current.detail)); }

    // 검사를 돌린 적이 없으면 없다고 적는다 — 통과했다고 꾸미지 않는다.
    var verification = snapshot.verification;
    select("#snapshot-verification-title").textContent = verification.status === "passed"
      ? "최근 검증 — " + verification.passed + "개 통과 · " + (verification.command || "")
      : verification.status === "failed"
        ? "최근 검증 — " + verification.failed + "개 실패 · " + (verification.command || "")
        : "검증 기록 없음 — 스냅샷을 --verify 로 다시 만드세요";

    var graphify = snapshot.graphify;
    var formatCount = function (value) { return Number.isInteger(value) ? value.toLocaleString("ko-KR") : "미확인"; };
    select("#snapshot-graphify-nodes").textContent = formatCount(graphify.nodes);
    select("#snapshot-graphify-links").textContent = formatCount(graphify.links);
    select("#snapshot-graphify-communities").textContent = formatCount(graphify.communities);
    select("#snapshot-obsidian-notes").textContent = formatCount(graphify.obsidian.notes);
    select("#snapshot-graphify-date").textContent = snapshot.generatedAt.slice(0, 10) + " 스냅샷";
    select("#snapshot-graphify-mode").textContent = graphify.nodes > 5000
      ? "대시보드: 커뮤니티 집약 뷰 · 전체 노드: Obsidian" : "대시보드: 전체 노드 뷰";
    var graphState = select("#snapshot-graphify-state");
    var graphReady = graphify.status === "available" && graphify.graphHtml;
    graphState.textContent = graphReady ? "그래프 연결됨" : "생성 결과 없음";
    graphState.className = "badge " + (graphReady ? "badge-done" : "badge-risk");
  }

  function showGraphUnavailable() {
    select("#graphify-frame-shell").hidden = true;
    select("#graphify-unavailable").hidden = false;
    ["#snapshot-graphify-nodes", "#snapshot-graphify-links", "#snapshot-graphify-communities", "#snapshot-obsidian-notes"]
      .forEach(function (selector) { select(selector).textContent = "미확인"; });
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

  function showToast(message) {
    var toast = select("#toast");
    toast.textContent = message;
    toast.classList.add("is-visible");
    window.setTimeout(function () { toast.classList.remove("is-visible"); }, 1800);
  }

  function copyText(value) {
    if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") { return navigator.clipboard.writeText(value); }
    return Promise.reject(new Error("Clipboard API unavailable"));
  }

  selectAll("[data-view-target]").forEach(function (button) {
    button.addEventListener("click", function () { showView(button.getAttribute("data-view-target"), true); });
  });
  selectAll("[data-view-jump]").forEach(function (button) {
    button.addEventListener("click", function () { showView(button.getAttribute("data-view-jump"), true); });
  });
  selectAll("[data-category]").forEach(function (button) {
    button.addEventListener("click", function () {
      category = button.getAttribute("data-category");
      selectAll("[data-category]").forEach(function (chip) {
        var active = chip === button;
        chip.classList.toggle("is-active", active);
        chip.setAttribute("aria-pressed", String(active));
      });
      renderKnowledge();
    });
  });
  select("#knowledge-search").addEventListener("input", function (event) { searchQuery = event.target.value; renderKnowledge(); });
  select("#menu-toggle").addEventListener("click", function (event) {
    var open = !document.body.classList.contains("menu-open");
    document.body.classList.toggle("menu-open", open);
    event.currentTarget.setAttribute("aria-expanded", String(open));
    event.currentTarget.setAttribute("aria-label", open ? "메뉴 닫기" : "메뉴 열기");
    if (open) { select(".primary-nav .nav-item.is-active").focus(); } else { event.currentTarget.focus(); }
  });
  selectAll("[data-copy-command]").forEach(function (button) {
    button.addEventListener("click", function () {
      copyText(button.getAttribute("data-copy-command")).then(
        function () { showToast("명령을 복사했습니다."); },
        function () { showToast("복사 기능을 쓸 수 없습니다. 명령을 직접 선택해 주세요."); });
    });
  });
  select("#graphify-frame").addEventListener("error", showGraphUnavailable);
  window.addEventListener("popstate", function () { showView(currentViewFromUrl(), false); });
  document.addEventListener("keydown", function (event) {
    if (event.key !== "Escape" || !document.body.classList.contains("menu-open")) { return; }
    document.body.classList.remove("menu-open");
    select("#menu-toggle").setAttribute("aria-expanded", "false");
    select("#menu-toggle").setAttribute("aria-label", "메뉴 열기");
    select("#menu-toggle").focus();
  });

  // 뒤에 실리는 화면 모듈이 쓴다. ArrowLeft·ArrowRight 로 탭을 넘기는 손놀림도 여기 한 벌만 둔다.
  window.LodDashboard = {
    onViewShown: function (watcher) { watchers.push(watcher); },
    show: function (view) { showView(view, true); },
    toast: showToast,
    tabKeys: function (host, attribute, onPick) {
      host.addEventListener("keydown", function (event) {
        if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") { return; }
        var tabs = selectAll("[" + attribute + "]", host);
        var offset = event.key === "ArrowRight" ? 1 : -1;
        var next = tabs[(tabs.indexOf(document.activeElement) + offset + tabs.length) % tabs.length];
        if (!next) { return; }
        event.preventDefault();
        onPick(next.getAttribute(attribute));
        next.focus();
      });
    },
  };

  renderSnapshot();
  verifyGraphAsset();
  renderKnowledge();
  showView(currentViewFromUrl(), false);
})();
