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

    // Every Click Opens The Menu Below The Button, Never Over It
    document.addEventListener(
        "click",
        (event) => {
            const button = event.target instanceof Element ? event.target.closest("[data-menu-at]") : null;
            if (!button) return;
            opener = button;
            if (event.isTrusted) {
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

// Escape Closes An Open Menu First, Focus Goes Back To Its Button
window.addEventListener(
    "keydown",
    (event) => {
        if (event.key !== "Escape") return;
        const activator = document.querySelector('.mud-menu :is(button, .mud-menu-activator)[aria-expanded="true"]');
        if (!activator) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        activator.click();
        activator.focus?.();
    },
    true
);

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
    const glideTime = 250;
    const clusterPixels = 24;
    let state = null;
    let frame = 0;
    let clusterTimer = 0;

    // Points From Blazor, Tied To Their Layer
    let sent = null;

    // Points Near A Group's First Point Join It, A Grid Finds Neighbours
    const cluster = () => {
        clearTimeout(clusterTimer);
        if (!state || sent?.layer !== state.layer) return;
        const { points } = sent;
        const groups = [];
        const loose = [];
        const folds = state.zoom < maxZoom * 0.99;
        const radius = clusterPixels / state.zoom;
        const seeds = new Map();
        points.forEach((point, index) => {
            if (point.solo || !folds) {
                loose.push(index);
                return;
            }
            const column = Math.floor(point.x / radius);
            const row = Math.floor(point.y / radius);
            let home = null;
            for (let stepX = -1; stepX <= 1 && !home; stepX++) {
                for (let stepY = -1; stepY <= 1 && !home; stepY++) {
                    home = seeds.get((column + stepX) * 65536 + row + stepY)?.find((group) => Math.hypot(group.x - point.x, group.y - point.y) < radius);
                }
            }
            if (home) {
                home.members.push(index);
                return;
            }
            const group = { x: point.x, y: point.y, members: [index] };
            groups.push(group);
            const cell = column * 65536 + row;
            if (!seeds.has(cell)) seeds.set(cell, []);
            seeds.get(cell).push(group);
        });
        const bubbles = [];
        for (const group of groups) {
            if (group.members.length < 2) {
                loose.push(group.members[0]);
                continue;
            }
            group.x = group.members.reduce((sum, index) => sum + points[index].x, 0) / group.members.length;
            group.y = group.members.reduce((sum, index) => sum + points[index].y, 0) / group.members.length;
            bubbles.push(group);
        }
        state.loose = loose.sort((first, second) => first - second);
        state.groups = bubbles;
        state.bubbles.forEach((bubble) => bubble.remove());
        state.bubbles.clear();
        state.zoomClustered = state.zoom;
        draw();
    };

    const clusterSoon = () => {
        clearTimeout(clusterTimer);
        clusterTimer = setTimeout(cluster, 120);
    };

    // Map Pixels In View, A Cached Size Never Forces Layout
    const viewOf = () => {
        const { width, height, x, y, zoom } = state;
        return { left: -x / zoom, top: -y / zoom, right: (width - x) / zoom, bottom: (height - y) / zoom };
    };

    const iconOf = (point, index) => {
        const icon = document.createElement("img");
        icon.src = point.icon;
        icon.alt = point.title;
        icon.className = "map-poi";
        icon.setAttribute("role", "button");
        icon.tabIndex = -1;
        icon.dataset.keyclick = "";
        icon.style.cssText = point.style;
        icon.pointIndex = index;
        return icon;
    };

    const bubbleOf = (group) => {
        const { points } = sent;
        const bubble = document.createElement("span");
        bubble.className = "map-cluster";
        bubble.textContent = group.members.length;
        bubble.style.setProperty("--x", group.x);
        bubble.style.setProperty("--y", group.y);
        // Whole Device Pixels Each Side Keep The Rim Sharp
        const size = Math.min(28 + Math.log2(group.members.length) * 2, 36);
        bubble.style.setProperty("--cluster-size", `${(Math.round((size * devicePixelRatio) / 2) * 2) / devicePixelRatio}px`);
        bubble.members = group.members.flatMap((index) => [points[index].x, points[index].y]);
        return bubble;
    };

    // Only Marks Near The View Exist, Restyles Stay Small
    const draw = () => {
        if (!state?.loose || sent?.layer !== state.layer) return;
        const view = viewOf();
        const marginX = (view.right - view.left) / 2;
        const marginY = (view.bottom - view.top) / 2;
        const area = { left: view.left - marginX, top: view.top - marginY, right: view.right + marginX, bottom: view.bottom + marginY };
        state.drawn = area;
        const inside = ({ x, y }) => x >= area.left && x <= area.right && y >= area.top && y <= area.bottom;
        const { points } = sent;
        const icons = new Set(state.loose.filter((index) => inside(points[index])));
        state.icons.forEach((icon, index) => {
            if (icons.has(index)) return;
            icon.remove();
            state.icons.delete(index);
        });
        const added = document.createDocumentFragment();
        icons.forEach((index) => {
            if (state.icons.has(index)) return;
            const icon = iconOf(points[index], index);
            state.icons.set(index, icon);
            added.append(icon);
        });
        state.layer.append(added);
        const bubbles = document.createDocumentFragment();
        state.groups.forEach((group) => {
            const shown = state.bubbles.get(group);
            if (!inside(group)) {
                shown?.remove();
                state.bubbles.delete(group);
            } else if (!shown) {
                const bubble = bubbleOf(group);
                state.bubbles.set(group, bubble);
                bubbles.append(bubble);
            }
        });
        state.clusters.append(bubbles);
        // An Arrow Key's Point Takes Focus Once It Exists
        const focused = state.icons.get(state.focusNext);
        if (focused) {
            focused.focus({ preventScroll: true });
            state.focusNext = undefined;
        }
    };

    // Points From Blazor Replace The Last, Folded Before They Paint
    const setPoints = (layer, points) => {
        sent = { layer, points };
        if (state?.layer !== layer) return;
        state.icons.forEach((icon) => icon.remove());
        state.icons.clear();
        cluster();
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

    // Few Marks Exist, All Follow The Zoom Each Frame
    const scaleMarks = () => {
        if (state.markZoom === state.zoom && state.ratio === devicePixelRatio) return;
        state.markZoom = state.zoom;
        state.ratio = devicePixelRatio;
        state.canvas.style.setProperty("--map-zoom", state.zoom);
        state.canvas.style.setProperty("--dpr", state.ratio);
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
        preview.ghost.style.setProperty("--x", x);
        preview.ghost.style.setProperty("--y", y);
    };

    // Draw When The View Leaves Its Area, Refold After Zooming
    const apply = () => {
        hideLabel();

        // Whole Device Pixels Keep The Map And Its Icons Sharp
        const bounds = state.viewport.getBoundingClientRect();
        const snap = (value, origin) => Math.round((value + origin) * devicePixelRatio) / devicePixelRatio - origin;
        state.canvas.style.transform = `translate(${snap(state.x, bounds.left)}px, ${snap(state.y, bounds.top)}px) scale(${state.zoom})`;
        scaleMarks();
        const view = viewOf();
        const area = state.drawn;
        if (area && (view.left < area.left || view.top < area.top || view.right > area.right || view.bottom > area.bottom)) {
            draw();
        }
        if (state.zoomClustered !== undefined && state.zoomClustered !== state.zoom) {
            clusterSoon();
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
            apply();
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
        const { width, height } = state;
        const zoom = Math.min(width, height) / mapSize;
        state.minZoom = zoom / 2;
        state.fitted = true;
        return { x: (width - mapSize * zoom) / 2, y: (height - mapSize * zoom) / 2, zoom };
    };

    const fit = () => {
        stop();
        Object.assign(state, wholeMap());
        apply();
    };

    const init = (viewport, canvas, page) => {
        state?.observer.disconnect();
        state = { viewport, canvas, page, drag: null, moved: false, width: viewport.clientWidth, height: viewport.clientHeight, icons: new Map(), bubbles: new Map() };
        state.clusters = canvas.querySelector(".map-clusters");
        state.layer = canvas.querySelector(".map-points");
        state.label = viewport.parentElement.querySelector(".map-label");
        fit();

        // Points Sent Before The Map Was Ready Fold Now
        cluster();

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
            const grownX = viewport.clientWidth - state.width;
            const grownY = viewport.clientHeight - state.height;
            state.width = viewport.clientWidth;
            state.height = viewport.clientHeight;
            if (state.fitted) {
                fit();
            } else {
                stop();
                state.x += grownX / 2;
                state.y += grownY / 2;
                apply();
            }
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
            const order = state.loose ?? [];
            if (order.length === 0) return;
            const current = order.indexOf(document.activeElement?.pointIndex);
            const next = order[current < 0 ? (step > 0 ? 0 : order.length - 1) : (current + step + order.length) % order.length];
            state.focusNext = next;
            state.icons.get(next)?.focus({ preventScroll: true });
            panTo(sent.points[next].x, sent.points[next].y);
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

    // Another Monitor's Scaling Snaps Every Mark And Bubble Again
    const watchScaling = () => {
        matchMedia(`(resolution: ${devicePixelRatio}dppx)`).addEventListener(
            "change",
            () => {
                if (state?.viewport.isConnected) {
                    apply();
                    cluster();
                }
                watchScaling();
            },
            { once: true }
        );
    };
    watchScaling();

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
        state.x = state.width / 2 - mapX * state.zoom;
        state.y = state.height / 2 - mapY * state.zoom;
        apply();
    };

    const panTo = (mapX, mapY) => {
        const zoom = state.target?.zoom ?? state.zoom;
        state.fitted = false;
        glideTo(state.width / 2 - mapX * zoom, state.height / 2 - mapY * zoom, zoom);
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
        zoomAt(state.width / 2, state.height / 2, factor);
    };

    const reset = () => {
        if (!state) return;
        const { x, y, zoom } = wholeMap();
        glideTo(x, y, zoom);
    };

    return { init, points: setPoints, centerOn, toMap, zoomBy, reset, preview };
})();
