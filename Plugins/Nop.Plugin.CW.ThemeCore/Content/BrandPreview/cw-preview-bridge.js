/* CW.ThemeCore — live-preview bridge.
   Runs inside the framed storefront ONLY when CW.ThemeCore validated a preview token for this request
   (the head component then emits window.__cwPreview with the allowed admin origin + region map). It:
   (1) applies the draft brand tokens and layout class the Brand-Designer admin page sends over postMessage;
   (2) makes the preview DISPLAY-ONLY — links/buttons don't navigate and forms don't submit (so a click never
       drops the preview token / breaks the frame or hits the real backend). Not covered: `change` events,
       script-called form.submit() and AJAX — an accepted limitation (they carry no preview token, so the server
       treats them as ordinary requests from the browser's own store session); and
   (3) in edit mode (Phase 2) relays clicks on styleable elements back to the admin.
   It never mutates the DOM structure and never reads storefront data — only :root custom properties, the
   <html> class list, a hover outline, and element bounding rects. */
(function () {
    "use strict";

    var cfg = window.__cwPreview || {};
    var adminOrigin = cfg.adminOrigin;
    if (!adminOrigin) return; // not a valid preview context

    var root = document.documentElement;
    var regionMap = Array.isArray(cfg.regionMap) ? cfg.regionMap : [];

    function apply(data) {
        if (!data) return;

        if (data.tokens) {
            Object.keys(data.tokens).forEach(function (name) {
                try { root.style.setProperty(name, data.tokens[name]); } catch (e) { /* ignore a bad token */ }
            });
        }

        if (data.layout) {
            (data.layout.allClasses || []).forEach(function (c) { if (c) root.classList.remove(c); });
            if (data.layout.selected) root.classList.add(data.layout.selected);
        }
    }

    // ---- edit mode (Phase 2: click-to-edit) --------------------------------------------------------------
    var editing = false;
    var hoverEl = null;
    var styleEl = null;

    function ensureStyle() {
        if (styleEl) return;
        styleEl = document.createElement("style");
        styleEl.setAttribute("data-cw-preview", "edit");
        styleEl.textContent = ".cw-preview-hover{outline:2px dashed var(--cw-primary,#2f6fed)!important;outline-offset:2px!important;cursor:pointer!important;}";
        (document.head || root).appendChild(styleEl);
    }

    // nearest self-or-ancestor that is a styleable region → { region, el }. A data-cw-style attribute wins;
    // otherwise the first matching regionMap selector (rules are ordered specific→general).
    function resolveRegion(start) {
        var node = start;
        while (node && node.nodeType === 1 && node !== root) {
            var tagged = node.getAttribute("data-cw-style");
            if (tagged) return { region: tagged, el: node };
            for (var i = 0; i < regionMap.length; i++) {
                try { if (regionMap[i].selector && node.matches(regionMap[i].selector)) return { region: regionMap[i].region, el: node }; }
                catch (e) { /* ignore a malformed selector */ }
            }
            node = node.parentElement;
        }
        return null;
    }

    function clearHover() {
        if (hoverEl) { hoverEl.classList.remove("cw-preview-hover"); hoverEl = null; }
    }

    function onOver(e) {
        var hit = resolveRegion(e.target);
        if (!hit) { clearHover(); return; }
        if (hit.el !== hoverEl) { clearHover(); hoverEl = hit.el; hoverEl.classList.add("cw-preview-hover"); }
    }

    // ---- display-only guard (always on in preview mode) + edit-mode selection ----------------------------
    function isActionable(target) {
        return target && target.closest && target.closest("a, button, input[type=submit], input[type=button], [role=button]");
    }

    function onPreviewClick(e) {
        if (editing) {
            var hit = resolveRegion(e.target);
            if (hit) {
                e.preventDefault(); e.stopPropagation(); e.stopImmediatePropagation();
                var r = hit.el.getBoundingClientRect();
                try {
                    window.parent.postMessage({
                        type: "cw-preview:selected",
                        region: hit.region,
                        rect: { x: r.left, y: r.top, width: r.width, height: r.height }
                    }, adminOrigin);
                } catch (err) { /* not framed */ }
                return;
            }
        }
        // never let a link/button in the preview navigate or fire its own handler
        if (isActionable(e.target)) { e.preventDefault(); e.stopPropagation(); }
    }

    function onPreviewSubmit(e) { e.preventDefault(); e.stopPropagation(); } // no form submits (search, add-to-cart, checkout)

    function editOn() {
        if (editing) return;
        editing = true;
        ensureStyle();
        document.addEventListener("mouseover", onOver, true);
    }

    function editOff() {
        if (!editing) return;
        editing = false;
        document.removeEventListener("mouseover", onOver, true);
        clearHover();
    }

    window.addEventListener("message", function (e) {
        if (e.origin !== adminOrigin) return;          // only the authorised admin origin
        var d = e.data;
        if (!d) return;
        if (d.type === "cw-preview:apply") apply(d);
        else if (d.type === "cw-preview:edit-on") editOn();
        else if (d.type === "cw-preview:edit-off") editOff();
    });

    // display-only guards run for the whole preview session (capture phase → before the page's own handlers)
    document.addEventListener("click", onPreviewClick, true);
    document.addEventListener("submit", onPreviewSubmit, true);

    // announce readiness so the admin posts the current draft
    try { window.parent.postMessage({ type: "cw-preview:ready" }, adminOrigin); } catch (e) { /* not framed */ }
})();
