/* 지도 — 맵을 원작 타일로 다시 그리고 출구 방향대로 띄워 놓아 화살표로 잇는다. 맵을 누르면 그 맵으로 다가가 나오는 괴물 · 떨어지는 것 · NPC.
   사용자 2026-10-09 「맵 복원해서 방향으로 연결하는 그런 방식」 · 「여백을 좀 주고 화살표 같은걸 넣고 해당 맵 클릭하면 확대시켜서 나오는 몬스터,
   드랍정보, npc 같은 정보 보여주게해」. 배치·그림: scripts/gen/world/build-atlas.py(atlas-layout-data.js) · 괴물·드랍·NPC: 게임 볼트 생성기
   (atlas-data.js — 이 화면을 처음 열 때 불러온다, 1MB). */
(function () {
  "use strict";
  var layout = window.LOD_ATLAS_LAYOUT;
  var dashboard = window.LodDashboard;
  var SVG = "http://www.w3.org/2000/svg";
  var stage = document.getElementById("atlas-stage");
  var svg = document.getElementById("atlas-svg");
  var panel = document.getElementById("atlas-panel");
  var state = document.getElementById("atlas-state");
  var tabs = document.getElementById("atlas-regions");
  if (!layout || !stage || !svg) { return; }

  var still = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  var region = null, chosen = null, box = null, started = false;

  function el(tag, attrs, parent) {
    var node = document.createElementNS(SVG, tag);
    Object.keys(attrs || {}).forEach(function (key) { node.setAttribute(key, attrs[key]); });
    if (parent) { parent.appendChild(node); }
    return node;
  }
  function esc(text) { return String(text == null ? "" : text).replace(/[&<>"]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]; }); }
  function pct(rate) { return rate == null ? "표" : (rate * 100).toFixed(rate < 0.01 ? 2 : 1) + "%"; }
  function atlas() { return window.LOD_ATLAS || null; }
  function info(id) { var a = atlas(); return a ? a["맵"][String(id)] : null; }
  // 지역 공통 앞말(「서의우드랜드」·「아벨해안」·「노비스」)을 떼어 짧게 — 「9-1」·「3-A」·「지하던전C1」. 전체 이름은 옆 판에.
  function short(id) {
    var base = region.name.replace(/(대기실|입구\d*|마을|\d+존)$/, ""), full = (info(id) || {}).name || ("맵 " + id);
    return base && full.indexOf(base) === 0 && full.length > base.length ? full.slice(base.length) : full;
  }

  // ---- 지역 고르기 ----
  function drawTabs() {
    tabs.innerHTML = layout["지역"].map(function (r, i) {
      return '<button type="button" role="tab" class="atlas-tab" data-region="' + i + '" aria-selected="' + (r === region) + '">' +
        esc(r.name) + '<span class="num">' + Object.keys(r.maps).length + '</span></button>';
    }).join("");
    Array.prototype.forEach.call(tabs.querySelectorAll("button"), function (button) {
      button.addEventListener("click", function () { open(layout["지역"][Number(button.getAttribute("data-region"))]); });
    });
  }

  function diamond(m) {
    var k = layout["한눈"], r = m.rows, c = m.cols;
    return [[r * 28 * k, 0], [(r + c) * 28 * k, c * 13 * k], [c * 28 * k, (c + r) * 13 * k], [0, r * 13 * k]]
      .map(function (p) { return (m.x + p[0]).toFixed(1) + "," + (m.y + p[1]).toFixed(1); }).join(" ");
  }

  // ---- 그리기 ----
  function open(next, focusMap) {
    region = next; chosen = null;
    drawTabs();
    svg.innerHTML = "";
    var defs = el("defs", {}, svg);
    var head = el("marker", { id: "atlas-arrow", viewBox: "0 0 10 10", refX: "8", refY: "5", markerWidth: "7", markerHeight: "7", orient: "auto-start-reverse" }, defs);
    el("path", { d: "M0,0 L10,5 L0,10 z", class: "atlas-arrow-head" }, head);

    var maps = el("g", { class: "atlas-maps" }, svg);
    Object.keys(region.maps).forEach(function (id) {
      var m = region.maps[id], about = info(id) || {};
      var g = el("g", { class: "atlas-map circle-" + (about.circle || 0), tabindex: "0", "data-map": id, role: "button", "aria-label": (about.name || id) + " 자세히" }, maps);
      el("image", { href: "atlas-maps/" + id + "-s.webp", x: m.x, y: m.y, width: m.w, height: m.h, preserveAspectRatio: "none" }, g);
      el("polygon", { points: diamond(m), class: "atlas-outline" }, g);
      g.addEventListener("click", function () { choose(id); });
      g.addEventListener("keydown", function (event) { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); choose(id); } });
    });

    var lines = el("g", { class: "atlas-arrows" }, svg);
    var stubs = [];
    region.arrows.forEach(function (a) {
      // 곧게 그으면 다른 맵을 지나가는 연결 — 양쪽 가장자리에 짧은 화살표와 갈 곳 이름만(사용자 「맵을 가르지르는 방식으로 화살표 그리지마」).
      if (a.stub) {
        [[a.a, a.aOut, a.to], [a.b, a.bOut, a.from]].forEach(function (end) {
          var p = end[0], v = end[1], tip = [p[0] + v[0] * 22, p[1] + v[1] * 22];
          el("line", { x1: p[0], y1: p[1], x2: tip[0], y2: tip[1], class: "atlas-arrow-halo" }, lines);
          el("line", { x1: p[0], y1: p[1], x2: tip[0], y2: tip[1], class: "atlas-arrow", "marker-end": "url(#atlas-arrow)" }, lines);
          stubs.push([tip, v, end[2]]);
        });
        return;
      }
      var dx = a.b[0] - a.a[0], dy = a.b[1] - a.a[1], len = Math.max(1, Math.hypot(dx, dy)), cut = Math.min(10, len / 4);
      var x1 = a.a[0] + dx / len * cut, y1 = a.a[1] + dy / len * cut, x2 = a.b[0] - dx / len * cut, y2 = a.b[1] - dy / len * cut;
      var attrs = { x1: x1, y1: y1, x2: x2, y2: y2, class: "atlas-arrow", "marker-end": "url(#atlas-arrow)" };
      if (a.both) { attrs["marker-start"] = "url(#atlas-arrow)"; }
      el("line", { x1: x1, y1: y1, x2: x2, y2: y2, class: "atlas-arrow-halo" }, lines);
      el("line", attrs, lines);
    });

    var doors = el("g", { class: "atlas-doors" }, svg);
    region.doors.forEach(function (d) {
      var dot = el("circle", { cx: d.at[0], cy: d.at[1], r: 3.2, class: "atlas-door" }, doors);
      el("title", {}, dot).textContent = "문 → " + d.toName;
    });

    var labels = el("g", { class: "atlas-labels" }, svg);
    stubs.forEach(function (s) {
      var about = info(s[2]) || {}, label = el("text", { x: s[0][0] + s[1][0] * 6, y: s[0][1] + s[1][1] * 6 + 4, class: "atlas-goto" }, labels);
      label.setAttribute("text-anchor", s[1][0] < -0.2 ? "end" : s[1][0] > 0.2 ? "start" : "middle");
      label.textContent = "→ " + short(s[2]);
    });
    Object.keys(region.maps).forEach(function (id) {
      var m = region.maps[id], about = info(id) || {};
      var t = el("text", { x: m.x + m.w / 2, y: m.y + m.h / 2, class: "atlas-label" }, labels);
      t.style.setProperty("--dy", "0");
      t.textContent = short(id);
      if (about.monsters && about.monsters.length) {
        var n = el("text", { x: m.x + m.w / 2, y: m.y + m.h / 2, dy: "1.3em", class: "atlas-sub" }, labels);
        n.textContent = "괴물 " + about.monsters.length + (about.circle ? " · 서클 " + about.circle : "");
      }
    });

    whole(true);
    describe(null);
    if (focusMap && region.maps[String(focusMap)]) { choose(String(focusMap)); }
  }

  // ---- 보는 칸(viewBox) ----
  // 글자는 확대와 상관없이 화면에서 같은 크기 — 지도 단위로 바꿔 무리에 준다.
  function setBox(next) {
    box = next;
    svg.setAttribute("viewBox", next.map(function (v) { return v.toFixed(1); }).join(" "));
    var unit = box[2] / Math.max(1, svg.getBoundingClientRect().width), labels = svg.querySelector(".atlas-labels");
    if (labels) { labels.style.setProperty("--atlas-unit", unit.toFixed(3)); }
    // 멀리서 볼 때는 맵 이름만 — 「괴물 n · 서클 n」 줄은 다가가면 보인다(24장 지역에서 이름이 겹쳤다).
    svg.classList.toggle("is-far", unit > 0.75);
  }
  function fly(target) {
    if (still || !box) { setBox(target); return; }
    var from = box.slice(), begin = performance.now();
    (function step(now) {
      var t = Math.min(1, (now - begin) / 260), e = 1 - Math.pow(1 - t, 3);
      setBox(from.map(function (v, i) { return v + (target[i] - v) * e; }));
      if (t < 1) { requestAnimationFrame(step); }
    })(begin);
  }
  function whole(instant) {
    var pad = Math.max(region.w, region.h) * 0.08;  // 가장자리 짧은 화살표의 이름이 잘리지 않게
    var target = [-pad, -pad, region.w + pad * 2, region.h + pad * 2];
    if (instant) { setBox(target); } else { fly(target); }
    chosen = null;
    Array.prototype.forEach.call(svg.querySelectorAll(".atlas-map.is-chosen"), function (g) { g.classList.remove("is-chosen"); });
  }

  function choose(id) {
    var m = region.maps[id];
    if (!m) { return; }
    chosen = id;
    Array.prototype.forEach.call(svg.querySelectorAll(".atlas-map"), function (g) { g.classList.toggle("is-chosen", g.getAttribute("data-map") === id); });
    var image = svg.querySelector('.atlas-map[data-map="' + id + '"] image');
    if (image && image.getAttribute("href").indexOf("-l.webp") < 0) { image.setAttribute("href", "atlas-maps/" + id + "-l.webp"); }
    var pad = Math.max(m.w, m.h) * 0.18;
    var rect = stage.getBoundingClientRect(), aspect = rect.width / Math.max(1, rect.height);
    var w = m.w + pad * 2, h = m.h + pad * 2;
    if (w / h < aspect) { w = h * aspect; } else { h = w / aspect; }
    fly([m.x + m.w / 2 - w / 2, m.y + m.h / 2 - h / 2, w, h]);
    describe(id);
  }

  // ---- 옆 판: 결론 · 괴물 · 떨어지는 것 · NPC · 이어진 곳 ----
  function describe(id) {
    var a = atlas();
    if (!a) { panel.innerHTML = '<p class="atlas-empty">괴물·드랍 자료를 불러오는 중입니다…</p>'; return; }
    if (!id) {
      panel.innerHTML = '<h2>' + esc(region.name) + '</h2><p class="atlas-lead">맵 <b class="num">' + Object.keys(region.maps).length +
        '</b>곳 · 화살표는 출구가 이어진 방향입니다. 맵을 누르면 다가가 나오는 괴물 · 떨어지는 것 · NPC 를 보여 줍니다.</p>' +
        '<ul class="atlas-legend"><li><i class="sw c1"></i>서클 1 · 1~10</li><li><i class="sw c2"></i>서클 2 · 11~40</li><li><i class="sw c3"></i>서클 3 · 41~70</li><li><i class="sw c4"></i>서클 4 · 71~98</li><li><i class="sw c5"></i>서클 5 · 99</li><li><i class="sw door"></i>문(건물·다른 지역)</li></ul>';
      return;
    }
    var m = info(id) || { name: id, monsters: [], npcs: [], exits: [] };
    var mons = m.monsters.map(function (key) { return [key, a["괴물"][key]]; }).filter(function (p) { return p[1]; });
    var items = a["아이템"];
    function gear(mon) { return mon.drops.reduce(function (s, d) { return s + ((items[d[0]] || {}).kind === "장비" && d[1] ? d[1] : 0); }, 0); }
    var drops = {};
    mons.forEach(function (p) { p[1].drops.forEach(function (d) { drops[d[0]] = Math.max(drops[d[0]] || 0, d[1] || 0); }); });
    var top = Object.keys(drops).sort(function (x, y) { return drops[y] - drops[x]; }).slice(0, 16);
    var most = top.length ? drops[top[0]] || 1 : 1;
    var avgGear = mons.length ? mons.reduce(function (s, p) { return s + gear(p[1]); }, 0) / mons.length : 0;
    var near = region.arrows.filter(function (x) { return String(x.from) === id || String(x.to) === id; })
      .map(function (x) { return String(x.from) === id ? x.to : x.from; });
    var doors = region.doors.filter(function (d) { return String(d.map) === id; });

    panel.innerHTML =
      '<div class="atlas-panel-head"><h2>' + esc(m.name) + '</h2><button type="button" class="atlas-back" id="atlas-back">전체 보기</button></div>' +
      '<p class="atlas-verdict">' + (m.circle ? '<strong>서클 ' + m.circle + '</strong> · ' : '') + '괴물 <b class="num">' + mons.length + '</b>종 · 한 마리 장비 확률 <b class="num">' + pct(avgGear) + '</b> · NPC <b class="num">' + m.npcs.length + '</b></p>' +
      (near.length || doors.length ? '<h3>이어진 곳</h3><div class="atlas-chips">' +
        near.map(function (to) { var n = info(to); return '<button type="button" data-go="' + to + '">→ ' + esc(n ? n.name : to) + '</button>'; }).join("") +
        doors.map(function (d) { return '<span class="atlas-chip-door">문 → ' + esc(d.toName) + '</span>'; }).join("") + '</div>' : '') +
      '<h3>괴물</h3>' + (mons.length ? '<table class="atlas-table"><thead><tr><th>이름</th><th class="r">레벨</th><th class="r">경험치</th><th class="r">장비</th></tr></thead><tbody>' +
        mons.map(function (p) { return '<tr><td>' + esc(p[1].name) + '</td><td class="r num">' + p[1].lv + '</td><td class="r num">' + p[1].exp.toLocaleString() + '</td><td class="r num">' + pct(gear(p[1])) + '</td></tr>'; }).join("") +
        '</tbody></table>' : '<p class="atlas-empty">괴물이 없는 곳입니다.</p>') +
      '<h3>떨어지는 것 <small>한 마리 최대 확률</small></h3>' + (top.length ? '<table class="atlas-table"><tbody>' +
        top.map(function (name) {
          var it = items[name] || {};
          var tags = it.kind === "장비" ? '<span class="atlas-tag">' + esc(it.slot) + ' <span class="num">' + it.lv + '</span></span>' + (it.sheet === "같음" ? '<span class="atlas-tag good">어둠템</span>' : it.sheet === "없음" ? '<span class="atlas-tag warn">팩</span>' : "") : '<span class="atlas-tag">' + esc(it.kind || "?") + '</span>';
          return '<tr><td>' + esc(name) + tags + '</td><td class="atlas-bar"><i style="width:' + Math.max(2, Math.round(100 * drops[name] / most)) + '%"></i></td><td class="r num">' + pct(drops[name]) + '</td></tr>';
        }).join("") + '</tbody></table>' : '<p class="atlas-empty">떨어지는 것이 없습니다.</p>') +
      '<h3>NPC</h3>' + (m.npcs.length ? '<ul class="atlas-npcs">' + m.npcs.map(function (key) { var n = a["NPC"][key] || {}; return '<li><b>' + esc(n.name) + '</b><span class="atlas-tag">' + esc(n.role) + '</span><small>' + esc(n.about) + '</small></li>'; }).join("") + '</ul>' : '<p class="atlas-empty">NPC 가 없습니다.</p>');

    document.getElementById("atlas-back").addEventListener("click", function () { whole(false); describe(null); });
    Array.prototype.forEach.call(panel.querySelectorAll("[data-go]"), function (b) { b.addEventListener("click", function () { choose(b.getAttribute("data-go")); }); });
  }

  // ---- 끌어 옮기기 · 휠/두 손가락 확대 ----
  var pointers = {}, lastSpread = null, moved = 0;
  function toBox(clientX, clientY) {
    var r = svg.getBoundingClientRect();
    return [box[0] + (clientX - r.left) / r.width * box[2], box[1] + (clientY - r.top) / r.height * box[3]];
  }
  function zoomAt(clientX, clientY, factor) {
    var at = toBox(clientX, clientY), w = Math.min(region.w * 2, Math.max(60, box[2] * factor)), h = w * box[3] / box[2];
    setBox([at[0] - (at[0] - box[0]) * (w / box[2]), at[1] - (at[1] - box[1]) * (h / box[3]), w, h]);
  }
  svg.addEventListener("wheel", function (event) { event.preventDefault(); zoomAt(event.clientX, event.clientY, event.deltaY > 0 ? 1.15 : 1 / 1.15); }, { passive: false });
  svg.addEventListener("pointerdown", function (event) { pointers[event.pointerId] = [event.clientX, event.clientY]; moved = 0; });
  svg.addEventListener("pointermove", function (event) {
    var was = pointers[event.pointerId];
    if (!was) { return; }
    pointers[event.pointerId] = [event.clientX, event.clientY];
    var ids = Object.keys(pointers), r = svg.getBoundingClientRect();
    if (ids.length === 2) {
      var p = pointers[ids[0]], q = pointers[ids[1]], spread = Math.hypot(p[0] - q[0], p[1] - q[1]);
      if (lastSpread) { zoomAt((p[0] + q[0]) / 2, (p[1] + q[1]) / 2, lastSpread / spread); }
      lastSpread = spread;
      return;
    }
    moved += Math.abs(event.clientX - was[0]) + Math.abs(event.clientY - was[1]);
    var dx = (event.clientX - was[0]) / r.width * box[2], dy = (event.clientY - was[1]) / r.height * box[3];
    setBox([box[0] - dx, box[1] - dy, box[2], box[3]]);
  });
  function lift(event) { delete pointers[event.pointerId]; lastSpread = null; }
  svg.addEventListener("pointerup", lift);
  svg.addEventListener("pointercancel", lift);
  svg.addEventListener("pointerleave", lift);
  // 끌어 옮긴 뒤 손을 떼면 맵 위라도 「눌렀다」로 치지 않는다.
  svg.addEventListener("click", function (event) { if (moved > 6) { event.stopPropagation(); } }, true);

  // ---- 처음 열 때 ----
  function start() {
    if (started) { return; }
    started = true;
    var params = new URLSearchParams(window.location.search);
    var pick = layout["지역"].filter(function (r) { return String(r.card) === params.get("card"); })[0] || layout["지역"].filter(function (r) { return r.name === "아벨해안대기실"; })[0] || layout["지역"][0];
    open(pick, params.get("map"));
    if (window.LOD_ATLAS) { state.hidden = true; return; }
    var script = document.createElement("script");
    script.src = "atlas-data.js";
    script.onload = function () { state.hidden = true; open(region, chosen || params.get("map")); };
    script.onerror = function () { state.textContent = "괴물·드랍 자료(atlas-data.js)를 불러오지 못했습니다 — 지도만 보입니다. 새로고침해 보세요."; };
    document.head.appendChild(script);
  }
  if (dashboard) { dashboard.onViewShown(function (view) { if (view === "world") { start(); } }); }
  var section = document.querySelector('[data-view="world"]');
  if (section && !section.hidden) { start(); }
})();
