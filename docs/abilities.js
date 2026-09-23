/* 기술·마법 도감 — 직업·갈래로 좁혀 보고, 가리키면 샌드백에 연출을 터뜨린다.
 *
 * 613개를 선행 사슬 트리로 보면 "무엇이 있는지" 가 안 보인다. 아이템 도감과 같은 카드 격자로
 * 바꾸고, 사슬은 카드 안에 「선행」한 줄로 남긴다.
 *
 * 무대에는 **둘**이 선다. 시전자와, 보는 쪽으로 한 칸 떨어진 원작 샌드백(MNS154)이다. 간격은
 * 원작 타일 그대로 (28, 13) 이라 「붙어 서면 이만큼」이 눈에 보인다.
 *
 * `effect @target` 만 그림으로 그린다. **`motion` 은 몸동작 번호이지 연출 그림이 아니다** —
 * `skill.tbl` 이 `번호 - 128` 로 (직업 파일, 시작칸, 칸수) 를 정한다. 같은 번호의 efct 파일이 따로
 * 있어서 한동안 그것을 시전자 위에 그렸는데, 크래셔 시전자에게 엉뚱한 불꽃이 얹혔다. 번호로만
 * 적고, 몸동작 그림을 한 줄 시트로 뽑으면 그때 그린다.
 *
 * 연출은 원작 `efct###.png`(프레임이 한 줄) 를 `steps()` 로 넘기고, **크기를 건드리지 않는다.**
 * 한 칸이 곧 원작 바탕이고, 그 바탕의 기준점이 대상의 발밑에 놓인다 — 그래서 일음지는 머리 위,
 * 발차기는 몸통에 뜬다. 예전에는 조각을 96px 높이로 늘려 샌드백 한가운데에 얹어서, 작은 연출일수록
 * 크게 부풀어 몸통을 덮었다(사용자, 2026-09-19).
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
  // 기본은 **모션·이펙트·소리가 다 나가는 것**으로 연다 — 613장을 다 펼치면 지금 실제로 보이는 것이
  // 묻히고, 「스크립트 있음」만으로 거르면 눌러도 아무 일 없는 것이 섞인다(사용자, 2026-09-19).
  // 더 보려면 칩을 옮긴다.
  var cls = "all", kind = "all", tier = "all", built = "다 나감", query = "";
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

  /**
   * **게임이 실제로 보내는 번호를 쓴다** — 노바온라인 팩 표(`row["모션"]`·`row["이펙트"]`·`row["소리"]`)가
   * 아니다. 둘은 딴판이다: 프라보는 팩 표가 43·33 인데 서버는 257 을 쏘고, 쿠로는 21 이 아니라 267,
   * 데프레코는 18·33 이 아니라 243 이다(사용자, 2026-09-19 — "직자 스팰 관련 이펙트가 제대로 된거
   * 같지 않은데?"). 팩 표는 카드 아래에 참고로만 남긴다.
   */
  function game(row, channel) { return (row["게임"] || {})[channel] || []; }

  /** 시전자가 하는 것. 캐릭터 위에 얹힌다. 그림이 있는 것만. */
  function motionsOf(row) {
    return game(row, "몸동작").filter(function (n) { return BODY["동작"][n]; });
  }

  /** 맞는 쪽에 걸리는 것. 샌드백 위에 얹힌다. 그림이 있는 것만. */
  function shotsOf(row) {
    return game(row, "이펙트").filter(function (n) { return SHOTS["연출"][n]; });
  }

  function anyOf(row) { return motionsOf(row).concat(shotsOf(row)); }

  function remember(name, value) {
    if (value) { typed[name] = value; } else { delete typed[name]; }
    try { localStorage.setItem(STORE, JSON.stringify(typed)); } catch (e) { /* 사생활 모드 */ }
  }

  /** 게임이 실제로 보내는 채널 수. 0~3. */
  function channels(row) {
    return (game(row, "몸동작").length ? 1 : 0) + (game(row, "이펙트").length ? 1 : 0)
         + (game(row, "소리").length ? 1 : 0);
  }

  /**
   * **게임이 이 기술로 실제로 무엇을 보내나.** 위의 「연출」은 노바온라인 팩의 표를 한글 이름으로
   * 찾은 것이고, 이것은 우리 서버의 템플릿·스크립트가 보내는 것이다. 둘은 다르다 — 일음지는 팩 표가
   * 42 인데 서버는 276 을 보내고, **발경은 `TargetAnimation` 이 0 이라 이펙트가 아예 안 나간다.**
   * 「스크립트 있음」만 보고 「구현됐다」고 적으면 눌러도 아무 일이 없는 것이 구현으로 읽힌다
   * (사용자, 2026-09-19).
   */
  function sends(row) { return channels(row) > 0; }

  /** 모션·이펙트·소리가 다 나가는 것. 화면의 기본이 이것이다. */
  function whole(row) { return !!row["구현"] && channels(row) === 3; }

  /** 스크립트는 있는데 게임이 아무것도 안 보내는 것. */
  function silent(row) { return !!row["구현"] && !sends(row); }

  function matches(row) {
    // `Assail`(평타)처럼 여러 직업에 걸린 기술은 직업마다 한 칸씩 있다. 직업을 안 고른 동안은
    // 대표 자리 하나만 보여 준다 — 같은 카드를 다섯 번 볼 까닭이 없다(사용자, 2026-09-19).
    if (cls === "all" && row["대표"] === false) { return false; }
    if (cls !== "all" && row["직업"] !== cls) { return false; }
    if (kind !== "all" && row["갈래"] !== kind) { return false; }
    if (tier !== "all" && ("" + row["차수"] + "차") !== tier) { return false; }
    if (built === "다 나감" && !whole(row)) { return false; }
    if (built === "구현" && !row["구현"]) { return false; }
    if (built === "안 나감" && !silent(row)) { return false; }
    if (built === "미구현" && row["구현"]) { return false; }
    if (onlyUnnamed && nameOf(row)) { return false; }
    if (onlyPlayable && !anyOf(row).length) { return false; }
    if (!query) { return true; }
    return (row["이름"] + " " + (row["한글"] || "") + " " + (typed[row["이름"]] || ""))
      .toLowerCase().indexOf(query) >= 0;
  }

  /* ── 샌드백 무대 ───────────────────────────────────────────────────── */
  /*
   * 원작 자 그대로 세운다. 그래야 「이게 실제로 어떻게 보이나」를 볼 수 있다.
   *
   * 예전에는 시전자와 샌드백을 flex 로 양쪽에 벌려 놓고, 연출은 그림 조각을 96px 높이로 늘려
   * 샌드백 한가운데에 얹었다. 그래서 일음지 같은 작은 반짝임이 몸통 전체를 덮었다(사용자,
   * 2026-09-19). 원작은 그렇게 그리지 않는다 — 연출마다 제 바탕과 기준점이 있고, 기준점이
   * 대상의 발밑에 놓인다. 일음지(efct042)는 111x85 바탕의 (48,10) 에 놓인 13x13 반짝임이라
   * 발밑에서 48~60px 위, 곧 **머리 위**에 뜬다. 발차기(efct069)는 낮게 그려져 몸통에 뜬다.
   * 빗나감 표시도 같은 자리, 머리 위다.
   *
   * 바탕·기준점은 `scripts/build-ability-sprites.py` 가 `efct###.epf` 의 화폭과 `efct###.tbl`
   * 에서 뽑아 적어 둔다(근거: docs/disassembly.md 4.51 `0x44ba04`).
   */
  var stage = null, timer = null;

  /** 한 칸 옮기면 화면으로 이만큼이다. 타일이 56x27 이므로 그 반이다 — `Art/IsometricFloor.cs`. */
  var STEP = { x: 28, y: 13 };

  /** 몸동작 그림이 없는 직업은 원작 걷기 시트의 앞모습 한 칸으로 그냥 서 있는다 (470x83 에 열 칸). */
  var IDLE = { wide: 47, tall: 83, floor: 83, file: "ui/assets/hero-walk.png", frame: 6 };

  /** 샌드백 — 원작 괴물 그림 MNS154 다. 5.99·노바 팩의 연습장이 세우는 「샌드백1」이 이 번호다. */
  var BAG = { wide: 56, tall: 56, frames: 16, file: "ui/assets/stage/sandbag.png" };

  /**
   * 무대는 **원래 크기 그대로, 카드마다 같다.** 원작 자 1배이고 창을 키우지 않는다.
   * 한때 카드마다 딱 맞게 키웠더니 가리킬 때마다 창이 커졌다 작아졌다 했고, 다음엔 제일 큰 연출에
   * 맞춰 고정했더니 창이 두 배로 커졌다(사용자, 2026-09-19). 창은 예전 크기로 두고, 바탕이 그보다
   * 큰 연출은 넘치는 만큼 잘린다 — 잘려도 **자리와 크기는 원작 그대로**다.
   */
  var FLOOR = { wide: 228, tall: 150, feetX: 126, feetY: 128 };

  function stopStage() {
    if (timer) { window.clearTimeout(timer); timer = null; }
    if (stage) { stage.hidden = true; stage.setAttribute("aria-hidden", "true"); }
  }

  /**
   * 시전자의 몸동작. 표는 `scripts/build-body-motions.py` 가 원작 `skill.tbl` 에서 만든다 —
   * `motion - 128` 이 그 표의 NO 이고 (직업 파일, 시작칸, 칸수) 를 준다. 한 동작은 등 구간 다음에
   * 앞 구간이 이어지고, 화면에는 앞 구간만 쓴다.
   *
   * 하데스도 같은 번호를 보낸다 — `Skills/DoublePunch.cs` 가 무도가일 때 `0x84`(132) 다.
   */
  var BODY = window.LOD_BODY_MOTIONS || { 동작: {}, 칸: 80, 높이: 88 };

  /** 이 기술이 시키는 몸동작 중 그림이 있는 첫 번째. 없으면 null. */
  function bodyOf(row) { return motionsOf(row)[0] || null; }

  /** 발밑을 기준으로 한 이 연출의 네모. `[왼쪽, 위, 너비, 높이]`. */
  function shotBox(number) {
    var info = SHOTS["연출"][number];
    if (!info || !info["바탕"] || !info["기준"]) { return null; }
    return [-info["기준"][0], -info["기준"][1], info["바탕"][0], info["바탕"][1]];
  }

  /**
   * 한 연출을 대상의 발밑에 맞춰 세운다. 크기는 바탕 그대로다 — 늘리지 않는다.
   *
   * 프레임은 한 줄로 늘어서 있고 한 칸이 곧 바탕이므로, 시트 너비는 `바탕 x 프레임수` 다.
   * 예전에는 그림이 실린 뒤 자연 크기를 재서 96px 높이에 맞췄는데, 그러면 연출마다 배율이
   * 제각각이 되고 작은 것일수록 크게 부풀었다.
   */
  function shotOn(place, number, delay, at) {
    var info = SHOTS["연출"][number];
    var box = shotBox(number);
    if (!info || !box) { return; }

    var wide = box[2], tall = box[3], frames = info["프레임"];
    var shot = el("i", "shot");
    shot.style.left = (at.x + box[0]) + "px";
    shot.style.top = (at.y + box[1]) + "px";
    shot.style.width = wide + "px";
    shot.style.height = tall + "px";
    shot.style.backgroundImage = "url(ui/assets/ability-effects/" + info["파일"] + ")";
    shot.style.backgroundSize = (wide * frames) + "px " + tall + "px";
    shot.style.setProperty("--frames", frames);
    shot.style.setProperty("--sheet", (-wide * frames) + "px");
    shot.style.animationDelay = delay + "s";
    shot.classList.add("playing");
    place.appendChild(shot);
  }

  /**
   * 기술이 부르는 소리. 번호가 그대로 `Legend.dat` 의 `<번호>.mp3` 다.
   *
   * **브라우저는 사람이 화면을 한 번 누르기 전까지 소리를 막는다. 가리키기는 「누름」으로 안 쳐준다.**
   * 그래서 카드를 가리키기만 하면 파일이 멀쩡해도 아무 소리도 안 난다 — 예전에는 막힌 것을 조용히
   * 넘겨서 "소리를 없앴다"로 보였다(사용자, 2026-09-19). 이제 막히면 무대에 그렇게 적는다.
   *
   * 소리를 내는 대는 **하나만** 둔다. 카드를 지날 때마다 `new Audio` 를 만들면 브라우저가 그만큼
   * 물어 두고, 앞의 소리가 겹쳐 울린다.
   */
  function soundsOf(row) {
    // 게임이 보내는 소리가 먼저다. 안 보내면 팩 표의 소리로 대신 울린다 — 둘 다 없으면 무대가
    // 조용해서 "소리를 없앴다"로 읽힌다(사용자, 2026-09-19). 무엇으로 울렸는지는 설명 줄에 적는다.
    var sent = game(row, "소리");
    return sent.length ? sent : (row["소리"] || []);
  }

  var speaker = null;

  /** 아직 브라우저가 소리를 막고 있나. 한 번이라도 울리면 꺼진다. */
  var muffled = false;

  function playSound(row) {
    var numbers = soundsOf(row);
    if (!numbers.length) { return; }

    if (!speaker) {
      speaker = new Audio();
      speaker.volume = 0.5;
    }
    speaker.src = "ui/assets/ability-sounds/" + numbers[0] + ".mp3";
    speaker.currentTime = 0;

    var played = speaker.play();
    if (played && played.then) {
      played.then(function () { muffled = false; }, function () { muffled = true; });
    }
  }

  // 첫 누름에 잠금이 풀린다. 그 뒤로 가리키기만 해도 소리가 난다 — 그때까지는 무대가 그렇게 말한다.
  ["pointerdown", "keydown"].forEach(function (name) {
    document.addEventListener(name, function () { muffled = false; }, { once: true });
  });

  function playStage(row, anchor) {
    if (!stage) { return; }
    var motions = motionsOf(row), shots = shotsOf(row);
    stage.replaceChildren();

    // 시전자는 원점, 샌드백은 보는 쪽으로 한 칸. 이 둘의 간격이 곧 게임에서 붙어 선 간격이다.
    var caster = { x: 0, y: 0 };
    var target = { x: STEP.x, y: STEP.y };
    var isJumpOver = false;

    // 이형환위(Ambush)는 상대를 뛰어넘는다
    var name = nameOf(row) || row["이름"];
    if (name === "이형환위" || row["영문"] === "Ambush") {
      isJumpOver = true;
      caster = { x: STEP.x * 2, y: STEP.y * 2 };
    }

    // 몸동작 그림이 있는 직업(지금은 무도가)은 그 칸으로, 없으면 서 있는 칸으로 선다. 칸 크기가
    // 달라 무대 너비도 달라지므로 먼저 정한다.
    var body = bodyOf(row);
    // 발은 칸 바닥이 아니라 그림 안의 **발밑 줄**에 있다(80x88 칸에서 77~85). 칸 바닥을 발밑으로
    // 치면 사람이 땅에 박혀 서고 샌드백과 바닥이 어긋난다 — 그 줄은 생성기가 재어 적어 둔다.
    var cell = body
      ? { wide: BODY["칸"], tall: BODY["높이"], floor: BODY["동작"][body]["발밑"] || BODY["높이"] }
      : IDLE;

    // 무대는 늘 같은 크기다. 맞는 쪽의 발밑이 창 안 정해진 자리에 오도록 좌표를 옮긴다.
    var left = target.x - FLOOR.feetX, top = target.y - FLOOR.feetY;

    var floor = el("div", "sandbag-floor");
    var view = el("div", "sandbag-view");
    floor.style.width = FLOOR.wide + "px";
    floor.style.height = FLOOR.tall + "px";
    view.style.width = FLOOR.wide + "px";
    view.style.height = FLOOR.tall + "px";

    /** 무대 좌표를 화면 좌표로. 발밑 기준의 자리를 왼쪽 위 기준으로 옮긴다. */
    function place(node, x, y) {
      node.style.left = (x - left) + "px";
      node.style.top = (y - top) + "px";
    }

    function shadow(at, width) {
      var mark = el("i", "stage-shadow");
      mark.style.width = width + "px";
      mark.style.height = Math.round(width * 0.36) + "px";
      place(mark, at.x - width / 2, at.y - Math.round(width * 0.18));
      floor.appendChild(mark);
    }

    shadow(caster, 34);
    shadow(target, 30);

    // 사람도 같이 움직여야 한다. 연출만 터지면 「무엇이 터졌나」는 보여도 「누가 무엇을 했나」는
    // 안 보인다. 그림이 있는 직업(지금은 무도가)만 실제 동작이 나가고, 나머지는 기본 공격 자세다.
    var hero = el("i", "hero stage-piece");
    place(hero, caster.x - cell.wide / 2, caster.y - cell.floor);
    // 뛰어넘은 직후 상대를 돌아본다 — 서버는 방향을 +2, 곧 **180°** 돌린다(MonkStrike.cs:193).
    // 반대쪽을 보려면 앞/등 그림을 바꾸고 좌우도 뒤집는다. 뒤집기만 하면 90° 돈 것으로 읽힌다
    // (docs/original-sprite-animation.md §2).
    var turned = isJumpOver;
    if (turned) { hero.style.transform = "scaleX(-1)"; }
    hero.style.width = cell.wide + "px";
    hero.style.height = cell.tall + "px";

    if (body) {
      var step = BODY["동작"][body];
      hero.classList.add("acting");
      hero.style.backgroundImage = "url(ui/assets/motion/" + (turned && step["등파일"] ? step["등파일"] : step["파일"]) + ")";
      hero.style.backgroundSize = (BODY["칸"] * step["칸수"]) + "px " + BODY["높이"] + "px";
      hero.style.setProperty("--frames", step["칸수"]);
      hero.style.setProperty("--from", "0px");
      hero.style.setProperty("--to", (-step["칸수"] * BODY["칸"]) + "px");
    } else {
      hero.style.backgroundImage = "url(" + IDLE.file + ")";
      // 걷기 시트는 등 0~4 · 앞 5~9 다 — 돌아서면 같은 자리의 등 칸을 쓴다.
      hero.style.backgroundPositionX = (-(turned ? IDLE.frame - 5 : IDLE.frame) * IDLE.wide) + "px";
    }
    floor.appendChild(hero);

    // 여기에 `efct<모션번호>` 를 그리면 안 된다. **`motion` 은 몸동작 번호이지 연출 그림이 아니다** —
    // `skill.tbl`(Legend.dat) 이 `번호 - 128` 로 (직업 파일, 시작칸, 칸수) 를 정한다.
    // 131=(무도 d, 0, 3) 발차기 · 132=(d, 6, 2) 정권 · 133=(d, 10, 4) 돌려차기.
    // 번호가 직업별로 갈리는 것이 그 증거다 — 128 은 치유마법 전부, 129·130 은 검 기술,
    // 135 는 도적 찌르기, 142 는 활. 같은 번호의 efct 파일이 따로 있는 것은 우연이다.

    // 샌드백. 서 있는 칸(0)만 쓴다 — 맞는 동작은 서버가 보내 줘야 아는 것이라 여기서는 짓지 않는다.
    var bag = el("i", "sandbag stage-piece");
    place(bag, target.x - BAG.wide / 2, target.y - BAG.tall);
    bag.style.width = BAG.wide + "px";
    bag.style.height = BAG.tall + "px";
    bag.style.backgroundImage = "url(" + BAG.file + ")";
    bag.style.backgroundSize = (BAG.wide * BAG.frames) + "px " + BAG.tall + "px";
    floor.appendChild(bag);

    shots.forEach(function (number, index) {
      shotOn(floor, number, 0.15 + index * 0.45, { x: target.x - left, y: target.y - top });
    });

    view.appendChild(floor);
    stage.appendChild(view);

    playSound(row);

    var caption = el("div", "sandbag-caption");
    caption.appendChild(el("b", "", nameOf(row) || row["이름"]));

    var said = [];
    if (motions.length) {
      said.push("몸동작 " + motions.join("·") + (body ? "" : "(아직 못 그림)"));
    }
    if (shots.length) {
      var box = shotBox(shots[0]);
      said.push("이펙트 " + shots.join("·") + (box ? " · 발밑에서 " + (-box[1]) + "px 위" : ""));
    }
    if (soundsOf(row).length) {
      said.push("소리 " + soundsOf(row).join("·")
                + (game(row, "소리").length ? "" : "(팩 표 — 서버는 안 보냄)"));
    }

    caption.appendChild(el("span", "", said.length
      ? said.join(" · ")
      : "이 기술의 연출은 아직 이어지지 않았어요"));
    if (muffled && soundsOf(row).length) {
      caption.appendChild(el("span", "sandbag-muffled",
        "소리는 화면을 한 번 누른 뒤부터 납니다 — 브라우저가 막고 있어요"));
    }
    stage.appendChild(caption);

    var box = anchor.getBoundingClientRect();
    stage.hidden = false;
    stage.setAttribute("aria-hidden", "false");
    var own = stage.getBoundingClientRect();
    var x = Math.min(box.right + 12, window.innerWidth - own.width - 12);
    var y = Math.min(box.top, window.innerHeight - own.height - 12);
    stage.style.transform = "translate(" + Math.max(12, x) + "px," + Math.max(12, y) + "px)";
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
    meta.appendChild(el("span", row["구현"] ? "ability-built" : "ability-unbuilt",
      row["구현"] ? "스크립트 있음" : "스크립트 없음"));
    // 스크립트가 있어도 서버가 아무것도 안 보내면 눌러도 화면에 아무 일이 없다. 「구현」 옆에
    // 이것을 안 적으면 화면이 거짓말을 한다.
    if (silent(row)) { meta.appendChild(el("span", "ability-silent", "게임에선 아무것도 안 나감")); }
    if (row["레벨"]) { meta.appendChild(el("span", "ability-lv", "Lv" + row["레벨"])); }
    // **연출이 비었다고 원작에 없는 것이 아니다.** 연출은 한글 이름으로만 찾으므로, 이름이
    // 안 정해진 것은 찾아보지도 못한 것이다 — 그래서 빈 이유를 함께 적는다(사용자, 2026-09-19).
    if (channels(row)) {
      meta.appendChild(el("span", "ability-play", "연출 " + channels(row) + "/3"));
    } else {
      meta.appendChild(el("span", "ability-noplay", row["연출막힘"] === "한글이름없음"
        ? "연출 못 찾음 · 한글 이름 없음" : "연출 못 찾음 · 표에 없음"));
    }
    if (row["공통"]) { meta.appendChild(el("span", "ability-shared", "공통 · " + row["직업들"].join("·"))); }
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
                 game(row, "몸동작").join(","), game(row, "이펙트").join(","),
                 game(row, "소리").join(",")].join("|");

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
    var playable = ALL.filter(function (r) { return r["대표"] && whole(r); }).length;
    $("ability-total").textContent = ALL.length.toLocaleString("ko-KR");
    $("ability-named").textContent = named + " (" + Math.round((named / ALL.length) * 100) + "%)";
    $("ability-playable").textContent = playable.toLocaleString("ko-KR");
    $("ability-built-count").textContent = ALL.filter(function (r) { return r["구현"]; }).length.toLocaleString("ko-KR");
    $("ability-shown").textContent = rows.length.toLocaleString("ko-KR");

    chips($("ability-classes"), DATA["직업"] || uniq("직업"), cls, function (v) { cls = v; page = 0; render(); });
    chips($("ability-kinds"), ["기술", "마법"], kind, function (v) { kind = v; page = 0; render(); });
    chips($("ability-stages"), ["1차", "2차"], tier, function (v) { tier = v; page = 0; render(); });
    chips($("ability-built"), ["다 나감", "구현", "안 나감", "미구현"], built,
          function (v) { built = v; page = 0; render(); });
    stopStage();
  }

  function renderOriginalMonk() {
    var host = $("ability-original-monk-table");
    var source = DATA["원작무도가"];
    if (!host || !source) { return; }
    host.replaceChildren();
    var table = el("table", "ability-source-table");
    var head = el("tr");
    ["원작표", "레벨", "비용", "재료", "배우는 곳", "현재 매칭 · 게임 연출"].forEach(function (label) {
      head.appendChild(el("th", "", label));
    });
    table.appendChild(el("thead", "", "")).appendChild(head);
    var body = el("tbody");
    source["목록"].forEach(function (row) {
      var tr = el("tr");
      tr.appendChild(el("th", "", row["이름"]));
      tr.appendChild(el("td", "", row["구간"] || (row["레벨"] ? "Lv." + row["레벨"] : "승급")));
      tr.appendChild(el("td", "", row["비용"] || "—"));
      tr.appendChild(el("td", "", row["재료"].join(" · ") || "—"));
      tr.appendChild(el("td", "", row["배우는곳"] || "—"));
      var matches = row["매칭"] || [];
      var match = matches.length ? matches.map(function (item) {
        var gameText = ["몸동작", "이펙트", "소리"].map(function (channel) {
          return channel + " " + ((item["게임"] || {})[channel] || []).join("·");
        }).join(" / ");
        return item["영문"] + " · " + gameText;
      }).join("; ") : "매칭 없음 — 연출 값 없음";
      tr.appendChild(el("td", matches.length ? "" : "source-unmatched", match));
      body.appendChild(tr);
    });
    table.appendChild(body);
    host.appendChild(table);
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
    presentationStrip();
    renderOriginalMonk();
    render();
  }

  /**
   * 연출은 세 채널이 따로 온다 — 몸동작 `0x1A` · 효과 `0x29` · 소리 `0x13`/`0x19`
   * (근거: docs/martial-artist-skill-presentation.md 1절).
   *
   * **「스크립트 있음」과 「눌러서 보인다」는 다른 말이다.** 스크립트는 템플릿이 가리키는 C# 파일이
   * 있다는 뜻일 뿐이고, 연출은 **한글 이름으로만** 찾는다. 원작에 연출 없는 기술은 없으므로
   * 빈 칸은 원작의 사실이 아니라 **우리가 못 이은 것**이다 — 그 이유까지 세어 보여 준다.
   */
  function presentationStrip() {
    var host = document.getElementById("ability-presentation");
    if (!host) { return; }
    // 여러 직업에 겹치는 기술은 대표 자리에서만 세어 둔 값이다 — 안 그러면 평타가 다섯 번 세어진다.
    var s = DATA["요약"];
    var cells = [
      { 이름: "다 나감 — 지금 보는 것", 값: s["게임연출셋다"],
        메모: "몸동작·이펙트·소리가 다 나간다. 화면은 이것부터 연다" },
      { 이름: "서로 다른 기술·마법", 값: s["서로다름"], 메모: "직업에 겹치는 것을 한 번만 세면" },
      { 이름: "스크립트 있음", 값: s["구현"], 메모: "템플릿이 가리키는 C# 파일이 있다 — 보인다는 뜻은 아니다" },
      { 이름: "몸동작 · 이펙트 · 소리",
        값: s["게임몸동작"] + " · " + s["게임이펙트"] + " · " + s["게임소리"],
        메모: "0x1A · 0x29 · 0x13/0x19 — 서버가 실제로 보내는 것을 채널마다 센 것" },
      { 이름: "스크립트는 있는데 아무것도 안 나감", 값: s["게임연출없음"],
        메모: "눌러도 화면에 아무 일이 없다 — 발경은 이펙트 번호가 0 이다" },
      { 이름: "한글 이름이 없어 못 찾음", 값: s["이름없어못찾음"],
        메모: "팩 표는 한글 이름으로만 찾는다 — 이름부터 정해야 한다" },
      { 이름: "이름은 있는데 팩 표에 없음", 값: s["표에없음"], 메모: "참고용 팩 표 256개에 그 이름이 없다" },
    ];
    host.replaceChildren();
    cells.forEach(function (cell) {
      var node = document.createElement("div");
      node.className = "tally";
      var name = document.createElement("span"); name.textContent = cell["이름"];
      var value = document.createElement("strong"); value.textContent = String(cell["값"]);
      var note = document.createElement("small"); note.textContent = cell["메모"];
      node.append(name, value, note);
      host.appendChild(node);
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
