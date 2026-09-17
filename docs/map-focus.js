/* Hades 지역별 실제 맵·워프 검토 — Hades 우선, 없을 때만 세 서버팩 합의를 쓴다. */
(function () {
  "use strict";
  var DATA = window.WORLD_MAP_DATA;
  var images = window.MAP_IMAGES || {};
  var factory = window.LODWorldMapModel;
  if (!DATA || !factory) return;

  var model = factory.create(DATA);

  function esc(value) {
    return String(value).replace(/[&<>"']/g, function (char) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[char];
    });
  }

  function warpData(image) {
    var hades = image && image["표시"] ? image["표시"] : [];
    var fallback = image && image["참고표시"] ? image["참고표시"] : [];
    if (hades.length) return { marks: hades, source: "Hades", fallback: false };
    if (fallback.length) return { marks: fallback, source: "서버팩 3개 일치", fallback: true };
    return { marks: [], source: "좌표 없음", fallback: false };
  }

  function directNeighborhood(centerName) {
    var center = model.maps.filter(function (map) { return map.name === centerName; })[0];
    if (!center) return [];
    var ids = {};
    ids[center.id] = true;
    var links = model.connections(center.id);
    links.incoming.concat(links.outgoing).forEach(function (route) { ids[route.id] = true; });
    return model.maps.filter(function (map) { return ids[map.id]; }).sort(function (a, b) {
      return Number(b.name === centerName) - Number(a.name === centerName) || Number(a.id) - Number(b.id);
    });
  }

  function pins(image) {
    var data = warpData(image);
    return data.marks.map(function (pin, index) {
      var coordinate = pin["칸"].join(",");
      var destination = pin["도착"].join(", ");
      var className = data.fallback ? "map-pin map-pin-reference" : "map-pin map-pin-hades";
      return '<button type="button" class="' + className + '" data-focus-pin="' + index + '" style="left:' +
        (pin.x / image["폭"] * 100).toFixed(3) + '%;top:' + (pin.y / image["높이"] * 100).toFixed(3) +
        '%" aria-label="' + data.source + ' 좌표 ' + coordinate + ', ' + esc(destination) +
        '(으)로 이동"><span><b>(' + coordinate + ')</b> → ' + esc(destination) + '</span></button>';
    }).join("");
  }

  function renderFocus(config) {
    var root = document.getElementById(config.rootId);
    if (!root) return;
    var maps = config.maps();
    var selectedId = maps.length ? (maps.filter(function (map) { return map.name === config.initialName; })[0] || maps[0]).id : null;

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
        var target = maps.filter(function (map) { return map.name === destination; })[0];
        var attrs = target ? ' data-focus-map="' + esc(target.id) + '"' : ' disabled';
        return '<button type="button"' + attrs + '><span>→ ' + esc(destination) + '</span><small>출발 칸 ' +
          esc(grouped[destination].join(" · ")) + '</small></button>';
      }).join("");
    }

    function routeSummary(map) {
      var data = warpData(images[map.name] || {});
      var destinations = [];
      data.marks.forEach(function (pin) {
        pin["도착"].forEach(function (destination) {
          if (destinations.indexOf(destination) < 0) destinations.push(destination);
        });
      });
      return destinations.length ? "→ " + destinations.map(config.shortName).join(", ") : "확인된 출구 없음";
    }

    function routeList(routes, direction) {
      if (!routes.length) return '<div class="hades-route-empty"><strong>0</strong><span>' + direction + ' 워프가 없습니다.</span></div>';
      return routes.map(function (route) {
        var target = maps.filter(function (map) { return map.id === route.id; })[0];
        var attrs = target ? ' data-focus-map="' + esc(route.id) + '"' : ' disabled';
        return '<button type="button"' + attrs + '><span>' + esc(model.byId[route.id].name) +
          '</span><small>워프 칸 ' + route.count + (route.reciprocal ? ' · 왕복' : ' · 편도') + '</small></button>';
      }).join("");
    }

    function render() {
      if (!selectedId) {
        root.innerHTML = '<p class="world-empty">Hades에서 ' + esc(config.regionName) + ' 맵을 찾지 못했습니다.</p>';
        return;
      }
      var selected = model.byId[selectedId];
      var image = images[selected.name];
      var links = model.connections(selectedId);
      var totalHadesPins = maps.reduce(function (sum, map) {
        return sum + ((images[map.name] && images[map.name]["표시"]) || []).length;
      }, 0);
      var totalFallbackPins = maps.reduce(function (sum, map) {
        return sum + ((images[map.name] && images[map.name]["참고표시"]) || []).length;
      }, 0);
      var nodes = maps.map(function (map) {
        var preview = images[map.name];
        return '<button type="button" class="hades-map-node" data-focus-map="' + map.id + '" aria-pressed="' +
          (map.id === selectedId) + '">' + (preview ? '<img src="' + esc(preview["그림"]) + '" alt="">' : '') +
          '<span><strong>' + esc(config.shortName(map.name)) + '</strong><small>#' + map.id + ' · ' +
          map.width + '×' + map.height + '</small><em>' + esc(routeSummary(map)) + '</em></span></button>';
      }).join("");
      var effective = warpData(image);
      var routeCount = effective.marks.length;
      var sourceClasses = { "Hades": "is-hades", "서버팩 3개 일치": "is-fallback", "좌표 없음": "is-missing" };
      var sourceClass = sourceClasses[effective.source];
      var caption = effective.fallback
        ? 'Hades에 좌표가 없어 5.99·혼든·Novaonline의 출발/도착 좌표가 모두 같은 워프만 표시합니다.'
        : effective.source === "Hades"
          ? 'Hades <code>templates/warps</code>의 출발 좌표입니다.'
          : 'Hades와 서버팩 모두 표시할 워프 좌표가 없습니다.';

      root.innerHTML = '<section class="hades-evidence"><div><span class="badge badge-verified">기준 · Hades</span>' +
        '<strong>실제 맵 파일 ' + maps.length + '장</strong><span>Hades 좌표 ' + totalHadesPins + '개 · fallback 좌표 ' +
        totalFallbackPins + '개</span></div><p><b>우선순위: Hades → 서버팩 3개 합의.</b> Hades 워프가 없고 5.99·혼든·Novaonline 좌표가 완전히 같을 때만 사용합니다.</p></section>' +
        '<section class="hades-relationship"><div class="forest-heading"><div><p class="eyebrow">Map relationship</p><h2>' +
        esc(config.groupTitle) + '</h2></div><div class="hades-map-legend"><span><i class="' + sourceClass +
        '"></i>현재 선택 · ' + esc(effective.source) + '</span><span class="is-missing">' + esc(config.statusLabel) +
        '</span></div></div><div class="hades-map-strip">' + nodes + '</div><p class="hades-relationship-note">' +
        esc(config.relationshipNote) + '</p></section><div class="hades-map-layout"><section class="hades-map-stage"><div class="forest-heading"><div>' +
        '<p class="eyebrow">Actual map</p><h2>' + esc(selected.name) + '</h2><p class="world-meta">#' + selected.id + ' · ' +
        selected.width + '×' + selected.height + '칸</p></div><span class="badge">' + esc(effective.source) + ' · 출구 칸 ' +
        routeCount + '개</span></div>' + (image ? '<figure class="map-figure"><div class="map-canvas"><img src="' +
        esc(image["그림"]) + '" alt="' + esc(selected.name) + ' Hades 실제 지형">' + pins(image) +
        '</div><figcaption><span class="map-legend-dot ' + sourceClass + '"></span>' + caption +
        ' 점을 누르면 좌표와 다음 맵이 보입니다.</figcaption></figure>' :
        '<div class="world-terrain-empty"><strong>Hades 맵 이미지를 만들지 못했습니다.</strong></div>') + '</section>' +
        '<aside class="hades-map-inspector"><p class="eyebrow">Route inspection</p><h2>이 맵에서 나가는 길</h2>' +
        '<p class="hades-inspector-note">현재 출처는 <b>' + esc(effective.source) + '</b>입니다. 항목을 누르면 다음 맵을 바로 엽니다.</p>' +
        '<div class="forest-route-group"><h3>적용 출구 <b>' + routeCount + '</b></h3>' + effectiveRoutes(image || {}) + '</div>' +
        '<details class="hades-source-detail"><summary>Hades 서버 연결 상태</summary><div class="forest-route-group"><h3>들어오는 맵 <b>' +
        links.incoming.length + '</b></h3>' + routeList(links.incoming, "들어오는") + '</div><div class="forest-route-group"><h3>나가는 맵 <b>' +
        links.outgoing.length + '</b></h3>' + routeList(links.outgoing, "나가는") + '</div></details><div class="hades-decision"><strong>' +
        esc(config.decisionTitle) + '</strong><p>' + esc(config.decisionText) + '</p></div></aside></div>';
    }

    root.addEventListener("click", function (event) {
      var mapButton = event.target.closest("[data-focus-map]");
      if (mapButton && model.byId[mapButton.getAttribute("data-focus-map")]) {
        selectedId = mapButton.getAttribute("data-focus-map");
        render();
        return;
      }
      var pin = event.target.closest("[data-focus-pin]");
      if (pin) pin.classList.toggle("is-open");
    });
    render();
  }

  var configs = [
    {
      key: "novice", rootId: "novice-village-focus", regionName: "노비스 마을", initialName: "노비스마을",
      maps: function () {
        var near = directNeighborhood("노비스마을");
        var ids = {};
        near.forEach(function (map) { ids[map.id] = true; });
        return near.concat(model.maps.filter(function (map) { return map.name.indexOf("노비스지하던전") === 0 && !ids[map.id]; })
          .sort(function (a, b) { return Number(a.id) - Number(b.id); }));
      },
      shortName: function (name) { return name.replace("노비스", "").replace(/^마을$/, "본마을"); },
      groupTitle: "노비스 마을과 사냥터(평원·지하던전)", statusLabel: "사냥터 괴물 확인",
      relationshipNote: "본마을·건물과 사냥터(평원 A·B, 지하던전 3×3)를 모았습니다. 카드의 화살표와 지도 점은 Hades 출구 좌표입니다.",
      decisionTitle: "사냥터 구현 — 레벨 범위·드롭 확률까지 5.99대로",
      decisionText: "마을→평원→지하던전 워프와 괴물이 Hades에 있고, 평원A·지하던전A1에 서면 괴물이 보이는 것을 실제 서버 시험(NoviceHuntingGroundTests)으로 확인했습니다. 입장 레벨은 5.99 범위(평원 1~22·지하던전 5~22·안쪽 10~22)로 막고 거절 문구도 5.99 서버 그대로입니다. 괴물 드롭은 5.99처럼 죽을 때 한 번 그 확률로(팜팻의정수 50% …) 떨어집니다. 모바일 맵(바닥·건물·벽)도 모두 있습니다."
    },
    {
      key: "porte", rootId: "porte-forest-focus", regionName: "포테의 숲", initialName: "포테의숲1존",
      maps: function () { return model.maps.filter(function (map) { return map.name.indexOf("포테의숲") === 0; })
        .sort(function (a, b) { return Number(a.id) - Number(b.id); }); },
      shortName: function (name) { return name.replace("포테의숲", ""); },
      groupTitle: "포테의숲 전체 맵", statusLabel: "1~6존 연결 · 보스존은 개인 던전",
      relationshipNote: "모든 맵을 한 화면 안에서 줄바꿈해 보여줍니다. 카드의 화살표는 Hades 우선 규칙으로 선택된 실제 출구만 나타냅니다.",
      decisionTitle: "포테의숲 구현 — 1~6존 · 보스존(개인 던전)까지",
      decisionText: "5.99 워프 목록이 싣지 않던 Suomi_Warp.txt 의 워프(포테의숲 37 · 수오미마을 건물 문 43)를 들였고 입장 레벨 21~51 을 적용했습니다. 보스존은 5.99처럼 개인 사본입니다 — 5존 위(26·27,0)를 밟아 입장하면 그 캐릭터 전용 오솔길 → 대기실(늑대 6마리를 다 잡아야 문이 열림) → 보스방이 지어지고, 보스방을 비우면 5초 뒤 경험치 20만과 함께 5존 16,16 으로 나옵니다. 실제 서버 시험 PoteForestTests · PoteDungeonTests 로 끝까지 확인했습니다(docs/pote-forest.md). 위 보스존 지도는 사본이 쓰는 원래 맵이라 고정 워프 점이 없습니다."
    },
    {
      key: "woodland", rootId: "woodland-focus", regionName: "우드랜드", initialName: "우드랜드입구",
      maps: function () { return directNeighborhood("우드랜드입구"); },
      shortName: function (name) { return name === "우드랜드입구" ? "입구" : name.replace("우드랜드", ""); },
      groupTitle: "우드랜드 입구와 바로 연결된 곳", statusLabel: "입구 동선 확인",
      relationshipNote: "입구에서 한 번에 오갈 수 있는 사냥터만 모았습니다. 카드의 화살표와 지도 점은 Hades 출구 좌표입니다.",
      decisionTitle: "우드랜드 입구 동선은 Hades에서 확인됨",
      decisionText: "입구에서 아홉 사냥터와 월드맵으로 가는 출발 좌표가 Hades에 있습니다. 화면의 점을 눌러 정확한 칸과 목적지를 확인할 수 있습니다."
    }
  ];
  configs.forEach(renderFocus);

  var tabs = Array.prototype.slice.call(document.querySelectorAll("[data-map-focus-target]"));
  function showRegion(key) {
    tabs.forEach(function (tab) {
      var active = tab.getAttribute("data-map-focus-target") === key;
      tab.setAttribute("aria-selected", String(active));
      tab.tabIndex = active ? 0 : -1;
      var panel = document.getElementById(tab.getAttribute("aria-controls"));
      if (panel) panel.hidden = !active;
    });
  }
  tabs.forEach(function (tab) {
    tab.addEventListener("click", function () { showRegion(tab.getAttribute("data-map-focus-target")); });
    tab.addEventListener("keydown", function (event) {
      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
      var offset = event.key === "ArrowRight" ? 1 : -1;
      var next = tabs[(tabs.indexOf(tab) + offset + tabs.length) % tabs.length];
      event.preventDefault();
      showRegion(next.getAttribute("data-map-focus-target"));
      next.focus();
    });
  });
  showRegion("novice");
})();
