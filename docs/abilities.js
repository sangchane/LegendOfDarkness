/* 현재 Hades 기술·마법과 운영 연출값을 한 화면에서 다룬다.
 * 자료: ability-operations-data.js / API: /api/ability-overrides
 */
(function () {
  "use strict";

  var source = window.LOD_ABILITY_OPERATIONS;
  var media = window.LOD_ABILITY_MEDIA;
  if (!source || !media) { return; }

  // 원작 표에만 있는 영어 이름 미구현 기술은 싣지 않는다(사용자, 2026-10-02) — 구현된 것은 영어 이름이어도 남긴다.
  var rows = (source["목록"] || []).filter(function (row) { return row.구현 || /[가-힣]/.test(row.이름); });
  var effects = media["이펙트"] || [];
  var sounds = media["소리"] || media["사운드"] || [];
  var byKey = {};
  var effectByNumber = {};
  var overrides = {};
  var changedAt = {};
  // 구현 = 게임 스킬창에 보이는 것(레벨 표 + 기본공격, 사용자 2026-10-03). 나머지는 「미구현」 칩에서 본다.
  var view = "구현";
  var revision = 0;
  var apiReady = false;
  var kind = "기술";
  var job = "전체";
  var query = "";
  var page = 0;
  var perPage = 60;
  var selected = null;
  var draft = { effect: null, speed: null, sound: null };
  var speaker = new Audio();
  speaker.volume = 0.55;

  rows.forEach(function (row) { byKey[row["운영키"]] = row; });
  effects.forEach(function (item) { effectByNumber[item["번호"]] = item; });

  function $(id) { return document.getElementById(id); }
  function node(tag, className, text) {
    var item = document.createElement(tag);
    if (className) { item.className = className; }
    if (text !== undefined) { item.textContent = text; }
    return item;
  }
  function toast(message) {
    if (window.LodDashboard) { window.LodDashboard.toast(message); }
  }
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
  function label(value) { return value === null || value === undefined ? "없음" : String(value); }

  function icon(row) {
    var item = node("i", "ability-ops-icon");
    var number = Number(row["아이콘"] || 0);
    var cell = 35;
    item.style.backgroundImage = "url(ability-icons/" + (row["갈래"] === "기술" ? "skill" : "spell") + ".png)";
    item.style.backgroundPosition = (-((number % 16) * cell)) + "px " + (-Math.floor(number / 16) * cell) + "px";
    item.setAttribute("aria-hidden", "true");
    return item;
  }

  function filtered() {
    var list = rows.filter(function (row) {
      if (row["갈래"] !== kind) { return false; }
      if (view === "구현" && !row.구현) { return false; }
      if (view === "미구현" && row.구현) { return false; }
      if (view === "노바와다름" && !row["노바와다름"]) { return false; }
      if (view === "운영수정" && !changed(row)) { return false; }
      if (job !== "전체" && row["직업"] !== job) { return false; }
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
    var jobs = ["전체", "공통", "전사", "도적", "마법사", "사제", "무도가"];
    host.replaceChildren();
    jobs.forEach(function (name) {
      var button = node("button", "chip" + (job === name ? " is-active" : ""), name);
      button.type = "button";
      button.setAttribute("aria-pressed", String(job === name));
      button.addEventListener("click", function () { job = name; page = 0; renderJobs(); render(); });
      host.appendChild(button);
    });
  }

  function card(row) {
    var button = node("button", "ability-ops-card" + (changed(row) ? " is-overridden" : ""));
    button.type = "button";
    button.dataset.abilityKey = row["운영키"];
    button.appendChild(icon(row));

    var copy = node("span", "ability-ops-copy");
    var title = node("span", "ability-ops-title");
    title.appendChild(node("strong", "", row["이름"] === "Assail" ? "기본공격" : row["이름"]));
    if (changed(row)) { title.appendChild(node("em", "", "운영 수정" + (when(row["운영키"]) ? " · " + when(row["운영키"]) : ""))); }
    if (row["노바와다름"]) { title.appendChild(node("em", "is-nova", "노바 표와 다름")); }
    copy.appendChild(title);
    copy.appendChild(node("small", "", row["직업"] + " · Lv " + row["레벨"] + (row["그룹"] ? " · " + row["그룹"] : "")));

    var values = node("span", "ability-ops-values");
    values.appendChild(node("span", "", "이펙트 " + label(effective(row, "effect"))));
    values.appendChild(node("span", "", "속도 " + label(effective(row, "speed"))));
    values.appendChild(node("span", "", "소리 " + label(effective(row, "sound"))));
    if (row["노바와다름"]) {
      values.appendChild(node("span", "is-nova", "노바 " + label(row["노바"].effect) + "·" + label(row["노바"].sound)));
    }
    copy.appendChild(values);
    button.appendChild(copy);
    button.appendChild(node("span", "ability-ops-chevron", "›"));
    button.addEventListener("click", function () { openEditor(row); });
    return button;
  }

  function render() {
    var list = filtered();
    var pages = Math.max(1, Math.ceil(list.length / perPage));
    page = Math.min(page, pages - 1);
    var shown = list.slice(page * perPage, (page + 1) * perPage);
    var host = $("ability-grid");
    host.replaceChildren();
    shown.forEach(function (row) { host.appendChild(card(row)); });
    $("ability-shown").textContent = list.length;
    $("ability-empty").hidden = list.length > 0;
    $("ability-override-count").textContent = "운영 수정 " + Object.keys(overrides).length + "개";
    $("ability-changed-count").textContent = rows.filter(function (row) { return row["갈래"] === kind && changed(row); }).length;
    $("ability-done-count").textContent = rows.filter(function (row) { return row["갈래"] === kind && row.구현; }).length;
    $("ability-todo-count").textContent = rows.filter(function (row) { return row["갈래"] === kind && !row.구현; }).length;
    $("ability-nova-count").textContent = rows.filter(function (row) { return row["갈래"] === kind && row["노바와다름"]; }).length;
    $("ability-empty").textContent = view === "운영수정" ? "아직 바꾼 " + kind + "이 없어요." : view === "노바와다름" ? "노바 표와 다른 " + kind + "이 없어요." : "조건에 맞는 " + kind + "이 없어요. 필터를 지워 보세요.";

    var pager = $("ability-pager");
    pager.hidden = pages <= 1;
    $("ability-pager-label").textContent = (page + 1) + " / " + pages;
    var buttons = pager.querySelectorAll("button");
    if (buttons.length === 2) { buttons[0].disabled = page === 0; buttons[1].disabled = page === pages - 1; }
  }

  function previewEffect(number) {
    var host = $("ability-effect-preview");
    host.replaceChildren();
    var info = effectByNumber[number];
    if (!info) { host.appendChild(node("span", "ability-preview-empty", number ? "그림 없음 · " + number : "이펙트 없음")); return; }
    var shot = node("i", "ability-preview-shot");
    var width = info["바탕"][0], height = info["바탕"][1], frames = info["프레임"];
    var scale = Math.min(1.35, 160 / Math.max(width, height));
    shot.style.width = width + "px";
    shot.style.height = height + "px";
    shot.style.backgroundImage = "url(ui/assets/ability-effects/" + info["파일"] + ")";
    shot.style.backgroundSize = (width * frames) + "px " + height + "px";
    shot.style.setProperty("--effect-sheet", (-width * frames) + "px");
    shot.style.setProperty("--effect-frames", frames);
    shot.style.setProperty("--effect-time", Math.max(120, Number(draft.speed || 100) * frames) + "ms");
    shot.style.transform = "scale(" + scale + ")";
    host.appendChild(shot);
  }

  function playSound(number) {
    if (number === null || number === undefined || number === "") { toast("고른 사운드가 없습니다."); return; }
    speaker.src = "ui/assets/ability-sounds/" + number + ".mp3";
    speaker.currentTime = 0;
    var result = speaker.play();
    if (result && result.catch) { result.catch(function () { toast("이 번호의 사운드 파일이 없습니다."); }); }
  }

  function mediaButton(type, item) {
    var number = item["번호"];
    var active = draft[type] === number;
    var button = node("button", "ability-media-item" + (active ? " is-active" : ""));
    button.type = "button";
    button.setAttribute("aria-pressed", String(active));
    button.setAttribute("aria-label", (type === "effect" ? "이펙트 " : "사운드 ") + number);
    if (type === "effect") {
      var thumb = node("i", "ability-effect-thumb");
      var width = item["바탕"][0], height = item["바탕"][1];
      var scale = Math.min(1.5, 44 / Math.max(width, height));
      thumb.style.width = Math.round(width * scale) + "px";
      thumb.style.height = Math.round(height * scale) + "px";
      thumb.style.backgroundImage = "url(ui/assets/ability-effects/" + item["파일"] + ")";
      thumb.style.backgroundSize = Math.round(width * item["프레임"] * scale) + "px " + Math.round(height * scale) + "px";
      button.appendChild(thumb);
    } else {
      button.appendChild(node("span", "ability-sound-mark", "▶"));
    }
    button.appendChild(node("small", "", String(number)));
    button.addEventListener("click", function () {
      draft[type] = number;
      if (type === "effect") { $("ability-effect-number").value = number; previewEffect(number); }
      else { $("ability-sound-number").value = number; playSound(number); }
      syncEditor();
    });
    return button;
  }

  function renderMedia() {
    var effectList = $("ability-effect-list");
    var soundList = $("ability-sound-list");
    effectList.replaceChildren(); soundList.replaceChildren();
    effects.forEach(function (item) { effectList.appendChild(mediaButton("effect", item)); });
    sounds.forEach(function (item) { soundList.appendChild(mediaButton("sound", item)); });
  }

  function syncEditor() {
    if (!selected) { return; }
    $("ability-effect-output").textContent = label(draft.effect);
    $("ability-speed-output").textContent = label(draft.speed);
    $("ability-sound-output").textContent = label(draft.sound);
    Array.prototype.forEach.call($("ability-effect-list").children, function (button) {
      button.classList.toggle("is-active", Number(button.querySelector("small").textContent) === draft.effect);
    });
    Array.prototype.forEach.call($("ability-sound-list").children, function (button) {
      button.classList.toggle("is-active", Number(button.querySelector("small").textContent) === draft.sound);
    });
  }

  function renderCompare(row) {
    var host = $("ability-compare");
    host.replaceChildren();
    var nova = row["노바"];
    var table = node("div", "ability-compare-grid");
    ["", "서버 기본", "노바 표", "지금 운영"].forEach(function (text) { table.appendChild(node("b", "", text)); });
    [["effect", "이펙트"], ["speed", "속도"], ["sound", "소리"]].forEach(function (pair) {
      var field = pair[0];
      var novaValue = nova && field !== "speed" ? nova[field] : null;
      table.appendChild(node("span", "", pair[1]));
      table.appendChild(node("span", "", label(row["기본"][field])));
      table.appendChild(node("span", novaValue !== null && novaValue !== undefined && novaValue !== row["기본"][field] ? "is-nova" : "", field === "speed" ? "—" : label(novaValue)));
      table.appendChild(node("span", overrides[row["운영키"]] && field in overrides[row["운영키"]] ? "is-changed" : "", label(effective(row, field))));
    });
    host.appendChild(table);
    if (nova) {
      var use = node("button", "ability-use-nova", "노바 표 값 넣기");
      use.type = "button";
      use.addEventListener("click", function () {
        if (nova.effect && row["반영가능"].effect) { draft.effect = nova.effect; $("ability-effect-number").value = nova.effect; previewEffect(nova.effect); }
        if (nova.sound !== null && nova.sound !== undefined && row["반영가능"].sound) { draft.sound = nova.sound; $("ability-sound-number").value = nova.sound; }
        syncEditor(); toast("노바 표 값을 넣었습니다. [운영에 반영]을 눌러야 저장됩니다.");
      });
      host.appendChild(use);
    }
  }

  function setControl(field, enabled) {
    var ids = field === "effect" ? ["ability-effect-number"] : field === "sound" ? ["ability-sound-number", "ability-sound-preview"] : ["ability-speed"];
    ids.forEach(function (id) { $(id).disabled = !enabled; });
    var selector = field === "effect" ? "#ability-effect-list button" : field === "sound" ? "#ability-sound-list button" : "";
    if (selector) { document.querySelectorAll(selector).forEach(function (button) { button.disabled = !enabled; }); }
  }

  function openEditor(row) {
    selected = row;
    draft.effect = effective(row, "effect");
    draft.speed = effective(row, "speed");
    draft.sound = effective(row, "sound");
    $("ability-editor-kind").textContent = row["갈래"];
    $("ability-editor-title").textContent = row["이름"];
    $("ability-editor-meta").textContent = row["직업"] + " · Lv " + row["레벨"] + (row["그룹"] ? " · " + row["그룹"] : "");
    $("ability-effect-number").value = draft.effect === null ? "" : draft.effect;
    $("ability-speed").value = draft.speed || 100;
    $("ability-sound-number").value = draft.sound === null ? "" : draft.sound;
    setControl("effect", row["반영가능"].effect);
    setControl("speed", row["반영가능"].speed);
    setControl("sound", row["반영가능"].sound);
    // 보기는 누구나 — 반영 단추는 로그인한 사람에게만 열린다(session.js).
    $("ability-apply").disabled = !apiReady || !window.LOD_SIGNED_IN;
    $("ability-reset").disabled = !apiReady || !window.LOD_SIGNED_IN || !changed(row);
    $("ability-apply").title = window.LOD_SIGNED_IN ? "" : "로그인해야 고칠 수 있습니다";
    renderCompare(row);
    renderMedia(); syncEditor(); previewEffect(draft.effect);
    $("ability-editor").hidden = false;
    document.querySelector(".ability-editor-scroll").scrollTop = 0;
    $("ability-editor-scrim").hidden = false;
    document.body.classList.add("ability-editor-open");
    $("ability-editor-close").focus();
  }

  function closeEditor() {
    $("ability-editor").hidden = true;
    $("ability-editor-scrim").hidden = true;
    document.body.classList.remove("ability-editor-open");
    var returning = selected && document.querySelector('[data-ability-key="' + CSS.escape(selected["운영키"]) + '"]');
    selected = null;
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
      if (result.status === 401) { state("로그인 필요", "is-warning"); toast("로그인해야 고칠 수 있습니다. 오른쪽 위 「로그인」."); return; }
      if (result.status === 409) {
        overrides = result.body.current.abilities || {};
        changedAt = result.body.current.changedAt || {};
        revision = result.body.current.revision || 0;
        state("저장 충돌 · 다시 확인", "is-warning");
        toast("다른 기기에서 먼저 바꿨습니다. 최신값을 불러왔어요.");
        render(); openEditor(byKey[key]);
        return;
      }
      if (!result.ok) { throw new Error(result.body.error || "저장하지 못했습니다."); }
      overrides = result.body.abilities || {};
      changedAt = result.body.changedAt || {};
      revision = result.body.revision || 0;
      state("운영에 반영됨", "is-live");
      render(); closeEditor(); toast(reset ? "서버 기본값으로 되돌렸습니다." : "운영값을 바로 반영했습니다.");
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
    }).catch(function () {
      apiReady = false;
      state("읽기 전용 · 운영 서버 아님", "is-readonly");
      render();
    });
  }

  $("ability-skill-count").textContent = rows.filter(function (row) { return row["갈래"] === "기술"; }).length;
  $("ability-spell-count").textContent = rows.filter(function (row) { return row["갈래"] === "마법"; }).length;
  $("ability-kind-tabs").addEventListener("click", function (event) {
    var button = event.target.closest("[data-ability-kind]");
    if (!button) { return; }
    kind = button.dataset.abilityKind; page = 0;
    $("ability-kind-tabs").querySelectorAll("button").forEach(function (tab) {
      tab.setAttribute("aria-selected", String(tab === button));
    });
    render();
  });
  $("ability-views").addEventListener("click", function (event) {
    var button = event.target.closest("[data-ability-view]");
    if (!button) { return; }
    view = button.dataset.abilityView; page = 0;
    $("ability-views").querySelectorAll("button").forEach(function (chip) {
      chip.classList.toggle("is-active", chip === button);
      chip.setAttribute("aria-pressed", String(chip === button));
    });
    render();
  });
  $("ability-search").addEventListener("input", function (event) { query = event.target.value.trim().toLowerCase(); page = 0; render(); });
  $("ability-pager").addEventListener("click", function (event) { var button = event.target.closest("[data-ability-page]"); if (button && !button.disabled) { page += Number(button.dataset.abilityPage); render(); } });
  $("ability-editor-close").addEventListener("click", closeEditor);
  $("ability-editor-scrim").addEventListener("click", closeEditor);
  $("ability-reset").addEventListener("click", function () { save(true); });
  $("ability-apply").addEventListener("click", function () { save(false); });
  $("ability-effect-number").addEventListener("input", function (event) { draft.effect = event.target.value === "" ? null : Number(event.target.value); previewEffect(draft.effect); syncEditor(); });
  $("ability-sound-number").addEventListener("input", function (event) { draft.sound = event.target.value === "" ? null : Number(event.target.value); syncEditor(); });
  $("ability-speed").addEventListener("input", function (event) { draft.speed = Number(event.target.value); previewEffect(draft.effect); syncEditor(); });
  $("ability-sound-preview").addEventListener("click", function () { playSound(draft.sound); });
  document.addEventListener("keydown", function (event) { if (event.key === "Escape" && selected) { closeEditor(); } });

  renderJobs(); render(); load();
}());
