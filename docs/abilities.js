/* 기술·마법 — 직업별로, 배우는 차례대로.
 *
 * 613개를 표로 늘어놓으면 무엇을 배워야 무엇이 열리는지가 안 보인다. 직업으로 나누고
 * 선행 사슬대로 들여 쓰면 보인다. 자동 한글 이름은 서버팩 2개 합의에서 오고,
 * 프로젝트 이름표와 브라우저 입력으로 언제든 바로잡아 내보낼 수 있다. */
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

  function currentName(a) {
    if (Object.prototype.hasOwnProperty.call(typed, a["이름"])) {
      return { value: typed[a["이름"]], source: "브라우저 수정" };
    }
    return { value: a["한글"] || "", source: a["이름출처"] || "미확정" };
  }

  function escapeHtml(value) {
    return String(value == null ? "" : value).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

  function paintSummary() {
    var s = DATA["요약"];
    var mine = Object.keys(typed).length;
    sumEl.innerHTML =
      [["전체", s["전체"]], ["기술", s["기술"]], ["마법", s["마법"]],
       ["두 팩 합의", s["자동확정"]], ["프로젝트 수정", s["사용자수정"]], ["브라우저 수정", mine]]
        .map(function (p) {
          return '<div class="world-stat"><span>' + p[0] + "</span><strong>" + p[1] + "</strong></div>";
        }).join("") +
      '<p class="world-note">정렬과 선행 관계는 Hades 기준입니다. <b>두 팩 합의</b>는 5.99·혼든의 이름이 같은 경우만 뜻합니다. ' +
      '한글 이름을 고치면 이 브라우저에 남고, <b>표로 내보내기</b>를 눌러 ' +
      "<code>data/기술마법-한글이름.tsv</code> 에 붙여 넣으세요. " +
      "아이콘은 <code>setoa.dat</code> 의 <code>skill001.epf</code>·<code>spell001.epf</code> 를 " +
      "같은 아카이브의 <code>gui06.pal</code> 로 그렸습니다. 원작 자료 뷰어가 두 아이콘 창에 " +
      "지정한 색표를 그대로 썼습니다.</p>" +
      '<button type="button" id="ability-export" class="quiet-link">표로 내보내기</button>';
    var b = document.getElementById("ability-export");
    if (b) b.addEventListener("click", exportTsv);
  }

  function exportTsv() {
    var lines = [];
    DATA["묶음"].forEach(function (g) {
      g["목록"].forEach(function (a) {
        var ko = currentName(a).value;
        if (ko) lines.push([g["갈래"], g["직업"], a["이름"], a["선행"], a["레벨"], ko].join("\t"));
      });
    });
    var text = lines.length ? lines.join("\n") : "아직 적은 이름이 없습니다.";
    detailEl.insertAdjacentHTML("afterbegin",
      '<pre class="ability-export">' + text.replace(/[&<]/g, function (c) {
        return c === "&" ? "&amp;" : "&lt;";
      }) + "</pre>");
  }

  /* 아이콘은 한 장짜리 시트를 잘라 쓴다. 번호는 자료의 raw[1] 첫 값이고, Hades 가 손으로
   * 넣어 둔 assail.json 의 Icon 과 맞는다. 여러 기술이 한 아이콘을 함께 쓰는 일은 흔하다
   * (Assail 과 Assault 가 둘 다 1 이다). 시트는 16칸씩 · 한 칸 35x35. */
  var CELL = 35, COLS = 16;

  function icon(kind, n) {
    var sheet = kind === "기술" ? "skill" : "spell";
    var x = (n % COLS) * CELL, y = Math.floor(n / COLS) * CELL;
    return '<i class="ability-icon" title="' + n + '" style="background-image:url(ability-icons/' +
      sheet + '.png);background-position:-' + x + "px -" + y + 'px"></i>';
  }

  function renderList(filter) {
    var q = (filter || "").trim().toLowerCase();
    var shown = DATA["묶음"].filter(function (g) {
      return !q || g["목록"].some(function (a) {
        return a["이름"].toLowerCase().indexOf(q) >= 0 ||
               currentName(a).value.toLowerCase().indexOf(q) >= 0;
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
      var active = b.getAttribute("data-key") === String(idx);
      b.classList.toggle("is-active", active);
      if (active) { b.setAttribute("aria-current", "true"); } else { b.removeAttribute("aria-current"); }
    });

    detailEl.innerHTML = "<h2>" + g["직업"] + " " + g["갈래"] + "</h2>" +
      '<p class="world-meta">' + g["목록"].length + "개 · 들여쓰기가 깊을수록 나중에 배웁니다</p>" +
      '<ol class="world-tree ability-tree">' + g["목록"].map(function (a) {
        var named = currentName(a);
        var hit = q && (a["이름"].toLowerCase().indexOf(q) >= 0 || named.value.toLowerCase().indexOf(q) >= 0);
        var sourceClass = { "서버팩 2개 일치": "is-consensus", "사용자 수정": "is-manual",
                            "브라우저 수정": "is-browser", "미확정": "is-empty" }[named.source] || "is-empty";
        return '<li style="--depth:' + a["깊이"] + '"' + (hit ? ' class="is-hit"' : "") + ">" +
          icon(g["갈래"], a["아이콘"]) +
          '<b>' + escapeHtml(a["이름"]) + "</b>" +
          '<em>' + (a["레벨"] ? "레벨 " + a["레벨"] : "") +
          (a["스크립트"] ? " · 스크립트 있음" : "") + "</em>" +
          '<span class="ability-name-source ' + sourceClass + '">' + escapeHtml(named.source) + '</span>' +
          '<input class="ability-name" data-for="' + escapeHtml(a["이름"]) + '" type="text" placeholder="한글 이름" value="' +
          escapeHtml(named.value) + '" aria-label="' + escapeHtml(a["이름"] + " 한글 이름") + '">' +
          "</li>";
      }).join("") + "</ol>";

    Array.prototype.forEach.call(detailEl.querySelectorAll(".ability-name"), function (input) {
      input.addEventListener("change", function () {
        remember(input.getAttribute("data-for"), input.value.trim());
        select(idx, q);
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
