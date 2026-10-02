/* 「앱 내려받기」 화면 — 공개 페이지(/download/)로 가는 길과, 등록된 내 기기에 바로 넣는 링크(로그인한 사람만). */
(function () {
  "use strict";
  document.querySelectorAll(".download-meta[data-file]").forEach(function (meta) {
    if (location.protocol === "file:") { meta.textContent = "클라우드에서 열면 최신 날짜·크기가 보입니다."; return; }
    fetch(meta.getAttribute("data-file"), { method: "HEAD", cache: "no-store" }).then(function (r) {
      if (!r.ok) { throw new Error(); }
      var when = new Date(r.headers.get("Last-Modified"));
      var mb = Math.round(Number(r.headers.get("Content-Length")) / 1048576);
      meta.textContent = "최신판 · " + when.toLocaleString("ko-KR", { month: "long", day: "numeric", hour: "2-digit", minute: "2-digit" }) + " · " + mb + "MB";
    }).catch(function () { meta.textContent = "아직 올린 파일이 없습니다."; });
  });

  function own(signedIn) {
    var link = document.getElementById("download-ota");
    var note = document.getElementById("download-ota-note");
    if (!link) { return; }
    // 우리 서명에 등록된 기기 전용이라 친구들이 보면 헷갈린다 — 카드째 로그인한 사람에게만(사용자 2026-10-02).
    link.closest(".download-own").hidden = !signedIn;
    link.hidden = !signedIn;
    note.textContent = signedIn
      ? "설치 확인 창에서 「설치」 → 홈 화면에서 아이콘이 채워질 때까지 기다립니다(1~3분)."
      : "로그인해야 보입니다.";
  }
  own(window.LOD_SIGNED_IN === true);
  document.addEventListener("lod-session", function (event) { own(event.detail.signedIn); });
})();
