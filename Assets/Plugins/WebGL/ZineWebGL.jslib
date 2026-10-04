mergeInto(LibraryManager.library, {
  Zine_IsMobileBrowser: function () {
    var userAgentMobile = /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent || "");
    var userAgentDataMobile = navigator.userAgentData && navigator.userAgentData.mobile === true;
    var coarsePointer = window.matchMedia && window.matchMedia("(pointer: coarse)").matches;

    return userAgentMobile || userAgentDataMobile || coarsePointer ? 1 : 0;
  },

  Zine_ReloadPage: function () {
    window.location.reload();
  },

  Zine_ShowLeaderboard: function (bodyCount, survivalMs) {
    if (window.ZineLeaderboard) {
      window.ZineLeaderboard.showGameOver(bodyCount, survivalMs);
    }
  },

  Zine_SetLeaderboardEnabled: function (enabled) {
    if (window.ZineLeaderboard) {
      window.ZineLeaderboard.setEnabled(enabled === 1);
    }
  }
});
