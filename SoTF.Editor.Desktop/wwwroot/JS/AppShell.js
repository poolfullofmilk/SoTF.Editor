// Rows, Cards And Map Points Marked For It Answer Enter And Space
document.addEventListener("keydown", (event) => {
    if ((event.key === "Enter" || event.key === " ") && event.target instanceof HTMLElement && event.target.hasAttribute("data-keyclick")) {
        event.preventDefault();
        event.target.click();
    }
});

// A Cursor Menu (data-menu-at) Takes The Keyboard Like A Select
(() => {
    let opener = null;
    let menu = null;
    const itemsOf = (popover) => [...popover.querySelectorAll('[role="menuitem"]:not([aria-disabled="true"])')];

    // The Picked Item Or The First Gets Focus, Centred
    const focusPicked = (tries = 30) => {
        requestAnimationFrame(() => {
            const popover = document.querySelector(".mud-popover-open:has(.mud-menu-item)");
            if (!popover) {
                if (tries > 0) focusPicked(tries - 1);
                return;
            }
            menu = popover;
            const items = itemsOf(popover);
            const item = items.find((candidate) => candidate.querySelector(".mud-menu-item-icon")) ?? items[0];
            if (item) {
                const list = item.closest(".mud-menu-list") ?? popover;
                item.focus({ preventScroll: true });
                list.scrollTop = item.offsetTop - (list.clientHeight - item.offsetHeight) / 2;
            }
        });
    };

    // Keyboard Clicks Have No Position, Click Again Below The Button
    document.addEventListener(
        "click",
        (event) => {
            const button = event.target instanceof Element ? event.target.closest("[data-menu-at]") : null;
            if (!button) return;
            opener = button;
            if (event.isTrusted && event.detail === 0) {
                event.stopImmediatePropagation();
                event.preventDefault();
                const rect = button.getBoundingClientRect();
                button.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true, clientX: rect.left, clientY: rect.bottom }));
            }
            focusPicked();
        },
        true
    );

    // Arrows Walk Items, Enter Or Space Picks, Before MudMenu Hears
    window.addEventListener(
        "keydown",
        (event) => {
            const item = event.target instanceof Element ? event.target.closest('[role="menuitem"]') : null;
            if (!item || !menu?.classList.contains("mud-popover-open") || !menu.contains(item)) return;
            if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                event.preventDefault();
                event.stopPropagation();
                const items = itemsOf(menu);
                items[items.indexOf(item) + (event.key === "ArrowDown" ? 1 : -1)]?.focus();
            } else if (event.key === "Enter" || event.key === " " || event.key === "Escape") {
                if (event.key !== "Escape") {
                    event.preventDefault();
                    event.stopPropagation();
                    item.click();
                }
                const button = opener;
                setTimeout(() => button?.isConnected && button.focus());
            }
        },
        true
    );
})();

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
    const clusterPixels = 52;
    let state = null;
    let settle = 0;
    let frame = 0;
    let clusterTimer = 0;

    // Points Near A Group's First Point Join It, A Grid Finds Neighbours
    const cluster = () => {
        if (!state?.clusters?.isConnected) return;
        const icons = state.canvas.querySelectorAll(".map-poi[data-keyclick]:not([data-solo])");
        const groups = [];
        if (state.zoom < maxZoom * 0.99) {
            const radius = clusterPixels / state.zoom;
            const seeds = new Map();
            const cellOf = (x, y) => [Math.floor(x / radius), Math.floor(y / radius)];
            for (const icon of icons) {
                const x = parseFloat(icon.style.left);
                const y = parseFloat(icon.style.top);
                const [column, row] = cellOf(x, y);
                let home = null;
                for (let stepX = -1; stepX <= 1 && !home; stepX++) {
                    for (let stepY = -1; stepY <= 1 && !home; stepY++) {
                        home = (seeds.get(`${column + stepX},${row + stepY}`) ?? []).find((group) => Math.hypot(group.x - x, group.y - y) < radius);
                    }
                }
                if (home) {
                    home.members.push({ icon, x, y });
                } else {
                    const group = { x, y, members: [{ icon, x, y }] };
                    groups.push(group);
                    if (!seeds.has(`${column},${row}`)) seeds.set(`${column},${row}`, []);
                    seeds.get(`${column},${row}`).push(group);
                }
            }
        }
        const grouped = new Set();
        const bubbles = [];
        for (const { members } of groups) {
            if (members.length < 2) continue;
            const xs = members.map((member) => member.x);
            const ys = members.map((member) => member.y);
            const bubble = document.createElement("span");
            bubble.className = "map-cluster";
            bubble.textContent = members.length;
            bubble.style.left = `${xs.reduce((sum, value) => sum + value) / members.length}px`;
            bubble.style.top = `${ys.reduce((sum, value) => sum + value) / members.length}px`;
            bubble.style.setProperty("--cluster-size", `${Math.min(28 + Math.log2(members.length) * 4, 48)}px`);
            bubble.members = members.flatMap((member) => [member.x, member.y]);
            bubbles.push(bubble);
            members.forEach((member) => grouped.add(member.icon));
        }
        icons.forEach((icon) => icon.classList.toggle("clustered", grouped.has(icon)));
        state.clusters.replaceChildren(...bubbles);
    };

    const clusterSoon = () => {
        clearTimeout(clusterTimer);
        clusterTimer = setTimeout(cluster, 120);
    };

    // The Map Panel's Name Tag, Placed Over The Hovered Point
    const hideLabel = () => state?.label?.classList.add("idle");
    const showLabel = (point) => {
        const { label } = state;
        if (!label) return;
        const bounds = point.getBoundingClientRect();
        const panel = label.offsetParent.getBoundingClientRect();
        label.textContent = point.alt;
        label.style.left = `${bounds.left + bounds.width / 2 - panel.left}px`;
        label.style.top = `${bounds.top - panel.top}px`;
        label.classList.remove("idle");
    };

    const scaleIcons = () => {
        if (state && state.iconZoom !== state.zoom) {
            state.iconZoom = state.zoom;
            state.canvas.style.setProperty("--map-zoom", state.zoom);
            clusterSoon();
        }
    };

    // A Click In Map Pixels, Plus The Zoom
    const toMap = (clientX, clientY) => {
        if (!state) return null;
        const bounds = state.viewport.getBoundingClientRect();
        return [(clientX - bounds.left - state.x) / state.zoom, (clientY - bounds.top - state.y) / state.zoom, state.zoom];
    };

    // A Waiting Zipline Follows The Cursor, Hidden Off The Map
    const placePreview = () => {
        const preview = state?.preview;
        if (!preview) return;
        const idle = !state.pointer;
        preview.line.ownerSVGElement.classList.toggle("idle", idle);
        preview.ghost.classList.toggle("idle", idle);
        if (idle) return;
        const [x, y] = toMap(state.pointer.x, state.pointer.y);
        preview.line.setAttribute("x2", x);
        preview.line.setAttribute("y2", y);
        preview.ghost.style.left = `${x}px`;
        preview.ghost.style.top = `${y}px`;
    };

    // Thousands Of Icons Restyle Slowly, So They Catch Up Once The Map Settles
    const apply = (moving = false) => {
        hideLabel();
        state.canvas.style.transform = `translate(${state.x}px, ${state.y}px) scale(${state.zoom})`;
        clearTimeout(settle);
        if (moving && state.canvas.childElementCount > manyPoints) {
            settle = setTimeout(scaleIcons, 150);
        } else {
            scaleIcons();
        }
        placePreview();
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

    const init = (viewport, canvas, page) => {
        state?.observer.disconnect();
        state?.points.disconnect();
        state = { viewport, canvas, page, drag: null, moved: false, width: viewport.clientWidth, height: viewport.clientHeight };
        state.clusters = canvas.querySelector(".map-clusters");
        state.label = viewport.parentElement.querySelector(".map-label");
        fit();

        // New Or Removed Icons Group Again Once They Land
        state.points = new MutationObserver(clusterSoon);
        state.points.observe(canvas, { childList: true });
        clusterSoon();

        // A Hovered Point Shows Its Name
        viewport.addEventListener("pointerover", (event) => {
            const point = event.target instanceof Element ? event.target.closest(".map-poi, .map-pin") : null;
            if (point?.alt && !state.drag) {
                showLabel(point);
            } else {
                hideLabel();
            }
        });
        viewport.addEventListener("pointerout", hideLabel);

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
            // Enter Clicks A Point's Middle, Stacked Points Then Ask
            if ((event.key === "Enter" || event.key === " ") && event.target.classList?.contains("map-poi")) {
                event.preventDefault();
                event.stopPropagation();
                const bounds = event.target.getBoundingClientRect();
                event.target.dispatchEvent(new MouseEvent("click", { bubbles: true, clientX: bounds.left + bounds.width / 2, clientY: bounds.top + bounds.height / 2 }));
                return;
            }
            const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[event.key];
            if (!step) return;
            event.preventDefault();
            const points = [...canvas.querySelectorAll(".map-poi[data-keyclick]:not(.clustered)")];
            if (points.length === 0) return;
            const current = points.indexOf(document.activeElement);
            const next = current < 0 ? (step > 0 ? 0 : points.length - 1) : (current + step + points.length) % points.length;
            points[next].focus({ preventScroll: true });
            panTo(parseFloat(points[next].style.left), parseFloat(points[next].style.top));
        });

        // A Right Click Picks A Spot, No Browser Menu
        viewport.addEventListener("contextmenu", (event) => event.preventDefault());

        // Icons Are Images, The Browser Would Drag Them Away
        viewport.addEventListener("dragstart", (event) => event.preventDefault());

        // The Cursor Over The Map, For A Zipline's Preview
        viewport.addEventListener("pointermove", (event) => {
            state.pointer = { x: event.clientX, y: event.clientY };
            placePreview();
        });
        viewport.addEventListener("pointerleave", () => {
            state.pointer = null;
            placePreview();
        });

        viewport.addEventListener("pointerdown", (event) => {
            if (event.button !== 0) return;
            hideLabel();
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
                    return;
                }

                // A Bubble Lists Its Points, The Map Click Never Runs
                const bubble = event.target instanceof Element ? event.target.closest(".map-cluster") : null;
                if (bubble) {
                    event.stopPropagation();
                    state.page.invokeMethodAsync("ShowStackAsync", bubble.members).catch(() => {});
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
            state.points.disconnect();
            hideLabel();
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

    // Escape Closes The Panel, Unless A Dialog, Menu Or Field
    document.addEventListener("keydown", (event) => {
        if (event.key !== "Escape" || !state?.page || !state.viewport.isConnected) return;
        if (document.querySelector(".mud-dialog-container, .mud-popover-open .mud-list")) return;
        if (event.target instanceof Element && event.target.closest("input, textarea, [contenteditable]")) return;
        state.page.invokeMethodAsync("CloseDetailsAsync").catch(() => {});
    });

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

    // Blazor Shows The Preview, JS Keeps It Under The Cursor
    const preview = (on) => {
        if (!state) return;
        const line = on ? state.canvas.querySelector(".map-pick-line line") : null;
        const ghost = on ? state.canvas.querySelector(".map-pick-ghost") : null;
        state.preview = line && ghost ? { line, ghost } : null;
        placePreview();
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

    return { init, centerOn, toMap, zoomBy, reset, preview };
})();
