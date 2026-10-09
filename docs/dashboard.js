(function () {
  "use strict";
  var model = window.LodDashboardModel;
  var snapshot = model.isValidDashboardSnapshot(window.LOD_DASHBOARD_SNAPSHOT) ? window.LOD_DASHBOARD_SNAPSHOT : null;
  var labels = {
    overview: "지금 되는 것", monsters: "괴물", npcs: "NPC", abilities: "기술·마법", items: "아이템",
    world: "지도", changes: "원작과 달라진 것", download: "앱 내려받기", activity: "접속·활동",
  };
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
      // 휴대폰의 가로 메뉴에서 고른 칸이 화면 밖이면 보이게 민다.
      if (active && button.scrollIntoView) { button.scrollIntoView({ block: "nearest", inline: "nearest" }); }
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

  function renderSnapshot() {
    if (!snapshot) {
      select("#snapshot-current-title").textContent = "스냅샷이 없습니다. node scripts/generate-dashboard-snapshot.js 로 만드세요.";
      return;
    }
    // 지금 하는 일의 제목만 한 줄씩 — 자세한 것은 NEXT.md 원문에 있다.
    var body = select("#snapshot-current-title");
    body.replaceChildren();
    snapshot.roadmap.current.forEach(function (task) { body.append(createTextElement("strong", "", task.title)); });

    // 검사를 돌린 적이 없으면 없다고 적는다 — 통과했다고 꾸미지 않는다.
    var verification = snapshot.verification;
    select("#snapshot-verification-title").textContent = verification.status === "passed"
      ? "최근 검증 — " + verification.passed + "개 통과 · " + (verification.command || "")
      : verification.status === "failed"
        ? "최근 검증 — " + verification.failed + "개 실패 · " + (verification.command || "")
        : "검증 기록 없음 — 스냅샷을 --verify 로 다시 만드세요";
  }

  function showToast(message) {
    var toast = select("#toast");
    toast.textContent = message;
    toast.classList.add("is-visible");
    window.setTimeout(function () { toast.classList.remove("is-visible"); }, 1800);
  }

  selectAll("[data-view-target]").forEach(function (button) {
    button.addEventListener("click", function () { showView(button.getAttribute("data-view-target"), true); });
  });
  selectAll("[data-view-jump]").forEach(function (button) {
    button.addEventListener("click", function () { showView(button.getAttribute("data-view-jump"), true); });
  });
  select("#menu-toggle").addEventListener("click", function (event) {
    var open = !document.body.classList.contains("menu-open");
    document.body.classList.toggle("menu-open", open);
    event.currentTarget.setAttribute("aria-expanded", String(open));
    event.currentTarget.setAttribute("aria-label", open ? "메뉴 닫기" : "메뉴 열기");
    if (open) { select(".primary-nav .nav-item.is-active").focus(); } else { event.currentTarget.focus(); }
  });
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
  showView(currentViewFromUrl(), false);
})();
