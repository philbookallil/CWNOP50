/* Bulk ordering: list loading, filters, sorting, paging, category selection.
   The DOM is the single source of truth for request state - nothing is mirrored in JS. */
window.CW = window.CW || {};

CW.BulkOrder = (function () {
    //two independent sequences: the list and a single card's grid must not cancel each other
    var container, requestToken = 0, gridToken = 0, adding = false, pinTimer;

    //the typed quantities a list load or a grid reload has set aside to put back. An add that comes back
    //meanwhile takes out the boxes it added (forgetAdded), so they stay cleared and the rest still come back
    var pendingTyped = [];

    function buildQuery(page) {
        return {
            FilterMode: document.getElementById('bulk-filter').value,
            OrderId: document.getElementById('bulk-order-id').value || 0,
            CategoryId: document.getElementById('bulk-category-id').value,
            Pins: collectPins(),
            OrderBy: document.getElementById('bulk-sort').value,
            ViewMode: document.getElementById('bulk-viewmode').value,
            PageNumber: page,
            PageSize: document.getElementById('bulk-pagesize').value
        };
    }

    function syncUrl(query) {
        var url = new URL(window.location.href);
        Object.keys(query).forEach(function (k) { url.searchParams.set(k, query[k]); });
        window.history.replaceState({ path: url.toString() }, '', url.toString());
    }

    function toggleOrderPicker() {
        var wrapper = document.getElementById('bulk-orders-wrapper');
        wrapper.style.display =
            document.getElementById('bulk-filter').value === 'LatestOrdered' ? '' : 'none';
    }

    function load(page) {
        var query = buildQuery(page);
        syncUrl(query);
        container.classList.add('loading');
        container.setAttribute('aria-busy', 'true');

        //what the customer has typed, before the markup holding it is replaced. Keyed by product and axis
        //coordinates - the same key reloadGrid uses for one card, because it is the same question asked of
        //the whole page: which box is this, once everything around it is new.
        var typed = collectTyped(container);
        var snapshot = { productId: null, values: typed };
        pendingTyped.push(snapshot);

        //the products whose cards had options chosen: the server keeps them for the cards still on the new list
        var pinnedProducts = query.Pins === '' ? [] : query.Pins.split(',').map(function (pin) { return pin.split(':')[0]; });

        var url = container.dataset.url + '?' + new URLSearchParams(query).toString();
        var token = ++requestToken;

        fetch(url, {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (r) {
                if (!r.ok) throw new Error(r.status);
                return r.text();
            })
            .then(function (html) {
                if (token !== requestToken) return;   //a newer request has already started
                container.innerHTML = html;

                //the server may have dropped a category that is no longer in the tree. The selected one is
                //`active last`: every category on its path is `active`, and the first of those is its top level
                var active = container.querySelector('.block-category-navigation li.active.last > [data-category-id]');
                document.getElementById('bulk-category-id').value = active ? active.dataset.categoryId : 0;

                //back into the same cells where they still exist. What had nowhere to go is SAID, at the
                //top of the list and in the live region - the page is the unit of a bulk order, so
                //paging away from typed boxes loses them, and losing them silently was the defect.
                //What an add that came back meanwhile has added is no longer among them.
                var lost = restoreTyped(container, typed);

                if (lost > 0) {
                    var note = document.createElement('div');
                    note.className = 'no-result typed-lost';
                    note.textContent = container.dataset.typedLost.replace('{0}', lost);
                    container.insertBefore(note, container.firstChild);
                }

                var pinsLost = pinnedProducts.some(function (productId) {
                    return !container.querySelector('.bulk-order-grid[data-product-id="' + productId + '"]');
                });

                if (pinsLost && container.dataset.pinsLost) {
                    var pinNote = document.createElement('div');
                    pinNote.className = 'no-result pins-lost';
                    pinNote.textContent = container.dataset.pinsLost;
                    container.insertBefore(pinNote, container.firstChild);
                }

                markApplied(container);

                //same as reloadGrid: restored quantities keep their value across the swap, so they must keep
                //their reason too
                container.querySelectorAll('.bulk-order-grid').forEach(flagOverMax);

                bindPager();
                pinRowLabels(container);
                announce();
                scrollToControls();
            })
            .catch(function () {
                if (token !== requestToken) return;

                //leave the previous result in place; the controls above it still work
                var message = container.querySelector('.load-failed') || document.createElement('div');
                message.className = 'no-result load-failed';
                message.textContent = container.dataset.loadFailed;
                container.insertBefore(message, container.firstChild);
                announce();
            })
            .finally(function () {
                forgetSnapshot(snapshot);
                if (token !== requestToken) return;

                container.classList.remove('loading');
                container.setAttribute('aria-busy', 'false');
            });
    }

    /* Every typed quantity in `scope`, keyed "productId|axisKey". A select's value is its option value, so
       the AllowedQuantities control round-trips through here unchanged. */
    function collectTyped(scope) {
        var typed = {};

        scope.querySelectorAll('.bulk-order-grid').forEach(function (grid) {
            grid.querySelectorAll('.bulk-qty[data-axis-key]').forEach(function (input) {
                if (input.value !== '' && input.value !== '0')
                    typed[grid.dataset.productId + '|' + input.dataset.axisKey] = input.value;
            });
        });

        return typed;
    }

    function forgetSnapshot(snapshot) {
        var at = pendingTyped.indexOf(snapshot);
        if (at >= 0) pendingTyped.splice(at, 1);
    }

    /* A box an add has just put in the cart: taken out of every set-aside snapshot, so a load or reload that
       started before the add does not put its quantity back (a second click would order it twice) */
    function forgetAdded(productId, axisKey) {
        pendingTyped.forEach(function (snapshot) {
            if (snapshot.productId === null) delete snapshot.values[productId + '|' + axisKey];
            else if (snapshot.productId === String(productId)) delete snapshot.values[axisKey];
        });
    }

    /* Writes them back where the same cell still exists. Returns how many had nowhere to go. */
    function restoreTyped(scope, typed) {
        var lost = 0;

        Object.keys(typed).forEach(function (key) {
            var parts = key.split('|');
            var grid = scope.querySelector('.bulk-order-grid[data-product-id="' + parts[0] + '"]');
            var input = grid && grid.querySelector('.bulk-qty[data-axis-key="' + parts[1] + '"]');

            if (input) input.value = typed[key]; else lost++;
        });

        return lost;
    }

    /* One delegated listener on the pager itself, so the number of buttons is irrelevant and a re-render
       cannot leave a stale handler behind. */
    function bindPager() {
        var pager = container.querySelector('.bulk-order-pager');
        if (!pager) return;

        pager.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-page]');

            //the pager element itself carries data-page; only its buttons should act
            if (!btn || btn === pager || btn.disabled) return;

            var page = parseInt(btn.dataset.page, 10);
            if (page > 0) load(page);
        });
    }

    /* The DOM owns the state. The pin selects ARE the pin state - there is no
   JavaScript copy of it, so a swap cannot desynchronise it. */
    function collectPins() {
        var pins = [];

        container.querySelectorAll('.bulk-pin').forEach(function (select) {
            if (select.value && select.value !== '0' && select.value !== select.dataset.defaultValue) {
                pins.push(select.dataset.productId + ':' + select.dataset.mappingId + ':' + select.value);
            }
        });

        container.querySelectorAll('.pin-checks').forEach(function (group) {
            var boxes = group.querySelectorAll('.bulk-pin-check');
            var dirty = false;
            var checked = [];

            boxes.forEach(function (box) {
                if (box.checked) checked.push(box.value);
                if (box.checked !== (box.dataset.defaultChecked === 'true')) dirty = true;
            });

            if (!dirty) return;

            //an emptied group must say so, or the server would apply the default instead of the clear
            if (checked.length === 0) {
                pins.push(group.dataset.productId + ':' + group.dataset.mappingId + ':0');
                return;
            }

            checked.forEach(function (valueId) {
                pins.push(group.dataset.productId + ':' + group.dataset.mappingId + ':' + valueId);
            });
        });

        return pins.join(',');
    }

    /* A pin change re-renders ONE card's grid. Quantities already typed in that card are
       re-applied where the cell still exists; cells that no longer exist lose their quantity, and the card
       says so rather than silently dropping it. */
    /* What each pin of the cards in `scope` holds as their grids were last rendered with it: a reload that fails
       puts the pins back to it, so what the card shows and what its cells would order cannot disagree. */
    function markApplied(scope) {
        gridsIn(scope).forEach(function (grid) {
            grid.querySelectorAll('.bulk-pin').forEach(function (select) { select.dataset.applied = select.value; });
            grid.querySelectorAll('.bulk-pin-check').forEach(function (box) { box.dataset.applied = box.checked ? 'true' : 'false'; });
        });
    }

    function restoreApplied(grid) {
        grid.querySelectorAll('.bulk-pin').forEach(function (select) {
            if (select.dataset.applied !== undefined) select.value = select.dataset.applied;
        });
        grid.querySelectorAll('.bulk-pin-check').forEach(function (box) {
            if (box.dataset.applied !== undefined) box.checked = box.dataset.applied === 'true';
        });
    }

    /* A sentence about the whole card, in the list above its grid (a live region), so it is said as well as shown */
    function cardNotice(grid, text) {
        var box = grid && grid.querySelector('.grid-warnings');
        if (!box || !text) return;

        //one notice at a time: a second failed reload replaces the first rather than repeating it
        box.querySelectorAll('li.card-notice').forEach(function (item) { item.remove(); });

        //unhide BEFORE appending (setStatus's rule)
        box.hidden = false;
        box.appendChild(warningItem({ text: text }, 'card-notice'));
    }

    function reloadGrid(grid) {
        var productId = grid.dataset.productId;
        var typed = {};
        var snapshot = { productId: String(productId), values: typed };

        //keyed by AXIS coordinates, not by data-selection: "M / Navy" is the same cell before and after a
        //pin change, while its selection legitimately gains the pin's value and would never match again
        grid.querySelectorAll('.bulk-qty[data-axis-key]').forEach(function (input) {
            if (input.value) typed[input.dataset.axisKey] = input.value;
        });

        pendingTyped.push(snapshot);

        var pins = [];

        grid.querySelectorAll('.bulk-pin').forEach(function (select) {
            if (select.value && select.value !== '0') {
                pins.push(productId + ':' + select.dataset.mappingId + ':' + select.value);
            }
        });

        grid.querySelectorAll('.pin-checks').forEach(function (group) {
            var checked = group.querySelectorAll('.bulk-pin-check:checked');

            if (checked.length === 0) {
                pins.push(productId + ':' + group.dataset.mappingId + ':0');
                return;
            }

            checked.forEach(function (box) {
                pins.push(productId + ':' + group.dataset.mappingId + ':' + box.value);
            });
        });

        var url = grid.dataset.url + '?productId=' + encodeURIComponent(productId) +
            '&pins=' + encodeURIComponent(pins.join(','));

        grid.classList.add('loading');
        grid.classList.remove('grid-failed');
        grid.setAttribute('aria-busy', 'true');

        //per-card, so a pin change on one card does not discard another card's response. While it is set the
        //card's cells still carry the old options, so adding from the card waits for it (submitLines).
        var token = String(++gridToken);
        grid.dataset.pending = token;

        //the reload did not bring a grid: the pins go back to what the cells were rendered with, and the card says so
        function failed() {
            restoreApplied(grid);
            grid.classList.add('grid-failed');
            cardNotice(grid, container.dataset.gridFailed);
        }

        fetch(url, {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (r) {
                if (!r.ok) throw new Error(r.status);
                return r.text();
            })
            .then(function (html) {
                if (grid.dataset.pending !== token) return;

                var replacement = document.createElement('div');
                replacement.innerHTML = html;

                var fresh = replacement.querySelector('.bulk-order-grid');
                if (!fresh) {
                    failed();
                    return;
                }

                grid.replaceWith(fresh);
                markApplied(fresh);

                var lost = 0;

                //what an add that came back meanwhile has added is no longer among them
                Object.keys(typed).forEach(function (axisKey) {
                    var input = fresh.querySelector('.bulk-qty[data-axis-key="' + axisKey + '"]');
                    if (input) { input.value = typed[axisKey]; } else { lost++; }
                });

                if (lost > 0) {
                    fresh.classList.add('quantities-lost');
                    cardNotice(fresh, container.dataset.gridLost.replace('{0}', lost));
                }

                //a restored quantity can still be over its cell's maximum, and the fresh markup carries
                //neither the mark nor the reason - re-derived here, or the swap silently un-says it
                flagOverMax(fresh);

                //same reason, for the same swap: a pin change rebuilds the table with different axes, so
                //the label columns and their widths are new and the offsets have to be measured again
                pinRowLabels(fresh);
            })
            .catch(function () {
                if (grid.dataset.pending !== token) return;

                failed();
            })
            .finally(function () {
                forgetSnapshot(snapshot);
                grid.classList.remove('loading');
                grid.removeAttribute('aria-busy');

                //a card that is still on the page (the reload failed) can be added from again
                if (grid.dataset.pending === token) delete grid.dataset.pending;
            });
    }

    /* Copies the one line that meaningfully changed into the live region. The pager summary already
    says "Page 2 of 3 (42 products)"; with no results it is the no-result message instead. */
    function announce() {
        var status = document.getElementById('bulk-order-status');
        if (!status) return;

        //failure first: on a failed load the previous result is still on screen, so its pager would
        //otherwise win and announce a state that is no longer true
        var source = container.querySelector('.load-failed')
            || container.querySelector('.typed-lost')
            || container.querySelector('.pins-lost')
            || container.querySelector('.bulk-pager-state')
            || container.querySelector('.no-result');

        //lost quantities and lost options are both said when both happened
        var pinsLost = container.querySelector('.pins-lost');
        var text = source ? source.textContent.trim() : '';
        if (pinsLost && source && source !== pinsLost && source.classList.contains('typed-lost'))
            text += ' ' + pinsLost.textContent.trim();

        status.textContent = text;
    }

    /* After a swap the old scroll position points into content that no longer exists. Return to the
    controls - not the list - so the filters are still reachable. No-op when they are already visible. */
    function scrollToControls() {
        var controls = document.querySelector('.bulk-order-controls');
        if (!controls || controls.getBoundingClientRect().top >= 0) return;

        var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        controls.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
    }

    /* ------------------------------------------------------------------ add to cart */

    /* Every quantity box in `scope` holding a positive number, as request lines. `scope` is either one grid
       (a card's button) or the whole container (the page button), so both shapes are handled here rather than
       by the callers - one path for one job. The DOM is the state, so this reads it rather than keeping a
       parallel copy that could disagree. */
    /* Every grid in `scope`, where `scope` is EITHER one grid (a card's button) or an ancestor of several
       (the page button). Asked in two places - collecting the lines to send, and re-deriving the over-max
       marks once the answer is in - and they have to agree about what a card is, so it is asked once here. */
    function gridsIn(scope) {
        if (!scope) return [];

        return scope.classList && scope.classList.contains('bulk-order-grid')
            ? [scope]
            : Array.prototype.slice.call(scope.querySelectorAll('.bulk-order-grid'));
    }

    /* A box's quantity: 0 when blank, NaN when it is not a whole number a cart line can hold. A number box the
       browser could not read ("2,5" in some languages) reports badInput and an empty value; parseInt would read
       "1e3" as 1 and "2.5" as 2 without a word. */
    function readQuantity(input) {
        if (input.validity && input.validity.badInput) return NaN;

        var raw = String(input.value).trim();
        if (raw === '') return 0;
        if (!/^\d+$/.test(raw)) return NaN;

        var quantity = Number(raw);
        return quantity <= 2147483647 ? quantity : NaN;
    }

    /* Every box in `scope` holding a quantity, as request lines; `invalid` lists the boxes whose quantity cannot be
       read, which are flagged instead of sent */
    function collectLines(scope, invalid) {
        var lines = [];

        gridsIn(scope).forEach(function (grid) {
            var productId = parseInt(grid.dataset.productId, 10);

            grid.querySelectorAll('.bulk-qty').forEach(function (input) {
                var quantity = readQuantity(input);

                if (isNaN(quantity)) {
                    if (invalid) invalid.push(input);
                    return;
                }

                if (!(quantity > 0)) return;

                //data-selection is "mappingId:valueId,..." - axis values plus every pin holding a value,
                //already filtered per cell by the server. Group by mapping so a checkbox attribute with
                //several ticked values arrives as one selection with several ids, which is the shape
                //AddProductAttribute expects.
                var byMapping = {};

                (input.dataset.selection || '').split(',').forEach(function (pair) {
                    var parts = pair.split(':');
                    if (parts.length !== 2) return;

                    var mappingId = parseInt(parts[0], 10);
                    var valueId = parseInt(parts[1], 10);
                    if (!mappingId || !valueId) return;

                    (byMapping[mappingId] = byMapping[mappingId] || []).push(valueId);
                });

                var selections = Object.keys(byMapping).map(function (mappingId) {
                    return {
                        productAttributeMappingId: parseInt(mappingId, 10),
                        valueIds: byMapping[mappingId],
                        enteredText: null
                    };
                });

                lines.push({
                    productId: productId,
                    selections: selections,
                    quantity: quantity,
                    cellKey: input.dataset.axisKey || ''
                });
            });
        });

        return lines;
    }

    /* Clears the marks AND the summary from a previous attempt, so a second submit does not read as the
       first one's result. Both in one place on purpose: a message that outlived the failure it described
       would sit at the top of a card whose cell is now fixed.
       The over-max hints go with them: those lines are about to be sent, and the server's answer - which
       knows the merged quantity, and ours does not - replaces the hint a moment later. */
    function clearLineResults(scope) {
        scope.querySelectorAll('.bulk-qty.line-failed, .bulk-qty.over-max').forEach(function (input) {
            input.classList.remove('line-failed');
            input.classList.remove('over-max');
            input.removeAttribute('aria-invalid');
        });

        scope.querySelectorAll('.grid-warnings').forEach(function (box) {
            box.textContent = '';
            box.hidden = true;
        });
    }

    /* One warning line: the cell's own name, then the sentence, unedited. Shared by the server's refusals and
       by the over-max hint, because they are the same thing said at two moments - and, on one cell, the same
       sentence.
       \u2014 as an escape, not the character: this string reaches the DOM, and the file is served with no
       charset of its own, so it decodes as whatever the page says. Every other non-ASCII in this file is in a
       comment, where it cannot be seen. */
    function warningItem(line, className) {
        var item = document.createElement('li');

        if (className) item.className = className;

        //which cell this sentence is about, so clearRefusal can withdraw exactly this one when that cell is
        //edited. Only the server's refusals carry it; the over-max hints are found by their class instead,
        //and are rebuilt wholesale on every change anyway.
        if (line.key) item.dataset.cell = line.key;

        if (line.label) {
            var name = document.createElement('span');
            name.className = 'cell-name';
            name.textContent = line.label;

            item.appendChild(name);
            item.appendChild(document.createTextNode(' \u2014 '));
        }

        item.appendChild(document.createTextNode(line.text));

        return item;
    }

    /* One card's failed lines, listed above its grid. nop's sentence is used UNEDITED and only prefixed with
       the cell it belongs to: the customer must not be told two different things about one rule by two
       different screens. Above the grid rather than inside the cell because a sentence in a table cell
       widens its whole column, and one refused cell then reflows every other column on the card. */
    function renderWarnings(grid, lines) {
        var box = grid.querySelector('.grid-warnings');
        if (!box) return;

        box.textContent = '';

        //unhide BEFORE appending, for the reason setStatus records: a hidden element is not in the
        //accessibility tree, so children added while it is still hidden never reach the live region
        box.hidden = lines.length === 0;

        lines.forEach(function (line) {
            box.appendChild(warningItem(line, null));
        });
    }

    /* Scrolls the first refused cell's CARD into view and puts the cursor in its box. The card, not the box:
       the reasons live above the grid, so scrolling to the input alone would land the customer on a
       red border with the explanation off screen above it.
       Same reduced-motion rule as scrollToControls - somebody who has asked for less movement gets a jump. */
    function revealFirstFailure() {
        //querySelector is document order, so this is the topmost failure whatever order the server answered in
        var input = container.querySelector('.bulk-qty.line-failed');
        if (!input) return;

        var card = input.closest('.bulk-order-product') || input;
        var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        card.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });

        //preventScroll leaves the scroll above in charge of the movement; without it the browser jumps to the
        //input first and the smooth scroll then animates from the wrong place
        input.focus({ preventScroll: true });
    }

    /* Successes clear their box, failures keep it, mark it, and say why above the grid.

       PascalCase on the way in. nop pins Newtonsoft to DefaultContractResolver
       (ServiceCollectionExtensions.AddNopMvc :327, with the comment "MVC now serializes JSON with camel case
       names by default, use this code to avoid it"), so a serialized record arrives with its declared member
       names. The two lowercase names read further down are nop's own field names for the cart blocks. */
    function applyLineResults(scope, result, status) {
        var failed = 0;

        //a refusal of the whole submit (too many lines) is about no cell: it is the status
        var general = (result.Results || []).filter(function (line) { return line.LineIndex < 0; });
        if (general.length > 0) {
            setStatus(status, general.map(function (line) { return (line.Warnings || []).join(' '); }).join(' '), 'error');
            return;
        }

        //grouped per card, because the list is per card: a page-wide add can refuse lines on four products
        //and each one's reasons belong above its own grid
        var byGrid = new Map();

        (result.Results || []).forEach(function (line) {
            if (!line.Warnings || line.Warnings.length === 0) forgetAdded(line.ProductId, line.CellKey);

            var grid = container.querySelector('.bulk-order-grid[data-product-id="' + line.ProductId + '"]');
            if (!grid) return;

            var input = grid.querySelector('.bulk-qty[data-axis-key="' + line.CellKey + '"]');
            if (!input) return;

            if (!line.Warnings || line.Warnings.length === 0) {
                //a select has no empty option - assigning '' to one matches nothing and silently leaves the
                //previous choice selected, so the box would read as still ordered after a successful add
                input.value = input.tagName === 'SELECT' ? '0' : '';
                return;
            }

            failed++;
            input.classList.add('line-failed');

            //the border says "this one" to everyone who can see it; this says it to everyone else. The
            //reason itself is in the summary above the grid, which is a list rather than one message per
            //box, so aria-describedby would point several inputs at the same node.
            input.setAttribute('aria-invalid', 'true');

            //the cell's own qualified name - "Size S, Color Navy" - which the factory already puts on the
            //element as its aria-label, so nothing new crosses the wire. A product with no attributes has
            //one cell with no coordinates, carrying the constant key "product": a message reading "Your
            //quantity exceeds..." would be worse than an unprefixed one, so it gets no prefix.
            var label = input.dataset.axisKey === 'product' ? '' : (input.getAttribute('aria-label') || '');

            if (!byGrid.has(grid)) byGrid.set(grid, []);

            byGrid.get(grid).push({ label: label, text: line.Warnings.join(' '), key: input.dataset.axisKey });
        });

        byGrid.forEach(function (lines, grid) {
            renderWarnings(grid, lines);
        });

        applyCartQuantities(result.CartQuantities);
        applyCartBlocks(result);

        var added = result.AddedQuantity || 0;

        if (added > 0 && window.CWCart)
            CWCart.notifyChanged();

        //`added` is UNITS and `failed` is CELLS - they answer different questions ("how much went in" and
        //"how many boxes do I go and fix"), which is why only one of them appears in each sentence. Putting
        //both in one line would read as two counts of the same thing.
        if (failed === 0)
            setStatus(status, container.dataset.added.replace('{0}', added), 'ok');
        else if (added === 0)
            setStatus(status, container.dataset.addFailed, 'error');
        else
            setStatus(status, container.dataset.someFailed.replace('{0}', added), 'warn');

        //A page-wide add is pressed from the BOTTOM of the list and its refusals are scattered up the page:
        //the pill says how many, never which. `scope` is the container for that button and the card
        //itself for a per-card add - which is also the first use this parameter has had, since everything
        //else here queries `container` directly.
        if (failed > 0 && scope === container) revealFirstFailure();
    }

    /* One cell's server refusal, withdrawn because the customer has just changed that cell. The mark, the
       aria state and the sentence go together - they are three faces of one claim, and leaving any of them
       behind restates a verdict on a number that is no longer in the box.
       Matched on the axis key rather than a CSS selector: the key contains ':' and ',' and, on a product
       with no attributes, the literal "product" - none of which are worth escaping into a selector. */
    function clearRefusal(input) {
        if (!input.classList.contains('line-failed')) return;

        input.classList.remove('line-failed');
        input.removeAttribute('aria-invalid');

        var grid = input.closest('.bulk-order-grid');
        var box = grid && grid.querySelector('.grid-warnings');
        if (!box) return;

        var key = input.dataset.axisKey || '';

        box.querySelectorAll('li[data-cell]').forEach(function (item) {
            if (item.dataset.cell === key) item.remove();
        });

        box.hidden = box.childElementCount === 0;
    }

    /* A quantity typed above what the cell can supply. The value is LEFT EXACTLY AS TYPED and the card says
       why, in nop's own sentence.

       It used to snap the value down to the maximum, and that was wrong twice over. The customer presses Add
       straight after typing, so `change` fires on the way to the click: the value became 10, submitLines then
       hid the note the clamp had just written, and the only thing left on screen was "10 added to your cart"
       - a number they never typed, reported as a success. And the number was not reliable either: nop
       validates the MERGED quantity (AddToCartAsync:1627), so with 6 of this cell already in the cart the true
       ceiling on a max-10 box is 4, and clamping to 10 buys a refusal quoting a figure nobody chose.

       So: no correction, only a reason, and the line is still submitted. nop refuses it with the same resource
       this reads (ShoppingCart.QuantityExceedsStock) against the quantity that actually counts, the typed
       value stays in the box to be fixed, and the other lines still commit. The server stays the only
       check - which is what the clamp always claimed to be leaving it, and was not. */
    function flagOverMax(grid) {
        if (!grid) return;

        var box = grid.querySelector('.grid-warnings');
        if (!box) return;

        //ours only: a server refusal's entry outlives an edit to another cell on the same card
        box.querySelectorAll('li.over-max').forEach(function (item) { item.remove(); });

        grid.querySelectorAll('input.bulk-qty').forEach(function (input) {
            var max = parseInt(input.getAttribute('max'), 10);
            var value = parseInt(input.value, 10);
            var over = max > 0 && value > max;
            var refused = input.classList.contains('line-failed');

            input.classList.toggle('over-max', over);

            if (over || refused) input.setAttribute('aria-invalid', 'true');
            else input.removeAttribute('aria-invalid');

            //the server has already answered about this cell and its number is the one that counts, so a
            //second sentence quoting the RENDERED maximum would contradict it
            if (!over || refused) return;

            //unhide BEFORE appending (setStatus's rule): a child added to a hidden element is added outside
            //the accessibility tree, and the live region never announces it
            box.hidden = false;

            box.appendChild(warningItem({
                label: input.dataset.axisKey === 'product' ? '' : (input.getAttribute('aria-label') || ''),
                text: container.dataset.exceedsStock.replace('{0}', max)
            }, 'over-max'));
        });

        box.hidden = box.childElementCount === 0;
    }

    /* One place decides how an outcome looks, so the five call sites cannot drift apart.
       drives the colour and the mark; success clears itself after a moment and failure does not,
       which is nop's own rule for its notification bar - 3500ms for a success, no timeout at all
       for an error (public.ajaxcart.js :148, :157). A message you have to act on must not vanish
       while you are still reading it, and a message that only says "done" must not sit there
       through the next four things you do. */
    function setStatus(status, text, state, passing) {
        if (!status) return;

        clearTimeout(status._bulkTimer);

        //unhide BEFORE writing: a hidden element is not in the accessibility tree, so text set while
        //it is still hidden can land without the live region ever announcing it
        status.className = 'bulk-add-status is-' + state;
        status.hidden = false;
        status.textContent = text;

        //a message to act on stays; "done" and "wait a moment" go
        if (state !== 'ok' && !passing) return;

        status._bulkTimer = setTimeout(function () {
            status.hidden = true;
            status.textContent = '';
        }, 4000);
    }

    /* The card's "n in cart". The server sends the whole map rather than a delta because nop merges a
       matching line into an existing one, and can auto-add a required product nobody typed a quantity for;
       an increment computed here would be wrong in both cases. Every card is written, including the ones
       absent from the map, so a product removed from the cart elsewhere does not leave a stale number. */
    function applyCartQuantities(quantities) {
        if (!quantities) return;

        container.querySelectorAll('.bulk-order-product').forEach(function (card) {
            var label = card.querySelector('.product-in-cart');
            if (!label) return;

            var quantity = quantities[card.dataset.productId] || 0;

            label.textContent = label.dataset.format.replace('{0}', quantity);
            label.hidden = quantity <= 0;
        });
    }

    /* The header count and the flyout live outside this page's markup. nop has no "cart changed" event to
       raise - what it does is return the two blocks as rendered HTML from the add endpoint and write them in
       (public.ajaxcart.js :136-144). The selectors come from AjaxCart, which the theme's HeaderLinks
       component initialises on every storefront page, so a theme that moves its cart block only has to say
       so in the one place it already says it. */
    function applyCartBlocks(result) {
        if (!window.AjaxCart) return;

        if (result.updatetopcartsectionhtml && AjaxCart.topcartselector) {
            document.querySelectorAll(AjaxCart.topcartselector).forEach(function (node) {
                node.innerHTML = result.updatetopcartsectionhtml;
            });
        }

        if (result.updateflyoutcartsectionhtml && AjaxCart.flyoutcartselector) {
            var flyout = document.querySelector(AjaxCart.flyoutcartselector);

            //outerHTML, not innerHTML: the fragment IS the flyout element, so writing it inside would nest a
            //second one and the next refresh would find the wrong node. nop uses .replaceWith() for this.
            if (flyout) flyout.outerHTML = result.updateflyoutcartsectionhtml;
        }
    }

    /* One endpoint for a single card and for the whole page. The only difference is scope. */
    function submitLines(scope, status, button) {
        //a second click before the first response returns posts the same lines again and silently doubles the
        //quantities; nop's own AjaxCart carries loadWaiting for exactly this
        if (adding) {
            setStatus(status, container.dataset.addBusy, 'warn', true);
            return;
        }

        //a card whose options are being updated still carries the old ones in its cells
        if (gridsIn(scope).some(function (grid) { return grid.dataset.pending; })) {
            setStatus(status, container.dataset.gridBusy, 'warn', true);
            return;
        }

        clearLineResults(scope);

        var invalid = [];
        var lines = collectLines(scope, invalid);

        //a box that does not hold a whole number is marked and nothing is sent: the customer fixes it first
        if (invalid.length > 0) {
            var byGrid = new Map();

            invalid.forEach(function (input) {
                input.classList.add('line-failed');
                input.setAttribute('aria-invalid', 'true');

                var grid = input.closest('.bulk-order-grid');
                if (!byGrid.has(grid)) byGrid.set(grid, []);

                byGrid.get(grid).push({
                    label: input.dataset.axisKey === 'product' ? '' : (input.getAttribute('aria-label') || ''),
                    text: container.dataset.invalidQuantity,
                    key: input.dataset.axisKey
                });
            });

            byGrid.forEach(function (items, grid) { renderWarnings(grid, items); });

            //clearLineResults took the over-max marks too, and their numbers are still in the boxes
            gridsIn(scope).forEach(function (grid) { flagOverMax(grid); });
            setStatus(status, container.dataset.invalidQuantity, 'error');
            invalid[0].focus();
            return;
        }

        if (lines.length === 0) {
            setStatus(status, container.dataset.nothing, 'warn');
            return;
        }

        //the pill from the previous attempt is about to be wrong either way, and leaving it up while
        //the retry is in flight shows a failure message over a request that may well succeed
        if (status) status.hidden = true;

        var token = document.querySelector('input[name="__RequestVerificationToken"]');

        adding = true;
        if (button) button.disabled = true;

        //nop's own global spinner (public.common.js:20), which is what the rest of the site uses. A page-wide add is one AddToCartAsync per line and takes real time; a disabled button
        //says "not again", it does not say "working".
        if (typeof displayAjaxLoading === 'function') displayAjaxLoading(true);

        fetch(container.dataset.addUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token ? token.value : '',
                //IWebHelper.IsAjaxRequest tests exactly this header (WebHelper.cs:426), and it is what makes
                //CWFeatureBlockResultBuilder answer a disabled feature with JSON instead of a 302 to the
                //homepage - which fetch would follow, and which we would then try to parse
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({ lines: lines })
        })
            .then(function (r) {
                if (!r.ok) throw new Error(r.status);
                return r.json();
            })
            .then(function (result) {
                //the feature gate answers an AJAX POST with {success:false, error} instead of an add result;
                //without this the page would read a blocked request as "0 lines added"
                if (result && result.success === false && result.error) {
                    setStatus(status, result.error, 'error');
                    return;
                }

                applyLineResults(scope, result, status);
            })
            .catch(function () {
                setStatus(status, container.dataset.addFailed, 'error');
            })
            .finally(function () {
                adding = false;

                if (typeof displayAjaxLoading === 'function') displayAjaxLoading(false);

                //the button may have been swapped out from under us by a grid reload; setting a property on a
                //detached node is harmless, and re-querying for it would find the wrong one
                if (button) button.disabled = false;

                //clearLineResults stripped the over-max marks on the way out, on the promise that the server's
                //answer would replace them - and two exits above never get one: the feature gate's
                //{success:false} and the network catch. The numbers are still in the boxes (flagOverMax does not rewrite
                //them), so without this the card ends up quietly accepting a quantity it had just refused.
                //Safe on the success path too: flagOverMax skips any cell the server has already answered
                //about, so it cannot say a second, different thing about it.
                gridsIn(scope).forEach(function (grid) { flagOverMax(grid); });
            });
    }

    /* Each row-axis column becomes its own sticky column, at the offset where it actually sits.
   CSS cannot do this: `left` has to be the summed width of the columns to the left, and those widths
   come from the attribute values. The old rule pinned every label column to `left: 0` and they stacked
   - on a three-axis product at 375px the second and third label columns showed as slivers under the
   first, and the first quantity column slid under the pile.
   Not every label column gets pinned. Three label columns are 232px of a 338px card, and pinning all
   three leaves 106px to scroll in, which is no longer a table. So columns are pinned from the left
   while the block stays under half the visible width; the first is always pinned however wide it is,
   because a quantity box whose row you cannot name is worth nothing.
   Sticky positioning does not change an element's in-flow size, so measuring and then pinning in the
   same pass cannot feed back into the widths it just measured - no second layout, no loop. */
    function pinRowLabels(scope) {
        if (!scope) return;

        scope.querySelectorAll('.grid-table').forEach(function (table) {
            var head = table.tHead && table.tHead.rows[0];
            if (!head) return;

            var scroll = table.closest('.grid-scroll') || table.parentNode;
            var rows = [].slice.call(table.rows);

            //one cell per row axis in the header, no colspan among them; every row - both header rows and
            //every body row - starts with exactly that many label cells before the quantity columns
            var count = head.querySelectorAll('.row-axis').length;
            var i, r;

            //cleared first, unconditionally: this runs again on resize and after every grid swap, and a
            //column that is no longer pinned must not keep last time's offset
            rows.forEach(function (row) {
                for (var c = 0; c < count && c < row.cells.length; c++) {
                    row.cells[c].classList.remove('pinned-col', 'pinned-last');
                    row.cells[c].style.left = '';
                }
            });

            if (!count) return;

            //nothing to pin against. At a desktop width most grids on a page fit their card and only the
            //widest scrolls at all; pinning the rest buys a sticky layer each, and a border
            //and a divider drawn over borders that are already there, to hold columns that never move.
            //Checked here rather than at the end because the answer decides whether to pin at all, not just
            //whether to draw the edge - and re-checked on every resize, which is when it changes.
            if (scroll.scrollWidth <= scroll.clientWidth) return;

            var budget = scroll.clientWidth / 2;
            var offset = 0;
            var pinned = 0;

            for (i = 0; i < count; i++) {
                var width = head.cells[i].getBoundingClientRect().width;

                //i > 0: the first column is pinned whatever it costs
                if (i > 0 && offset + width > budget) break;

                for (r = 0; r < rows.length; r++) {
                    var cell = rows[r].cells[i];
                    if (!cell) continue;

                    cell.classList.add('pinned-col');
                    cell.style.left = offset + 'px';
                }

                offset += width;
                pinned = i + 1;
            }

            rows.forEach(function (row) {
                var last = row.cells[pinned - 1];
                if (last) last.classList.add('pinned-last');
            });
        });
    }

    /* ------------------------------------------------------------------ binding */

    /* The handlers a CARD needs, wherever that card lives: its pin strip, a typed quantity, and its add
       button. Delegated to `scope`, so a grid that reloadGrid has replaced keeps working with no rebinding.
       Shared by init() and initCard() rather than copied: two copies of the add-to-cart path would have to
       be kept in step by hand, and there is meant to be one path for one job.
       The .bulk-add-all branch travels with them. It is list-only and matches nothing on a product page, so
       carrying it costs a failed `closest` per click and keeps the comment about token matching where the
       code it explains actually is. */
    function bindCardHandlers(scope) {
        //pin changes re-render one card; delegated, so it survives every content swap
        scope.addEventListener('change', function (e) {
            var pin = e.target.closest('.bulk-pin, .bulk-pin-check');
            if (!pin) return;

            reloadGrid(pin.closest('.bulk-order-grid'));
        });

        //A typed quantity above what the cell can supply. HTML's max constrains the spinner arrows and form
        //validation and constrains TYPING not at all, so the number sits there looking accepted.
        //`change` rather than `input`: on `input` the card would flash a refusal at the 1 of 15 before the
        //customer had finished typing it.
        //input only - the AllowedQuantities control is a select whose options _Grid.cshtml has already
        //filtered to what the cell can supply, so nothing there can exceed it.
        //The whole CARD is re-derived, not just this box: two cells can be over their maximum at once, and
        //correcting one must drop exactly its own entry.
        scope.addEventListener('change', function (e) {
            var input = e.target.closest('input.bulk-qty');
            if (!input) return;

            //the server's refusal was about the number that WAS in this box. Editing it makes that answer
            //stale, so it goes before anything is re-derived - otherwise the border and the sentence sit
            //there quoting a value nobody can see any more. Scoped to THIS box on purpose: another
            //cell's refusal is still true, and clearing the card would erase it.
            clearRefusal(input);

            flagOverMax(input.closest('.bulk-order-grid'));
        });

        //Both add buttons live INSIDE the scope - the per-card ones always did, and the page-wide one moved
        //next to the pager - so both are replaced on every content swap and neither can hold a direct
        //listener. One delegated handler covers them; note that closest('.bulk-add') does NOT match
        //.bulk-add-all, since class selectors match whole tokens, so the order of these two branches is
        //safety, not necessity.
        scope.addEventListener('click', function (e) {
            var all = e.target.closest('.bulk-add-all');

            if (all) {
                submitLines(container, all.parentNode.querySelector('.bulk-add-status'), all);
                return;
            }

            var button = e.target.closest('.bulk-add');
            if (!button) return;

            var grid = button.closest('.bulk-order-grid');
            if (!grid) return;

            submitLines(grid, button.parentNode.querySelector('.bulk-add-status'), button);
        });
    }

    /* Typed quantities exist only in the DOM and the browser discards the DOM on
       navigation, so the browser is the one to ask. Suppressed while an add is in flight: a submit never
       navigates, and the response is about to clear the boxes anyway.
       As true of one card on a product page as of fifteen on the list — a customer who typed into a matrix
       and clicked a link has lost the same work either way. */
    function bindUnloadGuard() {
        window.addEventListener('beforeunload', function (e) {
            if (adding || Object.keys(collectTyped(container)).length === 0) return;

            e.preventDefault();
            e.returnValue = '';
        });
    }

    /* The pin budget is a fraction of the visible width, so it is a different answer at a different width -
       and on a phone, rotating the device is exactly when it changes. */
    function bindPinResize() {
        window.addEventListener('resize', function () {
            clearTimeout(pinTimer);
            pinTimer = setTimeout(function () { pinRowLabels(container); }, 150);
        });
    }

    function init() {
        container = document.getElementById('bulk-order-content');

        bindUnloadGuard();

        ['bulk-order-id', 'bulk-sort', 'bulk-pagesize'].forEach(function (id) {
            document.getElementById(id).addEventListener('change', function () {
                load(1);          // any control change resets to page 1
            });
        });

        //view mode is two icon toggles over a hidden input. The input is what buildQuery reads,
        //so the request contract is unchanged; only the control the customer touches is different.
        var viewmode = document.querySelector('.bulk-order-viewmode');

        if (viewmode) {
            viewmode.addEventListener('click', function (e) {
                var btn = e.target.closest('[data-viewmode]');
                if (!btn || btn.classList.contains('selected')) return;

                viewmode.querySelectorAll('[data-viewmode]').forEach(function (other) {
                    var on = other === btn;
                    other.classList.toggle('selected', on);
                    other.setAttribute('aria-pressed', on ? 'true' : 'false');
                });

                document.getElementById('bulk-viewmode').value = btn.dataset.viewmode;
                load(1);
            });
        }

        //only the filter decides whether the order picker is relevant
        document.getElementById('bulk-filter').addEventListener('change', function () {
            toggleOrderPicker();
            load(1);
        });

        //delegated, so it survives every content swap without rebinding
        container.addEventListener('click', function (e) {
            var link = e.target.closest('[data-category-id]');
            if (!link) return;

            e.preventDefault();
            document.getElementById('bulk-category-id').value = link.dataset.categoryId;
            load(1);
        });

        bindCardHandlers(container);
        bindPinResize();

        toggleOrderPicker();
        markApplied(container);
        bindPager();   //the first page comes from the server; JS only takes over on change
        pinRowLabels(container);   //...and so does the first page's pinning
    }

    /* One card, on a product details page. `root` is the wrapper the widget component rendered, and it
       stands in for #bulk-order-content: it carries the same data-* attributes the add path reads off
       `container`, minus the three that only paging uses (data-url, data-typed-lost, data-load-failed).
       Assigning it to `container` is the point rather than a shortcut. Every function that reads
       `container` — flagOverMax's wording, submitLines' endpoint and messages, revealFirstFailure's search,
       applyLineResults' per-product lookup — is asking "what is the scope of this screen", and on a product
       page the scope IS this one card. The two screens never coexist, so the single module-level reference
       stays honest.
       Nothing list-shaped is bound: no pager, no filter, no sort, no page size, no view mode, no category
       tree, and no page-wide add. None of those elements exist here, and binding to them would be
       document.getElementById(...) on null.
       Two functions become deliberate no-ops rather than errors, both by their own existing guards:
       announce() finds no #bulk-order-status and returns, and applyCartQuantities finds no
       .bulk-order-product to write "n in cart" into. The second is a real gap rather than an oversight and
       it is logged as such — the product page has no in-cart line of ours to refresh. */
    function initCard(root) {
        if (!root) return;

        container = root;

        bindUnloadGuard();
        bindCardHandlers(root);
        bindPinResize();

        markApplied(root);
        pinRowLabels(root);
    }

    return { init: init, initCard: initCard, reload: load };
}());