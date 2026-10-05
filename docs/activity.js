(function () {
  "use strict";
  var panel = document.querySelector('[data-view="activity"]');
  var form = document.getElementById("activity-filter");
  var status = document.getElementById("activity-status");
  var results = document.getElementById("activity-results");
  var button = document.getElementById("activity-refresh");
  var sequence = 0;
  var names = { login:"접속 성공", legacy_login:"과거 환영 로그(접속 추정)", logout:"접속 종료", map:"맵 입장", kill:"괴물 처치(서버 카운터)", exchange:"교환 처리", state:"캐릭터 상태", request_skill:"기술 요청", request_spell:"마법 요청", request_pickup:"줍기 요청", request_drop:"버리기 요청", request_item:"아이템 사용 요청", request_drop_gold:"금화 버리기 요청", request_npc:"NPC 요청", request_npc_choice:"NPC 선택 요청", request_exchange:"교환 요청", request_profile:"정보 조회 요청", request_worldmap:"월드맵 요청", request_warp:"워프 요청", request_companion:"동료 조작 요청", request_shop:"상점 거래 요청" };
  Object.assign(names,{heartbeat:"접속 확인",ledger:"재화 원장",login_failure:"로그인 실패",app_device:"앱 기기",app_screen:"화면 이용",app_button:"버튼 사용",app_error:"앱 오류",app_lifecycle:"앱 상태"});
  var assets = {gold:"금화",xp:"경험치",item:"아이템"};
  function minutes(seconds) { return (Number(seconds||0)/60).toFixed(1)+"분"; }
  function metadata(row) { var m=row.meta||{};return Object.keys(m).map(function(key) { return key+"="+m[key]; }).join(" · "); }
  function day(date) { return new Intl.DateTimeFormat("sv-SE",{timeZone:"Asia/Seoul"}).format(date); }
  var today = new Date();
  document.getElementById("activity-to").value = day(today);
  document.getElementById("activity-from").value = day(new Date(today.getTime()-86400000));
  function number(value) { return Number(value || 0).toLocaleString("ko-KR"); }
  function time(value) { return value ? new Date(value).toLocaleString("ko-KR",{timeZone:"Asia/Seoul",hour12:false}) : "—"; }
  function bytes(value) { return (Number(value)/1048576).toFixed(1)+" MB"; }
  function table(id, title, headings, rows) {
    var host = document.getElementById(id);
    host.replaceChildren();
    if (!rows.length) { host.textContent = "이 기간에는 기록이 없습니다."; return; }
    var grid = document.createElement("table");
    var caption = grid.createCaption(); caption.textContent = title;
    var header = grid.createTHead().insertRow();
    headings.forEach(function (label) { var cell=document.createElement("th");cell.scope="col";cell.textContent=label;header.append(cell); });
    var body = grid.createTBody();
    rows.forEach(function (row) { var line=body.insertRow();row.forEach(function (value) { line.insertCell().textContent = value == null ? "—" : String(value); }); });
    host.append(grid);
  }
  function clear() { sequence++;results.hidden=true;results.querySelectorAll(".activity-table,#activity-summary,#activity-observation").forEach(function (host) { host.replaceChildren(); }); }
  function render(data) {
    var summary = document.getElementById("activity-summary");summary.replaceChildren();
    [["웹 페이지 요청",number(data.summary.visits)],["접속 IP",number(data.summary.addresses)],["파일 전송 요청",number(data.summary.downloads)],["전송량",bytes(data.summary.bytes)],["게임 접속",number(data.summary.logins)],["현재 게임 접속",data.current.stale?"확인 지연":number(data.current.players)]].forEach(function (item) {
      var block=document.createElement("div"),label=document.createElement("span"),value=document.createElement("strong");label.textContent=item[0];value.textContent=item[1];block.append(label,value);summary.append(block);
    });
    table("activity-daily","기간 전체 집계",["날짜","페이지 요청","다운로드","전송량","게임 접속","캐릭터"],data.daily.map(function (r) { return [r.day,number(r.visits),number(r.downloads),bytes(r.bytes),number(r.logins),number(r.players)]; }));
    table("activity-visitors","최근 접속 환경 최대 200개",["IP","브라우저·수집기","요청","다운로드","전송량","최근 접속"],data.visitors.map(function(r) { return [r.ip,r.agent,number(r.requests),number(r.downloads),bytes(r.bytes),time(r.lastAt)]; }));
    table("activity-downloads","최근 전송 최대 200건 · HEAD 제외",["시각","IP","파일","HTTP","전송량","브라우저·수집기"],data.downloads.map(function(r) { return [time(r.at),r.ip,r.path,r.status,bytes(r.bytes),r.agent]; }));
    table("activity-players","캐릭터별 기간 전체 집계",["캐릭터","접속","종료","접속 시간","처치","맵 입장","행동 요청","경험치 변화","금화 변화","최근 기록"],data.players.map(function(r) { return [r.player+(r.bot?" (봇)":""),number(r.logins),number(r.logouts),(r.seconds/60).toFixed(1)+"분",number(r.kills),number(r.maps),number(r.actions),number(r.xp),number(r.gold),time(r.lastAt)]; }));
    document.getElementById("activity-observation").textContent="확정 접속 관측 시작 "+(data.analyticsSince||"기록 없음")+" · 최근 접속 확인 "+time(data.current.lastObserved)+" · "+(data.current.stale?"90초 이상 확인 지연, 현재 인원은 확정할 수 없습니다.":"현재 인원은 날짜와 별개로 최근 접속 확인 기준입니다.")+" 시간별 최대는 접속 구간을 기준으로 계산합니다. 재방문·잔존율은 보관된 90일 안의 첫 관측 접속 기준으로 신규 가입률과 다릅니다. D1/7/30은 해당 날짜에 다시 접속한 비율이며 아직 그 날짜가 끝나지 않은 계정은 분모에서 제외합니다.";
    table("activity-concurrency","한국 시각 시간별 최대 동시 접속 · 중복 계정 제외",["시각","최대 캐릭터"],data.concurrency.map(function(r) { return [time(r.hour),number(r.peak)]; }));
    table("activity-returning","날짜별 재방문",["날짜","활동 캐릭터","첫 관측","재방문"],data.returning.map(function(r) { return [r.day,number(r.players),number(r.firstObserved),number(r.returning)]; }));
    table("activity-retention","선택 기간 첫 관측 코호트 · 전체 관측일까지 측정",["지표","코호트","관측 성숙 계정","재접속 계정","비율"],data.retention.map(function(r) { return ["D"+r.days,number(r.cohort),number(r.eligible),number(r.returned),r.rate==null?"자료 부족":r.rate+"%"]; }));
    table("activity-devices","기간 내 기기 환경 최대 200개 · 앱 자기보고",["캐릭터","설치 식별자","플랫폼","모델","OS","앱 버전","보고 횟수","최근 보고"],data.devices.map(function(r) { var m=r.meta;return [r.player,m.install,m.platform,m.model,m.os,m.version,number(r.logins),time(r.lastAt)]; }));
    table("activity-diagnostics","최근 실패·오류 최대 200건",["서버 기록 시각","앱 보고 시각","캐릭터","IP","구분","사유·화면","인증 전 앱 기록"],data.diagnostics.map(function(r) { return [time(r.at),time(r.meta.occurredAt),r.player,r.ip,names[r.kind]||r.kind,metadata(r)||r.detail,r.meta.beforeLogin?"예":"—"]; }));
    table("activity-screens","화면별 보고 최대 200개",["캐릭터","화면","보고 횟수","이용 시간"],data.screens.map(function(r) { return [r.player,r.screen,number(r.count),minutes(r.seconds)]; }));
    table("activity-buttons","버튼별 보고 최대 200개",["캐릭터","화면","버튼 코드","누른 횟수"],data.buttons.map(function(r) { return [r.player,r.screen,r.action,number(r.count)]; }));
    table("activity-exits","앱 상태·이탈 지점 최대 200개",["캐릭터","마지막 화면","상태","오류 종류","횟수"],data.exits.map(function(r) { return [r.player,r.screen,r.action,r.error,number(r.count)]; }));
    table("activity-economy","재화·사유별 집계",["캐릭터","재화","아이템","사유","획득","소비","순변화","이동·관측 수량","원장 행"],data.economy.map(function(r) { return [r.player,assets[r.asset]||r.asset,r.item,r.reason,number(r.gained),number(r.spent),number(r.net),number(r.quantity),number(r.transactions)]; }));
    table("activity-ledger","최근 재화 원장 최대 200건",["시각","캐릭터","재화","아이템","수량","변화","잔액·잔량","이동","사유","거래 ID"],data.ledger.map(function(r) { var m=r.meta;return [time(r.at),r.player,assets[m.asset]||m.asset,m.item,m.quantity,m.delta,m.balance,(m.from||"—")+" → "+(m.to||"—"),m.reason,m.transaction]; }));
    table("activity-saved","현재 캐릭터 저장 자료",["캐릭터","레벨","경험치","금화","맵","누적 처치","마지막 저장된 종료"],data.savedCharacters.map(function(r) { return [r.player+(r.bot?" (봇)":""),number(r.level),number(r.xp),number(r.gold),r.map,number(r.kills),time(r.lastLogout)]; }));
    var monsters=[];data.savedCharacters.forEach(function(r) { r.monsters.forEach(function(m) { monsters.push([r.player,m.monster,number(m.count),time(m.lastAt)]); }); });
    table("activity-monsters","괴물별 누적 처치",["캐릭터","괴물","누적 처치","최근 처치"],monsters);
    table("activity-actions","기간 전체 행동 집계",["행동","횟수"],data.actions.map(function(r) { return [names[r.kind]||r.kind,number(r.count)]; }));
    table("activity-events","최근 게임 활동 최대 200건",["시각","캐릭터","IP","활동","횟수","경험치 변화","금화 변화","내용"],data.events.map(function(r) { return [time(r.at),r.player+(r.bot?" (봇)":""),r.ip,names[r.kind]||r.kind,number(r.amount),number(r.xp),number(r.gold),r.detail+(Object.keys(r.meta||{}).length?" · "+metadata(r):"")]; }));
    results.hidden=false;
  }
  function load() {
    if (!window.LOD_SIGNED_IN || location.protocol === "file:") { clear();status.textContent="관리자 로그인 후 조회할 수 있습니다.";return; }
    var request = ++sequence;
    button.disabled=true;status.textContent="기록을 읽는 중입니다…";results.hidden=true;
    var query = new URLSearchParams({from:document.getElementById("activity-from").value,to:document.getElementById("activity-to").value,player:document.getElementById("activity-player").value.trim(),bots:document.getElementById("activity-bots").checked?"1":"0"});
    fetch("/api/activity?"+query,{cache:"no-store"}).then(function(response) {
      return response.json().then(function(data) { if (!response.ok) throw new Error(data.error||"기록을 읽지 못했습니다.");return data; });
    }).then(function(data) {
      if (request !== sequence || !window.LOD_SIGNED_IN) return;
      render(data);
      status.textContent="마지막 수집 "+time(data.collection.collectedAt)+" · 30초마다 수집 · "+(data.collection.errors.length?data.collection.errors.join(" / "):"수집 오류 없음");
    }).catch(function(error) { if (request===sequence) { clear();status.textContent=error.message; } }).finally(function() { button.disabled=false; });
  }
  form.addEventListener("submit",function(event) { event.preventDefault();load(); });
  document.addEventListener("lod-session",function() { if (!window.LOD_SIGNED_IN) {clear();status.textContent="관리자 로그인 후 조회할 수 있습니다.";} else if (!panel.hidden) load(); });
  window.LodDashboard.onViewShown(function(view) { if (view==="activity") load(); });
  if (!panel.hidden) load();
})();
