/* 월드 지도 — 연결 구역, 현재 맵, 워프 방향을 초보자도 순서대로 읽는다. */
(function () {
  "use strict";

  var DATA = window.WORLD_MAP_DATA;
  var MODEL = window.LODWorldMapModel;
  if (!DATA || !MODEL) return;

  var model = MODEL.create(DATA);
  var images = window.MAP_IMAGES || {};
  var summaryEl = document.getElementById("world-summary");
  var regionsEl = document.getElementById("world-regions");
  var detailEl = document.getElementById("world-detail");
  var listEl = document.getElementById("world-list");
  var countEl = document.getElementById("world-result-count");
  var searchEl = document.getElementById("world-search");
  var filtersEl = document.getElementById("world-filters");
  if (!summaryEl || !regionsEl || !detailEl || !listEl) return;

  var state = { selectedId: null, filter: "connected", query: "" };
  var statusCopy = {
    both: ["양방향 연결", "들어오는 길과 나가는 길이 모두 있습니다."],
    incoming: ["진입 전용", "들어올 수 있지만 이 데이터에는 나가는 워프가 없습니다."],
    outgoing: ["출발 전용", "나갈 수 있지만 이 데이터에는 들어오는 워프가 없습니다."],
    isolated: ["미연결", "현재 서버 데이터에서 들어오거나 나가는 워프를 찾지 못했습니다."]
  };

  function esc(value) {
    return String(value).replace(/[&<>"']/g, function (char) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[char];
    });
  }

  function mapName(id) {
    return model.byId[String(id)] ? model.byId[String(id)].name : "#" + id;
  }

  function routeCount(routes) {
    return routes.reduce(function (sum, route) { return sum + route.count; }, 0);
  }

  function representative(cluster) {
    return cluster.ids.slice().sort(function (a, b) {
      var ai = images[mapName(a)] ? 10000 : 0;
      var bi = images[mapName(b)] ? 10000 : 0;
      var ac = model.connections(a), bc = model.connections(b);
      return (bi + routeCount(bc.incoming) + routeCount(bc.outgoing)) -
        (ai + routeCount(ac.incoming) + routeCount(ac.outgoing));
    })[0];
  }

  function renderSummary() {
    var s = DATA["요약"];
    var values = [
      ["전체 맵", s["맵"], "서버 areas"],
      ["연결된 맵", s["이어진맵"], "워프 1개 이상"],
      ["연결 구역", s["덩어리"], "서로 닿는 묶음"],
      ["미연결 맵", s["혼자인맵"], "현재 워프 없음"]
    ];
    summaryEl.innerHTML = values.map(function (value) {
      return '<div class="world-stat"><span>' + value[0] + '</span><strong>' + value[1] +
        '</strong><small>' + value[2] + '</small></div>';
    }).join("") +
      '<p class="world-note"><strong>모두 Hades 기준입니다.</strong> 연결과 방향은 Hades 서버의 ' +
      '<code>areas/</code>·<code>templates/warps/</code>, 지형 그림은 Hades의 <code>server/maps/</code>와 <code>archives/seo/seo.dat</code>입니다. ' +
      '미연결은 곧 고장이라는 뜻이 아니라, 현재 로드된 워프 자료에서 연결을 확인하지 못했다는 뜻입니다.</p>';
  }

  function regionLabel(cluster) {
    var id = representative(cluster);
    return mapName(id) + " 일대";
  }

  function renderRegions() {
    var selectedCluster = state.selectedId == null ? null : model.clusterById[state.selectedId];
    var ordered = model.clusters.slice().sort(function (a, b) { return b.mapCount - a.mapCount; });
    regionsEl.innerHTML = ordered.map(function (cluster) {
      var current = selectedCluster === cluster.index;
      return '<button type="button" class="world-region" data-region="' + cluster.index +
        '" aria-pressed="' + current + '"><span class="world-region-mark" aria-hidden="true"></span>' +
        '<strong>' + esc(regionLabel(cluster)) + '</strong><small>맵 ' + cluster.mapCount +
        '개 · 워프 칸 ' + cluster.warpCount + '</small></button>';
    }).join("") +
      '<button type="button" class="world-region is-isolated" data-filter-region="isolated" ' +
      'aria-pressed="' + (state.filter === "isolated") + '"><span class="world-region-mark" aria-hidden="true"></span>' +
      '<strong>미연결 보관함</strong><small>맵 ' + DATA["요약"]["혼자인맵"] + '개 · 검색 권장</small></button>';
  }

  function filteredMaps() {
    return model.search(state.query).filter(function (map) {
      var connected = model.status(map.id) !== "isolated";
      if (state.filter === "connected") return connected;
      if (state.filter === "isolated") return !connected;
      if (state.filter === "image") return Boolean(images[map.name]);
      return true;
    });
  }

  function renderList(selectFirst) {
    var matches = filteredMaps();
    matches.sort(function (a, b) {
      return Number(b.id === state.selectedId) - Number(a.id === state.selectedId);
    });
    var shown = matches.slice(0, 160);
    countEl.textContent = matches.length + "개" + (matches.length > shown.length ? " · 앞 160개 표시" : "");
    listEl.innerHTML = shown.length ? shown.map(function (map) {
      var links = model.connections(map.id);
      var active = map.id === state.selectedId;
      return '<button type="button" class="world-item" data-map-id="' + esc(map.id) +
        '" aria-current="' + (active ? "true" : "false") + '"><span><strong>' + esc(map.name) +
        '</strong><small>#' + esc(map.id) + ' · ' + map.width + '×' + map.height + '</small></span><em>' +
        (images[map.name] ? '<i title="지형 그림 있음">▧</i> ' : '') +
        (links.incoming.length + links.outgoing.length) + '방향</em></button>';
    }).join("") : '<p class="world-empty"><strong>조건에 맞는 맵이 없습니다.</strong><span>검색어를 줄이거나 표시 범위를 바꿔 보세요.</span></p>';

    if (selectFirst && shown.length) selectMap(shown[0].id, false);
  }

  function routeButtons(routes, kind) {
    if (!routes.length) {
      return '<p class="world-route-empty">' + (kind === "incoming" ? "들어오는 워프 없음" : "나가는 워프 없음") + '</p>';
    }
    return routes.slice().sort(function (a, b) { return mapName(a.id).localeCompare(mapName(b.id), "ko"); })
      .map(function (route) {
        return '<button type="button" class="world-route" data-map-id="' + esc(route.id) + '"><span>' +
          esc(mapName(route.id)) + '</span><small>워프 칸 ' + route.count +
          (route.reciprocal ? ' · <b>왕복</b>' : ' · 편도') + '</small></button>';
      }).join("");
  }

  function renderPicture(map) {
    var image = images[map.name];
    if (!image) {
      return '<section class="world-terrain"><div class="world-terrain-heading"><div><p class="eyebrow">Terrain</p>' +
        '<h3>지형 그림</h3></div><span class="badge">미추출</span></div>' +
        '<div class="world-terrain-empty"><strong>이 맵의 지형 그림은 아직 없습니다.</strong>' +
        '<p>워프 연결은 위 구조도에서 확인할 수 있습니다. 실제 지형 그림은 지역별 검토가 끝난 것부터 추가합니다.</p></div></section>';
    }
    var pins = image["표시"].map(function (pin, index) {
      var destinations = pin["도착"].join(", ");
      var label = map.name + " " + pin["칸"][0] + "," + pin["칸"][1] + "에서 " + destinations + "(으)로";
      return '<button type="button" class="map-pin" data-map-pin="' + index + '" style="left:' +
        (pin.x / image["폭"] * 100).toFixed(3) + '%;top:' + (pin.y / image["높이"] * 100).toFixed(3) +
        '%" aria-label="' + esc(label) + '"><span>' + esc(destinations) + '</span></button>';
    }).join("");
    return '<section class="world-terrain"><div class="world-terrain-heading"><div><p class="eyebrow">Terrain</p>' +
      '<h3>지형과 워프 칸</h3></div><span class="badge badge-verified">Hades 실제 맵</span></div>' +
      '<figure class="map-figure"><figcaption>' + esc(map.name) + ' · ' + image["칸"][0] + '×' + image["칸"][1] +
      '칸 · 노란 점 ' + image["표시"].length + '개</figcaption><div class="map-canvas"><img src="' +
      esc(image["그림"]) + '" alt="' + esc(map.name) + ' 바닥 지형" loading="eager">' + pins + '</div>' +
      '<p class="map-hint">노란 점을 누르거나 키보드로 선택하면 도착지가 보입니다. 바닥 지형만 표시하며 건물·상점 오브젝트는 아직 포함하지 않습니다.</p></figure></section>';
  }

  function renderDetail() {
    var map = model.byId[state.selectedId];
    if (!map) return;
    var links = model.connections(map.id);
    var status = model.status(map.id);
    var copy = statusCopy[status];
    var clusterIndex = model.clusterById[map.id];
    var cluster = clusterIndex == null ? null : model.clusters[clusterIndex];
    detailEl.innerHTML = '<header class="world-map-heading"><div><p class="eyebrow">Selected map</p><h2>' +
      esc(map.name) + '</h2><p class="world-meta">맵 #' + esc(map.id) + ' · ' + map.width + '×' + map.height +
      (cluster ? ' · ' + esc(regionLabel(cluster)) : ' · 연결 구역 없음') + '</p></div><span class="world-status is-' +
      status + '">' + copy[0] + '</span></header>' +
      '<p class="world-status-help">' + copy[1] + '</p>' +
      '<section class="world-route-section" aria-labelledby="world-route-title"><div class="world-terrain-heading"><div>' +
      '<p class="eyebrow">Warp directions</p><h3 id="world-route-title">현재 맵 기준 워프 구조도</h3></div>' +
      '<div class="world-legend"><span><i class="is-round"></i>왕복</span><span><i></i>편도</span></div></div>' +
      '<div class="world-route-flow"><div class="world-route-column"><h4>들어오는 맵 <b>' + links.incoming.length +
      '</b></h4>' + routeButtons(links.incoming, "incoming") + '</div><div class="world-current-map"><small>현재 위치</small>' +
      '<strong>' + esc(map.name) + '</strong><span>#' + esc(map.id) + '</span></div><div class="world-route-column"><h4>나가는 맵 <b>' +
      links.outgoing.length + '</b></h4>' + routeButtons(links.outgoing, "outgoing") + '</div></div></section>' + renderPicture(map);
  }

  function selectMap(id, updateList) {
    id = String(id);
    if (!model.byId[id]) return;
    state.selectedId = id;
    renderRegions();
    renderDetail();
    if (updateList !== false) renderList(false);
  }

  function setFilter(filter, selectFirst) {
    state.filter = filter;
    Array.prototype.forEach.call(filtersEl.querySelectorAll("[data-world-filter]"), function (button) {
      button.setAttribute("aria-pressed", String(button.getAttribute("data-world-filter") === filter));
    });
    renderRegions();
    renderList(selectFirst);
  }

  regionsEl.addEventListener("click", function (event) {
    var button = event.target.closest("button");
    if (!button) return;
    if (button.hasAttribute("data-filter-region")) {
      state.query = ""; searchEl.value = ""; setFilter("isolated", true); return;
    }
    var cluster = model.clusters[Number(button.getAttribute("data-region"))];
    if (!cluster) return;
    state.query = ""; searchEl.value = ""; setFilter("connected", false);
    selectMap(representative(cluster), true);
  });

  listEl.addEventListener("click", function (event) {
    var button = event.target.closest("[data-map-id]");
    if (button) selectMap(button.getAttribute("data-map-id"), true);
  });
  detailEl.addEventListener("click", function (event) {
    var route = event.target.closest("[data-map-id]");
    if (route) {
      state.query = ""; searchEl.value = ""; setFilter(model.status(route.getAttribute("data-map-id")) === "isolated" ? "isolated" : "connected", false);
      selectMap(route.getAttribute("data-map-id"), true);
      detailEl.scrollIntoView({ behavior: "smooth", block: "start" });
      return;
    }
    var pin = event.target.closest("[data-map-pin]");
    if (pin) pin.classList.toggle("is-open");
  });
  searchEl.addEventListener("input", function () {
    state.query = searchEl.value;
    renderList(Boolean(state.query.trim()));
  });
  filtersEl.addEventListener("click", function (event) {
    var button = event.target.closest("[data-world-filter]");
    if (button) setFilter(button.getAttribute("data-world-filter"), true);
  });

  renderSummary();
  var preferred = model.maps.filter(function (map) { return map.name === "포테의숲1존"; })[0];
  state.selectedId = preferred ? preferred.id : representative(model.clusters[0]);
  renderRegions();
  renderList(false);
  renderDetail();
})();
