/* 보기는 누구나, 고치기는 로그인한 사람만(사용자 2026-10-02). 서버(/api/session)에 물어 위쪽 띠의 단추와
   window.LOD_SIGNED_IN 을 맞추고 "lod-session" 을 알린다 — 편집 화면이 그걸 보고 입력을 잠그거나 연다.
   파일로 열었을 때(file://)는 서버가 없으니 예전처럼 이 브라우저에서 고칠 수 있다. */
(function () {
  "use strict";
  var button = document.getElementById("session-button");
  var badge = document.getElementById("session-badge");

  function apply(signedIn) {
    window.LOD_SIGNED_IN = signedIn;
    document.body.classList.toggle("is-read-only", !signedIn);
    if (button) {
      button.textContent = signedIn ? "로그아웃" : "로그인";
      button.hidden = location.protocol === "file:";
    }
    if (badge) { badge.hidden = signedIn; }
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
