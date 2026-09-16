/* 아이템 도감 — 슬롯·직업으로 좁혀 보고, 한글 이름을 채운다.
 *
 * 978장을 한 번에 그리면 스크롤이 무거워서 쪽으로 끊는다. 카드에는 네 조각만 낸다
 * (한글·영문·슬롯/레벨/대표수치). 나머지는 카드에 올리면 읽을 게 너무 많아져서,
 * 가리켰을 때만 옆에 띄운다 — 손가락에는 hover 가 없으므로 탭도 같은 자리를 연다.
 *
 * 한글 이름을 고치면 이 브라우저에 남고, [표로 내보내기] 로 받아
 * `data/아이템-한글이름.tsv` 에 붙여 넣는다. 기술·마법(abilities.js)과 같은 흐름이다.
 */
(function () {
  "use strict";
  var data = window.LOD_ITEMS;
  if (!data) { return; }

  var STORE = "lod.item.korean.v1";
  var PER_PAGE = 60;

  var typed = {};
  try { typed = JSON.parse(localStorage.getItem(STORE) || "{}"); } catch (e) { typed = {}; }

  var slot = "all";
  var cls = "all";
  var query = "";
  var onlyUnnamed = false;
  var page = 0;

  function $(id) { return document.getElementById(id); }
  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) { node.className = className; }
    if (text != null) { node.textContent = text; }
    return node;
  }

  function nameOf(row) { return (typed[row.en] || row.ko || "").trim(); }

  function remember(en, value) {
    if (value) { typed[en] = value; } else { delete typed[en]; }
    try { localStorage.setItem(STORE, JSON.stringify(typed)); } catch (e) { /* 사생활 모드 */ }
  }

  function matches(row) {
    if (slot !== "all" && row.slot !== slot) { return false; }
    if (cls !== "all" && row.cls !== cls) { return false; }
    if (onlyUnnamed && nameOf(row)) { return false; }
    if (!query) { return true; }
    return (row.en + " " + (row.ko || "") + " " + (typed[row.en] || "")).toLowerCase()
      .indexOf(query) >= 0;
  }

  function visible() { return data.목록.filter(matches); }

  /* ── 가리켰을 때 뜨는 패널 ─────────────────────────────────────────── */
  var hover = null;

  function closeHover() {
    if (!hover) { return; }
    hover.hidden = true;
    hover.setAttribute("aria-hidden", "true");
  }

  function openHover(row, anchor) {
    if (!hover) { return; }
    hover.replaceChildren();
    var top = el("div", "item-hover-top");
    var big = el("i", "item-icon is-big");
    if (row.ic >= 0) {
      big.style.backgroundPosition = "-" + (row.ic * data.아이콘.너비 * 2) + "px 0";
    } else {
      big.classList.add("is-blank");
    }
    top.appendChild(big);
    var title = el("div", "");
    title.appendChild(el("b", "", nameOf(row) || row.en));
    if (nameOf(row)) { title.appendChild(el("em", "", row.en)); }
    top.appendChild(title);
    hover.appendChild(top);

    var facts = el("dl", "item-hover-facts");
    function add(term, value) {
      if (value === "" || value == null) { return; }
      facts.appendChild(el("dt", "", term));
      facts.appendChild(el("dd", "", String(value)));
    }
    add("슬롯", row.slot);
    add("직업", row.cls + (row.sex ? " · " + row.sex + "전용" : ""));
    add("요구 레벨", row.lv || "제한 없음");
    add("가치", row.val ? row.val.toLocaleString("ko-KR") : "");
    add("내구도", row.dur ? row.dur.toLocaleString("ko-KR") : "");
    add("그림", row.img || "");
    row.stats.forEach(function (pair) { add(pair[0], pair[1]); });
    add("이름 근거", row.src || (nameOf(row) ? "직접 입력" : "아직 없음"));
    hover.appendChild(facts);

    var box = anchor.getBoundingClientRect();
    hover.hidden = false;
    hover.setAttribute("aria-hidden", "false");
    var own = hover.getBoundingClientRect();
    var left = Math.min(box.right + 12, window.innerWidth - own.width - 12);
    var top = Math.min(box.top, window.innerHeight - own.height - 12);
    hover.style.transform = "translate(" + Math.max(12, left) + "px," + Math.max(12, top) + "px)";
  }

  /* ── 카드 ──────────────────────────────────────────────────────────── */
  function card(row) {
    var article = el("article", "item-card");
    if (!nameOf(row)) { article.classList.add("is-unnamed"); }

    // 아이콘은 한 줄짜리 띠 한 장이라 칸만큼 밀어 쓴다. 아카이브에 없는 여섯 장은 빈 자리다.
    var icon = el("i", "item-icon");
    if (row.ic >= 0) {
      icon.style.backgroundPosition = "-" + (row.ic * data.아이콘.너비) + "px 0";
    } else {
      icon.classList.add("is-blank");
    }
    article.appendChild(icon);

    var head = el("div", "item-card-head");
    head.appendChild(el("b", "", nameOf(row) || row.en));
    if (nameOf(row)) { head.appendChild(el("em", "", row.en)); }
    article.appendChild(head);

    var meta = el("div", "item-card-meta");
    meta.appendChild(el("span", "item-slot", row.slot));
    meta.appendChild(el("span", "item-lv", row.lv ? "Lv" + row.lv : "Lv—"));
    if (row.head) { meta.appendChild(el("span", "item-head", row.head)); }
    if (row.cls !== "공용") { meta.appendChild(el("span", "item-cls", row.cls)); }
    article.appendChild(meta);

    var input = el("input", "item-name");
    input.type = "text";
    input.value = typed[row.en] || row.ko || "";
    input.placeholder = "한글 이름";
    input.setAttribute("aria-label", row.en + " 한글 이름");
    input.addEventListener("change", function () {
      remember(row.en, input.value.trim());
      render();
    });
    article.appendChild(input);

    article.addEventListener("mouseenter", function () { openHover(row, article); });
    article.addEventListener("mouseleave", closeHover);
    article.addEventListener("click", function (event) {
      if (event.target === input) { return; }         // 입력칸을 누른 것은 편집이다
      openHover(row, article);
    });
    return article;
  }

  /* ── 그리기 ────────────────────────────────────────────────────────── */
  function renderChips(host, values, current, onPick) {
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
    var rows = visible();
    var pages = Math.max(1, Math.ceil(rows.length / PER_PAGE));
    if (page >= pages) { page = pages - 1; }
    var slice = rows.slice(page * PER_PAGE, page * PER_PAGE + PER_PAGE);

    var grid = $("item-grid");
    grid.replaceChildren();
    slice.forEach(function (row) { grid.appendChild(card(row)); });

    $("item-empty").hidden = rows.length !== 0;
    var pager = $("item-pager");
    pager.hidden = pages < 2;
    $("item-pager-label").textContent = (page + 1) + " / " + pages + " 쪽";

    var named = data.목록.filter(function (r) { return nameOf(r); }).length;
    $("item-total").textContent = data.총.toLocaleString("ko-KR");
    $("item-named").textContent = named.toLocaleString("ko-KR")
      + " (" + Math.round((named / data.총) * 100) + "%)";
    $("item-unnamed").textContent = (data.총 - named).toLocaleString("ko-KR");
    $("item-shown").textContent = rows.length.toLocaleString("ko-KR");
    $("item-page-info").textContent = rows.length
      ? (page * PER_PAGE + 1) + "–" + (page * PER_PAGE + slice.length) + "번째"
      : "조건에 맞는 것이 없어요";

    renderChips($("item-slots"), data.슬롯, slot, function (v) { slot = v; page = 0; render(); });
    renderChips($("item-classes"), data.직업, cls, function (v) { cls = v; page = 0; render(); });
    closeHover();
  }

  /* ── 표로 내보내기 ─────────────────────────────────────────────────── */
  function exportTsv() {
    var box = $("item-export-box");
    var lines = ["영문\t한글"];
    data.목록.forEach(function (row) {
      var value = typed[row.en];
      if (value) { lines.push(row.en + "\t" + value); }
    });
    box.replaceChildren();
    if (lines.length === 1) {
      box.appendChild(el("p", "notice", "이 브라우저에서 고친 이름이 아직 없어요. 카드의 칸에 적어 보세요."));
    } else {
      box.appendChild(el("p", "", "아래를 복사해 data/아이템-한글이름.tsv 에 붙여 넣으세요 ("
        + (lines.length - 1) + "줄). 반영: python3 scripts/compare-packs.py && python3 scripts/build-item-page-data.py"));
      box.appendChild(el("pre", "item-export", lines.join("\n")));
    }
    box.hidden = false;
  }

  /* ── 배선 ──────────────────────────────────────────────────────────── */
  function boot() {
    hover = $("item-hover");
    if (!$("item-grid")) { return; }

    $("item-search").addEventListener("input", function (event) {
      query = event.target.value.trim().toLowerCase();
      page = 0;
      render();
    });
    $("item-only-unnamed").addEventListener("click", function (event) {
      onlyUnnamed = !onlyUnnamed;
      event.currentTarget.classList.toggle("is-active", onlyUnnamed);
      event.currentTarget.setAttribute("aria-pressed", onlyUnnamed ? "true" : "false");
      page = 0;
      render();
    });
    $("item-export").addEventListener("click", exportTsv);
    Array.prototype.forEach.call(document.querySelectorAll("[data-page-step]"), function (button) {
      button.addEventListener("click", function () {
        page = Math.max(0, page + Number(button.getAttribute("data-page-step")));
        render();
        $("item-grid").scrollIntoView({ block: "start", behavior: "smooth" });
      });
    });
    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape") { closeHover(); }
    });
    render();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
