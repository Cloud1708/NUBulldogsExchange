window.nubeDownloadTextFile = function (fileName, content, mimeType) {
    const blob = new Blob([content], { type: mimeType || "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName || "download.txt";
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
};

window.nubeSetBodyScrollLock = function (locked) {
    document.documentElement.style.overflow = locked ? "hidden" : "";
    document.body.style.overflow = locked ? "hidden" : "";
};

window.nubeGetRect = function (el) {
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return {
        top: r.top,
        left: r.left,
        bottom: r.bottom,
        right: r.right,
        width: r.width,
        height: r.height,
        viewportHeight: window.innerHeight || document.documentElement.clientHeight || 0,
        viewportWidth: window.innerWidth || document.documentElement.clientWidth || 0
    };
};

window.nubeGetRectBySelector = function (selector) {
    const el = document.querySelector(selector);
    return window.nubeGetRect(el);
};

window.nubeScrollIntoView = function (el) {
    if (!el || typeof el.scrollIntoView !== "function") return;
    el.scrollIntoView({ behavior: "smooth", block: "start" });
};

window.nubeFocusById = function (id) {
    const el = document.getElementById(id);
    if (el && typeof el.focus === "function") el.focus();
};
