// Ctrl S Saves And F5 Reloads Through The Visible Buttons
const isTyping = (target) =>
    target instanceof HTMLElement &&
    (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));

const clickShortcut = (name) => document.querySelector(`[data-shortcut="${name}"]:not([disabled])`)?.click();

document.addEventListener("keydown", (event) => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
        event.preventDefault();
        // Fields Commit On Blur, Before The Save Runs
        document.activeElement?.blur?.();
        clickShortcut("save");
    } else if (event.key === "F5" && !isTyping(event.target)) {
        event.preventDefault();
        clickShortcut("reload");
    }
});

// Rows, Cards And Map Points Marked For It Answer Enter And Space
document.addEventListener("keydown", (event) => {
    if ((event.key === "Enter" || event.key === " ") && event.target instanceof HTMLElement && event.target.hasAttribute("data-keyclick")) {
        event.preventDefault();
        event.target.click();
    }
});

// A Dropped Folder Opens, The Host Reads Its Path
document.addEventListener("dragover", (event) => event.preventDefault());
document.addEventListener("drop", (event) => {
    event.preventDefault();
    if (event.dataTransfer?.files.length && window.chrome?.webview) {
        window.chrome.webview.postMessageWithAdditionalObjects("SavegameDropped", event.dataTransfer.files);
    }
});

// Pan And Zoom Stay In The Page, Blazor Hears Clicks
window.sotfMap = (() => {
    const mapSize = 4096;
    const maxZoom = 2;
    const dragThreshold = 4;
    let state = null;

    const apply = () => {
        state.canvas.style.transform = `translate(${state.x}px, ${state.y}px) scale(${state.zoom})`;
        state.canvas.style.setProperty("--map-zoom", state.zoom);
    };

    const zoomAt = (pointX, pointY, newZoom) => {
        const zoom = Math.min(Math.max(newZoom, state.minZoom), maxZoom);
        state.x = pointX - ((pointX - state.x) * zoom) / state.zoom;
        state.y = pointY - ((pointY - state.y) * zoom) / state.zoom;
        state.zoom = zoom;
        apply();
    };

    // The Whole Map Fits, Centred
    const fit = () => {
        const { viewport } = state;
        state.zoom = Math.min(viewport.clientWidth, viewport.clientHeight) / mapSize;
        state.minZoom = state.zoom / 2;
        state.x = (viewport.clientWidth - mapSize * state.zoom) / 2;
        state.y = (viewport.clientHeight - mapSize * state.zoom) / 2;
        apply();
    };

    const init = (viewport, canvas) => {
        state = { viewport, canvas, drag: null, moved: false };
        fit();

        viewport.addEventListener(
            "wheel",
            (event) => {
                event.preventDefault();
                const bounds = viewport.getBoundingClientRect();
                zoomAt(event.clientX - bounds.left, event.clientY - bounds.top, state.zoom * (event.deltaY < 0 ? 1.2 : 1 / 1.2));
            },
            { passive: false }
        );

        // Arrow Keys Step Through The Points, One Tab Stop For The Whole Map
        viewport.addEventListener("keydown", (event) => {
            const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[event.key];
            if (!step) return;
            event.preventDefault();
            const points = [...canvas.querySelectorAll(".map-poi[data-keyclick]")];
            if (points.length === 0) return;
            const current = points.indexOf(document.activeElement);
            const next = current < 0 ? (step > 0 ? 0 : points.length - 1) : (current + step + points.length) % points.length;
            points[next].focus({ preventScroll: true });
            panTo(parseFloat(points[next].style.left), parseFloat(points[next].style.top));
        });

        // Icons Are Images, The Browser Would Drag Them Away
        viewport.addEventListener("dragstart", (event) => event.preventDefault());

        viewport.addEventListener("pointerdown", (event) => {
            if (event.button !== 0) return;
            state.drag = { startX: event.clientX, startY: event.clientY, x: state.x, y: state.y };
            state.moved = false;
        });

        // A Drag Ending On An Icon Is No Click
        viewport.addEventListener(
            "click",
            (event) => {
                if (state.moved) {
                    event.stopPropagation();
                    state.moved = false;
                }
            },
            true
        );
    };

    // Registered Once, Every Opened Map Shares Them
    window.addEventListener("pointermove", (event) => {
        // A Map That Left The Page Lets Its Elements Go
        if (state && !state.viewport.isConnected) state = null;
        if (!state?.drag) return;
        const deltaX = event.clientX - state.drag.startX;
        const deltaY = event.clientY - state.drag.startY;
        if (!state.moved && Math.hypot(deltaX, deltaY) < dragThreshold) return;
        state.moved = true;
        state.viewport.classList.add("dragging");
        state.x = state.drag.x + deltaX;
        state.y = state.drag.y + deltaY;
        apply();
    });

    // A Cancelled Pointer Ends The Drag Too, Or The Map Keeps Following
    const endDrag = () => {
        if (!state) return;
        state.drag = null;
        state.viewport.classList.remove("dragging");
    };
    window.addEventListener("pointerup", endDrag);
    window.addEventListener("pointercancel", endDrag);

    const centerOn = (mapX, mapY) => {
        if (!state) return;
        state.zoom = maxZoom / 2;
        state.x = state.viewport.clientWidth / 2 - mapX * state.zoom;
        state.y = state.viewport.clientHeight / 2 - mapY * state.zoom;
        apply();
    };

    const panTo = (mapX, mapY) => {
        state.x = state.viewport.clientWidth / 2 - mapX * state.zoom;
        state.y = state.viewport.clientHeight / 2 - mapY * state.zoom;
        apply();
    };

    const zoomBy = (factor) => {
        if (!state) return;
        zoomAt(state.viewport.clientWidth / 2, state.viewport.clientHeight / 2, state.zoom * factor);
    };

    const reset = () => {
        if (!state) return;
        fit();
    };

    return { init, centerOn, zoomBy, reset };
})();
