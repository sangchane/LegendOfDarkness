(function () {
  "use strict";
  var data = window.LOD_CHANGES;
  if (!data) { return; }

  var list = document.querySelector("#change-list");
  var empty = document.querySelector("#change-empty");
  var state = { kind: "", area: "" };

  // 갈래마다 무게가 다르다. 「우리가정함」은 언젠가 버려야 할 값이라 눈에 먼저 들어와야 한다.
  var KINDS = {
    우리가정함: { 색: "badge-risk", 설명: "근거 없이 정했다 — 근거가 나오면 바꾼다" },
    원작복원: { 색: "badge-done", 설명: "원작 쪽으로 되돌렸다" },
    팩에서: { 색: "badge-partial", 설명: "원작 자료에 없어 서버팩 값을 썼다" },
    더한것: { 색: "badge-now", 설명: "원작에 없던 편의 — 뺄 수 있어야 한다" },
    범위: { 색: "badge-unknown", 설명: "원작에 있지만 아직 일부만" },
  };

  function text(tag, className, value) {
    var element = document.createElement(tag);
    if (className) { element.className = className; }
    if (value !== undefined) { element.textContent = value; }
    return element;
  }

  function unique(key) {
    var seen = [];
    data.항목.forEach(function (row) { if (seen.indexOf(row[key]) < 0) { seen.push(row[key]); } });
    return seen;
  }

  function chips(host, values, current, onPick) {
    host.replaceChildren();
    values.forEach(function (value) {
      var chip = text("button", "chip", value);
      chip.type = "button";
      var active = current === value;
      chip.classList.toggle("is-active", active);
      chip.setAttribute("aria-pressed", String(active));
      chip.addEventListener("click", function () { onPick(active ? "" : value); });
      host.appendChild(chip);
    });
  }

  function tally() {
    var host = document.querySelector("#change-tally");
    host.replaceChildren();
    unique("갈래").forEach(function (kind) {
      var count = data.항목.filter(function (row) { return row.갈래 === kind; }).length;
      var node = text("button", "tally" + (kind === "우리가정함" ? " is-warning" : ""));
      node.type = "button";
      node.append(text("span", "", kind), text("strong", "", String(count)),
        text("small", "", (KINDS[kind] || {}).설명 || ""));
      node.addEventListener("click", function () {
        state.kind = state.kind === kind ? "" : kind;
        render();
      });
      host.appendChild(node);
    });
  }

  function row(entry) {
    var article = text("article", "change-row");

    var head = text("header", "");
    head.append(text("h3", "", entry.제목));
    var marks = text("div", "change-marks");
    marks.append(text("span", "badge " + ((KINDS[entry.갈래] || {}).색 || "badge-unknown"), entry.갈래),
      text("span", "badge", entry.영역));
    head.appendChild(marks);
    article.appendChild(head);

    var diff = text("div", "change-diff");
    var before = text("div", "change-before");
    before.append(text("span", "", "전"), text("p", "", entry.전));
    var after = text("div", "change-after");
    after.append(text("span", "", "후"), text("p", "", entry.후));
    diff.append(before, text("i", "change-arrow", "→"), after);
    article.appendChild(diff);

    var notes = text("dl", "change-notes");
    notes.append(text("dt", "", "왜"), text("dd", "", entry.왜));
    if (entry.풀림) { notes.append(text("dt", "is-warning", "언제 버리나"), text("dd", "is-warning", entry.풀림)); }
    if (entry.남음) { notes.append(text("dt", "", "남은 것"), text("dd", "", entry.남음)); }
    article.appendChild(notes);

    var sources = text("div", "change-sources");
    entry.근거.forEach(function (path) { sources.appendChild(text("code", "", path)); });
    if (entry.문서) {
      var link = text("a", "quiet-link", entry.문서.replace(/^docs\//, "") + " →");
      link.href = entry.문서.replace(/^docs\//, "");
      sources.appendChild(link);
    }
    article.appendChild(sources);
    return article;
  }

  function render() {
    chips(document.querySelector("#change-kinds"), unique("갈래"), state.kind,
      function (value) { state.kind = value; render(); });
    chips(document.querySelector("#change-areas"), unique("영역"), state.area,
      function (value) { state.area = value; render(); });

    var shown = data.항목.filter(function (entry) {
      if (state.kind && entry.갈래 !== state.kind) { return false; }
      if (state.area && entry.영역 !== state.area) { return false; }
      return true;
    });
    // 버려야 할 값을 맨 위로.
    shown = shown.slice().sort(function (a, b) {
      return (b.갈래 === "우리가정함") - (a.갈래 === "우리가정함");
    });

    list.replaceChildren();
    shown.forEach(function (entry) { list.appendChild(row(entry)); });
    empty.hidden = shown.length !== 0;
  }

  tally();
  render();
})();
