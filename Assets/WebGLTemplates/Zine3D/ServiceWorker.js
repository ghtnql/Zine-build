#if USE_DATA_CACHING
const cacheName = {{{ JSON.stringify(COMPANY_NAME + "-" + PRODUCT_NAME + "-" + PRODUCT_VERSION + "-ui-v5") }}};
const contentToCache = [
  "./",
  "index.html",
  "manifest.webmanifest",
  "TemplateData/style.css",
  "TemplateData/leaderboard.js",
  "TemplateData/icon.png",
  "TemplateData/start-screen.png",
  "Build/{{{ LOADER_FILENAME }}}",
  "Build/{{{ FRAMEWORK_FILENAME }}}",
#if USE_THREADS
  "Build/{{{ WORKER_FILENAME }}}",
#endif
  "Build/{{{ DATA_FILENAME }}}",
  "Build/{{{ CODE_FILENAME }}}"
];
#endif

self.addEventListener("install", function (event) {
#if USE_DATA_CACHING
  event.waitUntil(
    caches.open(cacheName)
      .then(function (cache) { return cache.addAll(contentToCache); })
      .then(function () { return self.skipWaiting(); })
  );
#else
  self.skipWaiting();
#endif
});

self.addEventListener("activate", function (event) {
#if USE_DATA_CACHING
  event.waitUntil(
    caches.keys()
      .then(function (keys) {
        return Promise.all(keys.filter(function (key) { return key !== cacheName; }).map(function (key) {
          return caches.delete(key);
        }));
      })
      .then(function () { return self.clients.claim(); })
  );
#else
  event.waitUntil(self.clients.claim());
#endif
});

#if USE_DATA_CACHING
self.addEventListener("fetch", function (event) {
  if (event.request.method !== "GET") return;

  var pathname = new URL(event.request.url).pathname;
  if (pathname.endsWith("/leaderboard-config.js")) return;

  if (pathname.endsWith("/TemplateData/leaderboard.js")) {
    event.respondWith(
      fetch(event.request).then(function (response) {
        if (response && response.ok) {
          var copy = response.clone();
          caches.open(cacheName).then(function (cache) { cache.put(event.request, copy); });
        }
        return response;
      }).catch(function () { return caches.match(event.request); })
    );
    return;
  }

  if (event.request.mode === "navigate") {
    event.respondWith(
      fetch(event.request).then(function (response) {
        if (response && response.ok) {
          var copy = response.clone();
          caches.open(cacheName).then(function (cache) { cache.put(event.request, copy); });
        }
        return response;
      }).catch(function () {
        return caches.match(event.request).then(function (cached) { return cached || caches.match("index.html"); });
      })
    );
    return;
  }

  event.respondWith(
    caches.match(event.request).then(function (cached) {
      if (cached) return cached;
      return fetch(event.request).then(function (response) {
        if (!response || !response.ok) return response;
        var copy = response.clone();
        caches.open(cacheName).then(function (cache) { cache.put(event.request, copy); });
        return response;
      });
    })
  );
});
#endif
