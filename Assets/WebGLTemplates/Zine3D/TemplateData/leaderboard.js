(function () {
  "use strict";

  var apiBase = String(window.ZINE_LEADERBOARD_API || "").replace(/\/$/, "");
  var modal = document.querySelector("#leaderboard-modal");
  var openButton = document.querySelector("#leaderboard-button");
  var closeButton = document.querySelector("#leaderboard-close");
  var backdrop = document.querySelector("#leaderboard-backdrop");
  var nicknameForm = document.querySelector("#nickname-form");
  var nicknameInput = document.querySelector("#nickname-input");
  var nicknameSave = document.querySelector("#nickname-save");
  var status = document.querySelector("#leaderboard-status");
  var runSummary = document.querySelector("#leaderboard-run");
  var rows = document.querySelector("#leaderboard-rows");
  var pendingScore = null;
  var manuallyPaused = false;
  var leaderboardEnabled = true;
  var credentials = readCredentials();

  installBrowserFixes();
  setEnabled(true);

  if (credentials && credentials.nickname) {
    nicknameInput.value = credentials.nickname;
  }

  function isIOSWebKit() {
    var ua = navigator.userAgent || "";
    return /iPhone|iPad|iPod/i.test(ua) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
  }

  function installBrowserFixes() {
    var style = document.createElement("style");
    style.textContent = [
      "#leaderboard-button:disabled{opacity:.42;filter:grayscale(.55);cursor:not-allowed;pointer-events:none}",
      "html.ios-webkit #unity-container,html.ios-webkit.force-landscape #unity-container,html.ios-webkit.fullscreen-requested #unity-container{position:fixed!important;inset:0!important;top:0!important;left:0!important;width:100dvw!important;height:100dvh!important;transform:none!important;transform-origin:center!important}",
      "html.ios-webkit #unity-canvas,html.ios-webkit.force-landscape #unity-canvas,html.ios-webkit #unity-canvas.is-landscape-fullscreen{position:absolute!important;inset:0!important;width:100%!important;height:100%!important;max-width:none!important;max-height:none!important;transform:none!important;margin:0!important}",
      "#ios-landscape-gate{display:none;position:fixed;z-index:120;inset:0;place-items:center;padding:28px;background:#03150a;color:#fff;text-align:center;font:800 20px/1.45 -apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}",
      "#ios-landscape-gate small{display:block;margin-top:10px;color:rgba(255,255,255,.7);font-size:14px;font-weight:600}",
      "html.ios-webkit.ios-portrait #ios-landscape-gate{display:grid}"
    ].join("");
    document.head.appendChild(style);

    if (!isIOSWebKit()) return;

    document.documentElement.classList.add("ios-webkit");
    var gate = document.createElement("div");
    gate.id = "ios-landscape-gate";
    gate.innerHTML = "아이폰을 가로로 돌려주세요<small>iPhone 브라우저에서는 가로 화면에서 터치 좌표를 정확하게 맞춰 실행합니다.</small>";
    document.body.appendChild(gate);

    function syncIOSViewport() {
      var portrait = window.innerHeight > window.innerWidth;
      document.documentElement.classList.toggle("ios-portrait", portrait);
      document.documentElement.classList.remove("force-landscape");
      document.documentElement.classList.remove("fullscreen-requested");
      var canvas = document.querySelector("#unity-canvas");
      if (canvas) canvas.classList.remove("is-landscape-fullscreen");

      var container = document.querySelector("#unity-container");
      if (container && window.visualViewport) {
        container.style.width = window.visualViewport.width + "px";
        container.style.height = window.visualViewport.height + "px";
        container.style.left = window.visualViewport.offsetLeft + "px";
        container.style.top = window.visualViewport.offsetTop + "px";
      }
    }

    syncIOSViewport();
    window.addEventListener("resize", syncIOSViewport, {passive: true});
    window.addEventListener("orientationchange", function () {
      window.setTimeout(syncIOSViewport, 80);
      window.setTimeout(syncIOSViewport, 350);
    }, {passive: true});
    if (window.visualViewport) {
      window.visualViewport.addEventListener("resize", syncIOSViewport, {passive: true});
      window.visualViewport.addEventListener("scroll", syncIOSViewport, {passive: true});
    }
  }

  function setEnabled(enabled) {
    leaderboardEnabled = !!enabled;
    openButton.disabled = !leaderboardEnabled;
    openButton.setAttribute("aria-disabled", leaderboardEnabled ? "false" : "true");
    openButton.title = leaderboardEnabled ? "랭킹 보기" : "플레이 중에는 일시정지 후 랭킹을 볼 수 있습니다.";
  }

  function readCredentials() {
    try {
      var stored = window.localStorage.getItem("zineLeaderboardPlayer");
      return stored ? JSON.parse(stored) : null;
    } catch (_error) {
      return null;
    }
  }

  function writeCredentials(value) {
    credentials = value;
    try { window.localStorage.setItem("zineLeaderboardPlayer", JSON.stringify(value)); } catch (_error) {}
  }

  function clearCredentials() {
    credentials = null;
    try { window.localStorage.removeItem("zineLeaderboardPlayer"); } catch (_error) {}
  }

  function formatDuration(milliseconds) {
    var totalSeconds = Math.max(0, Math.floor(Number(milliseconds || 0) / 1000));
    var hours = Math.floor(totalSeconds / 3600);
    var minutes = Math.floor((totalSeconds % 3600) / 60);
    var seconds = totalSeconds % 60;
    if (hours > 0) {
      return [hours, minutes, seconds].map(function (part) { return String(part).padStart(2, "0"); }).join(":");
    }
    return [minutes, seconds].map(function (part) { return String(part).padStart(2, "0"); }).join(":");
  }

  function setStatus(message, kind) {
    status.textContent = message || "";
    status.className = kind || "";
  }

  async function api(path, options) {
    if (!apiBase) throw new Error("랭킹 서버 주소가 설정되지 않았습니다.");
    var response = await fetch(apiBase + path, options || {});
    var data = await response.json().catch(function () { return {}; });
    if (!response.ok) {
      var error = new Error(data.error || "랭킹 서버 요청에 실패했습니다.");
      error.status = response.status;
      throw error;
    }
    return data;
  }

  function authorizationHeaders() {
    return {"Authorization": "Bearer " + credentials.edit_token, "Content-Type": "application/json"};
  }

  async function loadLeaderboard() {
    rows.innerHTML = '<tr><td colspan="4" class="leaderboard-empty">랭킹을 불러오는 중…</td></tr>';
    try {
      var data = await api("/api/v1/leaderboard?limit=10");
      if (!data.entries || !data.entries.length) {
        rows.innerHTML = '<tr><td colspan="4" class="leaderboard-empty">첫 기록의 주인공이 되어보세요.</td></tr>';
        return;
      }
      rows.textContent = "";
      data.entries.forEach(function (entry) {
        var row = document.createElement("tr");
        ["#" + entry.rank, entry.nickname, String(entry.body_count), formatDuration(entry.survival_ms)].forEach(function (value, index) {
          var cell = document.createElement("td");
          cell.textContent = value;
          if (index === 0) cell.className = "leaderboard-rank";
          row.appendChild(cell);
        });
        rows.appendChild(row);
      });
    } catch (error) {
      rows.innerHTML = '<tr><td colspan="4" class="leaderboard-empty leaderboard-error">랭킹을 불러오지 못했습니다.</td></tr>';
      setStatus(error.message, "error");
    }
  }

  async function createPlayer(nickname) {
    var player = await api("/api/v1/players", {method: "POST", headers: {"Content-Type": "application/json"}, body: JSON.stringify({nickname: nickname})});
    writeCredentials(player);
    nicknameInput.value = player.nickname;
    return player;
  }

  async function saveNickname(nickname) {
    if (!credentials || !credentials.edit_token) return createPlayer(nickname);
    try {
      var updated = await api("/api/v1/players/me", {method: "PATCH", headers: authorizationHeaders(), body: JSON.stringify({nickname: nickname})});
      writeCredentials({player_id: credentials.player_id, edit_token: credentials.edit_token, nickname: updated.nickname});
      nicknameInput.value = updated.nickname;
      return updated;
    } catch (error) {
      if (error.status !== 401) throw error;
      clearCredentials();
      return createPlayer(nickname);
    }
  }

  async function submitPendingScore() {
    if (!pendingScore || !credentials || !credentials.edit_token) return;
    await api("/api/v1/scores", {method: "POST", headers: authorizationHeaders(), body: JSON.stringify(pendingScore)});
  }

  function showModal() {
    modal.classList.add("is-visible");
    modal.setAttribute("aria-hidden", "false");
    loadLeaderboard();
  }

  function closeModal() {
    modal.classList.remove("is-visible");
    modal.setAttribute("aria-hidden", "true");
    if (manuallyPaused && window.zineUnityInstance) {
      window.zineUnityInstance.SendMessage("Head", "ResumeAfterLeaderboard");
    }
    manuallyPaused = false;
  }

  async function showGameOver(bodyCount, survivalMs) {
    setEnabled(true);
    manuallyPaused = false;
    pendingScore = {body_count: Number(bodyCount), survival_ms: Number(survivalMs)};
    runSummary.hidden = false;
    runSummary.textContent = "이번 기록 · 코인 " + pendingScore.body_count + " · " + formatDuration(pendingScore.survival_ms);
    nicknameSave.textContent = credentials ? "닉네임 수정" : "기록 등록";
    showModal();

    if (credentials && credentials.edit_token) {
      setStatus("기록을 등록하는 중…");
      try {
        await submitPendingScore();
        setStatus("최고 기록을 확인했습니다.", "success");
        await loadLeaderboard();
      } catch (error) {
        if (error.status === 401) {
          clearCredentials();
          nicknameSave.textContent = "기록 등록";
          setStatus("닉네임을 입력해 기록을 등록하세요.");
        } else setStatus(error.message, "error");
      }
    } else {
      setStatus("닉네임을 입력해 이번 기록을 등록하세요.");
      window.setTimeout(function () { nicknameInput.focus(); }, 100);
    }
  }

  openButton.addEventListener("click", function () {
    if (!leaderboardEnabled) return;
    pendingScore = null;
    runSummary.hidden = true;
    setStatus("");
    manuallyPaused = true;
    if (window.zineUnityInstance) window.zineUnityInstance.SendMessage("Head", "PauseForLeaderboard");
    showModal();
  });

  closeButton.addEventListener("click", closeModal);
  backdrop.addEventListener("click", closeModal);
  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && modal.classList.contains("is-visible")) closeModal();
  });

  nicknameForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    nicknameSave.disabled = true;
    setStatus("저장하는 중…");
    try {
      var result = await saveNickname(nicknameInput.value);
      if (pendingScore) await submitPendingScore();
      nicknameSave.textContent = "닉네임 수정";
      setStatus(result.nickname === "**" || result.nickname.indexOf("**") >= 0 ? "비속어가 감지되어 ** 처리했습니다. 기록은 등록됐습니다." : "닉네임과 기록을 저장했습니다.", "success");
      await loadLeaderboard();
    } catch (error) {
      setStatus(error.message, "error");
    } finally {
      nicknameSave.disabled = false;
    }
  });

  window.ZineLeaderboard = {
    showGameOver: showGameOver,
    refresh: loadLeaderboard,
    setEnabled: setEnabled
  };
})();
