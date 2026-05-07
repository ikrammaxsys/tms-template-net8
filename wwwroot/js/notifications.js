function showBrowserAlert(message) {
    window.alert(message || "An error occurred.");
    return Promise.resolve();
}

export function showSuccess(message) {
    if (typeof Swal !== "undefined") {
        return Swal.fire({
            icon: "success",
            title: "Success",
            text: message || "Success.",
            confirmButtonText: "OK"
        });
    }

    return showBrowserAlert(message || "Success.");
}

export function showError(message) {
    if (typeof Swal !== "undefined") {
        return Swal.fire({
            icon: "error",
            title: "Error",
            text: message || "An error occurred.",
            confirmButtonText: "OK"
        });
    }

    return showBrowserAlert(message || "An error occurred.");
}
