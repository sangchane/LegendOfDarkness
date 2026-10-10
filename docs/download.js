/* 「앱 내려받기」 화면 — 옛 공개 내려받기 페이지(docs/download/)를 여기로 합쳤다(사용자 2026-10-09). 로그인한 사람(카카오 손님
   포함)만 안내·파일을 보고, 등록된 내 기기에 바로 넣는 링크는 관리자만. */
(function () {
  "use strict";
  var body = document.getElementById("download-body");
  var gate = document.getElementById("download-gate");
  if (!body || !gate) { return; }

  // 기기 탭: 윈도우에서 연 사람은 PC, 안드로이드에서 연 사람은 안드로이드, 나머지는 아이폰.
  function show(tab) {
    body.setAttribute("data-tab", tab);
    body.querySelectorAll("[data-to]").forEach(function (b) { b.setAttribute("aria-selected", String(b.getAttribute("data-to") === tab)); });
  }
  body.querySelectorAll("[data-to]").forEach(function (b) {
    b.addEventListener("click", function () { show(b.getAttribute("data-to")); });
  });
  show(/Android/i.test(navigator.userAgent) ? "and" : /Windows/i.test(navigator.userAgent) ? "pc" : "ios");
  if (/iPhone|iPad|Android/i.test(navigator.userAgent)) { document.getElementById("onphone").style.display = "block"; }

  // 최신 파일의 날짜·크기·앱 번호를 서버에서 바로 읽는다 — 로그인한 뒤에만(그 전엔 nginx 가 로그인 화면으로 돌려보낸다).
  function loadMeta() {
    document.querySelectorAll(".download-meta[data-file]").forEach(function (meta) {
      if (location.protocol === "file:") { meta.textContent = "클라우드에서 열면 최신 날짜·크기가 보입니다."; return; }
      fetch(meta.getAttribute("data-file"), { method: "HEAD", cache: "no-store" }).then(function (r) {
        if (!r.ok || r.redirected) { throw new Error(); }
        var when = new Date(r.headers.get("Last-Modified"));
        var mb = Math.round(Number(r.headers.get("Content-Length")) / 1048576);
        var line = "올린 시각 " + when.toLocaleString("ko-KR", { month: "long", day: "numeric", hour: "2-digit", minute: "2-digit" }) + " · " + mb + "MB";
        meta.textContent = line;
        // 앱 번호(빌드 시각, AppUpdate 가 견주는 값) — 앱 로그인 화면의 번호와 같으면 최신이다.
        var version = meta.getAttribute("data-version");
        if (!version) { return; }
        return fetch(version, { cache: "no-store" }).then(function (v) { return v.ok ? v.text() : ""; }).then(function (text) {
          if (text.trim()) { meta.textContent = "앱 번호 " + text.trim() + " · " + line; }
        });
      }).catch(function () { meta.textContent = "아직 올린 파일이 없습니다."; });
    });
  }

  var loaded = false;
  function apply(role, ota, settled) {
    gate.hidden = !settled || !!role;
    body.hidden = !role;
    if (role && !loaded) { loaded = true; loadMeta(); }
    // 우리 서명에 등록된 기기 전용이라 손님이 보면 헷갈린다 — 카드째 관리자에게만(사용자 2026-10-02).
    var link = document.getElementById("download-ota");
    if (!link) { return; }
    var admin = role === "admin";
    link.closest(".download-own").hidden = !admin;
    // 아이폰 시스템은 쿠키 없이 manifest·.ipa 를 받는다 — 관리자 표(ota)를 주소에 달아 서버가 알아보게 한다.
    link.href = admin && ota
      ? "itms-services://?action=download-manifest&url=" + encodeURIComponent(location.origin + "/download/manifest.plist?ota=" + ota)
      : "#";
  }
  apply(window.LOD_ROLE || null, window.LOD_OTA, window.LOD_SESSION_SETTLED === true);
  document.addEventListener("lod-session", function (event) { apply(event.detail.role, event.detail.ota, event.detail.settled); });
})();
