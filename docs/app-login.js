/* 앱 카카오 로그인의 끝(사용자 2026-10-11) — 주소가 #used 면 이미 쓰였거나 지난 로그인. 6자리 번호는 뺐다(사용자 같은 날). */
(function () {
  "use strict";
  var used = window.location.hash === "#used";
  document.getElementById("signed-in").hidden = used;
  document.getElementById("used").hidden = !used;
})();
