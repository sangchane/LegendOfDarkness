/* 기술·마법 — 직업별로, 배우는 차례대로.
 *
 * 613개를 표로 늘어놓으면 무엇을 배워야 무엇이 열리는지가 안 보인다. 직업으로 나누고
 * 선행 사슬대로 들여 쓰면 보인다. 한글 이름은 data/기술마법-한글이름.tsv 에서 오고,
 * 여기서 적어 둔 것은 그 표에 붙여 넣을 수 있게 모아 준다. */
(function () {
  "use strict";

  var DATA = window.ABILITY_DATA;
  if (!DATA) return;

  var listEl = document.getElementById("ability-list");
  var detailEl = document.getElementById("ability-detail");
  var searchEl = document.getElementById("ability-search");
  var sumEl = document.getElementById("ability-summary");
  if (!listEl || !detailEl) return;

  var STORE = "lod-ability-names";
  var typed = {};
  try { typed = JSON.parse(localStorage.getItem(STORE) || "{}"); } catch (e) { typed = {}; }

  function remember(name, value) {
    if (value) { typed[name] = value; } else { delete typed[name]; }
    try { localStorage.setItem(STORE, JSON.stringify(typed)); } catch (e) { /* 사생활 모드 */ }
    paintSummary();
  }

  function paintSummary() {
    var s = DATA["요약"];
    var mine = Object.keys(typed).length;
    sumEl.innerHTML =
      [["전체", s["전체"]], ["기술", s["기술"]], ["마법", s["마법"]],
       ["스크립트 있음", s["스크립트있음"]], ["채운 이름", s["한글채움"] + mine]]
        .map(function (p) {
          return '<div class="world-stat"><span>' + p[0] + "</span><strong>" + p[1] + "</strong></div>";
        }).join("") +
      '<p class="world-note">한글 이름 칸에 적으면 이 브라우저에 남습니다. 다 적은 뒤 <b>표로 내보내기</b>를 눌러 ' +
      "<code>data/기술마법-한글이름.tsv</code> 에 붙여 넣으세요. " +
      "<strong>아이콘은 아직 못 붙였습니다.</strong> 그림은 <code>skill001.epf</code>(266장)·" +
      "<code>spell001.epf</code> 에 있지만 제 색으로 그릴 색표를 못 찾았고, 어느 그림이 어느 기술인지도 " +
      "아직 모릅니다 — <code>원문</code> 은 자료의 둘째 칸 첫 값일 뿐이고 Assail 과 Assault 가 둘 다 1 이라 " +
      "아이콘 번호가 아닙니다.</p>" +
      '<button type="button" id="ability-export" class="quiet-link">표로 내보내기</button>';
    var b = document.getElementById("ability-export");
    if (b) b.addEventListener("click", exportTsv);
  }

  function exportTsv() {
    var lines = [];
    DATA["묶음"].forEach(function (g) {
      g["목록"].forEach(function (a) {
        var ko = typed[a["이름"]] || a["한글"];
        if (ko) lines.push([g["갈래"], g["직업"], a["이름"], a["선행"], a["레벨"], ko].join("\t"));
      });
    });
    var text = lines.length ? lines.join("\n") : "아직 적은 이름이 없습니다.";
    detailEl.insertAdjacentHTML("afterbegin",
      '<pre class="ability-export">' + text.replace(/[&<]/g, function (c) {
        return c === "&" ? "&amp;" : "&lt;";
      }) + "</pre>");
  }

  function renderList(filter) {
    var q = (filter || "").trim().toLowerCase();
    var shown = DATA["묶음"].filter(function (g) {
      return !q || g["목록"].some(function (a) {
        return a["이름"].toLowerCase().indexOf(q) >= 0 ||
               (typed[a["이름"]] || a["한글"] || "").indexOf(q) >= 0;
      });
    });
    listEl.innerHTML = shown.map(function (g, i) {
      return '<button type="button" class="world-item" data-key="' + DATA["묶음"].indexOf(g) +
        '"><span>' + g["직업"] + " " + g["갈래"] + "</span><em>" + g["목록"].length + "개</em></button>";
    }).join("") || '<p class="world-empty">그런 이름이 없습니다.</p>';
    if (shown.length) select(DATA["묶음"].indexOf(shown[0]), q);
  }

  function select(idx, q) {
    var g = DATA["묶음"][idx];
    if (!g) return;
    Array.prototype.forEach.call(listEl.querySelectorAll(".world-item"), function (b) {
      b.classList.toggle("is-active", b.getAttribute("data-key") === String(idx));
    });

    detailEl.innerHTML = "<h2>" + g["직업"] + " " + g["갈래"] + "</h2>" +
      '<p class="world-meta">' + g["목록"].length + "개 · 들여쓰기가 깊을수록 나중에 배웁니다</p>" +
      '<ol class="world-tree ability-tree">' + g["목록"].map(function (a) {
        var hit = q && a["이름"].toLowerCase().indexOf(q) >= 0;
        return '<li style="--depth:' + a["깊이"] + '"' + (hit ? ' class="is-hit"' : "") + ">" +
          '<b>' + a["이름"] + "</b>" +
          '<em>원문 ' + a["아이콘"] + (a["레벨"] ? " · 레벨 " + a["레벨"] : "") +
          (a["스크립트"] ? " · 스크립트 있음" : "") + "</em>" +
          '<input class="ability-name" data-for="' + a["이름"] + '" type="text" placeholder="한글 이름" value="' +
          (typed[a["이름"]] || a["한글"] || "") + '">' +
          "</li>";
      }).join("") + "</ol>";

    Array.prototype.forEach.call(detailEl.querySelectorAll(".ability-name"), function (input) {
      input.addEventListener("change", function () {
        remember(input.getAttribute("data-for"), input.value.trim());
      });
    });
  }

  listEl.addEventListener("click", function (ev) {
    var b = ev.target.closest ? ev.target.closest(".world-item") : null;
    if (b) select(Number(b.getAttribute("data-key")), (searchEl && searchEl.value || "").trim().toLowerCase());
  });
  if (searchEl) searchEl.addEventListener("input", function () { renderList(searchEl.value); });

  paintSummary();
  renderList("");
})();
