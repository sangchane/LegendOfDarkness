/**
 * 모든 페이지가 같은 왼쪽 메뉴를 쓰도록 한 곳에서 만든다.
 * 페이지마다 <script src="…/nav.js"></script> 뒤에 buildNav(활성항목, 기준경로) 를 부르면 된다.
 * file:// 로 열어도 동작해야 하므로 fetch 대신 스크립트로 주입한다.
 */
(function () {
  "use strict";

  var ITEMS = [
    { id: "now", label: "지금", href: "index.html#now" },
    { id: "roadmap", label: "로드맵", href: "index.html#roadmap" },
    { id: "flow", label: "작업 흐름", href: "index.html#flow" },
    { id: "screens", label: "화면 6장", href: "ui/wireframes.html" },
    { id: "hud", label: "HUD 자세히", href: "ui/hud-mockup.html" },
    { id: "assets", label: "자산", href: "index.html#assets" },
    { id: "decisions", label: "결정", href: "index.html#decisions" }
  ];

  var STYLE = [
    "body.has-nav{display:grid;grid-template-columns:230px 1fr;margin:0;padding:0;gap:0;align-items:start}",
    "body.has-nav > main{min-width:0;padding:30px 30px 80px}",
    "#sitenav{position:sticky;top:0;height:100dvh;overflow:auto;background:var(--stone-850,#10131c);",
    "  border-right:1px solid var(--edge,#2b3242);padding:22px 0;display:flex;flex-direction:column;gap:4px}",
    "#sitenav .brand{font-family:var(--serif,serif);font-size:17px;padding:0 20px 14px;",
    "  border-bottom:1px solid var(--edge,#2b3242);margin-bottom:10px;color:var(--ink,#e8e2d4)}",
    "#sitenav .brand small{display:block;font-family:var(--mono,monospace);font-size:10px;letter-spacing:.12em;",
    "  text-transform:uppercase;color:var(--gold,#c9a227);margin-top:4px}",
    "#sitenav a{display:flex;align-items:center;gap:9px;padding:9px 20px;color:var(--ink-dim,#98a0b0);",
    "  text-decoration:none;font-size:14px;border-left:2px solid transparent;transition:color .12s,background .12s}",
    "#sitenav a:hover{color:var(--ink,#e8e2d4);background:#ffffff08}",
    "#sitenav a.on{color:var(--ink,#e8e2d4);border-left-color:var(--gold,#c9a227);background:#c9a2270f}",
    "#sitenav a .dot{width:6px;height:6px;border-radius:50%;background:currentColor;opacity:.5}",
    "#sitenav .foot{margin-top:auto;padding:14px 20px 0;border-top:1px solid var(--edge,#2b3242);",
    "  font-family:var(--mono,monospace);font-size:11px;color:#6b7280}",
    "@media (max-width:860px){",
    "  body.has-nav{grid-template-columns:1fr}",
    "  #sitenav{position:static;height:auto;flex-direction:row;flex-wrap:wrap;padding:12px}",
    "  #sitenav .brand,#sitenav .foot{width:100%}#sitenav .foot{margin-top:8px}",
    "}"
  ].join("");

  window.buildNav = function (active, base) {
    var prefix = base || "";

    var style = document.createElement("style");
    style.textContent = STYLE;
    document.head.appendChild(style);

    var nav = document.createElement("nav");
    nav.id = "sitenav";

    var brand = document.createElement("div");
    brand.className = "brand";
    brand.innerHTML = "어둠의 전설 모바일<small>작업 현황</small>";
    nav.appendChild(brand);

    ITEMS.forEach(function (item) {
      var link = document.createElement("a");
      link.href = prefix + item.href;
      link.textContent = item.label;
      link.insertBefore(document.createElement("span"), link.firstChild).className = "dot";
      if (item.id === active) { link.className = "on"; }
      nav.appendChild(link);
    });

    var foot = document.createElement("div");
    foot.className = "foot";
    foot.innerHTML = "기준일 2026-09-10<br>수기 갱신";
    nav.appendChild(foot);

    document.body.classList.add("has-nav");
    document.body.insertBefore(nav, document.body.firstChild);
  };
})();
