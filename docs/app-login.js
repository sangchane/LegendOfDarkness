/* 앱 카카오 로그인의 끝(사용자 2026-10-11) — 주소 # 뒤 6자리를 크게 보여 준다. 앱에 이 번호를 넣어야 앱이 게임 표를 받는다
   (남이 만든 링크를 열어도 번호는 이 화면에만 있어 가로챌 수 없다 — 보안 리뷰 2026-10-11). # 뒤는 서버로 다시 가지 않는다. */
(function () {
  "use strict";
  var code = window.location.hash.slice(1);
  var ok = /^\d{6}$/.test(code);
  document.getElementById("code").textContent = ok ? code : "";
  document.getElementById("with-code").hidden = !ok;
  document.getElementById("without-code").hidden = ok;
})();
