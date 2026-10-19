// WebGL-only bridge: raw IndexedDB access plus publishing results to the DOM so they can be read from outside the canvas.
// Operations are polled from C# (no native-to-managed callbacks) to keep the spike simple.
mergeInto(LibraryManager.library, {
  SpikeIdb_Init: function () {
    if (Module.spikeIdb) { return; }
    Module.spikeIdb = { ops: {}, next: 1, db: null };
  },

  SpikeIdb_Put: function (keyPtr, dataPtr, length) {
    var s = Module.spikeIdb;
    var key = UTF8ToString(keyPtr);
    var bytes = HEAPU8.slice(dataPtr, dataPtr + length);
    var id = s.next++;
    var op = s.ops[id] = { state: 0, size: -1, data: null };
    var open = indexedDB.open('TransportTycoonSpike', 1);
    open.onupgradeneeded = function () { open.result.createObjectStore('saves'); };
    open.onerror = function () { op.state = -1; };
    open.onsuccess = function () {
      var tx = open.result.transaction('saves', 'readwrite');
      tx.objectStore('saves').put(bytes, key);
      tx.oncomplete = function () { op.state = 1; open.result.close(); };
      tx.onerror = function () { op.state = -1; };
      tx.onabort = function () { op.state = -1; };
    };
    return id;
  },

  SpikeIdb_Get: function (keyPtr) {
    var s = Module.spikeIdb;
    var key = UTF8ToString(keyPtr);
    var id = s.next++;
    var op = s.ops[id] = { state: 0, size: -1, data: null };
    var open = indexedDB.open('TransportTycoonSpike', 1);
    open.onupgradeneeded = function () { open.result.createObjectStore('saves'); };
    open.onerror = function () { op.state = -1; };
    open.onsuccess = function () {
      var req = open.result.transaction('saves', 'readonly').objectStore('saves').get(key);
      req.onsuccess = function () {
        if (req.result) { op.data = new Uint8Array(req.result); op.size = op.data.length; }
        op.state = 1;
        open.result.close();
      };
      req.onerror = function () { op.state = -1; };
    };
    return id;
  },

  SpikeIdb_State: function (id) {
    return Module.spikeIdb.ops[id].state;
  },

  SpikeIdb_ResultSize: function (id) {
    return Module.spikeIdb.ops[id].size;
  },

  SpikeIdb_ReadResult: function (id, destPtr, length) {
    var op = Module.spikeIdb.ops[id];
    HEAPU8.set(op.data.subarray(0, length), destPtr);
    delete Module.spikeIdb.ops[id];
  },

  SpikeIdb_Release: function (id) {
    delete Module.spikeIdb.ops[id];
  },

  SpikeWeb_Publish: function (textPtr) {
    var text = UTF8ToString(textPtr);
    window.__spikeResults = text;
    var el = document.getElementById('spike-results');
    if (!el) {
      el = document.createElement('pre');
      el.id = 'spike-results';
      el.style.cssText = 'position:fixed;left:0;bottom:0;max-height:30vh;overflow:auto;margin:0;padding:4px;background:#000c;color:#0f0;font-size:11px;z-index:9';
      document.body.appendChild(el);
    }
    el.textContent = text;
  },

  SpikeWeb_GetQuery: function () {
    var text = window.location.search || '';
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },
});
