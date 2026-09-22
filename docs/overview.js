(function () {
  "use strict";
  var features = window.LOD_FEATURES;
  if (!features) { return; }

  var dashboard = window.LodDashboard;
  var board = document.querySelector("#feature-board");
  var empty = document.querySelector("#feature-empty");

  // 서버·모바일 판정을 「사람이 지금 무엇을 할 수 있나」 한 줄로 접는다. 이 네 갈래가
  // 화면의 주 축이다 — 구현된 것부터 보고 싶다는 것이 이 화면의 요구였다.
  var STATES = [
    { id: "playable", 이름: "지금 만져진다", 설명: "서버가 돌고 모바일에도 있다",
      맞나: function (f) { return f.서버 === "돌아감" && f.모바일 === "됨"; } },
    { id: "partial", 이름: "반만 된다", 설명: "통신만 있거나 한쪽만 된다",
      맞나: function (f) { return f.모바일 === "일부"; } },
    { id: "server", 이름: "서버만 된다", 설명: "모바일에 화면이 없다",
      맞나: function (f) { return f.모바일 === "없음" && f.서버 !== "틀만" && f.서버 !== "없음"; } },
    { id: "none", 이름: "양쪽 다 없다", 설명: "서버부터 만들어야 한다",
      맞나: function (f) { return f.모바일 === "없음" && (f.서버 === "틀만" || f.서버 === "없음"); } },
    // 저장·괴물 젠처럼 모바일이 할 일이 아예 없는 것. 빨갛게 칠하면 안 만든 것처럼 읽힌다.
    { id: "na", 이름: "모바일 몫이 아니다", 설명: "서버 안에서 끝나는 일",
      맞나: function (f) { return f.모바일 === "해당없음"; } },
  ];

  var 서버색 = { 돌아감: "badge-done", 부분: "badge-partial", 틀만: "badge-risk", 없음: "badge-risk", 모름: "badge-partial" };
  var 모바일색 = { 됨: "badge-done", 일부: "badge-partial", 없음: "badge-risk", 해당없음: "badge-unknown", 모름: "badge-unknown" };

  var rows = features.묶음.reduce(function (all, group) {
    return all.concat(group.기능.map(function (feature) {
      var state = STATES.find(function (candidate) { return candidate.맞나(feature); });
      return Object.assign({ 묶음: group.이름, 상태: state ? state.id : "none" }, feature);
    }));
  }, []);

  var chosenState = "";
  var chosenGroup = "";
  var query = "";

  function text(tag, className, value) {
    var element = document.createElement(tag);
    if (className) { element.className = className; }
    if (value !== undefined) { element.textContent = value; }
    return element;
  }

  function tallyStrip() {
    var host = document.querySelector("#impl-tally");
    var abilities = window.ABILITY_DATA;
    var monsters = window.LOD_MONSTERS;
    var warps = window.LOD_REGION_WARPS;
    var items = window.LOD_ITEMS;

    var cells = STATES.map(function (state) {
      return {
        이름: state.이름,
        값: rows.filter(function (row) { return row.상태 === state.id; }).length + " / " + rows.length,
        메모: state.설명,
        누름: state.id,
      };
    });

    if (abilities) {
      // 「스크립트 있음」은 눌러서 보인다는 뜻이 아니다. 연출은 한글 이름으로만 찾으므로
      // 이름이 없으면 찾아보지도 못한다 — 두 숫자를 따로 세워야 화면이 거짓말을 안 한다.
      cells.push({ 이름: "기술·마법 스크립트", 값: abilities.요약.구현 + " / " + abilities.요약.서로다름,
        메모: "눌러서 보이는 것은 " + abilities.요약.연출셋다 + "개뿐", 화면: "abilities" });
      cells.push({ 이름: "연출을 못 찾은 것", 값: abilities.요약.이름없어못찾음 + abilities.요약.표에없음,
        메모: "대부분 한글 이름이 없어서다 — 원작에 연출 없는 기술은 없다", 화면: "abilities" });
    }
    if (monsters) {
      cells.push({ 이름: "괴물", 값: monsters.셈.이름 + "종",
        메모: monsters.셈.괴물자리 + "자리 · 그림 " + monsters.셈.그림있음, 화면: "monsters" });
    }
    if (warps) {
      var 맵 = 0, 닿음 = 0;
      Object.keys(warps.지역).forEach(function (name) { 맵 += warps.지역[name].셈.맵; 닿음 += warps.지역[name].셈.닿음; });
      cells.push({ 이름: "걸어 다닐 맵", 값: 닿음 + " / " + 맵,
        메모: "마을에서 못 닿는 맵 " + (맵 - 닿음) + "개", 화면: "world" });
    }
    if (items) {
      cells.push({ 이름: "아이템 한글 이름", 값: items.한글 + " / " + items.총,
        메모: "이름이 없으면 창에서 못 읽는다", 화면: "items" });
    }

    host.replaceChildren();
    cells.forEach(function (cell) {
      var node = text("button", "tally");
      node.type = "button";
      node.append(text("span", "", cell.이름), text("strong", "", cell.값), text("small", "", cell.메모));
      if (cell.누름) {
        node.addEventListener("click", function () { pickState(cell.누름); });
      } else {
        node.addEventListener("click", function () { dashboard.show(cell.화면); });
      }
      host.appendChild(node);
    });
  }

  function chips(host, values, current, onPick) {
    host.replaceChildren();
    values.forEach(function (value) {
      var chip = text("button", "chip", value.이름);
      chip.type = "button";
      var active = current === value.id;
      chip.classList.toggle("is-active", active);
      chip.setAttribute("aria-pressed", String(active));
      chip.addEventListener("click", function () { onPick(active ? "" : value.id); });
      host.appendChild(chip);
    });
  }

  function pickState(id) {
    chosenState = chosenState === id ? "" : id;
    render();
  }

  function matches(row) {
    if (chosenState && row.상태 !== chosenState) { return false; }
    if (chosenGroup && row.묶음 !== chosenGroup) { return false; }
    if (!query) { return true; }
    var hay = [row.이름, row.묶음, row.서버설명, row.모바일설명, row.근거, row.메모].join(" ").toLocaleLowerCase("ko");
    return hay.indexOf(query) >= 0;
  }

  function card(row) {
    var article = text("article", "feature-row is-" + row.상태);

    var head = text("div", "feature-head");
    head.append(text("span", "feature-number", String(row.번호).padStart(2, "0")), text("h3", "", row.이름));
    article.appendChild(head);

    var verdicts = text("div", "feature-verdicts");
    verdicts.append(
      text("span", "badge " + (서버색[row.서버] || "badge-unknown"), "서버 " + row.서버),
      text("span", "badge " + (모바일색[row.모바일] || "badge-unknown"), "모바일 " + row.모바일));
    article.appendChild(verdicts);

    var detail = text("dl", "feature-detail");
    if (row.서버설명) { detail.append(text("dt", "", "서버"), text("dd", "", row.서버설명)); }
    if (row.모바일설명) { detail.append(text("dt", "", "모바일"), text("dd", "", row.모바일설명)); }
    if (row.메모) { detail.append(text("dt", "", "메모"), text("dd", "", row.메모)); }
    if (detail.childElementCount) { article.appendChild(detail); }
    return article;
  }

  function render() {
    chips(document.querySelector("#feature-states"),
      STATES.map(function (s) { return { id: s.id, 이름: s.이름 }; }), chosenState, pickState);
    chips(document.querySelector("#feature-groups-filter"),
      features.묶음.map(function (g) { return { id: g.이름, 이름: g.이름 }; }), chosenGroup,
      function (id) { chosenGroup = id; render(); });

    var shown = rows.filter(matches);
    board.replaceChildren();
    features.묶음.forEach(function (group) {
      var inGroup = shown.filter(function (row) { return row.묶음 === group.이름; });
      if (!inGroup.length) { return; }
      var section = text("section", "feature-group");
      var heading = text("header", "");
      heading.append(text("h2", "", group.이름), text("span", "", inGroup.length + " / " + group.기능.length));
      section.appendChild(heading);
      inGroup.forEach(function (row) { section.appendChild(card(row)); });
      board.appendChild(section);
    });
    empty.hidden = shown.length !== 0;
  }

  document.querySelector("#feature-search").addEventListener("input", function (event) {
    query = String(event.target.value || "").trim().toLocaleLowerCase("ko");
    render();
  });

  tallyStrip();
  render();
})();
