/* 월드 지도 — 워프가 맵을 어떻게 잇는지 눈으로 본다.
 *
 * 개수 검사로는 세계가 이어졌는지 알 수 없다. 워프 890장이 다 실렸다는 말은 "파일 890개를
 * 읽었다"는 뜻이지 "갈 수 있다"는 뜻이 아니다. 그래서 덩어리로 묶어 보여 준다 —
 * 이어진 것끼리 한 덩어리, 아무 데도 안 이어진 맵은 따로. */
(function () {
  "use strict";

  var DATA = window.WORLD_MAP_DATA;
  if (!DATA) return;

  var listEl = document.getElementById("world-list");
  var detailEl = document.getElementById("world-detail");
  var searchEl = document.getElementById("world-search");
  var summaryEl = document.getElementById("world-summary");
  if (!listEl || !detailEl) return;

  var maps = DATA["맵"];
  var name = function (id) { return (maps[id] && maps[id]["이름"]) || ("#" + id); };

  // 이웃 목록. 방향을 살려 둔다 — 한쪽으로만 난 길이 실제로 있다.
  var out = {}, inn = {};
  DATA["간선"].forEach(function (e) {
    (out[e[0]] = out[e[0]] || []).push({ to: e[1], n: e[2] });
    (inn[e[1]] = inn[e[1]] || []).push({ to: e[0], n: e[2] });
  });

  function summary() {
    var s = DATA["요약"];
    summaryEl.innerHTML =
      [["맵", s["맵"]], ["워프", s["워프"]], ["이어진 맵", s["이어진맵"]],
       ["혼자인 맵", s["혼자인맵"]], ["덩어리", s["덩어리"]]]
        .map(function (p) {
          return '<div class="world-stat"><span>' + p[0] + '</span><strong>' + p[1] + "</strong></div>";
        }).join("") +
      '<p class="world-note">혼자인 맵 ' + s["혼자인맵"] + "개는 고장이 아니다 — 팩의 <code>warp_db.txt</code> 가 " +
      "워프 파일 17개 중 10개만 싣는다. 빠진 것: " + DATA["안싣는워프파일"].join(", ") + ".</p>";
  }

  var entries = [];   // 왼쪽 목록 한 줄 = 덩어리 하나 또는 혼자인 맵 하나
  DATA["덩어리"].forEach(function (ids, i) {
    entries.push({ key: "c" + i, label: name(ids[0]) + " 일대", count: ids.length, ids: ids, lone: false });
  });
  DATA["혼자"].forEach(function (id) {
    entries.push({ key: "s" + id, label: name(id), count: 1, ids: [id], lone: true });
  });

  function renderList(filter) {
    var q = (filter || "").trim().toLowerCase();
    var shown = entries.filter(function (e) {
      if (!q) return true;
      return e.ids.some(function (id) { return name(id).toLowerCase().indexOf(q) >= 0; });
    });
    listEl.innerHTML = shown.length
      ? shown.map(function (e) {
          return '<button type="button" class="world-item' + (e.lone ? " is-lone" : "") +
            '" data-key="' + e.key + '"><span>' + e.label + "</span><em>" +
            (e.lone ? "혼자" : e.count + "개") + "</em></button>";
        }).join("")
      : '<p class="world-empty">그런 이름의 맵이 없습니다.</p>';
    if (shown.length) select(shown[0].key);
  }

  function tree(rootId, ids) {
    // 가장 많이 이어진 맵에서 시작해 너비 우선으로 펼친다 — 세계를 걸어 들어가는 순서다.
    var inSet = {}; ids.forEach(function (i) { inSet[i] = true; });
    var seen = {}, rows = [], queue = [[rootId, 0]];
    seen[rootId] = true;
    while (queue.length) {
      var cur = queue.shift(), id = cur[0], depth = cur[1];
      var links = (out[id] || []).filter(function (l) { return inSet[l.to]; });
      rows.push({ id: id, depth: depth, links: links });
      links.forEach(function (l) {
        if (!seen[l.to]) { seen[l.to] = true; queue.push([l.to, depth + 1]); }
      });
    }
    // 한 방향으로만 이어져 BFS 가 못 닿은 맵도 빠뜨리지 않는다
    ids.forEach(function (i) { if (!seen[i]) rows.push({ id: i, depth: 0, links: [], orphan: true }); });
    return rows;
  }

  function select(key) {
    var e = entries.filter(function (x) { return x.key === key; })[0];
    if (!e) return;
    Array.prototype.forEach.call(listEl.querySelectorAll(".world-item"), function (b) {
      b.classList.toggle("is-active", b.getAttribute("data-key") === key);
    });

    if (e.lone) {
      var id = e.ids[0], m = maps[id];
      detailEl.innerHTML = '<h2>' + m["이름"] + "</h2>" +
        '<p class="world-meta">' + m["가로"] + "×" + m["세로"] + " · 번호 " + id + "</p>" +
        '<p class="world-warn">이 맵으로 들어오거나 나가는 워프가 <strong>없다.</strong> ' +
        "맵은 실렸지만 걸어서 갈 수 없다.</p>";
      return;
    }

    var rows = tree(e.ids[0], e.ids);
    detailEl.innerHTML = "<h2>" + e.label + "</h2>" +
      '<p class="world-meta">맵 ' + e.ids.length + "개 · " +
      DATA["간선"].filter(function (g) { return e.ids.indexOf(g[0]) >= 0; })
        .reduce(function (a, g) { return a + g[2]; }, 0) + "줄의 워프</p>" +
      '<ol class="world-tree">' + rows.map(function (r) {
        var m = maps[r.id];
        var links = r.links.map(function (l) {
          return '<span class="world-link">→ ' + name(l.to) + (l.n > 1 ? " ×" + l.n : "") + "</span>";
        }).join("");
        return '<li style="--depth:' + r.depth + '">' +
          '<b>' + m["이름"] + "</b>" +
          '<em>' + m["가로"] + "×" + m["세로"] + "</em>" +
          (r.orphan ? '<span class="world-oneway">들어오기만 한다</span>' : "") +
          (links ? '<div class="world-links">' + links + "</div>" : "") + "</li>";
      }).join("") + "</ol>";
  }

  listEl.addEventListener("click", function (ev) {
    var b = ev.target.closest ? ev.target.closest(".world-item") : null;
    if (b) select(b.getAttribute("data-key"));
  });
  if (searchEl) {
    searchEl.addEventListener("input", function () { renderList(searchEl.value); });
  }

  summary();
  renderList("");
})();
