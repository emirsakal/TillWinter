// Browser glue for the web build only (GitHub Pages). Each function is a no-op where the browser lacks the feature.
mergeInto(LibraryManager.library, {
  // Copies text to the clipboard. The modern API first; the hidden-textarea copy for browsers or pages without it.
  // Returns 1 when the text is on the clipboard, 0 when the browser refused (the caller then shows the code).
  TW_CopyText: function (ptr) {
    var text = UTF8ToString(ptr);
    var fallback = function () {
      try {
        var area = document.createElement("textarea");
        area.value = text;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.select();
        var ok = document.execCommand("copy");
        document.body.removeChild(area);
        return ok ? 1 : 0;
      } catch (e) { return 0; }
    };
    // execCommand answers at once; the async API is tried after it so a refusal never loses the copy.
    var done = fallback();
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).catch(function () {});
    return done || (navigator.clipboard ? 1 : 0);
  },

  // Asks the player to paste a farm code (reading the clipboard silently needs a permission most browsers deny).
  // Returns the text, or an empty string when cancelled.
  TW_PromptText: function (ptr) {
    var answer = window.prompt(UTF8ToString(ptr), "") || "";
    var size = lengthBytesUTF8(answer) + 1;
    var buffer = _malloc(size);
    stringToUTF8(answer, buffer, size);
    return buffer;
  },

  // Shows a farm code so it can be copied by hand when the clipboard refused it.
  TW_ShowText: function (titlePtr, textPtr) {
    window.prompt(UTF8ToString(titlePtr), UTF8ToString(textPtr));
  },

  // A short buzz where the browser has one (Android Chrome); silently nothing elsewhere (iOS Safari, desktop).
  TW_Vibrate: function (ms) {
    try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) {}
  },

  // Flushes the save folder to the browser's IndexedDB, so a farm written just before the tab closes survives it.
  TW_SyncFiles: function () {
    try { FS.syncfs(false, function () {}); } catch (e) {}
  }
});
