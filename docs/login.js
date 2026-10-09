/* 관리 페이지 로그인 — 브라우저 팝업 대신 이 화면(사용자 2026-09-30). 성공하면 서버가 쿠키를 주고, 원래 주소를 다시 연다. */
(function () {
  "use strict";
  var form = document.getElementById("login");
  var error = document.getElementById("error");
  form.addEventListener("submit", function (event) {
    event.preventDefault();
    var button = form.querySelector("button");
    button.disabled = true;
    error.textContent = "";
    fetch("/api/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ password: document.getElementById("password").value,
                             remember: document.getElementById("remember").checked })
    }).then(function (response) {
      return response.json().then(function (body) {
        if (!response.ok) { throw new Error(body.error || "들어가지 못했습니다."); }
        // 공개 화면의 「로그인」 단추로 왔으면 원래 화면으로 돌려보낸다(같은 사이트 경로만).
        var next = new URLSearchParams(window.location.search).get("next") || "/";
        // 「/\evil.com」처럼 브라우저가 바깥 주소로 읽는 것까지 막으려고 실제로 풀어 본 출처를 견준다.
        var target;
        try { target = new URL(next, window.location.origin); } catch (_) { target = null; }
        // 경로만 떼면 「/.//evil.com」이 「//evil.com」이 된다 — 출처를 견준 주소를 통째로 쓴다.
        window.location.href = target && target.origin === window.location.origin ? target.href : "/";
      });
    }).catch(function (e) {
      error.textContent = e.message;
      button.disabled = false;
    });
  });
})();
