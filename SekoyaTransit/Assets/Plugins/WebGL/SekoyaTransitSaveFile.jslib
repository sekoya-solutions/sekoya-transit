// Browser file transfer for the Debug download and Load from file (Issue #320).
mergeInto(LibraryManager.library, {
  $SekoyaTransitSaveFileState: {
    lastObjectUrl: null,
    activeInput: null,
    activeRequestId: 0
  },

  // Downloads length bytes at dataPointer as fileName. Returns 1 when the browser accepted the download, else 0.
  // The object URL of a download is revoked when the next download starts, never on a timer: by then the browser
  // has long resolved it, and at most one blob stays alive.
  SekoyaTransitDownloadFile__deps: ["$SekoyaTransitSaveFileState"],
  SekoyaTransitDownloadFile: function (fileNamePointer, dataPointer, length) {
    var state = SekoyaTransitSaveFileState;
    try {
      var fileName = UTF8ToString(fileNamePointer);
      var bytes = HEAPU8.slice(dataPointer, dataPointer + length);
      var url = URL.createObjectURL(new Blob([bytes], { type: "application/octet-stream" }));
      if (state.lastObjectUrl !== null) {
        URL.revokeObjectURL(state.lastObjectUrl);
      }

      state.lastObjectUrl = url;
      var link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      link.style.display = "none";
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      return 1;
    } catch (error) {
      console.error("Sekoya Transit: the download failed.", error);
      return 0;
    }
  },

  // Opens the browser file picker. Answers through SendMessage on gameObjectName, every answer prefixed with the
  // request id and "|": OnFilePicked (then the file as Base64), OnFileCancelled, or OnFileFailed (then a reason).
  // A newer request replaces an older one, whose late events are dropped.
  SekoyaTransitOpenFile__deps: ["$SekoyaTransitSaveFileState"],
  SekoyaTransitOpenFile: function (gameObjectNamePointer, requestId, maxBytes) {
    var state = SekoyaTransitSaveFileState;
    var gameObjectName = UTF8ToString(gameObjectNamePointer);
    function answer(method, text) {
      SendMessage(gameObjectName, method, requestId + "|" + text);
    }

    function isCurrent() {
      return state.activeRequestId === requestId;
    }

    function release(input) {
      if (input.parentNode !== null) {
        input.parentNode.removeChild(input);
      }

      if (state.activeInput === input) {
        state.activeInput = null;
      }
    }

    if (state.activeInput !== null) {
      release(state.activeInput);
    }

    state.activeRequestId = requestId;

    // A file picker needs a recent user click (transient activation); without one the browser ignores click()
    // silently, so report it instead of waiting forever. Browsers without the API go ahead.
    if (navigator.userActivation && !navigator.userActivation.isActive) {
      answer("OnFileFailed", "The browser blocked the file picker: no recent click.");
      return;
    }

    var input = document.createElement("input");
    input.type = "file";
    input.accept = ".sav";
    input.style.display = "none";
    state.activeInput = input;
    input.addEventListener("cancel", function () {
      release(input);
      if (isCurrent()) {
        answer("OnFileCancelled", "");
      }
    });
    input.addEventListener("change", function () {
      var file = input.files && input.files.length > 0 ? input.files[0] : null;
      release(input);
      if (!isCurrent()) {
        return;
      }

      if (file === null) {
        answer("OnFileCancelled", "");
        return;
      }

      if (file.size > maxBytes) {
        answer("OnFileFailed", "The file is larger than " + maxBytes + " bytes.");
        return;
      }

      var reader = new FileReader();
      reader.onload = function () {
        if (!isCurrent()) {
          return;
        }

        var dataUrl = String(reader.result);
        var comma = dataUrl.indexOf(",");
        answer("OnFilePicked", comma >= 0 ? dataUrl.substring(comma + 1) : "");
      };
      reader.onerror = function () {
        if (isCurrent()) {
          answer("OnFileFailed", reader.error ? String(reader.error) : "The file could not be read.");
        }
      };
      reader.readAsDataURL(file);
    });

    document.body.appendChild(input);
    try {
      input.click();
    } catch (error) {
      release(input);
      answer("OnFileFailed", String(error));
    }
  }
});
