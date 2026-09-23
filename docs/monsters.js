(function () {
  "use strict";
  var data = window.LOD_MONSTERS;
  if (!data) { return; }

  var SPRITE_DIR = "../mobile/client/assets/actor/creature/";
  var CLASSES = { 0: "", 1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "무도가" };
  var SORTS = [
    { id: "exp", 이름: "경험치", 재다: function (m) { return -m.경험치; } },
    { id: "hp", 이름: "체력", 재다: function (m) { return -m.체력; } },
    { id: "dmg", 이름: "때리는 힘", 재다: function (m) { return -m.피해[1]; } },
    { id: "map", 이름: "맵 차례", 재다: function (m) { return m.맵번호; } },
  ];

  var grid = document.querySelector("#monster-grid");
  var empty = document.querySelector("#monster-empty");
  var hover = document.querySelector("#monster-hover");
  var section = grid.closest(".view");
  var state = { query: "", region: "", map: "", sort: "exp", level: 1 };
  // 눌러서(탭해서) 고정한 괴물. 고정되면 마우스가 떠나거나 초점이 빠져도 풍선이 남는다.
  var pinned = null, shownFor = null;

  function text(tag, className, value) {
    var element = document.createElement(tag);
    if (className) { element.className = className; }
    if (value !== undefined) { element.textContent = value; }
    return element;
  }

  function number(value) { return Number(value || 0).toLocaleString("ko-KR"); }

  /** 레벨 차이로 깎인 뒤 실제로 손에 들어오는 경험치. monsterexp.cs 의 ForLevel 과 같은 식이다. */
  function earned(monster, level) {
    var rule = data.규칙.감산;
    var gap = level - monster.레벨;
    if (gap <= rule.용서) { return monster.경험치; }
    var share = Math.pow(0.5, (gap - rule.용서) / rule.반감);
    return Math.round(monster.경험치 * Math.max(rule.최소, share));
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
    box.title = art.이름 + " · " + art.칸 + "칸";

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

  function stat(label, value, note) {
    var cell = text("div", "monster-stat");
    cell.append(text("span", "", label), text("strong", "", value));
    if (note) { cell.append(text("small", "", note)); }
    return cell;
  }

  function dropRow(drop) {
    var row = text("div", "drop-row" + (drop.템플릿있음 ? "" : " is-missing"));
    var head = text("div", "drop-head");
    head.append(text("b", "", drop.이름), text("span", "drop-kind", drop.갈래));
    row.appendChild(head);

    var meter = text("div", "drop-meter");
    var fill = text("i", "");
    // 막대는 실제 확률을 그린다. 100%를 가득으로 두면 20%가 안 보이므로 50%를 가득으로 본다.
    fill.style.setProperty("--fill", Math.min(100, drop.실제확률 * 200) + "%");
    meter.appendChild(fill);
    row.appendChild(meter);

    var note = [(drop.실제확률 * 100).toFixed(1) + "%"];
    note.push("표 " + (drop.표확률 * 100).toFixed(0) + "% ÷ 목록");
    if (drop.체력회복) { note.push("체력 +" + drop.체력회복); }
    if (drop.마력회복) { note.push("마력 +" + drop.마력회복); }
    if (drop.값) { note.push(number(drop.값) + "전"); }
    if (CLASSES[drop.직업]) { note.push(CLASSES[drop.직업] + (drop.요구레벨 > 1 ? " " + drop.요구레벨 + "레벨" : "")); }
    if (!drop.템플릿있음) { note.push("템플릿 없음 — 안 떨어진다"); }
    row.appendChild(text("small", "drop-note", note.join(" · ")));
    return row;
  }

  function openHover(monster, anchor) {
    if (!hover) { return; }
    shownFor = monster;
    hover.replaceChildren();

    var top = text("div", "item-hover-top");
    top.appendChild(sprite(monster));
    var title = text("div", "");
    title.append(text("b", "", monster.이름), text("em", "", monster.맵 + " · " + monster.맵번호));
    top.appendChild(title);
    hover.appendChild(top);

    var stats = text("div", "monster-stats");
    var kills = killsToLevel(monster, state.level);
    var gain = earned(monster, state.level);
    stats.append(
      stat("체력", number(monster.체력)),
      stat("때리는 힘", monster.피해[0] + "~" + monster.피해[1], "방어 " + monster.방어),
      stat("경험치", number(gain), gain === monster.경험치 ? "표값 그대로" : "표값 " + number(monster.경험치) + " 에서 깎임"),
      stat("다음 레벨까지", kills === null ? "—" : number(kills) + "마리", "내 레벨 " + state.level),
      stat("한 맵 최대", monster.젠최대 + "마리", monster.젠주기 ? monster.젠주기 + "초마다" : ""),
      stat("금화", monster.금화[1] ? number(monster.금화[0]) + "~" + number(monster.금화[1]) : "없음"));
    hover.appendChild(stats);

    var drops = text("div", "monster-drops");
    if (monster.드랍.length) {
      drops.appendChild(text("h4", "", "떨구는 것 " + monster.드랍.length + "가지 — 하나를 골라 한 번 굴린다"));
      monster.드랍.forEach(function (drop) { drops.appendChild(dropRow(drop)); });
    } else {
      drops.appendChild(text("p", "monster-nodrop", "떨구는 것이 없습니다"));
    }
    hover.appendChild(drops);

    var source = text("code", "monster-source", monster.근거);
    hover.appendChild(source);

    var box = anchor.getBoundingClientRect();
    hover.hidden = false;
    hover.setAttribute("aria-hidden", "false");
    var own = hover.getBoundingClientRect();
    // 오른쪽에 공간이 있으면 오른쪽, 없으면 왼쪽에 둔다.
    var left = box.right + 12;
    if (left + own.width + 12 > window.innerWidth) {
      left = box.left - own.width - 12;
    }
    // 그래도 화면 밖이면 화면 경계에 맞춘다 (모바일 등 좁은 화면).
    left = Math.max(12, Math.min(left, window.innerWidth - own.width - 12));

    var topPos = Math.min(box.top, window.innerHeight - own.height - 12);
    topPos = Math.max(12, topPos);
    
    hover.style.transform = "translate(" + left + "px," + topPos + "px)";
  }

  function closeHover() {
    shownFor = null;
    if (hover) {
      hover.hidden = true;
      hover.setAttribute("aria-hidden", "true");
    }
  }

  /** 카드 위에 늘 보이는 한 줄 — 풍선을 열지 않아도 셈의 핵심은 읽힌다. */
  function keyLine(monster) {
    var kills = killsToLevel(monster, state.level);
    return "체력 " + number(monster.체력)
      + " · 피해 " + monster.피해[0] + "~" + monster.피해[1]
      + " · 경험치 " + number(earned(monster, state.level))
      + " · 다음 레벨 " + (kills === null ? "—" : number(kills) + "마리");
  }

  function card(monster) {
    var article = text("article", "monster-card");

    var head = text("header", "");
    head.appendChild(sprite(monster));
    var title = text("div", "");
    title.append(text("h3", "", monster.이름), text("span", "monster-where", monster.맵 + " · " + monster.맵번호));
    var marks = text("div", "monster-marks");
    marks.appendChild(text("span", "badge " + (monster.선공 === "선공" ? "badge-risk"
      : monster.선공 === "반반" ? "badge-partial" : "badge-done"), monster.선공));
    if (!monster.드랍켜짐) { marks.appendChild(text("span", "badge badge-risk", "드랍 꺼짐")); }
    title.appendChild(marks);
    head.appendChild(title);
    article.appendChild(head);
    article.appendChild(text("p", "monster-keyline", keyLine(monster)));

    // 풍선은 마우스·초점·탭 모두로 연다. 탭(누름)은 고정 — 다시 누르거나 바깥을 누르거나 Esc 로 닫는다.
    article.tabIndex = 0;
    article.setAttribute("aria-describedby", "monster-hover");
    article.addEventListener("mouseenter", function () { openHover(monster, article); });
    article.addEventListener("mouseleave", function () { if (pinned !== monster) { closeHover(); } });
    article.addEventListener("focus", function () { openHover(monster, article); });
    article.addEventListener("blur", function () { if (pinned !== monster) { closeHover(); } });
    article.addEventListener("click", function () {
      if (pinned === monster) { pinned = null; closeHover(); return; }
      pinned = monster;
      openHover(monster, article);
    });
    article.addEventListener("keydown", function (event) {
      if (event.key !== "Enter" && event.key !== " ") { return; }
      event.preventDefault();
      article.click();
    });
    cards.set(monster, article);

    return article;
  }


  function chips(host, values, current, onPick) {
    host.replaceChildren();
    values.forEach(function (value) {
      var chip = text("button", "chip", value.이름);
      chip.type = "button";
      var active = current === value.id;
      chip.classList.toggle("is-active", active);
      chip.setAttribute("aria-pressed", String(active));
      chip.addEventListener("click", function () { onPick(active && value.끌수있나 !== false ? "" : value.id); });
      host.appendChild(chip);
    });
  }

  function matches(monster) {
    if (state.region && monster.지역 !== state.region) { return false; }
    if (state.map && String(monster.맵번호) !== state.map) { return false; }
    if (!state.query) { return true; }
    return (monster.이름 + " " + monster.맵).toLocaleLowerCase("ko").indexOf(state.query) >= 0;
  }

  var cards = new Map();

  function render() {
    chips(document.querySelector("#monster-regions"),
      data.지역.map(function (name) { return { id: name, 이름: name }; }), state.region,
      function (id) { state.region = id; state.map = ""; render(); });

    var maps = [];
    data.괴물.forEach(function (m) {
      if (state.region && m.지역 !== state.region) { return; }
      if (!maps.some(function (entry) { return entry.id === String(m.맵번호); })) {
        maps.push({ id: String(m.맵번호), 이름: m.맵 });
      }
    });
    chips(document.querySelector("#monster-maps"), maps, state.map, function (id) { state.map = id; render(); });
    chips(document.querySelector("#monster-sorts"),
      SORTS.map(function (s) { return { id: s.id, 이름: s.이름, 끌수있나: false }; }), state.sort,
      function (id) { state.sort = id; render(); });

    var order = SORTS.find(function (s) { return s.id === state.sort; }) || SORTS[0];
    var shown = data.괴물.filter(matches).slice().sort(function (a, b) { return order.재다(a) - order.재다(b); });

    grid.replaceChildren();
    cards = new Map();
    shown.forEach(function (monster) { grid.appendChild(card(monster)); });
    empty.hidden = shown.length !== 0;

    // 열려 있던 풍선은 새 카드에 다시 붙여 새 레벨의 값으로 그린다. 걸러져 사라졌으면 닫는다.
    var still = shownFor && cards.get(shownFor);
    if (still) { openHover(shownFor, still); } else { pinned = null; closeHover(); }

    document.querySelector("#monster-kinds").textContent = data.셈.이름;
    document.querySelector("#monster-slots").textContent = data.셈.괴물자리;
    document.querySelector("#monster-sprites").textContent = data.셈.그림있음;
    document.querySelector("#monster-shown").textContent = shown.length;
  }

  function rules() {
    var host = document.querySelector("#monster-rules");
    host.replaceChildren();
    host.append(text("strong", "", "이 화면의 숫자가 어디서 오나"));
    var list = document.createElement("ul");
    [data.규칙.드랍, data.규칙.금화, data.규칙.선공, data.규칙.감산근거].forEach(function (line) {
      list.appendChild(text("li", "", line));
    });
    host.appendChild(list);
    if (data.빈맵.length) {
      host.appendChild(text("p", "", "괴물이 하나도 없는 맵 " + data.빈맵.length + "개: "
        + data.빈맵.map(function (m) { return m.맵; }).join(" · ")));
    }
  }

  var slider = document.querySelector("#monster-level");
  var output = document.querySelector("#monster-level-out");
  slider.addEventListener("input", function () {
    state.level = Number(slider.value);
    output.textContent = slider.value;
    render();
  });
  document.querySelector("#monster-search").addEventListener("input", function (event) {
    state.query = String(event.target.value || "").trim().toLocaleLowerCase("ko");
    render();
  });

  document.addEventListener("click", function (event) {
    if (!pinned || grid.contains(event.target) || event.target.closest(".monster-level-row")) { return; }
    pinned = null;
    closeHover();
  });
  document.addEventListener("keydown", function (event) {
    if (event.key !== "Escape" || !shownFor) { return; }
    pinned = null;
    closeHover();
  });

  // 네 방향을 1.5초마다 돈다: 0=북(등) 1=동(앞) 2=남(앞·뒤집기) 3=서(등·뒤집기).
  // 카드마다가 아니라 화면에 방향 하나만 적고, 어느 그림을 쓸지는 CSS 가 고른다.
  // 괴물 화면이 숨어 있거나 창이 가려지면 멈춘다.
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
  document.addEventListener("visibilitychange", startTurning);

  rules();
  render();
  startTurning();
})();
