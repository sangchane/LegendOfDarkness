/* 보기는 누구나, 고치기는 관리자만(사용자 2026-10-02). 서버(/api/session)에 물어 위쪽 띠의 단추와
   window.LOD_SIGNED_IN(관리자인가)·LOD_ROLE(admin·member·null)을 맞추고 "lod-session" 을 알린다 — 편집 화면이 그걸 보고
   입력을 잠그거나 연다. 손님(member, 카카오 로그인 — 사용자 2026-10-10)은 보기·내려받기만, 관리자 메뉴는 안 보인다.
   로그인하지 않은 사람은 서버가 이 화면 대신 로그인 화면을 준다.
   파일로 열었을 때(file://)는 서버가 없으니 예전처럼 이 브라우저에서 고칠 수 있다. */
(function () {
  "use strict";
  var button = document.getElementById("session-button");
  var badge = document.getElementById("session-badge");
  var account = document.getElementById("account-button");
  var dialog = document.getElementById("account-dialog");

  // 계정 관리 — 비밀번호 바꾸기(사용자 2026-10-02). 서버가 지금 비밀번호를 한 번 더 확인한다.
  if (account && dialog) {
    var form = document.getElementById("account-form");
    var error = document.getElementById("account-error");
    var people = document.getElementById("kakao-people-list");

    // 카카오로 들어온 사람 — 처음엔 허가, 관리자가 [거부]로 바꾼다(사용자 2026-10-10). 닉네임은 남이 정한 글자라 textContent 로만.
    function showPeople(list) {
      people.replaceChildren();
      if (!list.length) {
        var empty = document.createElement("li");
        empty.className = "kakao-empty";
        empty.textContent = "아직 카카오로 들어온 사람이 없습니다.";
        people.append(empty);
        return;
      }
      list.forEach(function (person) {
        var row = document.createElement("li");
        row.classList.toggle("is-denied", !person.allowed);
        var text = document.createElement("span");
        var name = document.createElement("b");
        name.textContent = person.name || "(이름 없음)";
        var when = document.createElement("small");
        var last = new Date(person.last || person.first);
        when.textContent = (person.allowed ? "허가" : "거부") + " · 마지막 " + last.toLocaleString("ko-KR", { month: "numeric", day: "numeric", hour: "2-digit", minute: "2-digit" });
        text.append(name, when);
        var flip = document.createElement("button");
        flip.type = "button";
        flip.className = person.allowed ? "session-plain" : "session-button";
        flip.textContent = person.allowed ? "거부" : "허가";
        flip.addEventListener("click", function () {
          flip.disabled = true;
          fetch("/api/kakao/users/" + encodeURIComponent(person.id), {
            method: "PUT", headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ allowed: !person.allowed })
          }).then(function (response) {
            return response.json().then(function (body) {
              if (!response.ok) { throw new Error(body.error || "바꾸지 못했습니다."); }
              showPeople(body);
            });
          }).catch(function (e) { error.textContent = e.message; flip.disabled = false; });
        });
        row.append(text, flip);
        people.append(row);
      });
    }

    account.addEventListener("click", function () {
      form.reset(); error.textContent = ""; dialog.showModal();
      fetch("/api/kakao/users", { cache: "no-store" })
        .then(function (response) { if (!response.ok) { throw new Error(); } return response.json(); })
        .then(showPeople)
        .catch(function () { error.textContent = "카카오 사람 목록을 읽지 못했습니다."; });
    });
    document.getElementById("account-cancel").addEventListener("click", function () { dialog.close(); });
    form.addEventListener("submit", function (event) {
      event.preventDefault();
      var next = document.getElementById("account-new").value;
      if (next !== document.getElementById("account-again").value) { error.textContent = "새 비밀번호 두 칸이 다릅니다."; return; }
      var save = document.getElementById("account-save");
      save.disabled = true;
      error.textContent = "";
      fetch("/api/password", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ current: document.getElementById("account-current").value, new: next })
      }).then(function (response) {
        return response.json().then(function (body) {
          if (!response.ok) { throw new Error(body.error || "바꾸지 못했습니다."); }
          dialog.close();
          alert("비밀번호를 바꿨습니다. 다른 기기에서는 새 비밀번호로 다시 로그인하세요.");
        });
      }).catch(function (e) { error.textContent = e.message; })
        .then(function () { save.disabled = false; });
    });
  }

  // settled: 서버 답을 받았나 — 받기 전에는 화면을 돌려보내거나 「로그인하세요」를 띄우지 않는다.
  function apply(role, ota, settled) {
    var signedIn = role === "admin";
    window.LOD_SIGNED_IN = signedIn;
    window.LOD_ROLE = role;
    window.LOD_OTA = ota || "";
    window.LOD_SESSION_SETTLED = settled;
    document.body.classList.toggle("is-read-only", !signedIn);
    if (button) {
      // 휴대폰 폭에서는 로그아웃을 아이콘만(글자가 세로로 꺾이던 것) — 글자는 .session-label 이 CSS 로 숨긴다.
      button.innerHTML = role
        ? '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3M10 16l-4-4 4-4M6 12h10"/></svg><span class="session-label">로그아웃</span>'
        : "로그인";
      button.setAttribute("aria-label", role ? "로그아웃" : "로그인");
      button.title = role ? "로그아웃" : "";
      button.hidden = location.protocol === "file:";
    }
    if (badge) { badge.hidden = signedIn; }
    if (account) { account.hidden = !signedIn || location.protocol === "file:"; }
    document.querySelectorAll("[data-admin-only]").forEach(function (item) { item.hidden = !signedIn; });
    // 관리자가 아닌데 접속·활동을 보고 있으면(주소로 들어온 경우) 첫 화면으로.
    var activity = document.querySelector('[data-view="activity"]');
    var home = document.querySelector('[data-view-target="overview"]');
    if (settled && !signedIn && activity && !activity.hidden && home) { home.click(); }
    document.dispatchEvent(new CustomEvent("lod-session", { detail: { signedIn: signedIn, role: role, ota: window.LOD_OTA, settled: settled } }));
  }

  if (button) {
    button.addEventListener("click", function () {
      if (!window.LOD_ROLE) {
        location.href = "/login.html?next=" + encodeURIComponent(location.pathname + location.search);
        return;
      }
      fetch("/api/logout", { method: "POST" }).then(function () { location.reload(); });
    });
  }

  if (location.protocol === "file:") { apply("admin", "", true); return; }
  apply(null, "", false);
  fetch("/api/session", { cache: "no-store" })
    .then(function (response) { return response.json(); })
    // role 이 없는 옛 서버(배포 순서가 어긋난 동안)는 signedIn 을 관리자로 읽는다.
    .then(function (body) { apply(body.role || (body.signedIn === true ? "admin" : null), body.ota, true); })
    .catch(function () { apply(null, "", true); });
})();
