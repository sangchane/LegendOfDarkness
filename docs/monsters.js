/* 괴물 도감 — 왼쪽 목록 | 오른쪽 상세(걷는 그림 · 결론 한 줄 · 수치 · 떨구는 것 · 같은 괴물의 다른 자리 · 지도에서 보기).
 * 자료: monsters-data.js · 아이템 아이콘은 items-data.js. 다른 화면은 LodMonsters.focus("이름@맵") 으로 연다.
 */
(function () {
  "use strict";
  var data = window.LOD_MONSTERS;
  if (!data) { return; }

  var SPRITE_DIR = "ui/assets/creature/";
  var CLASSES = { 0: "", 1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "무도가" };
  // 정렬은 괴물(이름) 단위 — 여러 맵이면 가장 센 맵 기준(레벨·맵 차례는 가장 낮은 맵).
  var SORTS = [
    { id: "exp", 이름: "경험치", 재다: function (g) { return -most(g.places, function (m) { return m.경험치; }); } },
    { id: "lv", 이름: "레벨", 재다: function (g) { return least(g.places, function (m) { return m.감산레벨; }); } },
    { id: "hp", 이름: "체력", 재다: function (g) { return -most(g.places, function (m) { return m.체력; }); } },
    { id: "dmg", 이름: "때리는 힘", 재다: function (g) { return -most(g.places, function (m) { return m.피해[1]; }); } },
    { id: "map", 이름: "맵 차례", 재다: function (g) { return least(g.places, function (m) { return m.맵번호; }); } },
  ];

  var list = document.getElementById("monster-grid");
  var empty = document.getElementById("monster-empty");
  var detail = document.getElementById("monster-detail");
  var section = list.closest(".view");
  // 처음부터 전부 펼치지 않는다(사용자 10-09) — 첫 지역(노비스)을 레벨 차례로 연다. 「모든 지역」은 고르기 상자 끝에.
  var state = { query: "", region: data.지역[0], sort: "lv", level: 1 };
  var selected = null;
  var rows = new Map();
  var phone = window.matchMedia ? window.matchMedia("(max-width: 960px)") : { matches: false };

  function text(tag, className, value) {
    var element = document.createElement(tag);
    if (className) { element.className = className; }
    if (value !== undefined) { element.textContent = value; }
    return element;
  }

  function number(value) { return Number(value || 0).toLocaleString("ko-KR"); }
  function keyOf(monster) { return monster.근거.slice(monster.근거.lastIndexOf("/") + 1).replace(/\.json$/, ""); }

  /** 레벨 차이로 깎인 뒤 실제로 손에 들어오는 경험치. monsterexp.cs 의 ForLevel 과 같은 식이다. */
  function earned(monster, level) {
    var rule = data.규칙.감산;
    var gap = level - monster.감산레벨;
    if (gap <= rule.용서) { return Math.max(1, monster.경험치); }
    var share = Math.pow(0.5, (gap - rule.용서) / rule.반감);
    return Math.max(1, Math.floor(monster.경험치 * Math.max(rule.최소, share)));
  }

  /** 지금 레벨에서 다음 레벨까지 몇 마리인가. 표에 없는 레벨이면 셈하지 않는다. */
  function killsToLevel(monster, level) {
    var need = data.레벨표[String(level + 1)];
    var gain = earned(monster, level);
    if (!need || gain <= 0) { return null; }
    return Math.ceil(need / gain);
  }

  function sprite(monster) {
    var art = monster.스프라이트;
    if (!art) { return text("div", "monster-art is-missing", "그림 없음"); }
    var frameWidth = Math.round(art.너비 / art.칸);
    var box = text("div", "monster-art is-animated");
    box.style.setProperty("--w", frameWidth + "px");
    box.style.setProperty("--h", art.높이 + "px");
    box.style.setProperty("--sheet", "url(" + SPRITE_DIR + art.이름 + ".png)");
    box.style.setProperty("--sheet-w", art.너비 + "px");

    // 걷기 구간: 등 구간 [시작, 칸수] 다음에 앞 구간이 바로 이어진다. 없으면 0 에서 한 칸.
    // 어느 쪽을 볼지는 CSS 가 화면의 data-monster-dir 로 고른다 — 만들자마자 움직인다.
    var walk = art.동작 && art.동작.walk ? art.동작.walk : [0, 1];
    var back = walk[0], count = walk[1] || 1, front = back + count;
    box.style.setProperty("--frames", count);
    box.style.setProperty("--back-from", (-back * frameWidth) + "px");
    box.style.setProperty("--back-to", (-(back + count) * frameWidth) + "px");
    box.style.setProperty("--front-from", (-front * frameWidth) + "px");
    box.style.setProperty("--front-to", (-(front + count) * frameWidth) + "px");
    return box;
  }

  /** 목록 칸 — 서 있는 첫 칸을 40 안에 줄여 넣는다. */
  function thumb(monster) {
    var art = monster.스프라이트;
    var box = text("i", "cdx-thumb");
    box.setAttribute("aria-hidden", "true");
    if (!art) { return box; }
    var w = Math.round(art.너비 / art.칸), h = art.높이;
    var scale = Math.min(1, 40 / Math.max(w, h));
    box.style.width = Math.round(w * scale) + "px";
    box.style.height = Math.round(h * scale) + "px";
    box.style.backgroundImage = "url(" + SPRITE_DIR + art.이름 + ".png)";
    box.style.backgroundSize = Math.round(art.너비 * scale) + "px " + Math.round(h * scale) + "px";
    return box;
  }

  function stat(label, value, note) {
    var cell = text("div", "monster-stat");
    cell.append(text("span", "", label), text("strong", "", value));
    if (note) { cell.append(text("small", "", note)); }
    return cell;
  }

  /** 팩이 치장·장식으로 나눈 장비 — 나중에 드랍에서 뺄지 정하려고 표시만 한다(사용자 2026-10-04). */
  function cosmetic(drop) { return /치장|장식/.test(drop.분류 || ""); }
  function percent(value) { return (value * 100).toFixed(value < 0.01 ? 2 : 1) + "%"; }
  function odds(drops) {
    var any = 0, gear = 0;
    drops.forEach(function (drop) { any += drop.실제확률; if (drop.갈래 === "장비") { gear += drop.실제확률; } });
    return { any: any, gear: gear };
  }

  // 아이템 도감의 아이콘 번호(items-data.js) — 같은 그림이면 같은 번호다.
  var ICONS = new Map();
  ((window.LOD_ITEMS && window.LOD_ITEMS.목록) || []).forEach(function (row) {
    if (row.ic >= 0) { ICONS.set(row.ko || row.en, row.ic); ICONS.set(row.en, row.ic); }
  });
  var ICON_WIDTH = (window.LOD_ITEMS && window.LOD_ITEMS.아이콘 && window.LOD_ITEMS.아이콘.너비) || 37;

  /** 같은 그림(아이콘 번호)의 물건을 한 묶음으로 — 접미사(로오의·화염의 …)만 다른 것이 많다. 합이 큰 차례. */
  function shapes(drops) {
    var byKey = new Map();
    drops.forEach(function (drop) {
      var ic = ICONS.has(drop.이름) ? ICONS.get(drop.이름) : -1;
      var key = ic >= 0 ? "ic" + ic : "name" + drop.이름;
      if (!byKey.has(key)) { byKey.set(key, { ic: ic, drops: [], sum: 0 }); }
      var shape = byKey.get(key);
      // 같은 물건이 목록에 두 번 있으면(파프리카 10% · 10%) 한 줄로 합친다 — 확률은 더한다. 자료는 고치지 않는다.
      var same = shape.drops.filter(function (seen) { return seen.이름 === drop.이름; })[0];
      if (same) { same.실제확률 += drop.실제확률; } else { shape.drops.push(Object.assign({}, drop)); }
      shape.sum += drop.실제확률;
    });
    var out = Array.from(byKey.values());
    out.forEach(function (shape) { shape.drops.sort(function (a, b) { return b.실제확률 - a.실제확률; }); });
    return out.sort(function (a, b) { return b.sum - a.sum; });
  }

  /** 묶음의 기본 이름 — 모두 「앞머리의 같은꼴」이면 그 꼴(은각반), 아니면 첫 이름. */
  function baseName(shape) {
    var tails = shape.drops.map(function (drop) { var at = drop.이름.indexOf("의"); return at > 0 ? drop.이름.slice(at + 1) : drop.이름; });
    return tails.every(function (tail) { return tail === tails[0]; }) && shape.drops.length > 1 ? tails[0] : shape.drops[0].이름;
  }

  function toItem(name) {
    return function () {
      if (window.LodItems && window.LodItems.focus(name)) { return; }
      window.LodDashboard.toast("아이템 도감에 없는 물건입니다 — " + name);
    };
  }

  function icon(ic) {
    var item = text("i", "item-icon");
    if (ic >= 0) { item.style.backgroundPosition = "-" + (ic * ICON_WIDTH) + "px 0"; } else { item.classList.add("is-blank"); }
    item.setAttribute("aria-hidden", "true");
    return item;
  }

  /** 떨구는 것 한 묶음 — 누르면 아이템 도감으로. 접미사만 다른 것은 아래 칩으로. */
  function shapeRow(shape) {
    var li = text("li");
    var first = shape.drops[0];
    var base = baseName(shape);
    var button = text("button", "cdx-link");
    button.type = "button";
    button.appendChild(icon(shape.ic));
    var copy = text("span", "cdx-link-copy");
    copy.appendChild(text("b", "", base + (cosmetic(first) ? " · 치장" : "")));
    var note = [first.분류 ? first.분류.replace(/^.*\//, "") : first.갈래];
    if (first.요구레벨 > 1) { note.push(first.요구레벨 + "레벨"); }
    if (CLASSES[first.직업]) { note.push(CLASSES[first.직업]); }
    if (first.체력회복) { note.push("체력 +" + number(first.체력회복)); }
    if (first.마력회복) { note.push("마력 +" + number(first.마력회복)); }
    if (!first.템플릿있음) { note.push("템플릿 없음 — 안 떨어진다"); }
    copy.appendChild(text("small", "", note.join(" · ")));
    button.appendChild(copy);
    var meter = text("span", "cdx-odds");
    meter.appendChild(text("b", "", percent(shape.sum)));
    var bar = text("i");
    // 막대는 실제 확률을 그린다. 100%를 가득으로 두면 20%가 안 보이므로 50%를 가득으로 본다.
    bar.style.setProperty("--fill", Math.min(100, shape.sum * 200) + "%");
    meter.appendChild(bar);
    button.appendChild(meter);
    button.title = first.이름 + " · 기준 " + first.표확률 + " ×1.5 ÷ 목록";
    button.addEventListener("click", toItem(first.이름));
    li.appendChild(button);

    if (shape.drops.length > 1 || base !== first.이름) {
      var variants = text("ul", "cdx-variants");
      shape.drops.forEach(function (drop) {
        var label = drop.이름.endsWith(base) && drop.이름 !== base ? drop.이름.slice(0, drop.이름.length - base.length).replace(/의$/, "") : drop.이름;
        var chip = text("button", drop.템플릿있음 ? "" : "is-missing");
        chip.type = "button";
        chip.append(text("span", "", label), text("small", "", percent(drop.실제확률)));
        chip.title = drop.이름;
        chip.addEventListener("click", toItem(drop.이름));
        var item = text("li");
        item.appendChild(chip);
        variants.appendChild(item);
      });
      li.appendChild(variants);
    }
    return li;
  }

  function aggro(monster) {
    var tag = text("span", "cdx-tag " + (monster.선공 === "선공" ? "is-risk" : monster.선공 === "반반" ? "is-gold" : "is-calm"), monster.선공);
    tag.title = monster.선공 === "선공" ? "보이면 먼저 덤빈다" : monster.선공 === "반반" ? "가끔 먼저 덤빈다" : "때려야 덤빈다";
    return tag;
  }

  /* ── 괴물 하나 = 이름 하나. 같은 괴물이 여러 맵에 나오면 한 줄로 묶고, 맵마다 다른 것만 표로 ── */

  // 수치 칸. 맵마다 같으면 값 하나, 다르면 범위와 「맵마다 다름」, 출현 맵 표에는 다른 것만 칸으로 낸다.
  var FIELDS = [
    { 이름: "체력", 글: function (m) { return number(m.체력); }, 낮: function (m) { return m.체력; }, 높: function (m) { return m.체력; } },
    { 이름: "때리는 힘", 글: function (m) { return m.피해[0] + "~" + m.피해[1]; }, 낮: function (m) { return m.피해[0]; }, 높: function (m) { return m.피해[1]; } },
    { 이름: "방어", 글: function (m) { return String(m.방어); }, 낮: function (m) { return m.방어; }, 높: function (m) { return m.방어; } },
    { 이름: "경험치(표)", 글: function (m) { return number(m.경험치); }, 낮: function (m) { return m.경험치; }, 높: function (m) { return m.경험치; } },
    { 이름: "금화", 글: function (m) { return m.금화[1] ? number(m.금화[0]) + "~" + number(m.금화[1]) : "없음"; }, 낮: function (m) { return m.금화[0]; }, 높: function (m) { return m.금화[1]; } },
    { 이름: "한 맵 최대", 글: function (m) { return m.젠최대 + "마리" + (m.젠주기 ? " · " + m.젠주기 + "초마다" : ""); }, 낮: function (m) { return m.젠최대; }, 높: function (m) { return m.젠최대; }, 단위: "마리" },
    { 이름: "걸음·공격", 글: function (m) { return (m.이동속도 / 1000).toFixed(1) + "초 · " + (m.공격속도 / 1000).toFixed(1) + "초"; } },
  ];

  var groups = [], groupOf = new Map();
  data.괴물.forEach(function (m) {
    var group = groupOf.get(m.이름);
    if (!group) { group = { 이름: m.이름, places: [] }; groupOf.set(m.이름, group); groups.push(group); }
    group.places.push(m);
  });
  groups.forEach(function (group) {
    group.places.sort(function (a, b) { return a.감산레벨 - b.감산레벨 || a.맵번호 - b.맵번호; });
    group.drops = new Set(group.places.map(function (m) {
      return JSON.stringify(m.드랍.map(function (d) { return [d.이름, d.실제확률]; }).sort());
    })).size === 1;
  });

  function most(list, pick) { return list.reduce(function (top, m) { return Math.max(top, pick(m)); }, -Infinity); }
  function least(list, pick) { return list.reduce(function (low, m) { return Math.min(low, pick(m)); }, Infinity); }
  function span(low, high, format) { format = format || number; return low === high ? format(low) : format(low) + "~" + format(high); }
  function same(list, pick) { return list.every(function (m) { return pick(m) === pick(list[0]); }); }
  function regionsOf(group) { return group.places.map(function (m) { return m.지역; }).filter(function (r, i, all) { return all.indexOf(r) === i; }); }

  function describe(group, place) {
    detail.replaceChildren();
    if (!group) { detail.appendChild(text("p", "cdx-empty", "왼쪽에서 괴물을 고르세요.")); return; }
    var places = group.places, many = places.length > 1;

    var back = text("button", "cdx-back", "← 목록");
    back.type = "button";
    back.addEventListener("click", closeSheet);
    detail.appendChild(back);

    var hero = text("header", "cdx-hero");
    var stageBox = text("div", "cdx-stage");
    // 두 배로 키우되 큰 괴물(드라코 …)은 판이 길어지지 않게 높이 110 안으로.
    if (place.스프라이트) { stageBox.style.setProperty("--zoom", Math.min(2, 110 / place.스프라이트.높이).toFixed(2)); }
    stageBox.appendChild(sprite(place));
    hero.appendChild(stageBox);
    var title = text("div");
    title.appendChild(text("h2", "", group.이름));
    title.appendChild(text("p", "", (many ? "맵 " + places.length + "곳" : place.맵) + " · " + regionsOf(group).join("·")
      + " · Lv " + span(least(places, function (m) { return m.감산레벨; }), most(places, function (m) { return m.감산레벨; }))));
    var tags = text("div", "cdx-tags");
    places.map(function (m) { return m.선공; }).filter(function (v, i, all) { return all.indexOf(v) === i; })
      .forEach(function (kind) { tags.appendChild(aggro({ 선공: kind })); });
    if (places.some(function (m) { return !m.드랍켜짐; })) { tags.appendChild(text("span", "cdx-tag is-risk", "드랍 꺼진 맵 있음")); }
    title.appendChild(tags);
    var actions = text("div", "cdx-hero-actions");
    var where = text("button", "", (many ? place.맵 + " " : "") + "지도에서 보기 →");
    where.type = "button";
    where.addEventListener("click", function () {
      if (!(window.LodAtlas && window.LodAtlas.focus(place.맵번호))) {
        window.LodDashboard.toast("이 맵은 지도에 아직 없습니다 — " + place.맵);
      }
    });
    actions.appendChild(where);
    title.appendChild(actions);
    hero.appendChild(title);
    detail.appendChild(hero);

    // 결론 한 줄 — 내 레벨에서 이 괴물이 어떤가. 여러 맵이면 범위로.
    var gains = places.map(function (m) { return earned(m, state.level); });
    var kills = places.map(function (m) { return killsToLevel(m, state.level); }).filter(function (k) { return k !== null; });
    var gear = places.map(function (m) { return odds(m.드랍).gear; });
    var any = places.map(function (m) { return odds(m.드랍).any; });
    var verdict = text("p", "cdx-verdict");
    verdict.append("내 레벨 " + state.level + ": 한 마리 경험치 ");
    verdict.appendChild(text("strong", "", span(Math.min.apply(null, gains), Math.max.apply(null, gains))));
    if (places.some(function (m, i) { return gains[i] !== m.경험치; })) { verdict.appendChild(text("span", "is-warn", " (레벨 차이로 깎임)")); }
    verdict.append(" → 다음 레벨까지 ");
    verdict.appendChild(text("strong", "", kills.length ? span(Math.min.apply(null, kills), Math.max.apply(null, kills)) + "마리" : "—"));
    verdict.append(" · 뭐라도 떨굴 확률 " + span(Math.min.apply(null, any), Math.max.apply(null, any), percent)
      + " · 장비 " + span(Math.min.apply(null, gear), Math.max.apply(null, gear), percent));
    detail.appendChild(verdict);

    // 같은 수치는 한 번, 다른 수치는 범위 — 무엇이 다른지는 아래 출현 맵 표.
    var stats = text("div", "monster-stats");
    var differ = [];
    FIELDS.forEach(function (field) {
      if (same(places, field.글)) { stats.appendChild(stat(field.이름, field.글(places[0]))); return; }
      differ.push(field);
      var range = field.낮 ? span(least(places, field.낮), most(places, field.높)) + (field.단위 || "") : "—";
      stats.appendChild(stat(field.이름, range, "맵마다 다름"));
    });
    detail.appendChild(stats);

    if (many) { detail.appendChild(placeTable(group, place, differ)); }

    var dropHead = text("h3", "", "떨구는 것");
    var dropNote = place.드랍.length ? place.드랍.length + "가지 — 한 마리가 하나를 골라 한 번 굴린다 · 누르면 아이템으로" : "";
    if (many) { dropNote = (group.drops ? "모든 맵 같음 · " : place.맵 + " 것 — 맵마다 다름(표에서 맵을 고르면 바뀐다) · ") + dropNote; }
    dropHead.appendChild(text("small", "", dropNote));
    detail.appendChild(dropHead);
    if (place.드랍.length) {
      var drops = text("ul", "cdx-links");
      shapes(place.드랍).forEach(function (shape) { drops.appendChild(shapeRow(shape)); });
      detail.appendChild(drops);
    } else {
      detail.appendChild(text("p", "cdx-empty", "떨구는 것이 없습니다"));
    }
    detail.appendChild(text("code", "cdx-source", place.근거));
  }

  /** 출현 맵 표 — 맵 · 레벨 · 맵마다 다른 수치만 · 내 레벨에서 몇 마리 · 장비 확률. 줄을 누르면 그 맵으로 맞춘다. */
  function placeTable(group, place, differ) {
    var box = text("section", "cdx-places-box");
    var head = text("h3", "", "출현 맵 " + group.places.length + "곳");
    head.appendChild(text("small", "", (differ.length ? "맵마다 다른 것만 칸으로 · " : "수치는 모든 맵 같음 · ") + "줄을 누르면 그 맵의 떨구는 것·지도"));
    box.appendChild(head);
    var scroller = text("div", "cdx-table-wrap");
    var table = text("table", "cdx-table");
    var row = text("tr");
    ["맵", "Lv"].concat(differ.map(function (f) { return f.이름; }), ["다음 레벨", "장비"]).forEach(function (label) { row.appendChild(text("th", "", label)); });
    var thead = text("thead");
    thead.appendChild(row);
    table.appendChild(thead);
    var body = text("tbody");
    group.places.forEach(function (m) {
      var tr = text("tr", m === place ? "is-current" : "");
      var cell = text("td");
      var pick = text("button", "", m.맵);
      pick.type = "button";
      pick.setAttribute("aria-pressed", String(m === place));
      pick.addEventListener("click", function () { selected = { group: group, place: m }; describe(group, m); });
      cell.appendChild(pick);
      tr.appendChild(cell);
      tr.appendChild(text("td", "num", String(m.감산레벨)));
      differ.forEach(function (field) { tr.appendChild(text("td", "num", field.글(m))); });
      var kills = killsToLevel(m, state.level);
      tr.appendChild(text("td", "num", kills === null ? "—" : number(kills) + "마리"));
      tr.appendChild(text("td", "num", percent(odds(m.드랍).gear)));
      body.appendChild(tr);
    });
    table.appendChild(body);
    scroller.appendChild(table);
    box.appendChild(scroller);
    return box;
  }

  /** 고르기 상자 — 처음 한 번 채우고, 그다음엔 고른 값만 맞춘다. */
  function fillSelect(host, values, current) {
    if (!host.children.length) {
      values.forEach(function (value) { var option = text("option", "", value.이름); option.value = value.id; host.appendChild(option); });
    }
    host.value = current;
  }

  /** 거르기는 맵 하나하나로 본다 — 그 지역·그 검색어(괴물·맵·떨구는 물건)에 맞는 맵이 하나라도 있으면 그 괴물이 남는다. */
  function placeMatches(monster) {
    if (state.region && monster.지역 !== state.region) { return false; }
    if (!state.query) { return true; }
    var names = monster.드랍.map(function (drop) { return drop.이름; }).join(" ");
    return (monster.이름 + " " + monster.맵 + " " + names).toLocaleLowerCase("ko").indexOf(state.query) >= 0;
  }
  function matches(group) { return group.places.some(placeMatches); }

  function row(group) {
    var li = text("li");
    var button = text("button", "cdx-row");
    button.type = "button";
    var places = group.places;
    button.appendChild(thumb(places[0]));
    var copy = text("span", "cdx-row-copy");
    copy.appendChild(text("b", "", group.이름));
    copy.appendChild(text("small", "", (places.length > 1 ? "맵 " + places.length + "곳" : places[0].맵)
      + " · Lv " + span(least(places, function (m) { return m.감산레벨; }), most(places, function (m) { return m.감산레벨; }))
      + (places.some(function (m) { return m.선공 === "선공"; }) ? " · 선공" : "")));
    button.appendChild(copy);
    var kills = places.map(function (m) { return killsToLevel(m, state.level); }).filter(function (k) { return k !== null; });
    var cut = places.some(function (m) { return earned(m, state.level) !== m.경험치; });
    button.appendChild(text("em", cut ? "is-warn" : "", kills.length ? span(Math.min.apply(null, kills), Math.max.apply(null, kills)) + "마리" : "—"));
    button.title = "내 레벨 " + state.level + "에서 다음 레벨까지";
    if (selected && group === selected.group) { button.setAttribute("aria-current", "true"); }
    button.addEventListener("click", function () { open(group, null, true); });
    li.appendChild(button);
    rows.set(group, button);
    return li;
  }

  function render() {
    fillSelect(document.getElementById("monster-regions"),
      data.지역.map(function (name) { return { id: name, 이름: name }; }).concat([{ id: "", 이름: "모든 지역" }]), state.region);
    fillSelect(document.getElementById("monster-sorts"), SORTS.map(function (s) { return { id: s.id, 이름: s.이름 + " 차례" }; }), state.sort);

    var order = SORTS.find(function (s) { return s.id === state.sort; }) || SORTS[0];
    var shown = groups.filter(matches).sort(function (a, b) { return order.재다(a) - order.재다(b); });
    rows = new Map();
    var fragment = document.createDocumentFragment();
    shown.forEach(function (group) { fragment.appendChild(row(group)); });
    list.replaceChildren(fragment);
    empty.hidden = shown.length !== 0;

    document.getElementById("monster-kinds").textContent = data.셈.이름;
    document.getElementById("monster-slots").textContent = data.셈.괴물자리;
    document.getElementById("monster-sprites").textContent = data.셈.그림있음;
    document.getElementById("monster-shown").textContent = shown.length;

    // 넓은 화면은 상세가 늘 보이므로 첫 줄을 고른다. 고른 것이 걸러져 사라졌어도 바꾼다. 폰은 누를 때만 연다.
    if (selected && shown.indexOf(selected.group) >= 0) { describe(selected.group, selected.place); }
    else if (!phone.matches && shown.length) { open(shown[0], null, false); }
    else if (!shown.length) { selected = null; describe(null); }
  }

  function mark() {
    rows.forEach(function (button, group) {
      if (selected && group === selected.group) { button.setAttribute("aria-current", "true"); } else { button.removeAttribute("aria-current"); }
    });
  }

  /** 괴물을 연다. 맵을 안 주면 지금 거르기에 맞는 첫 맵(없으면 첫 맵). */
  function open(group, place, fromUser) {
    place = place || group.places.filter(placeMatches)[0] || group.places[0];
    selected = { group: group, place: place };
    mark();
    describe(group, place);
    detail.scrollTop = 0;
    if (phone.matches && fromUser) {
      detail.classList.add("is-open");
      document.body.classList.add("codex-open");
      var back = detail.querySelector(".cdx-back");
      if (back) { back.focus(); }
    }
  }

  function closeSheet() {
    detail.classList.remove("is-open");
    document.body.classList.remove("codex-open");
    var returning = selected && rows.get(selected.group);
    if (returning) { returning.focus(); }
  }

  /** 다른 화면에서 이 괴물을 그 맵으로 연다 — 거르기를 지워 목록에 보이게 하고 그 줄로 굴린다. */
  function focus(key) {
    var monster = data.괴물.find(function (m) { return keyOf(m) === key; });
    if (!monster) { return false; }
    var group = groupOf.get(monster.이름);
    window.LodDashboard.show("monsters");
    if (!placeMatches(monster)) {
      // 그 괴물이 나오는 지역으로 옮긴다 — 「모든 지역」으로 풀면 목록이 다시 길어진다.
      state.region = monster.지역; state.query = "";
      document.getElementById("monster-search").value = "";
    }
    selected = { group: group, place: monster };
    render();
    open(group, monster, true);
    var button = rows.get(group);
    // 목록 칸 안에서만 굴린다 — scrollIntoView 는 창까지 밀어 머리줄이 올라간다. 폰은 상세가 덮으므로 그대로.
    if (button && !phone.matches) {
      var box = button.parentNode.parentNode.parentNode, at = button.getBoundingClientRect(), frame = box.getBoundingClientRect();
      box.scrollTop += at.top - frame.top - frame.height / 2;
    }
    return true;
  }
  window.LodMonsters = { focus: focus };

  document.getElementById("monster-regions").addEventListener("change", function (event) { state.region = event.target.value; render(); });
  document.getElementById("monster-sorts").addEventListener("change", function (event) { state.sort = event.target.value; render(); });
  var slider = document.getElementById("monster-level");
  var output = document.getElementById("monster-level-out");
  slider.addEventListener("input", function () {
    state.level = Number(slider.value);
    output.textContent = slider.value;
    render();
  });
  document.getElementById("monster-search").addEventListener("input", function (event) {
    state.query = String(event.target.value || "").trim().toLocaleLowerCase("ko");
    render();
  });
  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && detail.classList.contains("is-open")) { closeSheet(); }
  });

  // 네 방향을 1.5초마다 돈다: 0=북(등) 1=동(앞) 2=남(앞·뒤집기) 3=서(등·뒤집기).
  // 어느 그림을 쓸지는 CSS 가 화면의 방향 하나로 고른다. 괴물 화면이 숨어 있거나 창이 가려지면 멈춘다.
  var direction = 1, turner = null;
  function turn() {
    if (section.hidden || document.hidden) { window.clearInterval(turner); turner = null; return; }
    direction = (direction + 1) % 4;
    section.dataset.monsterDir = String(direction);
  }
  function startTurning() {
    if (turner || section.hidden || document.hidden) { return; }
    turner = window.setInterval(turn, 1500);
  }
  section.dataset.monsterDir = String(direction);
  window.LodDashboard.onViewShown(startTurning);
  // 다른 화면으로 가면(폰 뒤로 가기 포함) 상세 판과 스크롤 잠금을 푼다 — 상세는 방문 기록에 안 남는다.
  window.LodDashboard.onViewShown(function (view) {
    if (view === "monsters") { return; }
    detail.classList.remove("is-open");
    document.body.classList.remove("codex-open");
  });
  document.addEventListener("visibilitychange", startTurning);

  render();
  startTurning();
})();
