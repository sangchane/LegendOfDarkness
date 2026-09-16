/* 기술·마법 도감 — 직업·갈래로 좁혀 보고, 가리키면 샌드백에 연출을 터뜨린다.
 *
 * 613개를 선행 사슬 트리로 보면 "무엇이 있는지" 가 안 보인다. 아이템 도감과 같은 카드 격자로
 * 바꾸고, 사슬은 카드 안에 「선행」한 줄로 남긴다.
 *
 * 무대에는 **둘**이 선다. 왼쪽이 시전자, 오른쪽이 맞는 쪽이다.
 *
 * `effect @target` 만 그림으로 그린다. **`motion` 은 몸동작 번호이지 연출 그림이 아니다** —
 * `skill.tbl` 이 `번호 - 128` 로 (직업 파일, 시작칸, 칸수) 를 정한다. 같은 번호의 efct 파일이 따로
 * 있어서 한동안 그것을 시전자 위에 그렸는데, 크래셔 시전자에게 엉뚱한 불꽃이 얹혔다. 번호로만
 * 적고, 몸동작 그림을 한 줄 시트로 뽑으면 그때 그린다.
 *
 * 연출은 원작 `efct###.png`(프레임이 한 줄) 를 `steps()` 로 넘긴다. 칸 너비가 연출마다 다르므로
 * 그림이 실린 뒤 자연 크기를 재서 픽셀로 정한다 — 퍼센트로 두면 시트가 아니라 바닥을 기준으로
 * 재어 프레임과 어긋난다.
 *
 * 소리는 원작 `Legend.dat` 의 `<번호>.mp3` 를 그대로 튼다. 사람이 아직 아무것도 누르지 않은 창에서는
 * 브라우저가 막으므로, 막히면 조용히 넘긴다.
 */
(function () {
  "use strict";
  var DATA = window.ABILITY_DATA;      // 생성기가 쓰는 이름 (`build-ability-page-data.py`)
  var SHOTS = window.LOD_ABILITY_EFFECTS || { 연출: {} };
  if (!DATA) { return; }

  var STORE = "lod.ability.korean.v1";
  var PER_PAGE = 60;
  var CELL = 35, COLS = 16;          // 아이콘 시트: 한 칸 35x35, 한 줄 16칸

  var typed = {};
  try { typed = JSON.parse(localStorage.getItem(STORE) || "{}"); } catch (e) { typed = {}; }

  // 차수는 `raw[0]` 의 둘째 값이다 (1차 257 · 2차 356). 묶기는 켜 둔 채로 시작한다 — 613장을
  // 그대로 펼치면 같은 장면이 나오는 카드가 줄줄이라 무엇이 무엇인지 보이지 않는다.
  var cls = "all", kind = "all", tier = "all", built = "all", query = "";
  var onlyUnnamed = false, onlyPlayable = false, folding = true, page = 0;

  function $(id) { return document.getElementById(id); }
  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) { node.className = className; }
    if (text != null) { node.textContent = text; }
    return node;
  }

  var ALL = [];
  DATA["묶음"].forEach(function (group) {
    group["목록"].forEach(function (row) {
      ALL.push(Object.assign({ 직업: group["직업"], 갈래: group["갈래"] }, row));
    });
  });

  function nameOf(row) { return (typed[row["이름"]] || row["한글"] || "").trim(); }
  function drawn(list) { return (list || []).filter(function (n) { return SHOTS["연출"][n]; }); }

  /** 시전자가 하는 것. 캐릭터 위에 얹힌다. */
  function motionsOf(row) { return drawn(row["모션"]); }

  /** 맞는 쪽에 걸리는 것. 샌드백 위에 얹힌다. */
  function shotsOf(row) { return drawn(row["이펙트"]); }

  function anyOf(row) { return motionsOf(row).concat(shotsOf(row)); }

  function remember(name, value) {
    if (value) { typed[name] = value; } else { delete typed[name]; }
    try { localStorage.setItem(STORE, JSON.stringify(typed)); } catch (e) { /* 사생활 모드 */ }
  }

  function matches(row) {
    if (cls !== "all" && row["직업"] !== cls) { return false; }
    if (kind !== "all" && row["갈래"] !== kind) { return false; }
    if (tier !== "all" && ("" + row["차수"] + "차") !== tier) { return false; }
    if (built !== "all" && (row["구현"] ? "구현" : "미구현") !== built) { return false; }
    if (onlyUnnamed && nameOf(row)) { return false; }
    if (onlyPlayable && !anyOf(row).length) { return false; }
    if (!query) { return true; }
    return (row["이름"] + " " + (row["한글"] || "") + " " + (typed[row["이름"]] || ""))
      .toLowerCase().indexOf(query) >= 0;
  }

  /* ── 샌드백 무대 ───────────────────────────────────────────────────── */
  var stage = null, timer = null;

  function stopStage() {
    if (timer) { window.clearTimeout(timer); timer = null; }
    if (stage) { stage.hidden = true; stage.setAttribute("aria-hidden", "true"); }
  }

  /** 맞는 쪽에 걸리는 연출의 높이. 저마다 크기가 달라 이 높이에 맞춰 줄인다. */
  var SHOT_HEIGHT = 96;

  /**
   * 시전자의 몸동작. 표는 `scripts/build-body-motions.py` 가 원작 `skill.tbl` 에서 만든다 —
   * `motion - 128` 이 그 표의 NO 이고 (직업 파일, 시작칸, 칸수) 를 준다. 한 동작은 등 구간 다음에
   * 앞 구간이 이어지고, 화면에는 앞 구간만 쓴다.
   *
   * 하데스도 같은 번호를 보낸다 — `Skills/DoublePunch.cs` 가 무도가일 때 `0x84`(132) 다.
   */
  var BODY = window.LOD_BODY_MOTIONS || { 동작: {}, 칸: 80, 높이: 88 };

  /** 이 기술이 시키는 몸동작 중 그림이 있는 첫 번째. 없으면 null. */
  function bodyOf(row) {
    var numbers = row["모션"] || [];
    for (var i = 0; i < numbers.length; i++) {
      if (BODY["동작"][numbers[i]]) { return numbers[i]; }
    }
    return null;
  }

  /**
   * 한 연출을 그 자리에 세운다. 시트는 프레임이 한 줄로 늘어서 있고 칸 너비가 연출마다 다르므로,
   * 그림이 실제로 실린 뒤 자연 크기를 재서 픽셀로 정한다.
   *
   * 예전에는 칸 너비를 `100 / 프레임수` 퍼센트로 두었는데, 그 퍼센트는 시트가 아니라 **바닥**을
   * 기준으로 재는 값이라 프레임과 아무 상관이 없었다. 게다가 시트 자체가 열여섯 칸 격자로 그려져
   * 있어 뒤쪽 칸이 전부 비어 있었다 — 그래서 화면에서는 그냥 정지된 그림으로 보였다.
   */
  function shotOn(place, number, delay, height) {
    var info = SHOTS["연출"][number];
    if (!info) { return; }

    var shot = el("i", "shot");
    var sheet = new Image();

    sheet.onload = function () {
      var frames = info["프레임"];
      var scale = height / sheet.naturalHeight;
      var wide = sheet.naturalWidth * scale;

      shot.style.width = (wide / frames) + "px";
      shot.style.height = height + "px";
      shot.style.backgroundImage = "url(" + sheet.src + ")";
      shot.style.backgroundSize = wide + "px " + height + "px";
      shot.style.setProperty("--frames", frames);
      shot.style.setProperty("--sheet", (-wide) + "px");
      shot.style.animationDelay = delay + "s";
      shot.classList.add("playing");
    };

    sheet.src = "ui/assets/ability-effects/" + info["파일"];
    place.appendChild(shot);
  }

  /**
   * 기술이 부르는 소리. 번호가 그대로 `Legend.dat` 의 `<번호>.mp3` 다.
   * 브라우저는 사람이 아직 아무것도 누르지 않은 창에서 소리를 막는다 — 막히면 조용히 넘긴다.
   */
  function playSound(row) {
    var numbers = row["소리"] || [];
    if (!numbers.length) { return; }

    var sound = new Audio("ui/assets/ability-sounds/" + numbers[0] + ".mp3");
    sound.volume = 0.5;
    var played = sound.play();
    if (played && played.catch) { played.catch(function () { /* 창이 아직 조용하다 */ }); }
  }

  function playStage(row, anchor) {
    if (!stage) { return; }
    var motions = motionsOf(row), shots = shotsOf(row);
    stage.replaceChildren();

    var floor = el("div", "sandbag-floor");

    // 왼쪽이 시전자, 오른쪽이 맞는 쪽. 모션은 시전자 위에, 이펙트는 샌드백 위에 얹는다 —
    // 둘을 한 목록으로 합쳐 두었을 때는 전부 샌드백 위에 겹쳐 터져서 무엇이 무엇인지 알 수 없었다.
    var caster = el("div", "stage-side caster");

    // 사람도 같이 움직여야 한다. 연출만 터지면 「무엇이 터졌나」는 보여도 「누가 무엇을 했나」는
    // 안 보인다. 원작 공격 시트의 앞모습 두 칸을 넘긴다.
    var hero = el("i", "hero");
    var body = bodyOf(row);

    if (body) {
      var step = BODY["동작"][body];
      hero.classList.add("acting");
      hero.style.width = BODY["칸"] + "px";
      hero.style.height = BODY["높이"] + "px";
      hero.style.backgroundImage = "url(ui/assets/motion/" + step["파일"] + ")";
      hero.style.backgroundSize = (BODY["칸"] * step["전체"]) + "px " + BODY["높이"] + "px";
      hero.style.setProperty("--frames", step["칸수"]);
      hero.style.setProperty("--from", (-step["자리"] * BODY["칸"]) + "px");
      hero.style.setProperty("--to", (-(step["자리"] + step["칸수"]) * BODY["칸"]) + "px");
    } else if (motions.length) {
      // 그림이 아직 없는 직업이다. 기본 공격 자세로 대신한다.
      hero.classList.add("striking");
    }

    caster.appendChild(hero);

    // 여기에 `efct<모션번호>` 를 그리면 안 된다. **`motion` 은 몸동작 번호이지 연출 그림이 아니다** —
    // `skill.tbl`(Legend.dat) 이 `번호 - 128` 로 (직업 파일, 시작칸, 칸수) 를 정한다.
    // 131=(무도 d, 0, 3) 발차기 · 132=(d, 6, 2) 정권 · 133=(d, 10, 4) 돌려차기.
    // 번호가 직업별로 갈리는 것이 그 증거다 — 128 은 치유마법 전부, 129·130 은 검 기술,
    // 135 는 도적 찌르기, 142 는 활. 같은 번호의 efct 파일이 따로 있는 것은 우연이고, 그것을
    // 그렸더니 크래셔 시전자 위에 엉뚱한 불꽃이 얹혔다.
    // 몸동작 그림은 아직 한 줄 시트로 뽑지 않았다(있는 것은 문서용 다단 도판뿐이다).

    var target = el("div", "stage-side target");
    target.appendChild(el("i", "sandbag"));
    shots.forEach(function (number, index) {
      shotOn(target, number, 0.15 + index * 0.45, SHOT_HEIGHT);
    });

    floor.appendChild(caster);
    floor.appendChild(target);
    stage.appendChild(floor);

    playSound(row);

    var caption = el("div", "sandbag-caption");
    caption.appendChild(el("b", "", nameOf(row) || row["이름"]));

    var said = [];
    // 그림이 있는 직업(지금은 무도가)만 실제로 움직인다. 나머지는 번호만 적어, 그린 것처럼
    // 보이지 않게 한다.
    if (motions.length) {
      said.push("몸동작 " + motions.join("·") + (bodyOf(row) ? "" : "(아직 못 그림)"));
    }
    if (shots.length) { said.push("이펙트 " + shots.join("·")); }
    if ((row["소리"] || []).length) { said.push("소리 " + row["소리"].join("·")); }

    caption.appendChild(el("span", "", said.length
      ? said.join(" · ")
      : "이 기술의 연출은 아직 이어지지 않았어요"));
    stage.appendChild(caption);

    var box = anchor.getBoundingClientRect();
    stage.hidden = false;
    stage.setAttribute("aria-hidden", "false");
    var own = stage.getBoundingClientRect();
    var left = Math.min(box.right + 12, window.innerWidth - own.width - 12);
    var top = Math.min(box.top, window.innerHeight - own.height - 12);
    stage.style.transform = "translate(" + Math.max(12, left) + "px," + Math.max(12, top) + "px)";
  }

  /* ── 카드 ──────────────────────────────────────────────────────────── */
  function card(row) {
    var article = el("article", "ability-card");
    if (!nameOf(row)) { article.classList.add("is-unnamed"); }
    if (anyOf(row).length) { article.classList.add("is-playable"); }

    var icon = el("i", "ability-icon");
    var sheet = row["갈래"] === "기술" ? "skill" : "spell";
    icon.style.backgroundImage = "url(ability-icons/" + sheet + ".png)";
    icon.style.backgroundPosition = "-" + (row["아이콘"] % COLS) * CELL + "px -" +
      Math.floor(row["아이콘"] / COLS) * CELL + "px";
    article.appendChild(icon);

    var head = el("div", "ability-card-head");
    head.appendChild(el("b", "", nameOf(row) || row["이름"]));
    if (nameOf(row)) { head.appendChild(el("em", "", row["이름"])); }
    article.appendChild(head);

    var meta = el("div", "ability-card-meta");
    meta.appendChild(el("span", "ability-kind", row["갈래"]));
    meta.appendChild(el("span", "ability-cls", row["직업"]));
    meta.appendChild(el("span", row["구현"] ? "ability-built" : "ability-unbuilt", row["구현"] ? "구현" : "미구현"));
    if (row["레벨"]) { meta.appendChild(el("span", "ability-lv", "Lv" + row["레벨"])); }
    // 모션만 있고 이펙트가 없는 기술이 있다(투핸드어택). 이펙트만 세면 그런 것은 "연출 없음"으로
    // 보이는데 실제로는 시전자가 움직인다.
    if (anyOf(row).length) { meta.appendChild(el("span", "ability-play", "연출 " + anyOf(row).length)); }
    article.appendChild(meta);

    if (row["선행"]) { article.appendChild(el("p", "ability-pre", "선행 " + row["선행"])); }

    // 묶인 것들. 아이콘도 연출도 같고 세기만 다르니 이름과 요구 레벨만 적어 준다.
    if ((row["같은것"] || []).length) {
      var same = row["같은것"].map(function (one) {
        return (nameOf(one) || one["이름"]) + (one["레벨"] ? " Lv" + one["레벨"] : "");
      });
      article.appendChild(el("p", "ability-same",
        "같은 기술 " + (same.length + 1) + "단계 — " + same.join(" · ")));
    }

    var input = el("input", "ability-name");
    input.type = "text";
    input.value = typed[row["이름"]] || row["한글"] || "";
    input.placeholder = "한글 이름";
    input.setAttribute("aria-label", row["이름"] + " 한글 이름");
    input.addEventListener("change", function () {
      remember(row["이름"], input.value.trim());
      render();
    });
    article.appendChild(input);

    article.addEventListener("mouseenter", function () { playStage(row, article); });
    article.addEventListener("mouseleave", stopStage);
    article.addEventListener("click", function (event) {
      if (event.target !== input) { playStage(row, article); }
    });
    return article;
  }

  /* ── 그리기 ────────────────────────────────────────────────────────── */
  function chips(host, values, current, onPick) {
    host.replaceChildren();
    ["all"].concat(values).forEach(function (value) {
      var button = el("button", "chip", value === "all" ? "전체" : value);
      button.type = "button";
      if (value === current) { button.classList.add("is-active"); }
      button.setAttribute("aria-pressed", value === current ? "true" : "false");
      button.addEventListener("click", function () { onPick(value); });
      host.appendChild(button);
    });
  }

  /**
   * 같은 기술의 레벨 변형을 한 장으로 묶는다.
   *
   * 613장 중 402장이 `Mass Strike 1`~`6`, `Groo 1`~`11`, `Archery 1`~`6` 처럼 **이름 끝 숫자만
   * 다른 것**이다. 아이콘도 연출도 같고 세기만 다르니, 카드를 열한 장 놓을 이유가 없다 — 한 장에
   * 모으고 나머지는 카드 안에 이름과 요구 레벨만 적는다.
   *
   * **아이콘까지 같아야 묶는다.** 아이콘이 다르면 화면에서 다른 기술로 보이고 실제로도 다른
   * 기술이다 — 쿠라노(29)·쿠라노소(30)·수페라쿠라노(31)·엑스쿠라노(77)는 연출이 하나같이 같지만
   * 아이콘이 저마다 달라 묶을 것이 아니다. 열쇠에 직업·갈래·차수·연출도 함께 넣는다.
   */
  var TAIL = /\s+\d+$/;

  /** 이름 끝의 세기 숫자를 뗀 밑말. `Mass Strike 3` → `Mass Strike`. */
  function root(name) { return (name || "").replace(TAIL, ""); }

  function fold(rows) {
    if (!folding) { return rows; }

    var out = [], where = {};
    rows.forEach(function (row) {
      var key = [row["직업"], row["갈래"], row["차수"], row["구현"], row["아이콘"], root(row["이름"]),
                 (row["모션"] || []).join(","), (row["이펙트"] || []).join(","),
                 (row["소리"] || []).join(",")].join("|");

      if (where[key] === undefined) {
        where[key] = out.length;
        out.push(Object.assign({ 같은것: [] }, row));
        return;
      }
      out[where[key]]["같은것"].push(row);
    });
    return out;
  }

  function render() {
    var rows = fold(ALL.filter(matches));
    var pages = Math.max(1, Math.ceil(rows.length / PER_PAGE));
    if (page >= pages) { page = pages - 1; }
    var slice = rows.slice(page * PER_PAGE, page * PER_PAGE + PER_PAGE);

    var grid = $("ability-grid");
    grid.replaceChildren();
    slice.forEach(function (row) { grid.appendChild(card(row)); });

    $("ability-empty").hidden = rows.length !== 0;
    $("ability-pager").hidden = pages < 2;
    $("ability-pager-label").textContent = (page + 1) + " / " + pages + " 쪽";

    var named = ALL.filter(function (r) { return nameOf(r); }).length;
    var playable = ALL.filter(function (r) { return anyOf(r).length; }).length;
    $("ability-total").textContent = ALL.length.toLocaleString("ko-KR");
    $("ability-named").textContent = named + " (" + Math.round((named / ALL.length) * 100) + "%)";
    $("ability-playable").textContent = playable.toLocaleString("ko-KR");
    $("ability-built-count").textContent = ALL.filter(function (r) { return r["구현"]; }).length.toLocaleString("ko-KR");
    $("ability-shown").textContent = rows.length.toLocaleString("ko-KR");

    chips($("ability-classes"), DATA["직업"] || uniq("직업"), cls, function (v) { cls = v; page = 0; render(); });
    chips($("ability-kinds"), ["기술", "마법"], kind, function (v) { kind = v; page = 0; render(); });
    chips($("ability-stages"), ["1차", "2차"], tier, function (v) { tier = v; page = 0; render(); });
    chips($("ability-built"), ["구현", "미구현"], built, function (v) { built = v; page = 0; render(); });
    stopStage();
  }

  function uniq(key) {
    var out = [];
    ALL.forEach(function (r) { if (out.indexOf(r[key]) < 0) { out.push(r[key]); } });
    return out;
  }

  function exportTsv() {
    var box = $("ability-export-box");
    var lines = ["영문\t한글"];
    ALL.forEach(function (row) {
      if (typed[row["이름"]]) { lines.push(row["이름"] + "\t" + typed[row["이름"]]); }
    });
    box.replaceChildren();
    if (lines.length === 1) {
      box.appendChild(el("p", "notice", "이 브라우저에서 고친 이름이 아직 없어요."));
    } else {
      box.appendChild(el("p", "", "아래를 복사해 data/기술마법-한글이름.tsv 에 붙여 넣으세요 ("
        + (lines.length - 1) + "줄). 반영: python3 scripts/build-ability-page-data.py"));
      box.appendChild(el("pre", "ability-export", lines.join("\n")));
    }
    box.hidden = false;
  }

  function boot() {
    stage = $("ability-stage");
    if (!$("ability-grid")) { return; }
    $("ability-search").addEventListener("input", function (event) {
      query = event.target.value.trim().toLowerCase(); page = 0; render();
    });
    [["ability-only-unnamed", function () { onlyUnnamed = !onlyUnnamed; return onlyUnnamed; }],
     ["ability-only-playable", function () { onlyPlayable = !onlyPlayable; return onlyPlayable; }],
     ["ability-fold", function () { folding = !folding; return folding; }]
    ].forEach(function (pair) {
      $(pair[0]).addEventListener("click", function (event) {
        var on = pair[1]();
        event.currentTarget.classList.toggle("is-active", on);
        event.currentTarget.setAttribute("aria-pressed", on ? "true" : "false");
        page = 0; render();
      });
    });
    $("ability-export").addEventListener("click", exportTsv);
    Array.prototype.forEach.call(document.querySelectorAll("[data-ability-page]"), function (button) {
      button.addEventListener("click", function () {
        page = Math.max(0, page + Number(button.getAttribute("data-ability-page")));
        render();
        $("ability-grid").scrollIntoView({ block: "start", behavior: "smooth" });
      });
    });
    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape") { stopStage(); }
    });
    render();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
