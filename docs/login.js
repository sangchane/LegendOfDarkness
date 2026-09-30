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
        window.location.reload();
      });
    }).catch(function (e) {
      error.textContent = e.message;
      button.disabled = false;
    });
  });
})();
