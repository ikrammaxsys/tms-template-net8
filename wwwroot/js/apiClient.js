(function (global) {
    "use strict";

    function normalizeAppUrl(url) {
        if (typeof url !== "string") return url;
        if (/^https?:\/\//i.test(url)) return url;
        var raw = global.appConfig && global.appConfig.baseUrl ? String(global.appConfig.baseUrl) : "";
        var base = raw.replace(/\/$/, "");
        if (!base || url.charAt(0) !== "/") return url;
        if (url === base || url.indexOf(base + "/") === 0) return url;
        return base + url;
    }

    function resolveUrl(url, params) {
        if (!params || typeof url !== "string") return url;
        var resolved = url;
        Object.keys(params).forEach(function (key) {
            resolved = resolved.replace(new RegExp("\\{" + key + "\\}", "g"), encodeURIComponent(params[key]));
        });
        return resolved;
    }

    function request(options) {
        var url = normalizeAppUrl(resolveUrl(options.url, options.params));
        var method = (options.method || "GET").toUpperCase();
        var body = options.body;
        var headers = Object.assign({ "Content-Type": "application/json" }, options.headers || {});

        if (body !== undefined && body !== null && typeof body === "object" && !(body instanceof FormData)) {
            body = JSON.stringify(body);
        }

        var fetchOptions = { method: method, headers: headers };
        if (body !== undefined && method !== "GET") fetchOptions.body = body;

        return fetch(url, fetchOptions).then(function (response) {
            var contentType = response.headers.get("Content-Type") || "";
            var isJson = contentType.indexOf("application/json") !== -1;
            return (isJson ? response.json() : response.text()).then(function (data) {
                return { ok: response.ok, status: response.status, data: data };
            });
        }).catch(function (err) {
            return { ok: false, status: 0, data: null, error: err };
        });
    }

    global.apiClient = {
        request: request,
        get: function (url, params) { return request({ url: url, method: "GET", params: params }); },
        post: function (url, body, params) { return request({ url: url, method: "POST", body: body, params: params }); },
        put: function (url, body, params) { return request({ url: url, method: "PUT", body: body, params: params }); },
        delete: function (url, params) { return request({ url: url, method: "DELETE", params: params }); }
    };
})(typeof window !== "undefined" ? window : this);
