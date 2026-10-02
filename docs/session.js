/* 보기는 누구나, 고치기는 로그인한 사람만(사용자 2026-10-02). 서버(/api/session)에 물어 위쪽 띠의 단추와
   window.LOD_SIGNED_IN 을 맞추고 "lod-session" 을 알린다 — 편집 화면이 그걸 보고 입력을 잠그거나 연다.
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
    account.addEventListener("click", function () { form.reset(); error.textContent = ""; dialog.showModal(); });
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

  function apply(signedIn) {
    window.LOD_SIGNED_IN = signedIn;
    document.body.classList.toggle("is-read-only", !signedIn);
    if (button) {
      button.textContent = signedIn ? "로그아웃" : "로그인";
      button.hidden = location.protocol === "file:";
    }
    if (badge) { badge.hidden = signedIn; }
    if (account) { account.hidden = !signedIn || location.protocol === "file:"; }
    document.dispatchEvent(new CustomEvent("lod-session", { detail: { signedIn: signedIn } }));
  }

  if (button) {
    button.addEventListener("click", function () {
      if (!window.LOD_SIGNED_IN) {
        location.href = "/login.html?next=" + encodeURIComponent(location.pathname + location.search);
        return;
      }
      fetch("/api/logout", { method: "POST" }).then(function () { location.reload(); });
    });
  }

  window.LOD_SIGNED_IN = location.protocol === "file:";
  if (location.protocol === "file:") { apply(true); return; }
  apply(false);
  fetch("/api/session", { cache: "no-store" })
    .then(function (response) { return response.json(); })
    .then(function (body) { apply(body.signedIn === true); })
    .catch(function () { apply(false); });
})();
