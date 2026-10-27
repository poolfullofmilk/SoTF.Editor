// Rows, Cards And Map Points Marked For It Answer Enter And Space
document.addEventListener("keydown", (event) => {
    if ((event.key === "Enter" || event.key === " ") && event.target instanceof HTMLElement && event.target.hasAttribute("data-keyclick")) {
        event.preventDefault();
        event.target.click();
    }
});

// An Info Icon Inside A Switch Label Never Toggles The Switch
document.addEventListener("click", (event) => {
    if (event.target instanceof Element && event.target.closest(".info-icon")) {
        event.preventDefault();
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
    const manyPoints = 1500;
    const glideTime = 250;
    let state = null;
    let settle = 0;
    let frame = 0;

    const scaleIcons = () => {
        if (state && state.iconZoom !== state.zoom) {
            state.iconZoom = state.zoom;
            state.canvas.style.setProperty("--map-zoom", state.zoom);
        }
    };

    // Thousands Of Icons Restyle Slowly, So They Catch Up Once The Map Settles
    const apply = (moving = false) => {
        state.canvas.style.transform = `translate(${state.x}px, ${state.y}px) scale(${state.zoom})`;
        clearTimeout(settle);
        if (moving && state.canvas.childElementCount > manyPoints) {
            settle = setTimeout(scaleIcons, 150);
        } else {
            scaleIcons();
        }
    };

    const stop = () => {
        cancelAnimationFrame(frame);
        state.target = null;
    };

    // Eases To A View, Zooming About The Point Both Views Share
    const glideTo = (x, y, zoom) => {
        stop();
        const from = { x: state.x, y: state.y, zoom: state.zoom };
        const zooms = Math.abs(zoom / from.zoom - 1) > 0.001;
        const start = performance.now();
        state.target = { x, y, zoom };
        const step = (now) => {
            if (!state?.target) return;
            const progress = Math.min((now - start) / glideTime, 1);
            const eased = 1 - (1 - progress) ** 3;
            state.zoom = from.zoom * (zoom / from.zoom) ** eased;
            const share = zooms ? (from.zoom - state.zoom) / (from.zoom - zoom) : eased;
            state.x = from.x + (x - from.x) * share;
            state.y = from.y + (y - from.y) * share;
            apply(progress < 1);
            if (progress < 1) {
                frame = requestAnimationFrame(step);
            } else {
                state.target = null;
            }
        };
        frame = requestAnimationFrame(step);
    };

    // The Point Under The Cursor Stays, Quick Steps Add Up
    const zoomAt = (pointX, pointY, factor) => {
        const view = state.target ?? state;
        const zoom = Math.min(Math.max(view.zoom * factor, state.minZoom), maxZoom);
        state.fitted = false;
        glideTo(pointX - ((pointX - view.x) * zoom) / view.zoom, pointY - ((pointY - view.y) * zoom) / view.zoom, zoom);
    };

    // The Whole Map Fits, Centred
    const wholeMap = () => {
        const { viewport } = state;
        const zoom = Math.min(viewport.clientWidth, viewport.clientHeight) / mapSize;
        state.minZoom = zoom / 2;
        state.fitted = true;
        return { x: (viewport.clientWidth - mapSize * zoom) / 2, y: (viewport.clientHeight - mapSize * zoom) / 2, zoom };
    };

    const fit = (moving = false) => {
        stop();
        Object.assign(state, wholeMap());
        apply(moving);
    };

    const init = (viewport, canvas) => {
        state?.observer.disconnect();
        state = { viewport, canvas, drag: null, moved: false, width: viewport.clientWidth, height: viewport.clientHeight };
        fit();

        // Decoded Whole Up Front, The First Zoom In Has Nothing To Wait For
        canvas.querySelector(".map-image")?.decode().catch(() => {});

        // A Whole Map Stays Whole On Resize, Otherwise Its Middle Stays
        state.observer = new ResizeObserver(() => {
            if (!viewport.isConnected || state?.viewport !== viewport) return;
            if (state.fitted) {
                fit(true);
            } else {
                stop();
                state.x += (viewport.clientWidth - state.width) / 2;
                state.y += (viewport.clientHeight - state.height) / 2;
                apply(true);
            }
            state.width = viewport.clientWidth;
            state.height = viewport.clientHeight;
        });
        state.observer.observe(viewport);

        // A Wheel Notch Zooms By A Fifth, A Touchpad By Its Little Steps
        viewport.addEventListener(
            "wheel",
            (event) => {
                event.preventDefault();
                const bounds = viewport.getBoundingClientRect();
                const delta = Math.min(Math.max(event.deltaY, -200), 200);
                zoomAt(event.clientX - bounds.left, event.clientY - bounds.top, 1.2 ** (-delta / 100));
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
            stop();
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
        if (state && !state.viewport.isConnected) {
            state.observer.disconnect();
            state = null;
        }
        if (!state?.drag) return;
        const deltaX = event.clientX - state.drag.startX;
        const deltaY = event.clientY - state.drag.startY;
        if (!state.moved && Math.hypot(deltaX, deltaY) < dragThreshold) return;
        state.moved = true;
        state.fitted = false;
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

    // Show On Map Opens Right There, No Glide
    const centerOn = (mapX, mapY) => {
        if (!state) return;
        stop();
        state.fitted = false;
        state.zoom = maxZoom / 2;
        state.x = state.viewport.clientWidth / 2 - mapX * state.zoom;
        state.y = state.viewport.clientHeight / 2 - mapY * state.zoom;
        apply();
    };

    const panTo = (mapX, mapY) => {
        const zoom = state.target?.zoom ?? state.zoom;
        state.fitted = false;
        glideTo(state.viewport.clientWidth / 2 - mapX * zoom, state.viewport.clientHeight / 2 - mapY * zoom, zoom);
    };

    const zoomBy = (factor) => {
        if (!state) return;
        zoomAt(state.viewport.clientWidth / 2, state.viewport.clientHeight / 2, factor);
    };

    const reset = () => {
        if (!state) return;
        const { x, y, zoom } = wholeMap();
        glideTo(x, y, zoom);
    };

    return { init, centerOn, zoomBy, reset };
})();
