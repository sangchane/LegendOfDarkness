(function () {
  "use strict";
  var data = window.LOD_NPCS;
  if (!data) { return; }

  var ROLE_BADGE = {
    "상점": "badge-done",
    "기술/마법 사범": "badge-now",
    "퀘스트": "badge-partial",
    "제작": "badge-partial",
    "꾸밈": "badge-partial",
    "이동": "badge-partial",
    "승급/전직": "badge-partial",
    "안내": "badge-unknown",
  };

  var grid = document.querySelector("#npc-grid");
  var empty = document.querySelector("#npc-empty");
  var onlyNow = document.querySelector("#npc-only-now");
  var state = { query: "", town: "", role: "", onlyNow: true };

  function text(tag, className, value) {
    var element = document.createElement(tag);
    if (className) { element.className = className; }
    if (value !== undefined) { element.textContent = value; }
    return element;
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

  function matches(npc) {
    if (state.onlyNow && !npc.닿음) { return false; }
    if (state.town && npc.마을 !== state.town) { return false; }
    if (state.role && npc.역할 !== state.role) { return false; }
    if (!state.query) { return true; }
    return (npc.이름 + " " + npc.맵 + " " + npc.마을).toLocaleLowerCase("ko").indexOf(state.query) >= 0;
  }

  function stockDetail(npc) {
    var shop = npc.상점;
    var d = text("details", "");
    var summary = text("summary", "", "파는 것 " + shop.개수 + "개 — " + Object.keys(shop.갈래별)
      .map(function (k) { return k + " " + shop.갈래별[k]; }).join(" · "));
    d.appendChild(summary);
    var stock = text("div", "npc-stock");
    shop.목록.forEach(function (item) {
      stock.appendChild(text("span", item.템플릿있음 ? "" : "is-missing", item.이름));
    });
    d.appendChild(stock);
    return d;
  }

  function teachDetail(npc) {
    var d = text("details", "");
    d.appendChild(text("summary", "", "가르치는 것 " + npc.사범.가르침.length + "가지"));
    d.appendChild(text("p", "", npc.사범.가르침.join(" · ")));
    return d;
  }

  function card(npc) {
    var article = text("article", "npc-card" + (npc.닿음 ? "" : " is-unreached"));

    var header = text("header", "");
    var title = text("div", "");
    title.append(text("h3", "", npc.이름), text("span", "npc-where", npc.맵 + " · " + npc.맵번호
      + " (" + npc.좌표[0] + "," + npc.좌표[1] + ")"));
    header.appendChild(title);
    header.appendChild(text("span", "badge " + (ROLE_BADGE[npc.역할] || "badge-unknown"), npc.역할));
    article.appendChild(header);

    if (!npc.닿음) {
      article.appendChild(text("p", "npc-line", "아직 못 감 — 노비스·수오미 마을에서 안 이어진 마을"));
    }

    if (npc.대사) { article.appendChild(text("p", "npc-line", "“" + npc.대사 + "”")); }

    if (npc.역할 === "상점" && npc.상점) { article.appendChild(stockDetail(npc)); }
    if (npc.역할 === "기술/마법 사범" && npc.사범 && npc.사범.가르침.length) { article.appendChild(teachDetail(npc)); }

    article.appendChild(text("code", "npc-source", npc.원작근거 || npc.근거));
    return article;
  }

  function render() {
    var pool = data.NPC.filter(function (n) { return !state.onlyNow || n.닿음; });

    chips(document.querySelector("#npc-towns"),
      Array.from(new Set(pool.map(function (n) { return n.마을; }))).sort()
        .map(function (name) { return { id: name, 이름: name }; }), state.town,
      function (id) { state.town = id; render(); });

    chips(document.querySelector("#npc-roles"),
      Object.keys(data.셈.역할별).sort().map(function (role) { return { id: role, 이름: role }; }), state.role,
      function (id) { state.role = id; render(); });

    var shown = data.NPC.filter(matches);

    grid.replaceChildren();
    shown.forEach(function (npc) { grid.appendChild(card(npc)); });
    empty.hidden = shown.length !== 0;

    document.querySelector("#npc-total").textContent = data.셈.전체;
    document.querySelector("#npc-now").textContent = data.셈.지금서있다;
    document.querySelector("#npc-later").textContent = data.셈.아직못감;
    document.querySelector("#npc-shown").textContent = shown.length;
  }

  function rules() {
    var host = document.querySelector("#npc-rules");
    host.replaceChildren();
    host.append(text("strong", "", "역할을 가르는 법"));
    var list = document.createElement("ul");
    [data.규칙.닿음, data.규칙.역할, data.규칙.여관창고없음].forEach(function (line) {
      list.appendChild(text("li", "", line));
    });
    host.appendChild(list);
  }

  onlyNow.addEventListener("click", function () {
    state.onlyNow = !state.onlyNow;
    onlyNow.classList.toggle("is-active", state.onlyNow);
    onlyNow.setAttribute("aria-pressed", String(state.onlyNow));
    state.town = "";
    render();
  });
  document.querySelector("#npc-search").addEventListener("input", function (event) {
    state.query = String(event.target.value || "").trim().toLocaleLowerCase("ko");
    render();
  });

  rules();
  render();
})();
