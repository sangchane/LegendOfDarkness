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
  var state = { query: "", region: "", map: "", sort: "exp", level: 1 };

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
    // 한 칸만 보인다. 판을 칸 수만큼 넓게 깔고 왼쪽 끝으로 잘라 낸다.
    var frame = Math.round(art.너비 / art.칸);
    var box = text("div", "monster-art is-animated");
    box.style.setProperty("--w", frame + "px");
    box.style.setProperty("--h", art.높이 + "px");
    box.style.setProperty("--sheet", "url(" + SPRITE_DIR + art.이름 + ".png)");
    box.style.setProperty("--sheet-w", art.너비 + "px");
    box.style.setProperty("--frames", art.칸);
    box.title = art.이름 + " · " + art.칸 + "칸";
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
    var left = Math.min(box.right + 12, window.innerWidth - own.width - 12);
    var topPos = Math.min(box.top, window.innerHeight - own.height - 12);
    hover.style.transform = "translate(" + Math.max(12, left) + "px," + Math.max(12, topPos) + "px)";
  }

  function closeHover() {
    if (hover) {
      hover.hidden = true;
      hover.setAttribute("aria-hidden", "true");
    }
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

    article.addEventListener("mouseenter", function () { openHover(monster, article); });
    article.addEventListener("mouseleave", closeHover);
    article.addEventListener("click", function () { openHover(monster, article); });

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
    shown.forEach(function (monster) { grid.appendChild(card(monster)); });
    empty.hidden = shown.length !== 0;

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

  rules();
  render();
})();
