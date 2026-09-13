/* 포테의 숲 단일 검토 시안 — Hades 우선, 없을 때만 두 서버팩의 완전 일치 좌표를 쓴다. */
(function () {
  "use strict";
  var root = document.getElementById("porte-forest-focus");
  var DATA = window.WORLD_MAP_DATA;
  var images = window.MAP_IMAGES || {};
  var factory = window.LODWorldMapModel;
  if (!root || !DATA || !factory) return;

  var model = factory.create(DATA);
  var forest = model.maps.filter(function (map) { return map.name.indexOf("포테의숲") === 0; })
    .sort(function (a, b) { return Number(a.id) - Number(b.id); });
  var selectedId = forest.length ? forest[0].id : null;

  function esc(value) {
    return String(value).replace(/[&<>"']/g, function (char) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[char];
    });
  }

  function warpData(image) {
    var hades = image && image["표시"] ? image["표시"] : [];
    var fallback = image && image["참고표시"] ? image["참고표시"] : [];
    if (hades.length) return { marks: hades, source: "Hades", fallback: false };
    if (fallback.length) return { marks: fallback, source: "서버팩 2개 일치", fallback: true };
    return { marks: [], source: "좌표 없음", fallback: false };
  }

  function pins(map, image) {
    var data = warpData(image);
    return data.marks.map(function (pin, index) {
      var coordinate = pin["칸"].join(",");
      var destination = pin["도착"].join(", ");
      var className = data.fallback ? "map-pin map-pin-reference" : "map-pin map-pin-hades";
      return '<button type="button" class="' + className + '" data-forest-pin="' + index + '" style="left:' +
        (pin.x / image["폭"] * 100).toFixed(3) + '%;top:' + (pin.y / image["높이"] * 100).toFixed(3) +
        '%" aria-label="' + data.source + ' 좌표 ' + coordinate + ', ' + esc(destination) +
        '(으)로 이동"><span><b>(' + coordinate + ')</b> → ' +
        esc(destination) + '</span></button>';
    }).join("");
  }

  function effectiveRoutes(image) {
    var data = warpData(image);
    var grouped = {};
    data.marks.forEach(function (pin) {
      pin["도착"].forEach(function (destination) {
        if (!grouped[destination]) grouped[destination] = [];
        grouped[destination].push(pin["칸"].join(","));
      });
    });
    var destinations = Object.keys(grouped);
    if (!destinations.length) {
      return '<div class="hades-route-empty"><strong>0</strong><span>확인된 워프 좌표가 없습니다.</span></div>';
    }
    return destinations.map(function (destination) {
      var target = forest.filter(function (map) { return map.name === destination; })[0];
      var attrs = target ? ' data-hades-map="' + esc(target.id) + '"' : ' disabled';
      return '<button type="button"' + attrs + '><span>→ ' + esc(destination) + '</span><small>출발 칸 ' +
        esc(grouped[destination].join(" · ")) + '</small></button>';
    }).join("");
  }

  function routeSummary(map) {
    var image = images[map.name] || {};
    var data = warpData(image);
    var destinations = [];
    data.marks.forEach(function (pin) {
      pin["도착"].forEach(function (destination) {
        if (destinations.indexOf(destination) < 0) destinations.push(destination);
      });
    });
    return destinations.length ? "→ " + destinations.map(function (name) {
      return name.replace("포테의숲", "");
    }).join(", ") : "확인된 출구 없음";
  }

  function routeList(routes, direction) {
    if (!routes.length) return '<div class="hades-route-empty"><strong>0</strong><span>' + direction + ' 워프가 없습니다.</span></div>';
    return routes.map(function (route) {
      return '<button type="button" data-hades-map="' + esc(route.id) + '"><span>' + esc(model.byId[route.id].name) +
        '</span><small>워프 칸 ' + route.count + (route.reciprocal ? ' · 왕복' : ' · 편도') + '</small></button>';
    }).join("");
  }

  function render() {
    if (!selectedId) {
      root.innerHTML = '<p class="world-empty">Hades에서 포테의숲 맵을 찾지 못했습니다.</p>';
      return;
    }
    var selected = model.byId[selectedId];
    var image = images[selected.name];
    var links = model.connections(selectedId);
    var totalHadesPins = forest.reduce(function (sum, map) {
      return sum + ((images[map.name] && images[map.name]["표시"]) || []).length;
    }, 0);
    var totalFallbackPins = forest.reduce(function (sum, map) {
      return sum + ((images[map.name] && images[map.name]["참고표시"]) || []).length;
    }, 0);

    var nodes = forest.map(function (map) {
      var preview = images[map.name];
      return '<button type="button" class="hades-map-node" data-hades-map="' + map.id + '" aria-pressed="' +
        (map.id === selectedId) + '">' + (preview ? '<img src="' + esc(preview["그림"]) + '" alt="">' : '') +
        '<span><strong>' + esc(map.name.replace("포테의숲", "")) + '</strong><small>#' + map.id + ' · ' +
        map.width + '×' + map.height + '</small><em>' + esc(routeSummary(map)) + '</em></span></button>';
    }).join("");

    var effective = warpData(image);
    var routeCount = effective.marks.length;
    var sourceClasses = { "Hades": "is-hades", "서버팩 2개 일치": "is-fallback", "좌표 없음": "is-missing" };
    var sourceClass = sourceClasses[effective.source];
    var caption = effective.fallback
      ? 'Hades에 좌표가 없어 5.99·혼든의 출발/도착 좌표가 모두 같은 워프만 표시합니다.'
      : effective.source === "Hades"
        ? 'Hades <code>templates/warps</code>의 출발 좌표입니다.'
        : 'Hades와 서버팩 모두 표시할 워프 좌표가 없습니다.';

    root.innerHTML = '<section class="hades-evidence"><div><span class="badge badge-verified">기준 · Hades</span>' +
      '<strong>실제 맵 파일 ' + forest.length + '장</strong><span>Hades 좌표 ' + totalHadesPins + '개 · fallback 좌표 ' +
      totalFallbackPins + '개</span></div><p><b>우선순위: Hades → 서버팩 2개 합의.</b> Hades 워프가 없고 5.99·혼든 좌표가 완전히 같을 때만 사용합니다.</p></section>' +
      '<section class="hades-relationship" aria-labelledby="hades-relationship-title"><div class="forest-heading"><div>' +
      '<p class="eyebrow">Map relationship</p><h2 id="hades-relationship-title">포테의숲 전체 맵</h2></div>' +
      '<div class="hades-map-legend"><span><i class="' + sourceClass + '"></i>현재 선택 · ' + esc(effective.source) +
      '</span><span class="is-missing">보스존 연결 미확인</span></div></div>' +
      '<div class="hades-map-strip">' + nodes + '</div>' +
      '<p class="hades-relationship-note">모든 맵을 한 화면 안에서 줄바꿈해 보여줍니다. 카드의 화살표는 Hades 우선 규칙으로 선택된 실제 출구만 나타냅니다.</p></section>' +
      '<div class="hades-map-layout"><section class="hades-map-stage"><div class="forest-heading"><div><p class="eyebrow">Actual map</p>' +
      '<h2>' + esc(selected.name) + '</h2><p class="world-meta">#' + selected.id + ' · ' + selected.width + '×' + selected.height +
      '칸</p></div><span class="badge">' + esc(effective.source) + ' · 출구 칸 ' + routeCount + '개</span></div>' +
      (image ? '<figure class="map-figure"><div class="map-canvas"><img src="' + esc(image["그림"]) + '" alt="' +
        esc(selected.name) + ' Hades 실제 지형">' + pins(selected, image) + '</div><figcaption><span class="map-legend-dot ' +
        sourceClass + '"></span>' +
        caption + ' 점을 누르면 좌표와 다음 맵이 보입니다.</figcaption></figure>' :
        '<div class="world-terrain-empty"><strong>Hades 맵 이미지를 만들지 못했습니다.</strong></div>') + '</section>' +
      '<aside class="hades-map-inspector"><p class="eyebrow">Route inspection</p><h2>이 맵에서 나가는 길</h2>' +
      '<p class="hades-inspector-note">현재 출처는 <b>' + esc(effective.source) + '</b>입니다. 항목을 누르면 다음 맵을 바로 엽니다.</p>' +
      '<div class="forest-route-group"><h3>적용 출구 <b>' + routeCount + '</b></h3>' + effectiveRoutes(image || {}) + '</div>' +
      '<details class="hades-source-detail"><summary>Hades 서버 연결 상태</summary><div class="forest-route-group"><h3>들어오는 맵 <b>' +
      links.incoming.length + '</b></h3>' + routeList(links.incoming, "들어오는") + '</div><div class="forest-route-group"><h3>나가는 맵 <b>' +
      links.outgoing.length + '</b></h3>' + routeList(links.outgoing, "나가는") + '</div></details>' +
      '<div class="hades-decision"><strong>보스존 길은 아직 미확인</strong><p>Hades에도 없고 5.99·혼든이 일치하는 좌표도 없습니다. 근거가 생길 때까지 임의로 표시하지 않습니다.</p></div></aside></div>';
  }

  root.addEventListener("click", function (event) {
    var mapButton = event.target.closest("[data-hades-map]");
    if (mapButton) {
      selectedId = mapButton.getAttribute("data-hades-map");
      render();
      return;
    }
    var pin = event.target.closest("[data-forest-pin]");
    if (pin) pin.classList.toggle("is-open");
  });
  render();
})();
