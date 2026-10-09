/* 괴물 도감 — 왼쪽 목록 | 오른쪽 상세(걷는 그림 · 결론 한 줄 · 수치 · 떨구는 것 · 같은 괴물의 다른 자리 · 지도에서 보기).
 * 자료: monsters-data.js · 아이템 아이콘은 items-data.js. 다른 화면은 LodMonsters.focus("이름@맵") 으로 연다.
 */
(function () {
  "use strict";
  var data = window.LOD_MONSTERS;
  if (!data) { return; }

  var SPRITE_DIR = "ui/assets/creature/";
  var CLASSES = { 0: "", 1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "무도가" };
  var SORTS = [
    { id: "exp", 이름: "경험치", 재다: function (m) { return -m.경험치; } },
    { id: "lv", 이름: "레벨", 재다: function (m) { return m.감산레벨; } },
    { id: "hp", 이름: "체력", 재다: function (m) { return -m.체력; } },
    { id: "dmg", 이름: "때리는 힘", 재다: function (m) { return -m.피해[1]; } },
    { id: "map", 이름: "맵 차례", 재다: function (m) { return m.맵번호; } },
  ];

  var list = document.getElementById("monster-grid");
  var empty = document.getElementById("monster-empty");
  var detail = document.getElementById("monster-detail");
  var section = list.closest(".view");
  var state = { query: "", region: "", sort: "exp", level: 1 };
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

  function describe(monster) {
    detail.replaceChildren();
    if (!monster) { detail.appendChild(text("p", "cdx-empty", "왼쪽에서 괴물을 고르세요.")); return; }

    var back = text("button", "cdx-back", "← 목록");
    back.type = "button";
    back.addEventListener("click", closeSheet);
    detail.appendChild(back);

    var hero = text("header", "cdx-hero");
    var stageBox = text("div", "cdx-stage");
    stageBox.appendChild(sprite(monster));
    hero.appendChild(stageBox);
    var title = text("div");
    title.appendChild(text("h2", "", monster.이름));
    title.appendChild(text("p", "", monster.맵 + " · " + monster.지역 + " · Lv " + monster.감산레벨));
    var tags = text("div", "cdx-tags");
    tags.appendChild(aggro(monster));
    if (!monster.드랍켜짐) { tags.appendChild(text("span", "cdx-tag is-risk", "드랍 꺼짐")); }
    title.appendChild(tags);
    var actions = text("div", "cdx-hero-actions");
    var where = text("button", "", "지도에서 보기 →");
    where.type = "button";
    where.addEventListener("click", function () {
      if (!(window.LodAtlas && window.LodAtlas.focus(monster.맵번호))) {
        window.LodDashboard.toast("이 맵은 지도에 아직 없습니다 — " + monster.맵);
      }
    });
    actions.appendChild(where);
    title.appendChild(actions);
    hero.appendChild(title);
    detail.appendChild(hero);

    // 결론 한 줄 — 내 레벨에서 이 괴물이 어떤가.
    var gain = earned(monster, state.level);
    var kills = killsToLevel(monster, state.level);
    var chance = odds(monster.드랍);
    var verdict = text("p", "cdx-verdict");
    verdict.append("내 레벨 " + state.level + ": 한 마리 경험치 ");
    verdict.appendChild(text("strong", "", number(gain)));
    if (gain !== monster.경험치) { verdict.appendChild(text("span", "is-warn", " (표값 " + number(monster.경험치) + " 에서 깎임)")); }
    verdict.append(" → 다음 레벨까지 ");
    verdict.appendChild(text("strong", "", kills === null ? "—" : number(kills) + "마리"));
    verdict.append(" · 뭐라도 떨굴 확률 " + percent(chance.any) + " · 장비 " + percent(chance.gear));
    detail.appendChild(verdict);

    var stats = text("div", "monster-stats");
    stats.append(
      stat("체력", number(monster.체력)),
      stat("때리는 힘", monster.피해[0] + "~" + monster.피해[1], "방어 " + monster.방어),
      stat("경험치(표)", number(monster.경험치), "감산 레벨 " + monster.감산레벨),
      stat("금화", monster.금화[1] ? number(monster.금화[0]) + "~" + number(monster.금화[1]) : "없음"),
      stat("한 맵 최대", monster.젠최대 + "마리", monster.젠주기 ? monster.젠주기 + "초마다" : ""),
      stat("걸음·공격", (monster.이동속도 / 1000).toFixed(1) + "초 · " + (monster.공격속도 / 1000).toFixed(1) + "초"));
    detail.appendChild(stats);

    var dropHead = text("h3", "", "떨구는 것");
    dropHead.appendChild(text("small", "", monster.드랍.length ? monster.드랍.length + "가지 — 한 마리가 하나를 골라 한 번 굴린다 · 누르면 아이템으로" : ""));
    detail.appendChild(dropHead);
    if (monster.드랍.length) {
      var drops = text("ul", "cdx-links");
      shapes(monster.드랍).forEach(function (shape) { drops.appendChild(shapeRow(shape)); });
      detail.appendChild(drops);
    } else {
      detail.appendChild(text("p", "cdx-empty", "떨구는 것이 없습니다"));
    }

    var others = data.괴물.filter(function (m) { return m.이름 === monster.이름 && m !== monster; });
    if (others.length) {
      var otherHead = text("h3", "", "같은 괴물이 나오는 곳");
      otherHead.appendChild(text("small", "", others.length + "곳"));
      detail.appendChild(otherHead);
      var places = text("div", "cdx-places");
      others.forEach(function (m) {
        var chip = text("button", "chip", m.맵 + " · Lv " + m.감산레벨);
        chip.type = "button";
        chip.addEventListener("click", function () { focus(keyOf(m)); });
        places.appendChild(chip);
      });
      detail.appendChild(places);
    }
    detail.appendChild(text("code", "cdx-source", monster.근거));
  }

  function chips(host, values, current, onPick) {
    host.replaceChildren();
    values.forEach(function (value) {
      var chip = text("button", "chip", value.이름);
      chip.type = "button";
      var active = current === value.id;
      chip.classList.toggle("is-active", active);
      chip.setAttribute("aria-pressed", String(active));
      chip.addEventListener("click", function () { onPick(value.id); });
      host.appendChild(chip);
    });
  }

  function matches(monster) {
    if (state.region && monster.지역 !== state.region) { return false; }
    if (!state.query) { return true; }
    var names = monster.드랍.map(function (drop) { return drop.이름; }).join(" ");
    return (monster.이름 + " " + monster.맵 + " " + names).toLocaleLowerCase("ko").indexOf(state.query) >= 0;
  }

  function row(monster) {
    var li = text("li");
    var button = text("button", "cdx-row");
    button.type = "button";
    button.appendChild(thumb(monster));
    var copy = text("span", "cdx-row-copy");
    copy.appendChild(text("b", "", monster.이름));
    copy.appendChild(text("small", "", monster.맵 + " · Lv " + monster.감산레벨 + (monster.선공 === "선공" ? " · 선공" : "")));
    button.appendChild(copy);
    var kills = killsToLevel(monster, state.level);
    var gain = earned(monster, state.level);
    button.appendChild(text("em", gain !== monster.경험치 ? "is-warn" : "", kills === null ? number(gain) : number(kills) + "마리"));
    button.title = "다음 레벨까지 " + (kills === null ? "—" : number(kills) + "마리") + " · 한 마리 " + number(gain);
    if (monster === selected) { button.setAttribute("aria-current", "true"); }
    button.addEventListener("click", function () { open(monster, true); });
    li.appendChild(button);
    rows.set(monster, button);
    return li;
  }

  function render() {
    chips(document.getElementById("monster-regions"),
      [{ id: "", 이름: "모든 지역" }].concat(data.지역.map(function (name) { return { id: name, 이름: name }; })), state.region,
      function (id) { state.region = id; render(); });
    chips(document.getElementById("monster-sorts"),
      SORTS.map(function (s) { return { id: s.id, 이름: s.이름 }; }), state.sort,
      function (id) { state.sort = id; render(); });

    var order = SORTS.find(function (s) { return s.id === state.sort; }) || SORTS[0];
    var shown = data.괴물.filter(matches).slice().sort(function (a, b) { return order.재다(a) - order.재다(b); });
    rows = new Map();
    var fragment = document.createDocumentFragment();
    shown.forEach(function (monster) { fragment.appendChild(row(monster)); });
    list.replaceChildren(fragment);
    empty.hidden = shown.length !== 0;

    document.getElementById("monster-kinds").textContent = data.셈.이름;
    document.getElementById("monster-slots").textContent = data.셈.괴물자리;
    document.getElementById("monster-sprites").textContent = data.셈.그림있음;
    document.getElementById("monster-shown").textContent = shown.length;

    // 넓은 화면은 상세가 늘 보이므로 첫 줄을 고른다. 고른 것이 걸러져 사라졌어도 바꾼다. 폰은 누를 때만 연다.
    if (selected && shown.indexOf(selected) >= 0) { describe(selected); }
    else if (!phone.matches) { selected = shown[0] || null; mark(); describe(selected); }
  }

  function mark() {
    rows.forEach(function (button, monster) {
      if (monster === selected) { button.setAttribute("aria-current", "true"); } else { button.removeAttribute("aria-current"); }
    });
  }

  function open(monster, fromUser) {
    selected = monster;
    mark();
    describe(monster);
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
    var returning = selected && rows.get(selected);
    if (returning) { returning.focus(); }
  }

  /** 다른 화면에서 이 괴물을 연다 — 거르기를 지워 목록에 보이게 하고 그 줄로 스크롤한다. */
  function focus(key) {
    var monster = data.괴물.find(function (m) { return keyOf(m) === key; });
    if (!monster) { return false; }
    window.LodDashboard.show("monsters");
    if (!matches(monster)) {
      state.region = ""; state.query = "";
      document.getElementById("monster-search").value = "";
    }
    selected = monster;
    render();
    open(monster, true);
    var button = rows.get(monster);
    // 목록 칸 안에서만 굴린다 — scrollIntoView 는 창까지 밀어 머리줄이 올라간다. 폰은 상세가 덮으므로 그대로.
    if (button && !phone.matches) {
      var box = button.parentNode.parentNode.parentNode, at = button.getBoundingClientRect(), frame = box.getBoundingClientRect();
      box.scrollTop += at.top - frame.top - frame.height / 2;
    }
    return true;
  }
  window.LodMonsters = { focus: focus };

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
