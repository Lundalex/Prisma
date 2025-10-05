mergeInto(LibraryManager.library, {
  WF_IsViewportLikelyFullscreen: function () {
    try {
      var w = (typeof window !== 'undefined') ? window.innerWidth  : 0;
      var h = (typeof window !== 'undefined') ? window.innerHeight : 0;

      // screen.* can vary by browser; take the max available to be robust
      var sw = (typeof screen !== 'undefined') ? (screen.width  || screen.availWidth  || 0) : 0;
      var sh = (typeof screen !== 'undefined') ? (screen.height || screen.availHeight || 0) : 0;

      // Allow tiny UI bars / rounding
      var eps = 2;
      return ((w >= sw - eps) && (h >= sh - eps)) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }
});