let appConfig = window.appConfig || { baseUrl: "" };

const baseApiRoutes = {
    products: {
        list: "/api/products",
        show: "/api/products/{id}",
        store: "/api/products",
        update: "/api/products/{id}",
        delete: "/api/products/{id}"
    }
};

function prependBaseUrl(routes, baseUrl) {
    const result = {};
    for (const [key, value] of Object.entries(routes)) {
        if (typeof value === "string") {
            result[key] = baseUrl + value;
        } else if (typeof value === "object" && value !== null) {
            result[key] = prependBaseUrl(value, baseUrl);
        } else {
            result[key] = value;
        }
    }
    return result;
}

const apiRoutes = prependBaseUrl(baseApiRoutes, appConfig.baseUrl || "");

if (typeof window !== "undefined") {
    window.apiRoutes = apiRoutes;
}

export default apiRoutes;
