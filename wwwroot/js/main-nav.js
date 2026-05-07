(function () {
    function normalizeWithBase(url) {
        var base = (window.appConfig && window.appConfig.baseUrl ? window.appConfig.baseUrl : "").replace(/\/$/, "");
        if (!base) return url;
        if (!url || url.charAt(0) !== "/") return url;
        if (url === base || url.indexOf(base + "/") === 0) return url;
        return base + url;
    }

    window.spaNavigate = function (url) {
        window.location.href = normalizeWithBase(url);
    };

    // Notify module scripts that main content is mounted.
    window.dispatchEvent(new CustomEvent("app:main-mounted"));
})();
