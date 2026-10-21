mergeInto(LibraryManager.library, {
  TransportTycoonSyncFileSystem: function (gameObjectNamePointer) {
    var gameObjectName = UTF8ToString(gameObjectNamePointer);
    FS.syncfs(false, function (error) {
      if (error) {
        SendMessage(
          gameObjectName,
          "OnSynchronizationFailed",
          error.message || "Unknown filesystem synchronization error.");
      } else {
        SendMessage(gameObjectName, "OnSynchronizationSucceeded", "");
      }
    });
  }
});
