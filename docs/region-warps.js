(function () {
  "use strict";
  var data = window.LOD_REGION_WARPS;
  if (!data) { return; }

  var dashboard = window.LodDashboard;
  var chart = document.querySelector("#warp-chart");
  var empty = document.querySelector("#warp-empty");
  var tabs = document.querySelector("#warp-region-tabs");
  var names = Object.keys(data.지역);
  var current = names[0];
  var picked = null;

  function text(tag, className, value) {
    var element = document.createElement(tag);
    if (className) { element.className = className; }
    if (value !== undefined) { element.textContent = value; }
    return element;
  }

  /**
   * 출발점에서 워프를 몇 번 타야 닿나. 이 깊이가 곧 화면의 세로줄이다 —
   * 마을이 맨 왼쪽, 한 번 만에 가는 곳이 그다음, 그런 식으로 읽힌다.
   */
  function depths(region) {
    var next = {};
    region.연결.forEach(function (edge) {
      if (edge.밖으로) { return; }
      (next[edge.부터] = next[edge.부터] || []).push(edge.까지);
    });
    var found = {};
    found[region.출발점] = 0;
    var queue = [region.출발점];
    while (queue.length) {
      var here = queue.shift();
      (next[here] || []).forEach(function (there) {
        if (found[there] !== undefined) { return; }
        found[there] = found[here] + 1;
        queue.push(there);
      });
    }
    return found;
  }

  function mapNode(node, region) {
    var button = text("button", "warp-node is-" + node.갈래);
    button.type = "button";
    button.dataset.warpNode = String(node.번호);
    if (node.출발점) { button.classList.add("is-start"); }
    if (!node.닿음) { button.classList.add("is-orphan"); }

    button.append(text("strong", "", node.이름));
    var facts = text("span", "warp-facts");
    facts.appendChild(text("i", "warp-kind", node.갈래));
    if (node.괴물) { facts.appendChild(text("i", "warp-fact is-monster", "괴물 " + node.괴물)); }
    if (node.NPC) { facts.appendChild(text("i", "warp-fact is-npc", "NPC " + node.NPC)); }
    if (node.월드맵) { facts.appendChild(text("i", "warp-fact is-worldmap", "월드맵")); }
    if (!node.괴물 && !node.NPC && node.닿음) { facts.appendChild(text("i", "warp-fact is-bare", "비어 있음")); }
    button.appendChild(facts);

    button.addEventListener("click", function () {
      picked = picked === node.번호 ? null : node.번호;
      paint(region);
    });
    return button;
  }

  /** 고른 맵의 들어오는 길·나가는 길만 남기고 나머지는 흐리게. */
  function paint(region) {
    var linked = {};
    if (picked !== null) {
      linked[picked] = true;
      region.연결.forEach(function (edge) {
        if (edge.부터 === picked) { linked[edge.까지] = true; }
        if (edge.까지 === picked) { linked[edge.부터] = true; }
      });
    }
    Array.prototype.forEach.call(chart.querySelectorAll("[data-warp-node]"), function (node) {
      var id = Number(node.dataset.warpNode);
      node.classList.toggle("is-dim", picked !== null && !linked[id]);
      node.setAttribute("aria-pressed", String(picked === id));
    });
    Array.prototype.forEach.call(chart.querySelectorAll("[data-warp-edge]"), function (line) {
      var ends = line.dataset.warpEdge.split(">").map(Number);
      var on = picked === null || ends[0] === picked || ends[1] === picked;
      line.classList.toggle("is-dim", !on);
    });
  }

  /** 줄을 다 놓은 뒤에야 좌표를 알 수 있다. 화면이 숨어 있으면 폭이 0이라 그리지 않는다. */
  function drawLines(region) {
    var svg = chart.querySelector(".warp-lines");
    if (!svg || !region) { return; }
    svg.replaceChildren();
    if (!chart.clientWidth) { return; }

    var frame = chart.getBoundingClientRect();
    var boxes = {};
    Array.prototype.forEach.call(chart.querySelectorAll("[data-warp-node]"), function (node) {
      var box = node.getBoundingClientRect();
      boxes[node.dataset.warpNode] = {
        left: box.left - frame.left + chart.scrollLeft,
        right: box.right - frame.left + chart.scrollLeft,
        middle: box.top - frame.top + chart.scrollTop + box.height / 2,
      };
    });
    svg.setAttribute("viewBox", "0 0 " + chart.scrollWidth + " " + chart.scrollHeight);
    svg.setAttribute("width", chart.scrollWidth);
    svg.setAttribute("height", chart.scrollHeight);

    var drawn = {};
    region.연결.forEach(function (edge) {
      var from = boxes[edge.부터];
      var to = boxes[edge.까지];
      if (!from || !to) { return; }
      // 왕복은 한 줄만 그린다 — 두 번 그으면 같은 자리에 겹쳐 굵어 보일 뿐이다.
      var key = edge.왕복 ? [edge.부터, edge.까지].sort().join(">") : edge.부터 + ">" + edge.까지;
      if (drawn[key]) { return; }
      drawn[key] = true;

      var x1 = from.right, x2 = to.left;
      if (x2 < x1) { x1 = from.left; x2 = to.right; }
      var mid = (x1 + x2) / 2;
      var path = document.createElementNS("http://www.w3.org/2000/svg", "path");
      path.setAttribute("d", "M" + x1 + " " + from.middle
        + " C" + mid + " " + from.middle + " " + mid + " " + to.middle + " " + x2 + " " + to.middle);
      path.setAttribute("class", "warp-line" + (edge.왕복 ? "" : " is-oneway"));
      path.setAttribute("data-warp-edge", edge.부터 + ">" + edge.까지);
      path.setAttribute("fill", "none");
      svg.appendChild(path);
    });
    paint(region);
  }

  function tally(region) {
    var host = document.querySelector("#warp-tally");
    host.replaceChildren();
    [
      { 이름: "맵", 값: region.셈.맵, 메모: "이 지역에 정의된 것" },
      { 이름: "마을에서 닿음", 값: region.셈.닿음, 메모: "워프를 타고 갈 수 있다" },
      { 이름: "못 닿는 맵", 값: region.셈.고아, 메모: region.셈.고아 ? "워프가 하나도 없다" : "없다" },
      { 이름: "한 방향 길", 값: region.셈.한방향, 메모: region.셈.한방향 ? "들어가면 못 나온다" : "모두 왕복" },
      { 이름: "지역 밖으로", 값: region.밖.length, 메모: region.밖.length
        ? region.밖.map(function (edge) { return edge.까지; }).join(" · ") : "아직 없다" },
    ].forEach(function (cell) {
      var node = text("div", "tally");
      node.append(text("span", "", cell.이름), text("strong", "", String(cell.값)), text("small", "", cell.메모));
      host.appendChild(node);
    });
  }

  function render() {
    var region = data.지역[current];
    tabs.replaceChildren();
    names.forEach(function (name) {
      var tab = text("button", "", name + " 지역");
      tab.type = "button";
      tab.setAttribute("role", "tab");
      tab.dataset.warpRegion = name;
      tab.setAttribute("aria-selected", String(name === current));
      tab.tabIndex = name === current ? 0 : -1;
      tab.addEventListener("click", function () { current = name; picked = null; render(); });
      tabs.appendChild(tab);
    });

    if (!region) { chart.replaceChildren(); empty.hidden = false; return; }
    empty.hidden = true;
    tally(region);

    var found = depths(region);
    var reachable = region.맵.filter(function (node) { return found[node.번호] !== undefined; });
    var orphans = region.맵.filter(function (node) { return found[node.번호] === undefined; });
    var deepest = reachable.reduce(function (max, node) { return Math.max(max, found[node.번호]); }, 0);

    chart.replaceChildren();
    var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("class", "warp-lines");
    svg.setAttribute("aria-hidden", "true");
    chart.appendChild(svg);

    var columns = text("div", "warp-columns");
    for (var depth = 0; depth <= deepest; depth += 1) {
      var here = reachable.filter(function (node) { return found[node.번호] === depth; });
      if (!here.length) { continue; }
      var column = text("div", "warp-column");
      column.append(text("h3", "", depth === 0 ? "출발점" : "워프 " + depth + "번"));
      here.sort(function (a, b) { return b.괴물 - a.괴물 || a.번호 - b.번호; });
      here.forEach(function (node) { column.appendChild(mapNode(node, region)); });
      columns.appendChild(column);
    }
    chart.appendChild(columns);

    if (orphans.length) {
      var aside = text("div", "warp-orphans");
      aside.append(text("h3", "", "마을에서 못 닿는 맵 " + orphans.length + "개 — 워프가 없다"));
      var strip = text("div", "warp-orphan-strip");
      orphans.forEach(function (node) { strip.appendChild(mapNode(node, region)); });
      aside.appendChild(strip);
      chart.appendChild(aside);
    }

    if (region.밖.length) {
      chart.appendChild(text("p", "warp-outside", "이 지역에서 밖으로 나가는 길: "
        + region.밖.map(function (edge) { return edge.부터 + " → " + edge.까지; }).join(" · ")));
    }

    window.requestAnimationFrame(function () { drawLines(region); });
  }

  dashboard.tabKeys(tabs, "data-warp-region", function (name) { current = name; picked = null; render(); });
  dashboard.onViewShown(function (view) { if (view === "world") { render(); } });
  window.addEventListener("resize", function () { drawLines(data.지역[current]); });

  render();
})();
