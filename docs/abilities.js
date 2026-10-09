/* 기술·마법 — 누가 쓰고 → 누구에게 → 어떤 그림이 어디에 → 어떤 소리. 무대에서 다시 보고 바로 고친다.
 * 자료: ability-operations-data.js(자리 = 대상·쓴쪽·맞는쪽) · ability-media-catalog.js · body-motions-data.js · monsters-data.js
 * API: /api/ability-overrides — 서버가 패킷 직전에 이펙트·속도·사운드만 바꾼다(AbilityPresentationOverrides.cs).
 */
(function () {
  "use strict";

  var source = window.LOD_ABILITY_OPERATIONS;
  var media = window.LOD_ABILITY_MEDIA;
  if (!source || !media) { return; }
  var motionData = window.LOD_BODY_MOTIONS || {};
  var motions = motionData["동작"] || {};

  // 원작 표에만 있는 영어 이름 미구현 기술은 싣지 않는다(사용자, 2026-10-02) — 구현된 것은 영어 이름이어도 남긴다.
  var rows = (source["목록"] || []).filter(function (row) { return row.구현 || /[가-힣]/.test(row.이름); });
  var effects = media["이펙트"] || [];
  var sounds = media["소리"] || media["사운드"] || [];
  var byKey = {}, effectByNumber = {}, effectUsers = {}, soundUsers = {};
  var overrides = {}, changedAt = {};
  // 구현 = 게임 스킬창에 보이는 것(레벨 표 + 기본공격, 사용자 2026-10-03). 나머지는 「아직 없는 것」에서 본다.
  // 처음부터 전부 펼치지 않는다(사용자 10-09) — 전사(공통 포함)로 연다. 「모든 직업」은 고르기 상자 끝에.
  var view = "구현", kind = "기술", job = "전사", query = "";
  var revision = 0, apiReady = false;
  var selected = null;
  var draft = { effect: null, speed: null, sound: null };
  var effectFilter = "추천", soundFilter = "추천";
  var slow = false, withSound = true;
  var speaker = new Audio();
  speaker.volume = 0.55;
  var probe = new Audio();
  probe.preload = "metadata";
  var reduced = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  var phone = window.matchMedia ? window.matchMedia("(max-width: 960px)") : { matches: false };

  // 무대 — 논리 크기 300×190 을 상자 폭에 맞춰 키운다. 발 위치는 이 좌표로 둔다.
  var WORLD = { w: 300, h: 190 };
  var MOTION_DIR = "ui/assets/motion/";
  var CELL = { w: motionData["칸"] || 80, h: motionData["높이"] || 88 };
  // 그림의 기준점이 맞는 이의 발에서 이만큼 떨어진 곳에 온다(EffectSheet.AnchorFromFeet — 앱과 같은 자리).
  var ANCHOR_FROM_FEET = [30 - 31.5, 72 - 83];
  // 아군 맞는 쪽 — 무대용 사람 그림. 0번 칸이 서 있는 모습(ui/assets/stage/sandbag.txt).
  var PERSON = { sheet: "ui/assets/stage/sandbag.png", w: 56, h: 56, foot: 53, frames: [0] };
  var FOE = foe();

  rows.forEach(function (row) {
    byKey[row["운영키"]] = row;
    if (!row.구현) { return; }
    (row["게임"]["이펙트"] || []).forEach(function (n) { (effectUsers[n] = effectUsers[n] || []).push(title(row)); });
    (row["게임"]["소리"] || []).forEach(function (n) { (soundUsers[n] = soundUsers[n] || []).push(title(row)); });
  });
  effects.forEach(function (item) { effectByNumber[item["번호"]] = item; });

  function $(id) { return document.getElementById(id); }
  function node(tag, className, text) {
    var item = document.createElement(tag);
    if (className) { item.className = className; }
    if (text !== undefined) { item.textContent = text; }
    return item;
  }
  function toast(message) { if (window.LodDashboard) { window.LodDashboard.toast(message); } }
  function state(message, mode) {
    var badge = $("ability-api-state");
    badge.textContent = message;
    badge.className = "ability-api-state " + (mode || "");
  }
  function effective(row, field) {
    var saved = overrides[row["운영키"]] || {};
    return Object.prototype.hasOwnProperty.call(saved, field) ? saved[field] : row["기본"][field];
  }
  function changed(row) { return !!overrides[row["운영키"]]; }
  function title(row) { return row["이름"] === "Assail" ? "기본공격" : row["이름"]; }
  function unique(list) { return list.filter(function (value, index) { return list.indexOf(value) === index; }); }
  function range(count) { var out = []; for (var i = 0; i < count; i += 1) { out.push(i); } return out; }
  function who(list) {
    if (!list || !list.length) { return ""; }
    return list.slice(0, 2).join("·") + (list.length > 2 ? " 외 " + (list.length - 2) : "");
  }
  function look(number) {
    var info = effectByNumber[number];
    return info ? info["색"] + " 빛" : "그림 없음";
  }
  function heard(number) {
    if (number === null || number === undefined) { return "없음"; }
    return soundUsers[number] ? who(soundUsers[number]) + "의 소리" : "쓰지 않던 소리";
  }
  function hasSound(number) { return number !== null && number !== undefined; }
  function pace(speed) {
    var value = Number(speed || 100);
    if (value < 92) { return (100 / value).toFixed(1).replace(/\.0$/, "") + "배 빠르게"; }
    if (value > 108) { return (value / 100).toFixed(1).replace(/\.0$/, "") + "배 느리게"; }
    return "보통 빠르기";
  }

  /** 적 맞는 쪽 — 현황판 괴물 자료에서 서 있는 고블린(첫 칸이 왼쪽을 본다). 자료가 없으면 사람으로. */
  function foe() {
    var list = ((window.LOD_MONSTERS || {})["괴물"] || []).filter(function (m) { return m["스프라이트"]; });
    var pick = list.find(function (m) { return m["이름"] === "고블린전사"; }) || list.find(function (m) { return m["스프라이트"]["높이"] < 100; });
    if (!pick) { return PERSON; }
    var art = pick["스프라이트"];
    return { sheet: "ui/assets/creature/" + art["이름"] + ".png", w: Math.round(art["너비"] / art["칸"]), h: art["높이"], foot: art["높이"] - 3, frames: [0] };
  }

  function icon(row) {
    var item = node("i", "ability-ops-icon");
    var number = Number(row["아이콘"] || 0);
    var cell = 35;
    item.style.backgroundImage = "url(ability-icons/" + (row["갈래"] === "기술" ? "skill" : "spell") + ".png)";
    item.style.backgroundPosition = (-((number % 16) * cell)) + "px " + (-Math.floor(number / 16) * cell) + "px";
    item.setAttribute("aria-hidden", "true");
    return item;
  }

  /* ── 목록 ─────────────────────────────────────────── */

  function filtered() {
    var list = rows.filter(function (row) {
      if (row["갈래"] !== kind) { return false; }
      if (view === "구현" && !row.구현) { return false; }
      if (view === "미구현" && row.구현) { return false; }
      if (view === "노바와다름" && !row["노바와다름"]) { return false; }
      if (view === "운영수정" && !changed(row)) { return false; }
      // 직업을 고르면 그 직업이 쓰는 것 — 모두가 쓰는 공통도 함께(「공통만」은 공통만).
      if (job !== "전체" && row["직업"] !== job && !(job !== "공통" && row["직업"] === "공통")) { return false; }
      if (!query) { return true; }
      return (row["이름"] + " " + row["그룹"] + " " + row["직업"]).toLowerCase().indexOf(query) >= 0;
    });
    if (view === "운영수정") {
      list.sort(function (a, b) { return String(changedAt[b["운영키"]] || "").localeCompare(String(changedAt[a["운영키"]] || "")); });
    }
    return list;
  }
  function when(key) {
    var at = changedAt[key];
    if (!at) { return ""; }
    var date = new Date(at);
    return isNaN(date) ? "" : (date.getMonth() + 1) + "/" + date.getDate() + " " + String(date.getHours()).padStart(2, "0") + ":" + String(date.getMinutes()).padStart(2, "0");
  }

  function renderJobs() {
    var host = $("ability-classes");
    [["전사", "전사 (공통 포함)"], ["도적", "도적 (공통 포함)"], ["마법사", "마법사 (공통 포함)"], ["사제", "사제 (공통 포함)"],
      ["무도가", "무도가 (공통 포함)"], ["공통", "공통만"], ["전체", "모든 직업"]].forEach(function (pair) {
      var option = node("option", "", pair[1]);
      option.value = pair[0];
      host.appendChild(option);
    });
    host.value = job;
    host.addEventListener("change", function () { job = host.value; render(); });
  }

  function item(row) {
    var li = node("li");
    var button = node("button", "abx-row" + (changed(row) ? " is-overridden" : ""));
    button.type = "button";
    button.dataset.abilityKey = row["운영키"];
    if (selected === row) { button.setAttribute("aria-current", "true"); }
    button.appendChild(icon(row));
    var copy = node("span", "abx-row-copy");
    copy.appendChild(node("b", "", title(row)));
    copy.appendChild(node("small", "", row["직업"] + " · Lv " + row["레벨"] + " · " + row["자리"]["대상"]));
    button.appendChild(copy);
    if (changed(row)) { button.appendChild(node("em", "", "바꿈" + (when(row["운영키"]) ? " " + when(row["운영키"]) : ""))); }
    else if (row["노바와다름"]) { button.appendChild(node("em", "is-nova", "노바와 다름")); }
    button.addEventListener("click", function () { open(row, true); });
    li.appendChild(button);
    return li;
  }

  function render() {
    var list = filtered();
    var host = $("ability-grid");
    host.replaceChildren();
    list.forEach(function (row) { host.appendChild(item(row)); });
    $("ability-shown").textContent = list.length;
    $("ability-empty").hidden = list.length > 0;
    $("ability-override-count").textContent = Object.keys(overrides).length ? "· 바꾼 것 " + Object.keys(overrides).length : "";
    $("ability-changed-count").textContent = rows.filter(function (row) { return row["갈래"] === kind && changed(row); }).length;
    $("ability-done-count").textContent = rows.filter(function (row) { return row["갈래"] === kind && row.구현; }).length;
    $("ability-todo-count").textContent = rows.filter(function (row) { return row["갈래"] === kind && !row.구현; }).length;
    $("ability-nova-count").textContent = rows.filter(function (row) { return row["갈래"] === kind && row["노바와다름"]; }).length;
    $("ability-empty").textContent = view === "운영수정" ? "아직 바꾼 " + kind + "이 없어요." : view === "노바와다름" ? "노바 표와 다른 " + kind + "이 없어요." : "조건에 맞는 " + kind + "이 없어요. 거르기를 지워 보세요.";
    // 넓은 화면은 무대가 늘 보이므로 비지 않게 첫 줄을 고른다. 폰은 누를 때만 연다.
    if (!phone.matches && list.length && list.indexOf(selected) < 0) { open(list[0], false); }
  }

  /* ── 무대: 쓴 사람 → 맞는 쪽, 그림을 실제 자리에 ─────── */

  // 대상마다 서는 자리(발). 「자기 자신」은 쓴 사람 혼자 가운데, 나머지는 쓴 사람 왼쪽 · 맞는 쪽 오른쪽.
  var PLACES = {
    "자기 자신": { caster: [150, 160], targets: [] },
    "주변 적 여럿": { caster: [70, 160], targets: [[206, 128], [252, 156], [200, 180]] },
    "파티 모두": { caster: [70, 160], targets: [[208, 140], [248, 176]] }
  };
  function placeOf(row) { return PLACES[aimOf(row)] || { caster: [70, 160], targets: [[226, 160]] }; }
  function aimOf(row) { return row["자리"]["대상"]; }
  function friendly(row) { return /아군|파티/.test(aimOf(row)); }

  function casterArt(row) {
    var sheet = motions[String((row["게임"]["몸동작"] || [])[0])];
    if (sheet) {
      return { sheet: MOTION_DIR + sheet["파일"], w: CELL.w, h: CELL.h, foot: sheet["발밑"], frames: range(sheet["칸수"]), moves: true };
    }
    // 동작을 안 보내는 기술은 가만히 선다 — 같은 직업이 쓰는 동작 그림의 첫 칸(직업 옷 차림).
    var same = rows.find(function (other) { return other.구현 && other["직업"] === row["직업"] && motions[String((other["게임"]["몸동작"] || [])[0])]; });
    var still = same ? motions[String(same["게임"]["몸동작"][0])] : motions[Object.keys(motions)[0]];
    if (!still) { return null; }
    return { sheet: MOTION_DIR + still["파일"], w: CELL.w, h: CELL.h, foot: still["발밑"], frames: [0], moves: false };
  }

  function figure(art, feet, mirror, label) {
    var box = node("div", "abx-figure" + (mirror ? " is-mirror" : ""));
    box.style.width = art.w + "px";
    box.style.height = art.h + "px";
    box.style.left = (feet[0] - art.w / 2) + "px";
    box.style.top = (feet[1] - art.foot) + "px";
    box.style.backgroundImage = "url(" + art.sheet + ")";
    box.style.backgroundPosition = (-art.frames[0] * art.w) + "px 0";
    var shadow = node("i", "abx-shadow");
    shadow.style.left = (feet[0] - 22) + "px";
    shadow.style.top = (feet[1] - 6) + "px";
    var tag = node("span", "abx-tag", label);
    tag.style.left = feet[0] + "px";
    tag.style.top = (feet[1] + 8) + "px";
    tag.hidden = !label;
    return { box: box, shadow: shadow, tag: tag, art: art, feet: feet, w: art.w };
  }

  /** 이 자리에서 그릴 그림들 — 바꾸지 않았으면 원래대로, 바꿨으면 그림이 있던 자리마다 새 그림 하나(서버 Apply 와 같은 셈). */
  function pictures(row, side) {
    var list = row["자리"][side] || [];
    if (draft.effect === row["기본"].effect) { return list; }
    if (list.length) { return [draft.effect]; }
    var bare = !(row["자리"]["쓴쪽"] || []).length && !(row["자리"]["맞는쪽"] || []).length;
    return bare && side === "맞는쪽" ? [draft.effect] : [];
  }
  /** 그림이 어디에 뜨나를 낱말로 — 「쓴 사람 위」「맞는 쪽 위」. 자기에게 쓰면 맞는 쪽도 쓴 사람이다. */
  function spots(row) {
    var out = [];
    if (pictures(row, "쓴쪽").length) { out.push("쓴 사람 위"); }
    if (pictures(row, "맞는쪽").length) { out.push(placeOf(row).targets.length ? "맞는 쪽 위" : "쓴 사람 위"); }
    return unique(out);
  }

  function flash(number, feet) {
    var info = effectByNumber[number];
    if (!info) { return null; }
    var w = info["바탕"][0], h = info["바탕"][1];
    var box = node("div", "abx-flash");
    box.style.width = w + "px";
    box.style.height = h + "px";
    box.style.left = Math.round(feet[0] + ANCHOR_FROM_FEET[0] - info["기준"][0]) + "px";
    box.style.top = Math.round(feet[1] + ANCHOR_FROM_FEET[1] - info["기준"][1]) + "px";
    box.style.backgroundImage = "url(ui/assets/ability-effects/" + info["파일"] + ")";
    box.hidden = true;
    return { box: box, w: w, order: info["순서"] && info["순서"].length ? info["순서"] : range(info["프레임"]) };
  }

  var timers = [];
  function stopAll() { timers.forEach(clearTimeout); timers = []; }
  function later(fn, ms) { timers.push(setTimeout(fn, ms)); }
  // 칸마다 붙잡는 시간(밀리초) — 앱처럼 원작 「순서」대로 넘긴다(203 은 0 1 1).
  function play(sheet, steps, perStep, done) {
    steps.forEach(function (frame, index) {
      later(function () { sheet.box.hidden = false; sheet.box.style.backgroundPosition = (-frame * sheet.w) + "px 0"; }, index * perStep);
    });
    later(done, steps.length * perStep);
  }

  function arrows(place, aim) {
    var ns = "http://www.w3.org/2000/svg";
    var svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "0 0 " + WORLD.w + " " + WORLD.h);
    svg.setAttribute("class", "abx-arrows");
    svg.setAttribute("aria-hidden", "true");
    svg.innerHTML = '<defs><marker id="abx-head" viewBox="0 0 10 10" refX="7" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M0,0 L10,5 L0,10 z"></path></marker></defs>';
    var path = document.createElementNS(ns, "path");
    var text = document.createElementNS(ns, "text");
    var c = place.caster;
    if (place.targets.length) {
      var from = [c[0] + 28, c[1] - 72];
      var to = [place.targets[0][0] - 30, place.targets[0][1] - 62];
      var top = Math.min(from[1], to[1]) - 40;
      path.setAttribute("d", "M" + from + " Q" + ((from[0] + to[0]) / 2) + "," + top + " " + to);
      text.setAttribute("x", (from[0] + to[0]) / 2);
      text.setAttribute("y", top + 14);
    } else {
      // 발밑을 한 바퀴 도는 고리 — 그림이 쓴 사람 자신에게 돌아온다.
      path.setAttribute("d", "M" + (c[0] + 38) + "," + (c[1] - 2) + " A40,13 0 1,1 " + (c[0] + 33) + "," + (c[1] + 7));
      text.setAttribute("x", c[0]);
      text.setAttribute("y", c[1] - 104);
    }
    path.setAttribute("class", "abx-arrow");
    path.setAttribute("marker-end", "url(#abx-head)");
    text.setAttribute("class", "abx-arrow-label");
    text.textContent = aim + "에게";
    svg.appendChild(path);
    svg.appendChild(text);
    return svg;
  }

  function stage(withAudio) {
    stopAll();
    probe.onloadedmetadata = null;
    speaker.pause();
    var row = selected;
    var world = $("ability-world");
    world.replaceChildren();
    if (!row) { return; }
    var place = placeOf(row);
    var art = casterArt(row);
    var caster = art ? figure(art, place.caster, false, "쓴 사람 · " + row["직업"]) : null;
    var targetArt = friendly(row) ? PERSON : FOE;
    var targets = place.targets.map(function (feet, index) {
      return figure(targetArt, feet, targetArt === PERSON, index === 0 ? (friendly(row) ? "아군" : "적") : "");
    });
    world.appendChild(arrows(place, aimOf(row)));
    var everyone = (caster ? [caster] : []).concat(targets);
    everyone.forEach(function (f) { world.appendChild(f.shadow); });
    everyone.forEach(function (f) { world.appendChild(f.box); world.appendChild(f.tag); });

    var factor = slow ? 3 : 1;
    var lanes = [];
    // 몸동작 — 서버가 0x1A 로 보낸 동작을 칸마다 110ms 로. 그림·소리와 같은 순간에 시작한다.
    if (caster && caster.art.moves) {
      var perMotion = 110 * factor;
      if (!reduced) { play(caster, caster.art.frames, perMotion, function () { caster.box.style.backgroundPosition = "0 0"; }); }
      lanes.push({ label: "몸동작", detail: "쓴 사람이 움직임", ms: caster.art.frames.length * perMotion, cls: "is-motion" });
    } else {
      var sent = (row["게임"]["몸동작"] || []).length;
      lanes.push({ label: "몸동작", detail: sent ? "보내지만 움직이는 그림이 아직 없음" : "보내지 않음 — 가만히", ms: 0, cls: "is-motion" });
    }

    // 그림 — 쓴쪽은 쓴 사람 발에, 맞는쪽은 맞는 이마다. 자기에게 쓰면 맞는쪽도 쓴 사람이다.
    var perStep = Math.min(300, Math.max(30, Number(draft.speed || 100))) * factor;
    var longest = 0;
    [["쓴쪽", caster ? [caster] : []], ["맞는쪽", targets.length ? targets : (caster ? [caster] : [])]].forEach(function (pair) {
      pictures(row, pair[0]).forEach(function (number) {
        pair[1].forEach(function (f) {
          var made = flash(number, f.feet);
          if (!made) { return; }
          world.appendChild(made.box);
          if (reduced) {
            made.box.hidden = false;
            made.box.style.backgroundPosition = (-made.order[Math.floor(made.order.length / 2)] * made.w) + "px 0";
            return;
          }
          play(made, made.order, perStep, function () { made.box.hidden = true; });
          longest = Math.max(longest, made.order.length * perStep);
        });
      });
    });
    var where = spots(row);
    var shown = draft.effect || (row["게임"]["이펙트"] || [])[0];
    lanes.push({ label: "그림", detail: where.length ? where.join(" + ") + " · " + look(shown) : "보내지 않음", ms: longest, cls: "is-effect" });

    // 소리 — 그림과 같은 순간. 길이는 파일을 읽은 뒤에 안다.
    var soundLane = { label: "소리", detail: hasSound(draft.sound) ? (withSound ? heard(draft.sound) : "꺼 둠") : "보내지 않음", ms: 0, cls: "is-sound" };
    lanes.push(soundLane);
    if (hasSound(draft.sound)) {
      probe.onloadedmetadata = function () { if (row !== selected) { return; } soundLane.ms = (probe.duration || 0) * 1000 / (slow ? 0.6 : 1); timeline(lanes); };
      probe.src = "ui/assets/ability-sounds/" + draft.sound + ".mp3";
    }
    if (hasSound(draft.sound) && withSound && withAudio) {
      speaker.src = "ui/assets/ability-sounds/" + draft.sound + ".mp3";
      speaker.playbackRate = slow ? 0.6 : 1;
      var result = speaker.play();
      if (result && result.catch) { result.catch(function () {}); }
    }
    timeline(lanes);
    flow(row, where, shown);
    fit();
  }

  function timeline(lanes) {
    var host = $("ability-timeline");
    host.replaceChildren();
    var span = Math.max(600, lanes.reduce(function (most, lane) { return Math.max(most, lane.ms); }, 0));
    lanes.forEach(function (lane) {
      var line = node("div", "abx-lane " + lane.cls);
      line.appendChild(node("b", "", lane.label));
      var track = node("span", "abx-track");
      var bar = node("i");
      bar.style.width = (lane.ms ? Math.max(3, lane.ms / span * 100) : 0) + "%";
      track.appendChild(bar);
      line.appendChild(track);
      line.appendChild(node("small", "", lane.detail + (lane.ms ? " · " + (lane.ms / 1000).toFixed(1) + "초" : "")));
      host.appendChild(line);
    });
  }

  /** 무대 위 한 줄 — 쓴 사람 → 대상 → 그림(어디에) → 소리. 관계를 낱말로. */
  function flow(row, where, shown) {
    var host = $("ability-flow");
    host.replaceChildren();
    [
      ["쓴 사람", row["직업"] + " · " + ((row["게임"]["몸동작"] || []).length ? "동작 있음" : "가만히")],
      ["대상", aimOf(row)],
      ["그림", where.length ? where.join(" + ") + " · " + look(shown) : "없음"],
      ["소리", hasSound(draft.sound) ? heard(draft.sound) : "없음"]
    ].forEach(function (step) {
      var li = node("li");
      li.appendChild(node("small", "", step[0]));
      li.appendChild(node("b", "", step[1]));
      host.appendChild(li);
    });
  }

  // 무대는 남은 높이를 채운다 — 넓이·높이 중 작은 쪽에 맞추고 가운데에 둔다(낮은 창에서 칸이 넘치지 않게).
  function fit() {
    var box = $("ability-stage"), w = box.clientWidth, h = box.clientHeight;
    var scale = Math.min(w / WORLD.w, h / WORLD.h);
    if (!scale) { return; }
    $("ability-world").style.transform = "translate(" + Math.round((w - WORLD.w * scale) / 2) + "px," + Math.round((h - WORLD.h * scale) / 2) + "px) scale(" + scale + ")";
  }

  /* ── 편집 ─────────────────────────────────────────── */

  function thumb(number, size) {
    var info = effectByNumber[number];
    var item = node("i", "abx-thumb");
    if (!info) { item.classList.add("is-none"); return item; }
    var w = info["바탕"][0], h = info["바탕"][1], frames = info["프레임"];
    var scale = Math.min(1.2, size / Math.max(w, h));
    var cell = Math.round(w * scale);
    item.style.width = cell + "px";
    item.style.height = Math.round(h * scale) + "px";
    item.style.backgroundImage = "url(ui/assets/ability-effects/" + info["파일"] + ")";
    item.style.backgroundSize = (cell * frames) + "px " + Math.round(h * scale) + "px";
    // 가운데 칸을 보여 준다 — 첫 칸은 대개 비어 있다. 움직임은 마우스를 올리거나 골랐을 때만(CSS).
    item.style.backgroundPosition = (-Math.floor(frames / 2) * cell) + "px 0";
    item.style.setProperty("--sheet", (-cell * frames) + "px");
    item.style.setProperty("--frames", frames);
    return item;
  }

  function kinOf(row, channel) {
    var out = [];
    rows.forEach(function (other) {
      if (other.구현 && other["직업"] === row["직업"]) {
        (other["게임"][channel] || []).forEach(function (n) { if (out.indexOf(n) < 0) { out.push(n); } });
      }
    });
    return out;
  }

  /** 거르기 고르기 상자 — 처음 한 번 채우고 고른 값만 맞춘다(바꾸면 아래 배선이 다시 그린다). */
  function pickFrom(host, options, current) {
    if (!host.children.length) {
      options.forEach(function (pair) { var option = node("option", "", pair[1]); option.value = pair[0]; host.appendChild(option); });
    }
    host.value = current;
  }

  function renderEffects(row) {
    pickFrom($("ability-effect-filter"),
      [["추천", "이 직업이 쓰는 것"], ["모두", "모든 그림"]].concat(unique(effects.map(function (e) { return e["색"]; })).map(function (c) { return [c, c + " 빛"]; })),
      effectFilter);
    var host = $("ability-effect-list");
    host.replaceChildren();
    var mine = row["게임"]["이펙트"] || [];
    var list = effectFilter === "추천"
      ? unique(mine.concat(kinOf(row, "이펙트"))).map(function (n) { return effectByNumber[n]; }).filter(Boolean)
      : effects.filter(function (e) { return effectFilter === "모두" || e["색"] === effectFilter; });
    if (effectByNumber[draft.effect] && list.indexOf(effectByNumber[draft.effect]) < 0) { list.unshift(effectByNumber[draft.effect]); }
    list.forEach(function (info) {
      var number = info["번호"];
      var button = node("button", "abx-effect");
      button.type = "button";
      button.dataset.effect = number;
      button.title = look(number) + (effectUsers[number] ? " · " + who(effectUsers[number]) : "");
      button.appendChild(thumb(number, 44));
      button.appendChild(node("b", "", info["색"] + (mine.indexOf(number) >= 0 ? " · 원래" : "")));
      button.appendChild(node("small", "", who(effectUsers[number]) || "쓰지 않던 그림"));
      button.disabled = !row["반영가능"].effect;
      button.addEventListener("click", function () { draft.effect = number; syncEditor(); stage(true); });
      host.appendChild(button);
    });
    syncEditor();
  }

  function renderSounds(row) {
    pickFrom($("ability-sound-filter"), [["추천", "이 직업이 쓰는 것"], ["쓰는것", "쓰이는 소리"], ["모두", "모든 소리"]], soundFilter);
    var host = $("ability-sound-list");
    host.replaceChildren();
    var kin = kinOf(row, "소리");
    var mine = row["게임"]["소리"] || [];
    sounds.filter(function (s) {
      var n = s["번호"];
      if (n === draft.sound || soundFilter === "모두") { return true; }
      return soundFilter === "추천" ? kin.indexOf(n) >= 0 : !!soundUsers[n];
    }).forEach(function (s) {
      var number = s["번호"];
      var li = node("li");
      var button = node("button", "abx-sound");
      button.type = "button";
      button.dataset.sound = number;
      button.appendChild(node("span", "abx-sound-play", "▶"));
      button.appendChild(node("b", "", heard(number)));
      if (mine.indexOf(number) >= 0) { button.appendChild(node("em", "", "원래")); }
      button.disabled = !row["반영가능"].sound;
      button.addEventListener("click", function () { draft.sound = number; syncEditor(); stage(false); listen(number); });
      li.appendChild(button);
      host.appendChild(li);
    });
    syncEditor();
  }

  function listen(number) {
    if (number === null || number === undefined) { return; }
    speaker.src = "ui/assets/ability-sounds/" + number + ".mp3";
    speaker.playbackRate = 1;
    var result = speaker.play();
    if (result && result.catch) { result.catch(function () { toast("이 소리 파일이 없습니다."); }); }
  }

  function syncEditor() {
    if (!selected) { return; }
    $("ability-effect-output").textContent = draft.effect ? look(draft.effect) : "";
    $("ability-speed-output").textContent = pace(draft.speed);
    $("ability-sound-output").textContent = hasSound(draft.sound) ? heard(draft.sound) : "";
    $("ability-speed").value = draft.speed || 100;
    document.querySelectorAll("#ability-effect-list .abx-effect").forEach(function (button) {
      var on = Number(button.dataset.effect) === draft.effect;
      button.classList.toggle("is-active", on);
      button.setAttribute("aria-pressed", String(on));
    });
    document.querySelectorAll("#ability-sound-list .abx-sound").forEach(function (button) {
      var on = Number(button.dataset.sound) === draft.sound;
      button.classList.toggle("is-active", on);
      button.setAttribute("aria-pressed", String(on));
    });
    renderCompare(selected);
  }

  /** 원래 → 지금. 다른 줄만 강조하고, 노바 표가 다르면 한 번에 넣는 단추. */
  function renderCompare(row) {
    var host = $("ability-compare");
    host.replaceChildren();
    var nova = row["노바"];
    [["effect", "그림"], ["speed", "빠르기"], ["sound", "소리"]].forEach(function (pair) {
      var field = pair[0];
      var before = row["기본"][field];
      var line = node("div", "abx-pair" + (before !== draft[field] ? " is-changed" : ""));
      line.appendChild(node("b", "", pair[1]));
      line.appendChild(value(field, before));
      line.appendChild(node("span", "abx-to", "→"));
      line.appendChild(value(field, draft[field]));
      host.appendChild(line);
    });
    if (nova && row["노바와다름"]) {
      var use = node("button", "abx-nova");
      use.type = "button";
      use.appendChild(node("span", "", "노바 표는"));
      use.appendChild(value("effect", nova.effect));
      use.appendChild(node("span", "", "— 이 값 넣기"));
      use.addEventListener("click", function () {
        if (nova.effect && row["반영가능"].effect) { draft.effect = nova.effect; }
        if (nova.sound !== null && nova.sound !== undefined && row["반영가능"].sound) { draft.sound = nova.sound; }
        renderEffects(row); renderSounds(row); stage(true);
        toast("노바 표 값을 넣었습니다. [운영에 반영]을 눌러야 저장됩니다.");
      });
      host.appendChild(use);
    }
    var cast = row["자리"]["쓴쪽"] || [], hit = row["자리"]["맞는쪽"] || [];
    if (row["반영가능"].effect && unique(cast.concat(hit)).length > 1) {
      host.appendChild(node("p", "abx-note", "그림을 바꾸면 " + (cast.length && hit.length ? "쓴 사람 위와 맞는 쪽 위" : "그림이 뜨던 자리") + " 모두 같은 그림이 됩니다."));
    }
  }
  function value(field, number) {
    if (field === "effect") {
      var box = node("span", "abx-value");
      box.appendChild(thumb(number, 30));
      box.appendChild(node("small", "", number ? look(number) : "없음"));
      return box;
    }
    return node("span", "abx-value", field === "speed" ? pace(number) : heard(number));
  }

  function setControl(field, enabled) {
    if (field === "speed") { $("ability-speed").disabled = !enabled; return; }
    $("ability-" + field + "-lock").hidden = enabled;
    $("ability-" + field + "-filter").hidden = !enabled;
  }

  function editable(row) {
    // 보기는 누구나 — 반영 단추는 로그인한 사람에게만 열린다(session.js).
    $("ability-apply").disabled = !apiReady || !window.LOD_SIGNED_IN;
    $("ability-reset").disabled = !apiReady || !window.LOD_SIGNED_IN || !changed(row);
    $("ability-apply").title = window.LOD_SIGNED_IN ? "" : "로그인해야 고칠 수 있습니다";
  }

  function open(row, fromUser) {
    selected = row;
    draft.effect = effective(row, "effect");
    draft.speed = effective(row, "speed");
    draft.sound = effective(row, "sound");
    $("ability-editor-kind").textContent = row["갈래"];
    $("ability-editor-title").textContent = title(row);
    $("ability-editor-meta").textContent = row["직업"] + " · Lv " + row["레벨"] + (row["그룹"] ? " · " + row["그룹"] : "");
    setControl("effect", row["반영가능"].effect);
    setControl("speed", row["반영가능"].speed);
    setControl("sound", row["반영가능"].sound);
    editable(row);
    renderEffects(row);
    renderSounds(row);
    document.querySelectorAll("#ability-grid .abx-row").forEach(function (button) {
      if (button.dataset.abilityKey === row["운영키"]) { button.setAttribute("aria-current", "true"); } else { button.removeAttribute("aria-current"); }
    });
    if (phone.matches && fromUser) {
      $("ability-sheet").classList.add("is-open");
      document.body.classList.add("ability-editor-open");
      $("ability-sheet").scrollTop = 0;
      $("ability-editor-close").focus();
    }
    // 무대 상자가 그려진 뒤에 크기를 잰다.
    requestAnimationFrame(function () { stage(fromUser); });
  }

  function closeDetail() {
    $("ability-sheet").classList.remove("is-open");
    document.body.classList.remove("ability-editor-open");
    stopAll();
    speaker.pause();
    var returning = selected && document.querySelector('[data-ability-key="' + CSS.escape(selected["운영키"]) + '"]');
    if (returning) { returning.focus(); }
  }

  function payload(reset) {
    var values = {};
    ["effect", "speed", "sound"].forEach(function (field) {
      if (selected["반영가능"][field]) { values[field] = reset ? null : draft[field]; }
    });
    return { revision: revision, values: values };
  }

  function save(reset) {
    if (!selected || !apiReady) { return; }
    var key = selected["운영키"];
    $("ability-apply").disabled = true;
    state("운영에 반영 중", "is-loading");
    fetch("/api/ability-overrides/" + encodeURIComponent(key), {
      method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload(reset))
    }).then(function (response) {
      return response.json().then(function (body) { return { status: response.status, ok: response.ok, body: body }; });
    }).then(function (result) {
      if (result.status === 401) { state("로그인 필요", "is-warning"); toast("로그인해야 고칠 수 있습니다. 오른쪽 위 「로그인」."); $("ability-apply").disabled = false; return; }
      if (result.status === 409) {
        overrides = result.body.current.abilities || {};
        changedAt = result.body.current.changedAt || {};
        revision = result.body.current.revision || 0;
        state("저장 충돌 · 다시 확인", "is-warning");
        toast("다른 기기에서 먼저 바꿨습니다. 최신값을 불러왔어요.");
        render(); open(byKey[key], false);
        return;
      }
      if (!result.ok) { throw new Error(result.body.error || "저장하지 못했습니다."); }
      overrides = result.body.abilities || {};
      changedAt = result.body.changedAt || {};
      revision = result.body.revision || 0;
      state("운영에 반영됨", "is-live");
      render(); open(byKey[key], false);
      toast(reset ? "원래 값으로 되돌렸습니다." : "게임에 바로 반영했습니다.");
    }).catch(function (error) {
      state("저장 실패", "is-error");
      $("ability-apply").disabled = false;
      toast(error.message);
    });
  }

  function load() {
    fetch("/api/ability-overrides", { cache: "no-store" }).then(function (response) {
      if (!response.ok) { throw new Error("API unavailable"); }
      return response.json();
    }).then(function (data) {
      overrides = data.abilities || {};
      changedAt = data.changedAt || {};
      revision = data.revision || 0;
      apiReady = true;
      state("운영 연결됨", "is-live");
      render();
      if (selected) { open(selected, false); }
    }).catch(function () {
      apiReady = false;
      state("읽기 전용 · 운영 서버 아님", "is-readonly");
      render();
    });
  }

  $("ability-skill-count").textContent = rows.filter(function (row) { return row["갈래"] === "기술" && row.구현; }).length;
  $("ability-spell-count").textContent = rows.filter(function (row) { return row["갈래"] === "마법" && row.구현; }).length;
  $("ability-kind-tabs").addEventListener("click", function (event) {
    var button = event.target.closest("[data-ability-kind]");
    if (!button) { return; }
    kind = button.dataset.abilityKind;
    $("ability-kind-tabs").querySelectorAll("button").forEach(function (tab) { tab.setAttribute("aria-selected", String(tab === button)); });
    render();
  });
  $("ability-views").addEventListener("click", function (event) {
    var button = event.target.closest("[data-ability-view]");
    if (!button) { return; }
    view = button.dataset.abilityView;
    $("ability-views").querySelectorAll("button").forEach(function (chip) {
      chip.classList.toggle("is-active", chip === button);
      chip.setAttribute("aria-pressed", String(chip === button));
    });
    render();
  });
  $("ability-search").addEventListener("input", function (event) { query = event.target.value.trim().toLowerCase(); render(); });
  $("ability-editor-close").addEventListener("click", closeDetail);
  $("ability-reset").addEventListener("click", function () { save(true); });
  $("ability-apply").addEventListener("click", function () { save(false); });
  $("ability-speed").addEventListener("input", function (event) { draft.speed = Number(event.target.value); syncEditor(); });
  $("ability-speed").addEventListener("change", function () { stage(false); });
  $("ability-replay").addEventListener("click", function () { stage(true); });
  $("ability-effect-filter").addEventListener("change", function (event) { effectFilter = event.target.value; if (selected) { renderEffects(selected); } });
  $("ability-sound-filter").addEventListener("change", function (event) { soundFilter = event.target.value; if (selected) { renderSounds(selected); } });
  $("ability-slow").addEventListener("click", function (event) {
    slow = !slow;
    event.currentTarget.setAttribute("aria-pressed", String(slow));
    event.currentTarget.textContent = slow ? "느리게 보는 중" : "느리게 보기";
    stage(true);
  });
  $("ability-sound-preview").addEventListener("click", function (event) {
    withSound = !withSound;
    event.currentTarget.setAttribute("aria-pressed", String(withSound));
    event.currentTarget.textContent = withSound ? "소리 켜짐" : "소리 꺼짐";
    if (!withSound) { speaker.pause(); }
  });
  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && $("ability-sheet").classList.contains("is-open")) { closeDetail(); }
  });
  document.addEventListener("lod-session", function () { if (selected) { editable(selected); } });
  // 넓은 화면은 숨은 채로 첫 줄을 고른다 — 화면이 보일 때 무대를 다시 올린다.
  if (window.LodDashboard && window.LodDashboard.onViewShown) {
    window.LodDashboard.onViewShown(function (shown) { if (shown === "abilities" && selected) { stage(false); } });
  }
  if (window.ResizeObserver) { new ResizeObserver(fit).observe($("ability-stage")); }

  renderJobs(); render(); load();
}());
