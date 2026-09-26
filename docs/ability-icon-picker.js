/* 무도가 아이콘 고르기 — 사용자가 그림을 보고 번호를 고르면, 바꾼 것만 글로 내준다(채팅에 붙여 넣으면 템플릿 Icon 을 고친다).
   자료: ability-icon-picker-data.js (scripts/build-ability-icon-picker.py). 고른 것은 이 브라우저에만 기억한다. */
(function () {
  "use strict";

  const data = window.ABILITY_ICON_PICKER;
  const root = document.getElementById("ability-icon-picker");
  if (!data || !root) return;

  const SCALE = 1.25;
  const side = Math.round(data.한변 * SCALE);
  const KEY = "lod.abilityIconPicker.v1";
  const KIND = { skill: "기술", spell: "마법" };

  let chosen = {};
  try { chosen = JSON.parse(localStorage.getItem(KEY) || "{}") || {}; } catch (e) { chosen = {}; }
  const save = () => { try { localStorage.setItem(KEY, JSON.stringify(chosen)); } catch (e) { /* 기억 못 해도 고르기는 된다 */ } };

  const keyOf = (row) => `${row.종류}\t${row.이름}`;
  const numberOf = (row) => (keyOf(row) in chosen ? chosen[keyOf(row)] : row.번호);

  function icon(kind, number) {
    const sheet = data.시트[kind];
    const el = document.createElement("span");
    el.className = "aip-icon";
    el.style.width = el.style.height = `${side}px`;
    el.style.backgroundImage = `url("${sheet.그림}")`;
    el.style.backgroundSize = `${sheet.폭 * SCALE}px ${sheet.높이 * SCALE}px`;
    el.style.backgroundPosition = `-${(number % data.한줄) * side}px -${Math.floor(number / data.한줄) * side}px`;
    el.setAttribute("aria-hidden", "true");
    return el;
  }

  const rowsBox = root.querySelector(".aip-rows");
  const sheetBox = root.querySelector(".aip-sheet");
  const sheetTitle = root.querySelector(".aip-sheet-title");
  const output = root.querySelector(".aip-output");
  const copy = root.querySelector(".aip-copy");
  const reset = root.querySelector(".aip-reset");
  const copied = root.querySelector(".aip-copied");
  let current = null;

  function renderRows() {
    rowsBox.replaceChildren();
    for (const row of data.목록) {
      const number = numberOf(row);
      const changed = keyOf(row) in chosen && chosen[keyOf(row)] !== row.번호;
      const button = document.createElement("button");
      button.type = "button";
      button.className = "aip-row";
      button.setAttribute("aria-pressed", String(current === row));
      if (changed) button.classList.add("is-changed");
      button.append(icon(row.종류, number));
      const text = document.createElement("span");
      text.className = "aip-row-text";
      const label = number === 0 ? "0 · 번호 없음(칼 그림)" : `#${number}`;
      text.innerHTML = `<strong></strong><small>${KIND[row.종류]} · ${row.레벨}레벨 · ${label}${changed ? ` · 원래 #${row.번호}` : ""}</small>`;
      text.querySelector("strong").textContent = row.이름;
      button.append(text);
      button.addEventListener("click", () => { current = row; renderRows(); renderSheet(); });
      rowsBox.append(button);
    }
    renderOutput();
  }

  function renderSheet() {
    sheetBox.replaceChildren();
    if (!current) {
      sheetTitle.textContent = "위에서 기술·마법을 하나 누르세요.";
      return;
    }
    const sheet = data.시트[current.종류];
    const now = numberOf(current);
    sheetTitle.textContent = `${current.이름} — ${KIND[current.종류]} 그림 ${sheet.칸}칸 중에서 고르세요 (지금 #${now})`;
    for (let n = 0; n < sheet.칸; n += 1) {
      const cell = document.createElement("button");
      cell.type = "button";
      cell.className = "aip-cell";
      cell.title = `#${n}`;
      cell.setAttribute("aria-label", `${KIND[current.종류]} 그림 ${n}번`);
      cell.setAttribute("aria-pressed", String(n === now));
      cell.append(icon(current.종류, n));
      const tag = document.createElement("small");
      tag.textContent = n;
      cell.append(tag);
      cell.addEventListener("click", () => {
        if (n === current.번호) delete chosen[keyOf(current)]; else chosen[keyOf(current)] = n;
        save();
        renderRows();
        renderSheet();
      });
      sheetBox.append(cell);
    }
  }

  function renderOutput() {
    const lines = data.목록
      .filter((row) => keyOf(row) in chosen && chosen[keyOf(row)] !== row.번호)
      .map((row) => `${data.직업}\t${row.종류}\t${row.이름}\t${row.번호} → ${chosen[keyOf(row)]}`);
    output.value = lines.length ? `아이콘 바꿔줘\n${lines.join("\n")}` : "";
    output.placeholder = "고른 것이 없습니다. 바꾸면 여기에 글이 생깁니다.";
    copy.disabled = !lines.length;
  }

  copy.addEventListener("click", async () => {
    try { await navigator.clipboard.writeText(output.value); } catch (e) { output.select(); document.execCommand("copy"); }
    copied.hidden = false;
    setTimeout(() => { copied.hidden = true; }, 1500);
  });

  reset.addEventListener("click", () => {
    chosen = {};
    save();
    renderRows();
    renderSheet();
  });

  renderRows();
  renderSheet();
})();
