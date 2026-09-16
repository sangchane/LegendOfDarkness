/* 기술·마법 도감 — 직업·갈래로 좁혀 보고, 가리키면 샌드백에 연출을 터뜨린다.
 *
 * 613개를 선행 사슬 트리로 보면 "무엇이 있는지" 가 안 보인다. 아이템 도감과 같은 카드 격자로
 * 바꾸고, 사슬은 카드 안에 「선행」한 줄로 남긴다.
 *
 * 연출은 원작 `efct###.png`(프레임 여러 장이 한 줄) 를 `steps()` 로 넘긴다. 시전자 자세가 아니라
 * **기술이 부르는 연출**이라 기술마다 화면이 다르다. 한글 이름이 정해진 것만 이어져 있어
 * (하데스 쪽에는 이펙트가 없다) 나머지는 「연출 없음」으로 둔다.
 *
 * 소리는 번호만 안다 — 원작 음원을 아직 안 뽑았고, 자동 재생은 브라우저가 막는다. 번호만 적는다.
 */
(function () {
  "use strict";
  var DATA = window.ABILITY_DATA;      // 생성기가 쓰는 이름 (`build-ability-page-data.py`)
  var SHOTS = window.LOD_ABILITY_EFFECTS || { 연출: {} };
  if (!DATA) { return; }

  var STORE = "lod.ability.korean.v1";
  var PER_PAGE = 60;
  var CELL = 35, COLS = 16;          // 아이콘 시트: 한 칸 35x35, 한 줄 16칸

  var typed = {};
  try { typed = JSON.parse(localStorage.getItem(STORE) || "{}"); } catch (e) { typed = {}; }

  var cls = "all", kind = "all", query = "", onlyUnnamed = false, onlyPlayable = false, page = 0;

  function $(id) { return document.getElementById(id); }
  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) { node.className = className; }
    if (text != null) { node.textContent = text; }
    return node;
  }

  var ALL = [];
  DATA["묶음"].forEach(function (group) {
    group["목록"].forEach(function (row) {
      ALL.push(Object.assign({ 직업: group["직업"], 갈래: group["갈래"] }, row));
    });
  });

  function nameOf(row) { return (typed[row["이름"]] || row["한글"] || "").trim(); }
  function shotsOf(row) { return (row["연출"] || []).filter(function (n) { return SHOTS["연출"][n]; }); }

  function remember(name, value) {
    if (value) { typed[name] = value; } else { delete typed[name]; }
    try { localStorage.setItem(STORE, JSON.stringify(typed)); } catch (e) { /* 사생활 모드 */ }
  }

  function matches(row) {
    if (cls !== "all" && row["직업"] !== cls) { return false; }
    if (kind !== "all" && row["갈래"] !== kind) { return false; }
    if (onlyUnnamed && nameOf(row)) { return false; }
    if (onlyPlayable && !shotsOf(row).length) { return false; }
    if (!query) { return true; }
    return (row["이름"] + " " + (row["한글"] || "") + " " + (typed[row["이름"]] || ""))
      .toLowerCase().indexOf(query) >= 0;
  }

  /* ── 샌드백 무대 ───────────────────────────────────────────────────── */
  var stage = null, timer = null;

  function stopStage() {
    if (timer) { window.clearTimeout(timer); timer = null; }
    if (stage) { stage.hidden = true; stage.setAttribute("aria-hidden", "true"); }
  }

  function playStage(row, anchor) {
    if (!stage) { return; }
    var shots = shotsOf(row);
    stage.replaceChildren();

    var floor = el("div", "sandbag-floor");
    floor.appendChild(el("i", "sandbag"));            // 맞는 쪽 — 연출이 이 위에 얹힌다
    shots.forEach(function (number, index) {
      var info = SHOTS["연출"][number];
      var shot = el("i", "sandbag-shot");
      shot.style.backgroundImage = "url(ui/assets/ability-effects/" + info["파일"] + ")";
      shot.style.width = (100 / info["프레임"]) + "%";
      shot.style.setProperty("--frames", info["프레임"]);
      shot.style.setProperty("--delay", (index * 0.45) + "s");
      floor.appendChild(shot);
    });
    stage.appendChild(floor);

    var caption = el("div", "sandbag-caption");
    caption.appendChild(el("b", "", nameOf(row) || row["이름"]));
    caption.appendChild(el("span", "", shots.length
      ? "연출 " + shots.join(" · ") + (row["소리"] && row["소리"].length ? " · 소리 " + row["소리"].join(",") : "")
      : "이 기술의 연출은 아직 이어지지 않았어요"));
    stage.appendChild(caption);

    var box = anchor.getBoundingClientRect();
    stage.hidden = false;
    stage.setAttribute("aria-hidden", "false");
    var own = stage.getBoundingClientRect();
    var left = Math.min(box.right + 12, window.innerWidth - own.width - 12);
    var top = Math.min(box.top, window.innerHeight - own.height - 12);
    stage.style.transform = "translate(" + Math.max(12, left) + "px," + Math.max(12, top) + "px)";
  }

  /* ── 카드 ──────────────────────────────────────────────────────────── */
  function card(row) {
    var article = el("article", "ability-card");
    if (!nameOf(row)) { article.classList.add("is-unnamed"); }
    if (shotsOf(row).length) { article.classList.add("is-playable"); }

    var icon = el("i", "ability-icon");
    var sheet = row["갈래"] === "기술" ? "skill" : "spell";
    icon.style.backgroundImage = "url(ability-icons/" + sheet + ".png)";
    icon.style.backgroundPosition = "-" + (row["아이콘"] % COLS) * CELL + "px -" +
      Math.floor(row["아이콘"] / COLS) * CELL + "px";
    article.appendChild(icon);

    var head = el("div", "ability-card-head");
    head.appendChild(el("b", "", nameOf(row) || row["이름"]));
    if (nameOf(row)) { head.appendChild(el("em", "", row["이름"])); }
    article.appendChild(head);

    var meta = el("div", "ability-card-meta");
    meta.appendChild(el("span", "ability-kind", row["갈래"]));
    meta.appendChild(el("span", "ability-cls", row["직업"]));
    if (row["레벨"]) { meta.appendChild(el("span", "ability-lv", "Lv" + row["레벨"])); }
    if (shotsOf(row).length) { meta.appendChild(el("span", "ability-play", "연출 " + shotsOf(row).length)); }
    article.appendChild(meta);

    if (row["선행"]) { article.appendChild(el("p", "ability-pre", "선행 " + row["선행"])); }

    var input = el("input", "ability-name");
    input.type = "text";
    input.value = typed[row["이름"]] || row["한글"] || "";
    input.placeholder = "한글 이름";
    input.setAttribute("aria-label", row["이름"] + " 한글 이름");
    input.addEventListener("change", function () {
      remember(row["이름"], input.value.trim());
      render();
    });
    article.appendChild(input);

    article.addEventListener("mouseenter", function () { playStage(row, article); });
    article.addEventListener("mouseleave", stopStage);
    article.addEventListener("click", function (event) {
      if (event.target !== input) { playStage(row, article); }
    });
    return article;
  }

  /* ── 그리기 ────────────────────────────────────────────────────────── */
  function chips(host, values, current, onPick) {
    host.replaceChildren();
    ["all"].concat(values).forEach(function (value) {
      var button = el("button", "chip", value === "all" ? "전체" : value);
      button.type = "button";
      if (value === current) { button.classList.add("is-active"); }
      button.setAttribute("aria-pressed", value === current ? "true" : "false");
      button.addEventListener("click", function () { onPick(value); });
      host.appendChild(button);
    });
  }

  function render() {
    var rows = ALL.filter(matches);
    var pages = Math.max(1, Math.ceil(rows.length / PER_PAGE));
    if (page >= pages) { page = pages - 1; }
    var slice = rows.slice(page * PER_PAGE, page * PER_PAGE + PER_PAGE);

    var grid = $("ability-grid");
    grid.replaceChildren();
    slice.forEach(function (row) { grid.appendChild(card(row)); });

    $("ability-empty").hidden = rows.length !== 0;
    $("ability-pager").hidden = pages < 2;
    $("ability-pager-label").textContent = (page + 1) + " / " + pages + " 쪽";

    var named = ALL.filter(function (r) { return nameOf(r); }).length;
    var playable = ALL.filter(function (r) { return shotsOf(r).length; }).length;
    $("ability-total").textContent = ALL.length.toLocaleString("ko-KR");
    $("ability-named").textContent = named + " (" + Math.round((named / ALL.length) * 100) + "%)";
    $("ability-playable").textContent = playable.toLocaleString("ko-KR");
    $("ability-shown").textContent = rows.length.toLocaleString("ko-KR");

    chips($("ability-classes"), DATA["직업"] || uniq("직업"), cls, function (v) { cls = v; page = 0; render(); });
    chips($("ability-kinds"), ["기술", "마법"], kind, function (v) { kind = v; page = 0; render(); });
    stopStage();
  }

  function uniq(key) {
    var out = [];
    ALL.forEach(function (r) { if (out.indexOf(r[key]) < 0) { out.push(r[key]); } });
    return out;
  }

  function exportTsv() {
    var box = $("ability-export-box");
    var lines = ["영문\t한글"];
    ALL.forEach(function (row) {
      if (typed[row["이름"]]) { lines.push(row["이름"] + "\t" + typed[row["이름"]]); }
    });
    box.replaceChildren();
    if (lines.length === 1) {
      box.appendChild(el("p", "notice", "이 브라우저에서 고친 이름이 아직 없어요."));
    } else {
      box.appendChild(el("p", "", "아래를 복사해 data/기술마법-한글이름.tsv 에 붙여 넣으세요 ("
        + (lines.length - 1) + "줄). 반영: python3 scripts/build-ability-page-data.py"));
      box.appendChild(el("pre", "ability-export", lines.join("\n")));
    }
    box.hidden = false;
  }

  function boot() {
    stage = $("ability-stage");
    if (!$("ability-grid")) { return; }
    $("ability-search").addEventListener("input", function (event) {
      query = event.target.value.trim().toLowerCase(); page = 0; render();
    });
    [["ability-only-unnamed", function () { onlyUnnamed = !onlyUnnamed; return onlyUnnamed; }],
     ["ability-only-playable", function () { onlyPlayable = !onlyPlayable; return onlyPlayable; }]
    ].forEach(function (pair) {
      $(pair[0]).addEventListener("click", function (event) {
        var on = pair[1]();
        event.currentTarget.classList.toggle("is-active", on);
        event.currentTarget.setAttribute("aria-pressed", on ? "true" : "false");
        page = 0; render();
      });
    });
    $("ability-export").addEventListener("click", exportTsv);
    Array.prototype.forEach.call(document.querySelectorAll("[data-ability-page]"), function (button) {
      button.addEventListener("click", function () {
        page = Math.max(0, page + Number(button.getAttribute("data-ability-page")));
        render();
        $("ability-grid").scrollIntoView({ block: "start", behavior: "smooth" });
      });
    });
    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape") { stopStage(); }
    });
    render();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
