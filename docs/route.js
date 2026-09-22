/* 가는 길 — 노비스마을에서 포테의숲까지, 지도 그림 위에 「어느 칸을 밟으면 넘어가는가」로 보여 준다.
 *
 * 워프 연결도(`region-warps.js`)는 "이어져 있다"까지만 말해 준다. 처음 가는 사람에게 필요한 것은
 * **그 맵의 어디로 걸어가야 하는가** 다(사용자, 2026-09-19). 그래서 길을 한 줄로 세우고, 칸마다
 * 그 맵의 실제 그림을 워프 자리 둘레만 잘라 보여 준다.
 *
 * 칸 좌표·그림은 `map-images-data.js`(= `scripts/build-map-images.py`, Hades `templates/warps` 기준)
 * 에서 그대로 온다 — 여기서 지어내는 자리는 없다. 자료에 없는 칸(월드맵 창처럼 지도가 아닌 것)은
 * 그림 없이 무엇을 하는지만 적는다.
 */
(function () {
  "use strict";
  var MAPS = window.MAP_IMAGES;
  if (!MAPS) { return; }

  /** 잘라 보여 줄 창. 워프 자리를 가운데 두고 이만큼만 보여 준다. */
  var VIEW = { wide: 360, tall: 210 };

  /**
   * 길. **순서와 「무엇을 하는가」만 사람이 적고**, 칸 좌표는 자료에서 찾는다.
   * 21레벨은 포테의숲1존이 요구하는 것이 아니라 그 앞 사냥터를 졸업하는 목표다(`NovicePlay`).
   */
  var LEGS = [
    { 부터: "노비스마을", 까지: "노비스평원A",
      할일: "마을을 나와 사냥터로 간다", 메모: "평원 B 로 나가는 문도 따로 있다" },
    { 부터: "노비스평원A", 까지: "노비스마을",
      할일: "21레벨이 될 때까지 잡고 마을로 돌아온다", 문턱: "21레벨" },
    { 부터: "노비스마을", 까지: "월드맵",
      할일: "월드맵 칸을 밟는다", 메모: "마을 한가운데 한 칸뿐이다" },
    { 부터: "월드맵", 까지: "수오미마을",
      할일: "열린 목록에서 수오미를 고른다", 메모: "지도가 아니라 창이라 그림이 없다" },
    { 부터: "수오미마을", 까지: "포테의숲1존",
      할일: "마을을 가로질러 끝까지 걸어간다" }
  ];

  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) { node.className = className; }
    if (text != null) { node.textContent = text; }
    return node;
  }

  /** 이 맵에서 저 맵으로 가는 칸들. 자료에 적힌 그대로. */
  function doorsTo(from, to) {
    var info = MAPS[from];
    if (!info) { return []; }
    return info["표시"].filter(function (mark) { return mark["도착"].indexOf(to) >= 0; });
  }

  /**
   * 그림에서 어느 쪽인지. 나침반 방향을 말하면 거짓이 될 수 있어(마름모로 그려진 지도다)
   * **보는 사람 눈에 보이는 자리**로 말한다.
   */
  function whereOn(info, doors) {
    var x = doors.reduce(function (sum, d) { return sum + d.x; }, 0) / doors.length;
    var y = doors.reduce(function (sum, d) { return sum + d.y; }, 0) / doors.length;
    var across = x / info["폭"], down = y / info["높이"];
    var side = across > 0.72 ? "오른쪽" : across < 0.28 ? "왼쪽" : "";
    var level = down > 0.72 ? "아래" : down < 0.28 ? "위" : "";
    if (side && level) { return side + " " + level + "쪽 끝"; }
    if (side) { return side + " 끝"; }
    if (level) { return level + "쪽 끝"; }
    return "한가운데쯤";
  }

  /** 워프 자리 둘레만 잘라 보여 준다. 전체를 줄여 놓으면 칸이 점이 되어 안 보인다. */
  function window_(info, doors) {
    var x = doors.reduce(function (sum, d) { return sum + d.x; }, 0) / doors.length;
    var y = doors.reduce(function (sum, d) { return sum + d.y; }, 0) / doors.length;
    var wide = Math.min(VIEW.wide, info["폭"]), tall = Math.min(VIEW.tall, info["높이"]);
    var left = Math.max(0, Math.min(x - wide / 2, info["폭"] - wide));
    var top = Math.max(0, Math.min(y - tall / 2, info["높이"] - tall));

    var shot = el("div", "route-shot");
    shot.style.width = wide + "px";
    shot.style.height = tall + "px";
    shot.style.backgroundImage = "url(" + info["그림"] + ")";
    shot.style.backgroundPosition = (-left) + "px " + (-top) + "px";

    doors.forEach(function (door) {
      var pin = el("i", "route-pin");
      pin.style.left = (door.x - left) + "px";
      pin.style.top = (door.y - top) + "px";
      shot.appendChild(pin);
    });
    return shot;
  }

  function leg(step, index) {
    var item = el("li", "route-leg");

    var head = el("div", "route-head");
    head.appendChild(el("span", "route-no", String(index + 1)));
    head.appendChild(el("b", "", step["부터"] + " → " + step["까지"]));
    if (step["문턱"]) { head.appendChild(el("span", "route-gate", step["문턱"])); }
    item.appendChild(head);

    var info = MAPS[step["부터"]];
    var doors = doorsTo(step["부터"], step["까지"]);

    if (info && doors.length) {
      item.appendChild(window_(info, doors));
      item.appendChild(el("p", "route-what",
        step["할일"] + " — 그림의 " + whereOn(info, doors) + ", 밝게 찍힌 "
        + doors.length + "칸 중 아무 데나 밟으면 넘어간다"));
      item.appendChild(el("p", "route-where",
        "칸 " + doors.map(function (d) { return d["칸"].join(","); }).join(" · ")));
    } else {
      // 지도가 아닌 칸(월드맵 창)이나 아직 그림을 안 뽑은 맵. 없는 것을 있는 척하지 않는다.
      item.appendChild(el("div", "route-shot is-empty",
        info ? "이 맵에서 " + step["까지"] + " 로 가는 칸이 자료에 없습니다" : "지도 그림이 아직 없습니다"));
      item.appendChild(el("p", "route-what", step["할일"]));
    }

    if (step["메모"]) { item.appendChild(el("p", "route-note", step["메모"])); }
    return item;
  }

  function boot() {
    var host = document.getElementById("route-legs");
    if (!host) { return; }
    host.replaceChildren();
    LEGS.forEach(function (step, index) { host.appendChild(leg(step, index)); });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
