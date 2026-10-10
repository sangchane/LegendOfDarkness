/* 관리 페이지 로그인 — 브라우저 팝업 대신 이 화면(사용자 2026-09-30). 카카오 로그인(사용자 2026-10-10)이 먼저, 관리자 비밀번호는 접힌 칸.
   성공하면 서버가 쿠키를 주고, 원래 주소를 다시 연다. */
(function () {
  "use strict";
  var form = document.getElementById("login");
  var error = document.getElementById("error");
  var params = new URLSearchParams(window.location.search);

  // 공개 화면의 「로그인」 단추로 왔으면 원래 화면으로 돌려보낸다(같은 사이트 경로만).
  // 「/\evil.com」처럼 브라우저가 바깥 주소로 읽는 것까지 막으려고 실제로 풀어 본 출처를 견준다.
  // 경로만 떼면 「/.//evil.com」이 「//evil.com」이 된다 — 출처를 견준 주소를 통째로 쓴다.
  var target;
  try { target = new URL(params.get("next") || "/", window.location.origin); } catch (_) { target = null; }
  // 로그인 화면 자신으로 돌아가면 아래 「이미 로그인」이 끝없이 다시 연다.
  if (!target || target.origin !== window.location.origin || target.pathname === "/login.html") { target = new URL("/", window.location.origin); }

  document.getElementById("kakao").href = "/api/kakao/start?next=" + encodeURIComponent(target.pathname + target.search);
  error.textContent = {
    "kakao": "카카오 로그인을 마치지 못했습니다. 다시 해 주세요.",
    "denied": "들어올 수 없는 계정입니다. 관리자에게 물어보세요.",
    "kakao-off": "카카오 로그인이 아직 준비되지 않았습니다."
  }[params.get("error")] || "";

  // 관리자면 바로 원래 화면으로 — 카톡 링크처럼 바깥에서 열면 관리자 쿠키(SameSite=Strict)가 첫 요청에 안 실려 여기로 온다.
  // 카카오 손님이 여기 왔으면 관리자로 바꾸러 온 것 — 관리자 칸을 펼쳐 둔다.
  fetch("/api/session", { cache: "no-store" })
    .then(function (response) { return response.json(); })
    .then(function (body) {
      if (body.role === "admin") { window.location.replace(target.href); }
      else if (body.role === "member") { form.closest("details").open = true; }
      else if (body.pending) { join.hidden = false; document.getElementById("kakao").hidden = true; document.getElementById("join-code").focus(); }
    })
    .catch(function () {});

  // 초대 번호 — 맞으면 서버가 허가로 바꾸고, 원래 가려던 화면으로.
  var join = document.getElementById("join");
  join.addEventListener("submit", function (event) {
    event.preventDefault();
    var button = join.querySelector("button");
    button.disabled = true;
    error.textContent = "";
    fetch("/api/kakao/join", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ code: document.getElementById("join-code").value })
    }).then(function (response) {
      return response.json().then(function (body) {
        if (!response.ok) { throw new Error(body.error || "들어가지 못했습니다."); }
        window.location.href = target.href;
      });
    }).catch(function (e) {
      error.textContent = e.message;
      button.disabled = false;
    });
  });

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
        window.location.href = target.href;
      });
    }).catch(function (e) {
      error.textContent = e.message;
      button.disabled = false;
    });
  });
})();
