/* 아이템 도감 — 왼쪽 목록 | 오른쪽 상세(큰 아이콘 · 한글 이름 칸 · 어둠템 판정 · 수치 · 떨구는 괴물 · 파는 곳).
 *
 * 한글 이름을 고치면 이 브라우저와 관리 페이지 서버(`/api/state/item-names`)에 남고, [표로 내보내기] 로 받아
 * `data/아이템-한글이름.tsv` 에 붙여 넣는다. 떨구는 괴물·파는 곳·어둠템 판정은 atlas-data.js(1MB) — 이 화면을
 * 처음 열 때 불러온다. 다른 화면은 LodItems.focus(이름) 으로 연다.
 */
(function () {
  "use strict";
  var data = window.LOD_ITEMS;
  if (!data) { return; }

  var STORE = "lod.item.korean.v1";
  // 서클 — scripts/lib/_npcs.py CIRCLES 와 같은 레벨 구간.
  var CIRCLES = [[1, 1, 10], [2, 11, 40], [3, 41, 70], [4, 71, 98], [5, 99, 99]];
  var SHEET = {
    "같음": ["어둠템과 같은 수치", ""],
    "없음": ["어둠템에 없는 물건 — 5.99 팩 수치 그대로", ""],
    "어긋남": ["어둠템과 수치가 어긋남", "is-warn"],
  };

  var typed = {};
  try { typed = JSON.parse(localStorage.getItem(STORE) || "{}"); } catch (e) { typed = {}; }

  // 처음부터 전부 펼치지 않는다(사용자 10-09) — 무기 · 전사(공용 포함) · 1서클로 연다. 「모든 …」은 고르기 상자 끝에.
  var slot = data.슬롯[0];
  var cls = "전사";
  var circle = 1;
  var query = "";
  var onlyUnnamed = false;
  var selected = null;
  var rows = new Map();
  var loading = false;
  var phone = window.matchMedia ? window.matchMedia("(max-width: 960px)") : { matches: false };

  function $(id) { return document.getElementById(id); }
  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) { node.className = className; }
    if (text != null) { node.textContent = text; }
    return node;
  }
  function number(value) { return Number(value || 0).toLocaleString("ko-KR"); }
  function percent(value) { return (value * 100).toFixed(value < 0.01 ? 2 : 1) + "%"; }

  function nameOf(row) { return (typed[row.en] || row.ko || "").trim(); }
  function circleOf(row) {
    if (!row.lv) { return 0; }
    var hit = CIRCLES.filter(function (c) { return row.lv >= c[1] && row.lv <= c[2]; })[0];
    return hit ? hit[0] : 0;
  }

  function remember(en, value) {
    if (value) { typed[en] = value; } else { delete typed[en]; }
    try { localStorage.setItem(STORE, JSON.stringify(typed)); } catch (e) { /* 사생활 모드 */ }
    var change = {};
    change[en] = value || null;
    push(change);
  }

  function toast(message) {
    if (window.LodDashboard) { window.LodDashboard.toast(message); }
  }

  // 관리 페이지(클라우드)에서는 서버에도 둔다 — 기기를 바꿔도 남고 서버가 바뀐 기록을 쌓는다(백업).
  // 파일로 열었을 때(file://)는 서버가 없으니 이 브라우저에만 남는다.
  // 바꾼 칸만 보낸다 — 통째로 보내면 다른 기기가 그사이 고친 다른 이름을 지운다. 실패는 알리고 다음 입력 때 다시 보낸다.
  var SERVER = "/api/state/item-names";
  var serverReady = false;
  var pushTimer = null;
  var pending = {};
  function push(changes) {
    if (!serverReady) { return; }
    Object.keys(changes).forEach(function (en) { pending[en] = changes[en]; });
    clearTimeout(pushTimer);
    pushTimer = setTimeout(function () {
      var sending = pending;
      pending = {};
      fetch(SERVER, { method: "PUT", headers: { "Content-Type": "application/json" },
                      body: JSON.stringify({ changes: sending }) }).then(function (response) {
        if (response.ok) { return; }
        return response.json().catch(function () { return {}; }).then(function (body) {
          throw new Error(response.status === 401 ? "로그인해야 고칠 수 있습니다. 오른쪽 위 「로그인」."
            : (body.error || "저장하지 못했습니다."));
        });
      }).catch(function (error) {
        Object.keys(sending).forEach(function (en) { if (!(en in pending)) { pending[en] = sending[en]; } });
        toast("한글 이름을 서버에 저장하지 못했습니다 — " + error.message);
      });
    }, 600);
  }
  if (location.protocol !== "file:") {
    fetch(SERVER, { cache: "no-store" }).then(function (response) {
      if (!response.ok) { throw new Error("no server"); }
      return response.json();
    }).then(function (saved) {
      // 서버 값이 기준이다(다른 기기에서 지운 이름이 되살아나지 않게). 서버가 아직 비어 있을 때만
      // 이 브라우저에 모아 둔 것을 처음 한 번 올린다.
      var first = !Object.keys(saved).length && Object.keys(typed).length;
      if (!first) { typed = saved; }
      try { localStorage.setItem(STORE, JSON.stringify(typed)); } catch (e) { /* 사생활 모드 */ }
      serverReady = true;
      if (first) { push(typed); }
      if ($("item-grid")) { render(); }
    }).catch(function () { /* 서버 없음 — 브라우저에만 */ });
  }

  function matches(row) {
    if (slot !== "all" && row.slot !== slot) { return false; }
    // 직업을 고르면 그 직업이 쓸 수 있는 것 — 공용도 함께(「공용만」은 공용만).
    if (cls !== "all" && row.cls !== cls && !(cls !== "공용" && row.cls === "공용")) { return false; }
    if (circle && circleOf(row) !== circle) { return false; }
    if (onlyUnnamed && nameOf(row)) { return false; }
    if (!query) { return true; }
    return (row.en + " " + (row.ko || "") + " " + (typed[row.en] || "")).toLowerCase()
      .indexOf(query) >= 0;
  }

  function visible() { return data.목록.filter(matches); }

  /* ── 떨구는 곳·파는 곳(atlas-data.js) ─────────────────────────────── */
  function atlas() { return window.LOD_ATLAS || null; }
  function about(row) { var a = atlas(); return a ? (a.아이템[row.en] || a.아이템[row.ko] || null) : null; }

  // 지도가 먼저 불렀으면 그 줄을 같이 기다린다(1MB 를 두 번 받지 않게).
  function need() {
    if (atlas() || loading) { return; }
    loading = true;
    var script = document.querySelector('script[src="atlas-data.js"]');
    if (!script) {
      script = document.createElement("script");
      script.src = "atlas-data.js";
      document.head.appendChild(script);
    }
    script.addEventListener("load", function () { if (selected) { describe(selected); } });
    script.addEventListener("error", function () {
      loading = false;
      script.remove();
      $("item-verdict").textContent = "떨구는 곳·파는 곳 자료(atlas-data.js)를 불러오지 못했습니다 — 새로고침해 보세요.";
    });
  }

  function icon(ic, big) {
    var item = el("i", "item-icon" + (big ? " is-big" : ""));
    if (ic >= 0) {
      item.style.backgroundPosition = "-" + (ic * data.아이콘.너비 * (big ? 2 : 1)) + "px 0";
    } else {
      item.classList.add("is-blank");
    }
    item.setAttribute("aria-hidden", "true");
    return item;
  }

  function link(parts, onClick) {
    var li = el("li");
    var button = el("button", "cdx-link");
    button.type = "button";
    button.appendChild(parts.art || el("i"));
    var copy = el("span", "cdx-link-copy");
    copy.appendChild(el("b", "", parts.name));
    copy.appendChild(el("small", "", parts.note));
    button.appendChild(copy);
    if (parts.rate != null) {
      var meter = el("span", "cdx-odds");
      meter.appendChild(el("b", "", percent(parts.rate)));
      var bar = el("i");
      // 50% 를 가득으로 — 드랍은 대개 몇 % 라 100% 기준이면 막대가 안 보인다.
      bar.style.setProperty("--fill", Math.min(100, parts.rate * 200) + "%");
      meter.appendChild(bar);
      button.appendChild(meter);
    }
    if (onClick) { button.addEventListener("click", onClick); } else { button.disabled = true; }
    li.appendChild(button);
    return li;
  }

  /** 떨구는 괴물의 그림 — 괴물 도감 자료에 있으면 서 있는 첫 칸. */
  var MONSTER_ART = new Map();
  ((window.LOD_MONSTERS && window.LOD_MONSTERS.괴물) || []).forEach(function (m) {
    MONSTER_ART.set(m.근거.slice(m.근거.lastIndexOf("/") + 1).replace(/\.json$/, ""), m.스프라이트);
  });
  function monsterThumb(key) {
    var art = MONSTER_ART.get(key);
    var box = el("i", "cdx-thumb");
    if (!art) { return box; }
    var w = Math.round(art.너비 / art.칸), h = art.높이, scale = Math.min(1, 40 / Math.max(w, h));
    box.style.width = Math.round(w * scale) + "px";
    box.style.height = Math.round(h * scale) + "px";
    box.style.backgroundImage = "url(ui/assets/creature/" + art.이름 + ".png)";
    box.style.backgroundSize = Math.round(art.너비 * scale) + "px " + Math.round(h * scale) + "px";
    return box;
  }

  function goMap(mapId, label) {
    return function () {
      if (window.LodAtlas && window.LodAtlas.focus(mapId)) { return; }
      toast("이 맵은 지도에 아직 없습니다 — " + label);
    };
  }

  function droppers(a) {
    var host = $("item-droppers");
    host.replaceChildren();
    var list = a ? a.droppedBy.slice().sort(function (x, y) { return y[1] - x[1]; }) : [];
    $("item-drop-count").textContent = a && list.length ? list.length + "곳 · 누르면 그 괴물로" : "";
    if (!a) { return; }
    if (!list.length) { host.appendChild(el("li", "cdx-empty", "떨구는 괴물이 없습니다")); return; }
    var maps = atlas().맵, mobs = atlas().괴물;
    list.slice(0, 15).forEach(function (pair) {
      var key = pair[0], mob = mobs[key] || {}, map = maps[mob.map] || {};
      var place = map.name || key.slice(key.indexOf("@") + 1);
      var known = MONSTER_ART.has(key);
      var go = known ? function () { window.LodMonsters.focus(key); } : (mob.map ? goMap(mob.map, place) : null);
      host.appendChild(link({ art: monsterThumb(key), name: mob.name || key.split("@")[0],
        note: place + (mob.lv ? " · Lv " + mob.lv : "") + (known ? "" : " · 지도로"), rate: pair[1] }, go));
    });
    if (list.length > 15) { host.appendChild(el("li", "cdx-more", "그 밖 " + (list.length - 15) + "곳")); }
  }

  function sellers(a) {
    var host = $("item-sellers");
    host.replaceChildren();
    var list = a ? a.soldBy : [];
    $("item-sell-count").textContent = a && list.length ? list.length + "곳 · 누르면 지도의 그 맵" : "";
    if (!a) { return; }
    if (!list.length) { host.appendChild(el("li", "cdx-empty", "파는 상점이 없습니다")); return; }
    var npcs = atlas().NPC, maps = atlas().맵;
    list.forEach(function (key) {
      var npc = npcs[key] || {}, map = maps[npc.map] || {};
      var place = map.name || key.slice(key.indexOf("@") + 1);
      host.appendChild(link({ art: el("i"), name: npc.name || key.split("@")[0], note: place + (npc.role ? " · " + npc.role : "") },
        npc.map ? goMap(npc.map, place) : null));
    });
  }

  function fact(host, term, value) {
    if (value === "" || value == null) { return; }
    var cell = el("div");
    cell.appendChild(el("dt", "", term));
    cell.appendChild(el("dd", "", String(value)));
    host.appendChild(cell);
  }

  function describe(row) {
    var a = about(row);
    var big = $("item-big-icon");
    // 띠를 정확히 두 배로 — 높이만 72 로 맞추면(37→72) 칸이 조금씩 밀려 뒤 번호는 딴 그림이 된다.
    big.style.backgroundSize = (data.아이콘.칸수 * data.아이콘.너비 * 2) + "px " + (data.아이콘.높이 * 2) + "px";
    big.style.backgroundPosition = row.ic >= 0 ? "-" + (row.ic * data.아이콘.너비 * 2) + "px 0" : "";
    if (row.ic >= 0) { big.classList.remove("is-blank"); } else { big.classList.add("is-blank"); }
    $("item-title").textContent = nameOf(row) || row.en;
    $("item-sub").textContent = nameOf(row) && nameOf(row) !== row.en ? row.en : "";

    var tags = $("item-tags");
    tags.replaceChildren();
    [row.slot, row.cls + (row.sex ? " · " + row.sex + "전용" : ""), row.lv ? "Lv " + row.lv : "레벨 제한 없음",
      circleOf(row) ? circleOf(row) + "서클" : ""].forEach(function (label) {
      if (label) { tags.appendChild(el("span", "cdx-tag", label)); }
    });

    var input = $("item-name");
    // 이 물건 이름을 적는 중이면 덮지 않는다 — 1MB 자료·서버 이름·로그인 확인이 늦게 와도 describe 가 다시 불린다.
    if (document.activeElement !== input || input.getAttribute("data-en") !== row.en) {
      input.value = typed[row.en] || row.ko || "";
      input.setAttribute("data-en", row.en);
    }
    input.readOnly = window.LOD_SIGNED_IN === false;  // 보기는 누구나, 고치기는 로그인한 사람(session.js)
    input.title = input.readOnly ? "로그인해야 고칠 수 있습니다" : "";

    var verdict = $("item-verdict");
    verdict.replaceChildren();
    if (!atlas()) {
      verdict.textContent = "떨구는 괴물·파는 곳·어둠템 판정을 불러오는 중…";
    } else if (!a) {
      verdict.textContent = "게임 볼트에 기록이 없는 물건입니다 — 떨구는 괴물·파는 곳을 모릅니다.";
    } else {
      var sheet = SHEET[a.sheet];
      if (sheet) { verdict.appendChild(el("span", sheet[1], sheet[0])); verdict.append(" · "); }
      var best = a.droppedBy.reduce(function (most, pair) { return Math.max(most, pair[1]); }, 0);
      verdict.append(a.droppedBy.length ? "괴물 " + a.droppedBy.length + "곳이 떨굼(가장 높은 곳 " : "떨구는 괴물 없음");
      if (a.droppedBy.length) { verdict.appendChild(el("strong", "", percent(best))); verdict.append(")"); }
      verdict.append(" · " + (a.soldBy.length ? "상점 " + a.soldBy.length + "곳" : "파는 곳 없음"));
    }

    var facts = $("item-facts");
    facts.replaceChildren();
    fact(facts, "요구 레벨", row.lv || "제한 없음");
    fact(facts, "가치", a && a.value ? number(a.value) : row.val ? number(row.val) : "");
    fact(facts, "내구도", row.dur ? number(row.dur) : "");
    fact(facts, "대표 수치", row.head || "");
    // 수치는 지금 서버 템플릿(게임 볼트 — 어둠템을 맞춘 뒤) 쪽을 먼저, 없으면 도감 표.
    if (a && Object.keys(a.stats).length) {
      // 공격력은 「1~2」 글자, 나머지는 더하고 빼는 수다.
      Object.keys(a.stats).forEach(function (name) {
        var value = a.stats[name];
        fact(facts, name, typeof value === "number" ? (value > 0 ? "+" : "") + number(value) : value);
      });
    } else {
      row.stats.forEach(function (pair) { fact(facts, pair[0], pair[1]); });
    }
    fact(facts, "이름 출처", row.src || "");
    droppers(a);
    sellers(a);
  }

  /* ── 목록 ──────────────────────────────────────────────────────────── */
  function item(row) {
    var li = el("li");
    var button = el("button", "cdx-row");
    button.type = "button";
    button.appendChild(icon(row.ic, false));
    var copy = el("span", "cdx-row-copy");
    copy.appendChild(el("b", "", nameOf(row) || row.en));
    copy.appendChild(el("small", "", [row.slot, row.lv ? "Lv " + row.lv : "", row.cls !== "공용" ? row.cls : ""].filter(Boolean).join(" · ")));
    button.appendChild(copy);
    button.appendChild(el("em", nameOf(row) ? "" : "is-warn", nameOf(row) ? (row.head || "") : "한글 없음"));
    button.setAttribute("aria-current", row === selected ? "true" : "false");
    button.addEventListener("click", function () { open(row, true); });
    li.appendChild(button);
    rows.set(row, button);
    return li;
  }

  /** 고르기 상자 — 처음 한 번 채우고, 그다음엔 고른 값만 맞춘다. */
  function fillSelect(host, options, current) {
    if (!host.children.length) {
      options.forEach(function (pair) { var option = el("option", "", pair[1]); option.value = String(pair[0]); host.appendChild(option); });
    }
    host.value = String(current);
  }

  function tally() {
    var named = data.목록.filter(function (r) { return nameOf(r); }).length;
    $("item-total").textContent = data.총.toLocaleString("ko-KR");
    $("item-named").textContent = named.toLocaleString("ko-KR")
      + " (" + Math.round((named / data.총) * 100) + "%)";
    $("item-unnamed").textContent = (data.총 - named).toLocaleString("ko-KR");
  }

  function render() {
    var shown = visible();
    rows = new Map();
    var grid = $("item-grid");
    grid.replaceChildren.apply(grid, shown.map(item));
    $("item-empty").hidden = shown.length !== 0;

    tally();
    $("item-shown").textContent = shown.length.toLocaleString("ko-KR");

    fillSelect($("item-slots"), data.슬롯.map(function (v) { return [v, v]; }).concat([["all", "모든 슬롯"]]), slot);
    fillSelect($("item-classes"), data.직업.filter(function (v) { return v !== "공용"; })
      .map(function (v) { return [v, v + " (공용 포함)"]; }).concat([["공용", "공용만"], ["all", "모든 직업"]]), cls);
    fillSelect($("item-circles"), CIRCLES.map(function (c) { return [c[0], c[0] + "서클 (Lv " + c[1] + (c[2] > c[1] ? "~" + c[2] : "") + ")"]; })
      .concat([[0, "모든 서클"]]), circle);

    // 넓은 화면은 상세가 늘 보이므로 첫 줄을 고른다. 고른 것이 걸러져 사라졌어도 바꾼다. 폰은 누를 때만 연다.
    if (selected && shown.indexOf(selected) >= 0) { describe(selected); }
    else if (!phone.matches && shown.length) { open(shown[0], false); }
  }

  function open(row, fromUser) {
    selected = row;
    rows.forEach(function (button, other) { button.setAttribute("aria-current", other === row ? "true" : "false"); });
    describe(row);
    var detail = $("item-detail");
    detail.scrollTop = 0;
    if (phone.matches && fromUser) {
      detail.classList.add("is-open");
      document.body.classList.add("codex-open");
      $("item-back").focus();
    }
  }

  function closeSheet() {
    $("item-detail").classList.remove("is-open");
    document.body.classList.remove("codex-open");
    var returning = selected && rows.get(selected);
    if (returning) { returning.focus(); }
  }

  /** 다른 화면에서 이 물건을 연다 — 한글·영문 이름 어느 쪽으로도. 거르기에 걸리면 지운다. */
  function focus(name) {
    var row = data.목록.filter(function (r) { return r.en === name || r.ko === name || typed[r.en] === name; })[0];
    if (!row) { return false; }
    if (window.LodDashboard) { window.LodDashboard.show("items"); }
    need();
    if (!matches(row)) {
      // 그 물건이 보이는 갈래로 옮긴다 — 「전체」로 풀면 목록이 다시 길어진다.
      slot = row.slot; circle = circleOf(row);
      if (row.cls !== "공용") { cls = row.cls; }
      onlyUnnamed = false; query = "";
      $("item-search").value = "";
      $("item-only-unnamed").classList.remove("is-active");
      $("item-only-unnamed").setAttribute("aria-pressed", "false");
    }
    selected = row;
    render();
    open(row, true);
    var button = rows.get(row);
    // 목록 칸 안에서만 굴린다 — scrollIntoView 는 창까지 밀어 머리줄이 올라간다. 폰은 상세가 덮으므로 그대로.
    if (button && !phone.matches) {
      var box = button.parentNode.parentNode.parentNode, at = button.getBoundingClientRect(), frame = box.getBoundingClientRect();
      box.scrollTop += at.top - frame.top - frame.height / 2;
    }
    return true;
  }
  window.LodItems = { focus: focus };

  /* ── 표로 내보내기 ─────────────────────────────────────────────────── */
  function exportTsv() {
    var box = $("item-export-box");
    var lines = ["영문\t한글"];
    data.목록.forEach(function (row) {
      var value = typed[row.en];
      if (value) { lines.push(row.en + "\t" + value); }
    });
    box.replaceChildren();
    if (lines.length === 1) {
      box.appendChild(el("p", "notice", "이 브라우저에서 고친 이름이 아직 없어요. 오른쪽 「한글 이름」 칸에 적어 보세요."));
    } else {
      box.appendChild(el("p", "", "아래를 복사해 data/아이템-한글이름.tsv 에 붙여 넣으세요 ("
        + (lines.length - 1) + "줄). 반영: python3 scripts/gen/pack/compare-packs.py && python3 scripts/gen/items/build-item-page-data.py"));
      box.appendChild(el("pre", "item-export", lines.join("\n")));
    }
    box.hidden = false;
  }

  /* ── 배선 ──────────────────────────────────────────────────────────── */
  function boot() {
    if (!$("item-grid")) { return; }

    $("item-search").addEventListener("input", function (event) {
      query = event.target.value.trim().toLowerCase();
      render();
    });
    $("item-only-unnamed").addEventListener("click", function (event) {
      onlyUnnamed = !onlyUnnamed;
      event.currentTarget.classList.toggle("is-active", onlyUnnamed);
      event.currentTarget.setAttribute("aria-pressed", onlyUnnamed ? "true" : "false");
      render();
    });
    $("item-export").addEventListener("click", exportTsv);
    $("item-slots").addEventListener("change", function (event) { slot = event.target.value; render(); });
    $("item-classes").addEventListener("change", function (event) { cls = event.target.value; render(); });
    $("item-circles").addEventListener("change", function (event) { circle = Number(event.target.value); render(); });
    $("item-name").addEventListener("change", function () {
      if (!selected) { return; }
      remember(selected.en, $("item-name").value.trim());
      // 그 줄과 셈만 고친다 — 목록을 통째로 새로 그리면 막 누른 다른 줄이 사라져 첫 누름이 헛돈다.
      var button = rows.get(selected);
      if (button && button.parentNode) { button.parentNode.replaceChildren(item(selected)); }
      tally();
      $("item-title").textContent = nameOf(selected) || selected.en;
    });
    $("item-back").addEventListener("click", closeSheet);
    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape" && selected && phone.matches) { closeSheet(); }
    });
    if (window.LodDashboard && window.LodDashboard.onViewShown) {
      // 다른 화면으로 가면(폰 뒤로 가기 포함) 상세 판과 스크롤 잠금을 푼다 — 상세는 방문 기록에 안 남는다.
      window.LodDashboard.onViewShown(function (view) {
        if (view === "items") { need(); return; }
        $("item-detail").classList.remove("is-open");
        document.body.classList.remove("codex-open");
      });
    }
    var section = document.querySelector && document.querySelector('[data-view="items"]');
    if (section && !section.hidden) { need(); }
    render();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
  // 로그인 여부가 늦게 오면 이름 칸의 잠금을 다시 맞춘다.
  document.addEventListener("lod-session", function () { if (selected) { describe(selected); } });
})();
