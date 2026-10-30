var Admin = Admin || {};

Admin.StoreNav = (function () {
    let _storeId = 0;

    // ── Resources ──────────────────────────────────────────────────────
    // Filled by init() from the one host view (_CreateOrUpdate.cshtml). The fallback is the English
    // text, so a missing resource degrades to readable English rather than to "undefined".
    // Set as JSON through Html.Raw, NOT via @T(...).Text: Razor HTML-encodes .Text, which is how
    // round J shipped a button reading "Saving&#x2026;".
    let _res = {};

    function res(key, fallback) {
        return _res[key] || fallback || '';
    }

    //DATATABLES RENDER CALLBACKS LIVE HERE, NOT ON window. The DataTables template emits
    //`return <FunctionName>(data, type, row, meta);` with the name as a raw JS expression, so a
    //dotted path works and `RenderCustom("Admin.StoreNav.render.gender")` resolves. That is what lets
    //these stop being globals.
    //
    //gender was declared SEVEN TIMES, byte-identical, as a global: _PackNames, _Departments,
    //_Branches.PackNamesInline, _Branches.PackNameListDetail.Managers,
    //_Departments.DepartmentDetail.Managers, ManagerAddPopup, DepartmentManagerAddPopup. Tab panels
    //stay in the DOM once loaded, so whichever rendered last won. One copy, here.
    //
    //packNameEdit/packNameDelete emit data-id ONLY. The previous version interpolated row.Name into
    //an onclick, which is the entire reason jsArg exists; the delegated handler reads the name from
    //the DataTable row API instead, so there is nothing to escape.
    const render = {
        gender: function(data) {
            if (data === 'I') return res('gender.mixed', 'Mixed');
            if (data === 'M') return res('gender.male', 'Male');
            if (data === 'F') return res('gender.female', 'Female');
            if (data === 'N') return res('gender.neutral', 'Neutral');
            return '';
        },

        packNameEdit: function(data) {
            return '<button type="button" class="btn btn-default btn-edit-pn" data-id="' + data + '">'
                + '<i class="fas fa-pencil-alt"></i>' + res('common.edit', 'Edit') + '</button>';
        },

        packNameDelete: function(data) {
            return '<button type="button" class="btn btn-default btn-delete-pn" data-id="' + data + '">'
                + '<i class="far fa-trash-alt"></i>' + res('common.delete', 'Delete') + '</button>';
        },

        //ONE PAIR FOR ALL THREE TAB GRIDS (cost centres, departments, packNames). The three tab
        //partials each carried their own identical pair, differing only in the global they called
        //from an onclick=. These emit the row id and nothing else; each tab's IIFE delegates from
        //its own #tab-panel-* node, so the shared class names cannot cross tabs.
        tabEdit: function(data) {
            return '<button type="button" class="btn btn-default btn-tab-edit" data-id="' + data + '">'
                + '<i class="fas fa-pencil-alt"></i>' + res('common.edit', 'Edit') + '</button>';
        },

        tabDelete: function (data) {
            return '<button type="button" class="btn btn-default btn-tab-delete" data-id="' + data + '">'
                + '<i class="far fa-trash-alt"></i>' + res('common.delete', 'Delete') + '</button>';
        },

        //THE PRODUCT GROUP ICON, drawn by three grids on this page (the product-groups tab, the
        //subsidy detail's product-group grid, the packname panel's per-subsidy grid). Each carried its
        //own byte-identical copy as a global; whichever loaded last won. One copy, here. The svg sprite
        //path is a static content file, not a route, so it is the one literal path in this module.
        productGroupIcon: function (data, type, row) {
            return data
                ? '<svg class="pg-icon" style="color:' + (row.Colour ? row.Colour : '#000000') + '" aria-hidden="true">'
                + '<use href="/Plugins/CW.Core/Content/icons/pg-icons.svg#' + data + '"/></svg>'
                : '';
        },

        //THE SUBSIDY DETAIL'S PACKNAME GRID. A distinct class from btn-tab-edit, because the detail
        //panel sits inside #tab-panel-subsidies and the tab's own delegated handler would otherwise
        //open a subsidy for a click on a packname row. Removal there is nop's RenderButtonRemove.
        subsidyVariationEdit: function (data) {
            return '<button type="button" class="btn btn-default btn-subsidy-variation-edit" data-id="' + data + '">'
                + '<i class="fas fa-pencil-alt"></i>' + res('common.edit', 'Edit') + '</button>';
        },

        //THE DEPARTMENTS CHILD GRID needs a pair of ids, not one: the managers table is a DataTables
        //child table, so its rows are not reachable through the parent table's row() API and the
        //department id cannot be looked up the way tabEdit looks up a name.
        departmentManagerDelete: function(data, type, row) {
            return '<button type="button" class="btn btn-default btn-dep-manager-delete"'
                + ' data-id="' + data + '" data-department-id="' + row.DepartmentId + '">'
                + '<i class="far fa-trash-alt"></i>' + res('common.delete', 'Delete') + '</button>';
        },

        //ONE PAIR FOR ALL FOUR ADDRESS GRIDS. The four *Detail.Addresses partials carried eight
        //near-identical render functions between them, differing only in a popup action, a
        //query-parameter name, a button id and a form id — all of which each partial now holds in its
        //own closure. These emit the address id and nothing else.
        //
        //IsEditable IS THE RULE, NOT A PACKNAME QUIRK. CWAddressModelFactory defaults it true, so the
        //two cost-centre grids get the button exactly as they do today; PackNameModelFactory sets it
        //false for an address inherited from the customer or from the branch and puts a label in
        //BranchCode. Omitting this branch would have put Edit and Delete on rows that cannot be
        //edited and thrown the branch code away.
        addressEdit: function(data, type, row) {
            if (row.IsEditable === false)
                return '<div class="grid-actions">' + (row.BranchCode || '') + '</div>';

            return '<button type="button" class="btn btn-default btn-edit-address" data-address-id="' + row.AddressId + '">'
                + '<i class="fas fa-pencil-alt"></i>' + res('common.edit', 'Edit') + '</button>';
        },

        addressDelete: function(data, type, row) {
            if (row.IsEditable === false)
                return '<div class="grid-actions"></div>';

            return '<button type="button" class="btn btn-default btn-delete-address" data-address-id="' + data + '">'
                + '<i class="far fa-trash-alt"></i>' + res('common.delete', 'Delete') + '</button>';
        }
    };

    function jsArg(value) {
        return "'" + String(value === null || value === undefined ? '' : value)
            .replace(/\\/g, '\\\\')
            .replace(/'/g, "\\'")
            .replace(/\r?\n/g, '\\n')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;') + "'";
    }

    // -- Unsaved-edit guard and the saved stamp -------------------------
    // SAVE NEVER NAVIGATES: every detail form stays open after a successful save, so leaving is
    // always the admin's own act. There are about forty places in this section that can tear an
    // open form down - breadcrumbs, the tree, tabs, row clicks, add buttons - so the guard is NOT
    // at the call sites. It sits inside the six functions that actually do the tearing down
    // (navigate-with-reload, goTab, selectBranch, enterFocusMode, exitFocusMode,
    // loadNewChildDetail) plus the three tab exits. A new call site cannot forget it.
    //
    // DIRTINESS IS READ FROM THE DOM, not tracked with events. An earlier version listened for
    // input/change on :input; any editor widget that fires change while initialising would have
    // marked the form dirty before the admin touched it, and every Back click would have prompted.
    // Comparing serialize() against the baseline taken at bind time cannot do that, and it also
    // reports clean when someone types something and undoes it.
    let _leaveAnswer = null;
    let _flashSaved = false;

    // THE ENTITY'S OWN FIELDS ONLY. The child grids - managers, addresses - are rendered INSIDE the
    // <form>, and DataTables puts its own controls in there with them: the page-length select posts
    // a name, so $form.serialize() changes when the admin pages a grid and the form would read as
    // edited without a single field being touched. Every child section is wrapped in
    // .card-ms-inner-section, which is the line between "the entity" and "things the entity owns";
    // a new child section must keep that class or it will start faking unsaved edits.
    function formState($form) {
        return $form.find(':input').not('.card-ms-inner-section :input').serialize();
    }

    // Called once per detail form, right after it is bound. The class is on the <form> in the
    // view; this records what "unchanged" looks like for it.
    function watchForm($form) {
        if (!$form || !$form.length) return;
        $form.data('ms-baseline', formState($form));
    }

    // The server rejected the post, so the redisplayed form holds edits that were never written.
    // Its own state now matches what came back, so only an impossible baseline keeps it dirty.
    function markFormDirty($form) {
        if (!$form || !$form.length) return;
        $form.data('ms-baseline', '\u0000never');
    }

    // The record behind this form is gone (deleted), so there is nothing left to save.
    function releaseForm($form) {
        if (!$form || !$form.length) return;
        $form.removeData('ms-baseline');
    }

    // The gate. ONE QUESTION PER GESTURE: a single click can pass through two gated functions
    // (_Branches.CostCenters.cshtml calls navigate() and then enterFocusMode()), so the answer is
    // memoised and dropped at the end of the current task. Without that, saying "no" to the first
    // would be asked again by the second.
    function canLeaveForm() {
        if (_leaveAnswer !== null) return _leaveAnswer;

        //NO :visible FILTER HERE. #branch-detail-body is a collapsible card and .collapsed is
        //display:none in the stylesheet, so a branch form the admin edited and then collapsed
        //was skipped by the gate and its edits thrown away without a word. Staleness is what
        //:visible was really guarding against, and that is handled on the way out instead.
        const $dirty = $('.ms-detail-form').filter(function() {
            const baseline = $(this).data('ms-baseline');
            return baseline !== undefined && formState($(this)) !== baseline;
        });
        if (!$dirty.length) return true;

        const ok = confirm(res('common.unsavedChanges', 'This form has unsaved changes. Leave without saving?'));

        //ACCEPTING THE LOSS CLEARS THE BASELINE. A tab panel is hidden, not removed, so a form the
        //admin walked away from stays in the DOM holding the edits they just agreed to discard.
        //Without this it keeps reporting dirty and prompts on every later exit, about work that is
        //already gone.
        if (ok) $dirty.each(function() { releaseForm($(this)); });

        _leaveAnswer = ok;
        setTimeout(function() { _leaveAnswer = null; }, 0);
        return ok;
    }

    // "Saved 14:06" in the open form's header. Every detail partial carries one empty
    // <span class="ms-saved-stamp">, hidden until a save actually lands - the panel did not move,
    // so a page-level notification would be noise, but silence would be worse.
    // SCOPED TO ONE FORM ON PURPOSE: the tabs are hidden with .hide(), not removed, so several
    // detail panels - each with its own stamp - sit in the DOM at once. An unscoped selector
    // stamped all of them, and the hidden ones then showed a stale time the next time that tab was
    // opened. Every other stamp is cleared here, so only the form that was saved carries one.
    //
    // THIS IS ALSO WHERE STALE VALIDATION GOES. A save that lands makes every error on the form
    // untrue, and the server-rendered ones are plain markup the validator knows nothing about -
    // resetForm() cannot touch them. While a save closed the panel they disappeared with it; now
    // the panel stays, so "Name is required" sat under a name that had just been saved. This runs
    // on the edit path only, which is the only path where the markup survives: a create reloads
    // the panel and comes back clean.
    function markSaved($scope) {
        const now = new Date();
        const hh = ('0' + now.getHours()).slice(-2);
        const mm = ('0' + now.getMinutes()).slice(-2);
        $('.ms-saved-stamp').hide().text('');

        const $form = $scope ? $($scope) : $();

        const validator = $form.data('validator');
        if (validator) validator.resetForm();

        //Only the replaceable spans are emptied: data-valmsg-replace="false" marks one holding
        //static text that belongs to the layout, not to an error.
        $form.find('.field-validation-error')
            .removeClass('field-validation-error')
            .addClass('field-validation-valid')
            .filter('[data-valmsg-replace="true"]')
            .empty();

        $form.find('.input-validation-error')
            .removeClass('input-validation-error')
            .addClass('input-validation-valid');

        $form.find('.validation-summary-errors')
            .removeClass('validation-summary-errors')
            .addClass('validation-summary-valid')
            .find('ul').empty();

        $form.find('.ms-saved-stamp')
            .text(res('common.saved', 'Saved') + ' ' + hh + ':' + mm)
            .show();
    }

    // A CREATE reloads the panel for the new id, which throws the stamp away with the old markup.
    // This flag survives that one reload, so the stamp lands on the form the admin is left looking
    // at rather than on the one about to be replaced.
    function flashSavedAfterReload() { _flashSaved = true; }

    function consumeSavedFlash($scope) {
        if (!_flashSaved) return;
        _flashSaved = false;
        markSaved($scope);
    }

    // -- Collections: the inline add row and the empty state ------------
    // A COLLECTION IS NEVER AN EMPTY TABLE. The last row of every collection is the row that adds
    // to it, and when the collection has nothing in it that add row is all there is - no column
    // headers over no rows, no DataTables "No data available in table", no icon-and-paragraph
    // illustration. Three collections on the branch page share these two helpers rather than
    // carrying three copies of the same logic.

    // THE ADD ROW LIVES OUTSIDE THE TABLE. DataTables owns its tbody and rewrites it on every draw,
    // so a row injected there disappears on the next refresh. It sits directly below the table
    // inside the same bordered container, styled to read as the last row.
    //
    // config: { input, url, fields(name) -> {..post..}, onCreated(result), onError(message) }
    //Set by a successful add, consumed by the next add row that binds. Survives the one panel
    //reload between the two, and is cleared on the way out so a later render does not steal focus.
    let _focusAddRow = null;

    //THE BRANCH DETAILS DISCLOSURE, remembered for the session. null means "no opinion yet", so
    //the page decides from Model.IsNew: a branch being created has nothing but its form, an
    //existing one has its collections and its add rows. Once the admin opens or closes it by
    //hand that choice is theirs and follows them from branch to branch, which is the point - an
    //admin who said "I don't want to see this" should not be shown it again on the next branch.
    let _branchDetailOpen = null;

    function branchDetailOpen(value) {
        if (value === undefined) return _branchDetailOpen;
        _branchDetailOpen = value;
    }

    function bindInlineAdd(config) {
        const $input = $(config.input);
        if (!$input.length) return;

        if (_focusAddRow === config.input) {
            _focusAddRow = null;
            $input.trigger('focus');
        }

        //VAR, NOT LET - DO NOT "MODERNISE" THIS. WebOptimizer minifies the admin bundle
        //(WebOptimizer.EnableJavaScriptBundling in appsettings.json turns bundling and minification
        //on), and NUglify rewrites the early return above into a wrapping if-block. Doing so it
        //moved this declaration INSIDE that block while leaving the hoisted function commit() outside
        //it, so commit's `busy` resolved to an unrelated outer variable that happened to be truthy and
        //`if (!name || busy) return;` bailed on every keystroke and every click - no request, no error,
        //nothing in the console. var is function-scoped whatever the minifier does with the
        //declaration, and the block runs at bind time, long before any handler can fire.
        //Only reproducible in the minified bundle; the unminified source works, which is what made
        //this so slow to find.
        var busy = false;

        function commit() {
            const name = $.trim($input.val());
            if (!name || busy) return;

            //THE SAME POST THE FULL FORM MAKES when you type only a name. The defaults come from
            //the factories: PreparePackNameListModelAsync and PrepareCostCenterModelAsync both set
            //Active = true on a new record and leave everything else false, and SavePackNameList
            //makes the first list on a branch the default by itself. This is a shortcut through the
            //same door, never a second way in with different rules.
            const postData = config.fields(name);
            addAntiForgeryToken(postData);

            busy = true;
            $input.prop('disabled', true);

            $.ajax({
                type: 'POST',
                url: config.url,
                data: postData,
                success: function(result, status, xhr) {
                    busy = false;
                    $input.prop('disabled', false);

                    const ct = xhr.getResponseHeader('Content-Type') || '';

                    //A VALIDATION FAILURE COMES BACK AS THE WHOLE DETAIL PARTIAL, which has nowhere
                    //to go from an add row. The only rule a name-only post can break is a name the
                    //server rejects, so it is reported on the row itself and the typing is kept.
                    if (ct.indexOf('text/html') !== -1 || (result && result.success === false)) {
                        showError(result && result.error);
                        return;
                    }

                    clearError();
                    $input.val('');

                    //STRAIGHT BACK INTO THE ADD ROW: setting up a branch means creating several in
                    //a row, and returning focus is the whole point of the inline form. WHICH ROW
                    //depends on what onCreated does. A grid refresh leaves this input alive, so
                    //focusing it here works. A panel reload replaces it, so focusing it here put
                    //the cursor in an element that was about to be thrown away - the flag below is
                    //picked up by whichever add row renders next instead.
                    _focusAddRow = config.input;
                    if (config.onCreated) config.onCreated(result);

                    //STILL IN THE DOCUMENT means onCreated refreshed a grid rather than reloading
                    //the panel, so this input is the one to focus and the flag has no journey to
                    //survive. Clearing it here matters: left set, it is picked up the next time this
                    //panel renders for any other reason - coming back from a list, switching away
                    //and back - and the cursor jumps into the add row with nothing having been added.
                    if ($.contains(document, $input[0])) {
                        _focusAddRow = null;
                        $input.trigger('focus');
                    }
                },
                error: function() {
                    busy = false;
                    $input.prop('disabled', false);
                    showError(null);
                }
            });
        }

        function showError(message) {
            $input.closest('.ms-add-row').addClass('has-error')
                .find('.ms-add-error')
                .text(message || res('common.serverError', 'A server error occurred. Please try again.'))
                .show();
        }

        function clearError() {
            $input.closest('.ms-add-row').removeClass('has-error')
                .find('.ms-add-error').hide().text('');
        }

        $input.on('keydown', function(e) {
            if (e.key === 'Enter' || e.keyCode === 13) {
                //Enter inside a form submits it. This input sits inside the branch page's forms, so
                //without this the browser posts the detail form instead of adding a row.
                e.preventDefault();
                commit();
                return;
            }
            if (e.key === 'Escape' || e.keyCode === 27) {
                $input.val('');
                clearError();
            }
        });

        $input.on('input', clearError);

        //THE PLUS COMMITS TOO. Enter alone was undiscoverable: the row showed a caret in a text
        //field and no control, so it read as decoration rather than as the way to add something.
        $input.closest('.ms-add-row').find('.ms-add-plus').on('click', function() {
            if (!$.trim($input.val())) {
                $input.trigger('focus');
                return;
            }
            commit();
        });
    }

    // Hides the grid while the collection is empty, so the add row is the whole empty state, and
    // keeps the heading's count chip honest. draw.dt fires on the first draw as well as on every
    // refresh, so there is no separate initial pass.
    //
    // config: { grid, hide, count }
    function bindGridEmptyState(config) {
        const $grid = $(config.grid);
        if (!$grid.length) return;

        $grid.on('draw.dt', function() {
            const api = $grid.DataTable();
            const total = api.page.info().recordsTotal;

            $(config.hide).toggle(total > 0);
            if (config.count) $(config.count).text(total);
        });
    }

    // ── State ──────────────────────────────────────────────────────────
    // mode: 'browse' | 'focus'
    // focusEntity: null | 'branch' | 'list' | 'costcenter' | 'packname'
    let _state = {
        tab: 'info',
        branchPath: [],
        listId: null,
        packNameId: null,
        costCenterId: null,
        departmentId: null,
        productGroupId: null,
        subsidyId: null,
        mode: 'browse',
        focusEntity: null,
        nameCache: {}
    };

    let _features = { packName: true, subsidy: true, exchanges: true, creditNotes: true };

    // Track what's currently rendered in each slot
    // so we don't reload a panel that's already showing the right content
    let _rendered = {
        branchTree: null,
        branchDetail: null,
        departments: null,
        costcenters: null,
        packnames: null,
        settings: null,
        productgroups: null,
        subsidies: null,
    };

    let _pendingRequests = {};

    // ── Name cache ─────────────────────────────────────────────────────

    function getCachedName(type, id) {
        return _state.nameCache[type + ':' + String(id)] || null;
    }

    function cacheNames(items) {
        items.forEach(item => {
            _state.nameCache[item.Type + ':' + String(item.Id)] = item.Name;
        });
    }

    function cacheName(type, id, name) {
        _state.nameCache[type + ':' + String(id)] = name;
    }

    function missingIds() {
        const needed = [];
        _state.branchPath.forEach(b => {
            if (!_state.nameCache['branch:' + String(b.id)]) needed.push({ type: 'branch', id: b.id });
        });
        if (_state.listId && !_state.nameCache['list:' + String(_state.listId)])
            needed.push({ type: 'list', id: _state.listId });
        if (hasValue(_state.packNameId) && !_state.nameCache['pn:' + String(_state.packNameId)])
            needed.push({ type: 'pn', id: _state.packNameId });
        if (hasValue(_state.costCenterId) && !_state.nameCache['cc:' + String(_state.costCenterId)])
            needed.push({ type: 'cc', id: _state.costCenterId });
        if (hasValue(_state.departmentId) && !_state.nameCache['dep:' + String(_state.departmentId)])
            needed.push({ type: 'dep', id: _state.departmentId });
        if (hasValue(_state.productGroupId) && !_state.nameCache['pg:' + String(_state.productGroupId)])
            needed.push({ type: 'pg', id: _state.productGroupId });
        if (hasValue(_state.subsidyId) && !_state.nameCache['sub:' + String(_state.subsidyId)])
            needed.push({ type: 'sub', id: _state.subsidyId });

        return needed;
    }

    // --- Fetch names for any IDs not yet in cache ---
    function fetchMissingNames(onComplete) {
        const missing = missingIds();
        if (missing.length === 0) { onComplete(); return; }

        const branchIds = missing
            .filter(m => m.type === 'branch')
            .map(m => m.id);
        const listItem = missing.find(m => m.type === 'list');
        const packNameItem = missing.find(m => m.type === 'pn');
        const costCenterItem = missing.find(m => m.type === 'cc');
        const departmentItem = missing.find(m => m.type === 'dep');
        const productGroupItem = missing.find(m => m.type === 'pg');
        const subsidyItem = missing.find(m => m.type === 'sub');

        const params = { storeId: _storeId };
        if (branchIds.length) params.branchIds = branchIds;
        if (listItem) params.listId = listItem.id;
        if (packNameItem) params.packNameId = packNameItem.id;
        if (costCenterItem) params.costCenterId = costCenterItem.id;
        if (departmentItem) params.departmentId = departmentItem.id;
        if (productGroupItem) params.productGroupId = productGroupItem.id;
        if (subsidyItem) params.subsidyId = subsidyItem.id;

        $.ajax({
            url: '/Admin/MultiStore/BreadcrumbData',
            type: 'GET',
            data: params,
            traditional: true,          // ← serializes as BranchIds=1&BranchIds=12
            dataType: 'json',
            success: items => { cacheNames(items); onComplete(); },
            error: () => onComplete()
        });
    }

    // ── Hash ───────────────────────────────────────────────────────────

    function buildHash() {
        const parts = [_state.tab];
        _state.branchPath.forEach(b => parts.push('branch:' + b.id));
        if (_state.listId) parts.push('list:' + _state.listId);
        if (hasValue(_state.packNameId)) parts.push('pn:' + _state.packNameId);
        if (hasValue(_state.costCenterId)) parts.push('cc:' + _state.costCenterId);
        if (hasValue(_state.departmentId)) parts.push('dep:' + _state.departmentId);
        if (hasValue(_state.productGroupId)) parts.push('pg:' + _state.productGroupId);
        if (hasValue(_state.subsidyId)) parts.push('sub:' + _state.subsidyId);
        if (_state.focusEntity) parts.push('edit:' + _state.focusEntity);
        return '#' + parts.join('/');
    }

    function parseHash() {
        const raw = location.hash.replace('#', '') || 'info';
        const parts = raw.split('/');

        _state.tab = parts[0];
        _state.branchPath = [];
        _state.listId = null;
        _state.packNameId = null;
        _state.costCenterId = null;
        _state.departmentId = null;
        _state.productGroupId = null;
        _state.subsidyId = null;
        _state.mode = 'browse';
        _state.focusEntity = null;

        parts.slice(1).forEach(segment => {
            const [type, id] = segment.split(':');
            if (type === 'branch') _state.branchPath.push({ id });
            if (type === 'list') _state.listId = id;
            if (type === 'pn') _state.packNameId = id;
            if (type === 'cc') _state.costCenterId = id;
            if (type === 'dep') _state.departmentId = id;
            if (type === 'pg') _state.productGroupId = id;
            if (type === 'sub') _state.subsidyId = id;
            if (type === 'edit') {
                _state.focusEntity = id;
                _state.mode = 'focus';
            }
        });
    }

    // ── Breadcrumb ─────────────────────────────────────────────────────

    function updateBreadcrumb() {
        const $bc = $('#section-breadcrumb');
        $bc.empty();

        const storeName = getCachedName('store', _storeId) || res('entity.store', 'Store');
        $bc.append(crumbLink(storeName, () => goTab('info')));

        let tabLabel = $('#tab-' + _state.tab).text();

        if (_state.tab === 'branches') {
            $bc.append(crumbSep());

            if (!_state.branchPath.length) {
                $bc.append(crumbCurrent(tabLabel));
                return;
            }

            $bc.append(crumbLink(tabLabel, () => selectBranch([])));

            _state.branchPath.forEach((branch, i) => {
                $bc.append(crumbSep());
                const name = getCachedName('branch', branch.id) || res('entity.branch', 'Branch') + ' #' + branch.id;
                const isLast = i === _state.branchPath.length - 1 && !_state.focusEntity;

                if (isLast) {
                    $bc.append(crumbCurrent(name));
                } else {
                    const pathUpTo = _state.branchPath.slice(0, i + 1);
                    $bc.append(crumbLink(name, () => {
                        //NO GUARD HERE. Both calls are gated, and canLeaveForm() memoises its answer
                        //for the current gesture: one question, and on "no" both return early so
                        //nothing is torn down. An explicit guard on this line would have run before
                        //exitFocusMode and then been asked again by selectBranch.
                        exitFocusMode();
                        selectBranch(pathUpTo);
                    }));
                }
            });

            // Focus mode crumbs
            if (_state.focusEntity === 'list') {
                $bc.append(crumbSep());
                const listName = getCachedName('list', _state.listId) || res('entity.packNameList', 'PackName list');
                $bc.append(crumbCurrent(listName));
            } else if (_state.focusEntity === 'packname') {
                $bc.append(crumbSep());
                const listName = getCachedName('list', _state.listId) || res('entity.packNameList', 'PackName list');
                $bc.append(crumbLink(listName, () => enterFocusMode('list', _state.listId)));
                $bc.append(crumbSep());
                const pnName = getCachedName('pn', _state.packNameId) || res('entity.packName', 'PackName');
                $bc.append(crumbCurrent(pnName));
            } else if (_state.focusEntity === 'costcenter') {
                $bc.append(crumbSep());
                const ccName = getCachedName('cc', _state.costCenterId) || res('entity.costCentre', 'Cost centre');
                $bc.append(crumbCurrent(ccName));
            }

        } else if (_state.tab === 'departments') {
            $bc.append(crumbSep());

            if (!hasValue(_state.departmentId)) {
                $bc.append(crumbCurrent(tabLabel));
                return;
            }

            $bc.append(crumbLink(tabLabel, () => {
                navigate({ departmentId: null });
            }));

            const depName = getCachedName('dep', _state.departmentId) || res('entity.department', 'Department');
            $bc.append(crumbCurrent(depName));

        } else if (_state.tab === 'costcenters') {
            $bc.append(crumbSep());

            if (!hasValue(_state.costCenterId)) {
                $bc.append(crumbCurrent(tabLabel));
                return;
            }

            $bc.append(crumbLink(tabLabel, () => {
                navigate({ costCenterId: null });
            }));

            const ccName = getCachedName('cc', _state.costCenterId) || res('entity.costCentre', 'Cost centre');
            $bc.append(crumbCurrent(ccName));

        } else if (_state.tab === 'packnames') {
            $bc.append(crumbSep());

            if (!hasValue(_state.packNameId)) {
                $bc.append(crumbCurrent(tabLabel));
                return;
            }

            $bc.append(crumbLink(tabLabel, () => {
                navigate({ packNameId: null });
            }));

            const pnName = getCachedName('pn', _state.packNameId) || res('entity.packName', 'PackName');
            $bc.append(crumbCurrent(pnName));

        } else if (_state.tab === 'productgroups') {
            $bc.append(crumbSep());

            if (!hasValue(_state.productGroupId)) {
                $bc.append(crumbCurrent(tabLabel));
                return;
            }

            $bc.append(crumbLink(tabLabel, () => {
                navigate({ productGroupId: null });
            }));

            const pgName = getCachedName('pg', _state.productGroupId) || res('entity.productGroup', 'Product Group');
            $bc.append(crumbCurrent(pgName));

        } else if (_state.tab === 'subsidies') {
            $bc.append(crumbSep());

            if (!hasValue(_state.subsidyId)) {
                $bc.append(crumbCurrent(tabLabel));
                return;
            }

            $bc.append(crumbLink(tabLabel, () => {
                navigate({ subsidyId: null });
            }));

            const subName = getCachedName('sub', _state.subsidyId) || res('entity.subsidy', 'Subsidy');
            $bc.append(crumbCurrent(subName));

        } else {
            $bc.append(crumbSep());
            $bc.append(crumbCurrent(tabLabel));
        }
    }

    function crumbLink(label, onClick) {
        return $('<li class="breadcrumb-item"></li>')
            .append($('<a href="javascript:void(0)"></a>').text(label).on('click', onClick));
    }

    function crumbCurrent(label) {
        return $('<li class="breadcrumb-item active" aria-current="page"></li>').text(label);
    }

    function crumbSep() { return ''; }

    // ── Mode management ────────────────────────────────────────────────

    function enterFocusMode(entity, entityId) {
        //A TEARDOWN: this replaces whatever is in the right column, including an open form.
        if (!canLeaveForm()) return;

        _state.mode = 'focus';
        _state.focusEntity = entity;

        if (entity === 'list') _state.listId = entityId;
        if (entity === 'packname') _state.packNameId = entityId;
        if (entity === 'costcenter') _state.costCenterId = entityId;

        history.replaceState(null, '', buildHash());
        updateBreadcrumb();
        renderFocusMode();
    }

    function exitFocusMode() {
        //A TEARDOWN. The delete handlers call releaseForm() first, so a deleted record never asks.
        if (!canLeaveForm()) return;

        _state.mode = 'browse';
        _state.focusEntity = null;
        _state.packNameId = null;
        // Keep listId and costCenterId so the browse view re-expands them

        history.replaceState(null, '', buildHash());
        updateBreadcrumb();
        renderBrowseMode();
    }

    // ── Panel loading — top-level router ──────────────────────────────

    function loadPanels() {
        hideAllTabContent();

        if (_state.tab === 'info') {
            $('#tab-content-info').show();
            return;
        }
        if (_state.tab === 'branches') {
            $('#tab-content-branches').show();

            // Tree always loads once
            if (_rendered.branchTree !== _storeId) {
                loadBranchTree();
            } else {
                highlightBranchTree();
            }

            const currentBranchId = currentBranch();

            if (!currentBranchId) {
                // No branch selected — show new root form in browse mode
                renderBrowseMode();
                return;
            }

            if (_state.mode === 'focus') {
                renderFocusMode();
            } else {
                renderBrowseMode();
            }
            return;
        }

        if (_state.tab === 'departments') {
            $('#tab-content-departments').show();
            loadTabPanel('departments');
            return;
        }

        if (_state.tab === 'costcenters') {
            $('#tab-content-costcenters').show();
            loadTabPanel('costcenters');
            return;
        }

        if (_state.tab === 'packnames') {
            $('#tab-content-packnames').show();
            loadTabPanel('packnames');
            return;
        }

        if (_state.tab === 'settings') {
            $('#tab-content-settings').show();
            if (_rendered.settings !== _storeId) loadTabPanel('settings');
            return;
        }

        if (_state.tab === 'exchangesettings') {
            $('#tab-content-exchangesettings').show();
            return;
        }

        if (_state.tab === 'feesettings') {
            $('#tab-content-feesettings').show();
            return;
        }

        if (_state.tab === 'productgroups') {
            $('#tab-content-productgroups').show();
            loadTabPanel('productgroups');
            return;
        }

        if (_state.tab === 'subsidies') {
            $('#tab-content-subsidies').show();
            loadTabPanel('subsidies');
            return;
        }
    }

    // ── Browse mode ────────────────────────────────────────────────────
    // Shows: branch detail card + packName lists card + cost centers card
    // stacked vertically, all collapsible.

    function renderBrowseMode() {
        const branchId = currentBranch();

        if (!branchId) {
            loadBranchDetail(null, null);
            return;
        }

        // Only reload branch detail if branch changed
        if (_rendered.branchDetail !== branchId) {
            loadBranchDetail(branchId, null);
        } else {
            // Branch unchanged (normal case when returning from focus mode) —
            // right col container already has the branch detail card in it,
            // just reload the sub-panels below it
            reloadSubPanels(branchId);
        }

        // PackName lists and cost centers always reload on branch change
        // They are loaded into named divs inside #branch-right-col
        // which is rebuilt by loadBranchDetail's callback
        // so we trigger them there via afterBrowseDetailLoaded().
    }

    function reloadSubPanels(branchId) {
        //NO ensureSubPanel CALLS HERE. Each loader below ensures its own slot, so the two that used
        //to sit here never did anything - and did it in the wrong order: they created cc-panel and
        //branch-addresses-panel before pnlists-panel existed, so on any path where the slots really
        //had been wiped, the cost centres would have been appended above the packname lists.
        loadPackNameListsPanel(branchId);
        loadCostCentersPanel(branchId);
        loadBranchAddressesPanel(branchId);
    }

    // Called after branch detail loads in browse mode — loads the sub-panels
    function afterBrowseDetailLoaded(branchId) {
        loadPackNameListsPanel(branchId);
        loadCostCentersPanel(branchId);
        loadBranchAddressesPanel(branchId);
    }

    // ── Focus mode ─────────────────────────────────────────────────────
    // Hides all browse cards, shows only the focused entity form.

    function renderFocusMode() {
        // Focus mode replaces the entire right col —
        // mark branch detail as gone so renderBrowseMode reloads it on return
        _rendered.branchDetail = null;

        var entity = _state.focusEntity;

        if (!_features.packName && (entity === 'list' || entity === 'packname')) {
            exitFocusMode();
            return;
        }

        if (entity === 'list') {
            loadPartial(
                '/Admin/MultiStore/PackNameListDetail',
                { branchId: currentBranch(), listId: _state.listId || '' },
                'branch-right-col',
                bindListDetailEvents
            );
        } else if (entity === 'packname') {
            loadPartial(
                '/Admin/MultiStore/BranchPackNameDetail',
                { listId: _state.listId, packNameId: _state.packNameId || '' },
                'branch-right-col',
                bindPackNameDetailEvents
            );
        } else if (entity === 'costcenter') {
            loadPartial(
                '/Admin/MultiStore/BranchCostCenterDetail',
                { branchId: currentBranch(), costCenterId: _state.costCenterId || '' },
                'branch-right-col',
                bindCostCenterDetailEvents
            );
        }
    }

    // ── Branch detail (browse mode) ────────────────────────────────────

    function loadBranchDetail(branchId, parentId) {
        const params = { storeId: _storeId };
        if (branchId) params.branchId = branchId;
        else if (parentId) { params.parentBranchId = parentId; }

        loadPartial(
            '/Admin/MultiStore/BranchDetail',
            params,
            'branch-right-col',
            function () {
                _rendered.branchDetail = branchId || null;
                if (branchId) afterBrowseDetailLoaded(branchId);
            }
        );
    }

    // ── PackName lists panel (browse mode) ────────────────────────────

    function loadPackNameListsPanel(branchId) {
        // Load into a named div that lives inside #branch-right-col
        // We append it if missing, or reload in-place if present.

        if (!_features.packName) {
            $('#pnlists-panel').remove();
            return;
        }

        ensureSubPanel('pnlists-panel');
        loadPartial(
            '/Admin/MultiStore/PackNameLists',
            { branchId },
            'pnlists-panel',
            function () {
            }
        );
    }

    // ── Cost centers panel (browse mode) ──────────────────────────────

    function loadCostCentersPanel(branchId) {
        ensureSubPanel('cc-panel');
        loadPartial(
            '/Admin/MultiStore/BranchCostCenters',
            { branchId },
            'cc-panel',
            function () {
            }
        );
    }

    // ── Branch addresses panel (browse mode) ──────────────────────────────

    function loadBranchAddressesPanel(branchId) {
        ensureSubPanel('branch-addresses-panel');
        loadPartial(
            '/Admin/MultiStore/BranchAddresses',
            { branchId },
            'branch-addresses-panel',
            function () {
            }
        );
    }

    // ── Focus mode form events ─────────────────────────────────────────
    // All three focus forms follow the same pattern:
    //   - Re-parse unobtrusive validation
    //   - "← Back" button calls exitFocusMode()
    //   - Save POST returns HTML (validation fail) or JSON (success)
    function bindFocusForm(formId, onSuccess) {
        const $form = $('#' + formId);
        $.validator.unobtrusive.parse($form);

        //WAS THIS A CREATE? Read it from the form BEFORE the post, never from the reply: the insert
        //branch of SavePackNameList returns {success,id,name} with no isNew at all, while the update
        //branch returns isNew:false, so the two cannot be told apart from the payload.
        const wasNew = String($form.find('[name="IsNew"]').val()).toLowerCase() === 'true';

        //Baseline for the unsaved-edit gate. exitFocusMode() asks; this is what it compares against.
        watchForm($form);
        consumeSavedFlash($form);

        // Back button
        $('#btn-focus-back').on('click', () => exitFocusMode());

        // Save
        $('#btn-focus-save').on('click', function() {
            const $btn = $(this);
            if (!$form.valid()) return;
            $btn.prop('disabled', true).text(res('common.saving', 'Saving...'));

            $.ajax({
                type: 'POST',
                url: $form.attr('action'),
                data: $form.serialize(),
                success: function(result, status, xhr) {
                    $btn.prop('disabled', false).text(res('common.save', 'Save'));
                    const ct = xhr.getResponseHeader('Content-Type') || '';
                    if (ct.indexOf('text/html') !== -1) {
                        // Validation failed — swap HTML and re-bind.
                        //NOTHING WAS WRITTEN, so the form stays dirty and no stamp is shown.
                        $('#branch-right-col').html(result);
                        onSuccess(null); // re-binds
                        //AFTER THE RE-BIND, not before: binding the swapped-in form takes a fresh
                        //baseline from the posted values, which would read as clean. The rejected
                        //edits are real work, so the form must still ask before it is abandoned.
                        markFormDirty($('#' + formId));
                        return;
                    }
                    //JSON REFUSAL, the same shape the branch save handler already guards against.
                    //Without this a {success:false} payload is read as a saved record: the panel
                    //would reload for an id that was never written, and the stamp would claim a save
                    //that did not happen.
                    if (result && result.success === false) {
                        alert(result.error || result.message || res('common.serverError', 'A server error occurred. Please try again.'));
                        return;
                    }
                    watchForm($form);
                    onSuccess(result, wasNew);
                },
                error: function() {
                    $btn.prop('disabled', false).text(res('common.save', 'Save'));
                    alert(res('common.serverError', 'A server error occurred. Please try again.'));
                }
            });
        });

        // Delete (optional — only present on edit forms)
        $('#btn-focus-delete').on('click', function() {
            const id = $(this).data('id');
            const message = $(this).data('confirm') || res('common.confirmDelete', 'Delete this item?');
            if (!confirm(message)) return;

            postData = { id };
            addAntiForgeryToken(postData);

            $.post($(this).data('url'),
                postData,
                function(result) {
                    if (!result.success) { alert(result.error || res('common.couldNotDelete', 'Could not delete.')); return; }
                    //The record is gone, so there is nothing left to save and nothing to ask about.
                    releaseForm($form);
                    exitFocusMode();
                }
            );
        });
    }

    function bindListDetailEvents() {
        bindFocusForm('pnlist-detail-form', function (result, wasNew) {
            if (!result) { bindListDetailEvents(); return; }
            //SAVE NEVER NAVIGATES. On an edit the form already shows what was saved and its manager
            //grid is live, so the panel simply stays and takes the stamp. On a create it reloads for
            //the new id, which is what turns "save this list first" into the working grid - the
            //grids are server-built DataTables with the id baked into UrlRead, so they cannot be
            //unlocked client-side. The lists panel behind is refreshed by Back, through
            //renderBrowseMode -> loadBranchDetail -> afterBrowseDetailLoaded.
            cacheName('list', String(result.id), result.name);
            if (wasNew) {
                flashSavedAfterReload();
                enterFocusMode('list', String(result.id));
                return;
            }
            _state.listId = String(result.id);
            updateBreadcrumb();
            markSaved($('#pnlist-detail-form'));
        });
    }

    function bindPackNameDetailEvents() {
        bindFocusForm('pn-detail-form', function (result, wasNew) {
            if (!result) { bindPackNameDetailEvents(); return; }
            cacheName('pn', String(result.id), result.name);
            if (wasNew) {
                flashSavedAfterReload();
                enterFocusMode('packname', String(result.id));
                return;
            }
            updateBreadcrumb();
            markSaved($('#pn-detail-form'));
        });
    }

    function bindCostCenterDetailEvents() {
        bindFocusForm('cc-detail-form', function (result, wasNew) {
            if (!result) { bindCostCenterDetailEvents(); return; }
            cacheName('cc', String(result.id), result.name);
            if (wasNew) {
                flashSavedAfterReload();
                enterFocusMode('costcenter', String(result.id));
                return;
            }
            updateBreadcrumb();
            markSaved($('#cc-detail-form'));
        });
    }

    // ── Tree helpers ───────────────────────────────────────────────────

    function loadBranchTree() {
        const selectedIds = _state.branchPath.map(b => b.id).join(',');
        loadPartial(
            '/Admin/MultiStore/BranchTree',
            { storeId: _storeId, selectedIds },
            'branch-panel',
            function () {
                _rendered.branchTree = _storeId;
            }
        );
    }

    function highlightBranchTree() {
        const selectedIds = _state.branchPath.map(b => String(b.id));
        $('#branch-panel .branch-row').each(function () {
            $(this).toggleClass('selected', selectedIds.includes(String($(this).data('id'))));
        });
    }

    function loadNewChildDetail(parentId) {
        //A TEARDOWN: "add child branch" replaces the right column, and the branch's own form is in
        //it. This was the one exit with no guard at all before the gate moved in here.
        if (!canLeaveForm()) return;

        loadPartial(
            '/Admin/MultiStore/BranchDetail',
            { storeId: _storeId, parentBranchId: parentId },
            'branch-right-col',
            function() {
                _rendered.branchDetail = null;
            }
        );
    }

    function appendNodeToTree(result) {
        const { id, name, branchCode, active, parentId } = result;
        let depth = 0, $target;

        if (parentId) {
            const $parentRow = $('#branch-panel').find('[data-id="' + parentId + '"]');
            if (!$parentRow.length) return;
            depth = (parseInt($parentRow.data('depth')) || 0) + 1;
            $parentRow.find('.branch-toggle').removeClass('leaf');
            const $parentNode = $parentRow.closest('.branch-node');
            if (!$parentNode.find('> .branch-children').length) {
                $parentNode.append('<ul class="branch-children expanded"></ul>');
                $parentRow.find('.branch-toggle').addClass('open');
            } else {
                $parentNode.find('> .branch-children').addClass('expanded');
                $parentRow.find('.branch-toggle').addClass('open');
            }
            $target = $parentNode.find('> .branch-children');
        } else {
            let $tree = $('#branch-panel').find('#branch-tree-root');
            if (!$tree.length) {
                $('#branch-panel').find('#empty-tree-msg').remove();
                $('#branch-panel').find('.card-body').prepend('<ul class="branch-tree" id="branch-tree-root"></ul>');
                $tree = $('#branch-panel').find('#branch-tree-root');
            }
            $target = $tree;
        }

        const indentPx = depth > 0 ? depth * 16 + 12 : 12;
        $target.append(buildTreeNodeHtml(id, name, branchCode, active, parentId, depth, indentPx));
        cacheName('branch', String(result.id), result.name);
    }

    function buildTreeNodeHtml(id, name, branchCode, active, parentId, depth, indentPx) {
        const activeClass = active ? '' : 'branch-inactive';
        const dotClass = active ? 'active' : 'inactive';
        return '<li class="branch-node" data-node-id="' + id + '">' +
            '<div class="branch-row ' + activeClass + '"' +
            ' data-id="' + id + '"' +
            ' data-name="' + escHtml(name) + '"' +
            ' data-parent-id="' + (parentId || '') + '"' +
            ' data-depth="' + depth + '"' +
            ' style="padding-left:' + indentPx + 'px">' +
            '<i class="fas fa-caret-right branch-toggle leaf" aria-hidden="true"></i>' +
            '<span class="branch-status-dot ' + dotClass + '"></span>' +
            '<i class="far fa-home branch-icon" aria-hidden="true"></i>' +
            '<span class="branch-name" title="' + escHtml(name) + '">' + escHtml(name) + '</span>' +
            '<span class="branch-code-badge">' + escHtml(branchCode) + '</span>' +
            '</div>' +
            '</li>';
    }

    function updateNodeInTree(result) {
        const $row = $('#branch-panel').find('[data-id="' + result.id + '"]');
        if (!$row.length) return;
        $row.data('name', result.name).attr('data-name', result.name);
        $row.data('active', result.active).attr('data-active', result.active);
        $row.find('.branch-name').text(result.name);
        $row.find('.branch-code-badge').text(result.branchCode);
        $row.toggleClass('branch-inactive', !result.active);
        $row.find('.branch-status-dot')
            .toggleClass('active', result.active)
            .toggleClass('inactive', !result.active);
        //The save handler owns the button now - a tree helper reaching into the detail form's controls
        //was the coupling that left the form dead whenever this function returned early.
        cacheName('branch', String(result.id), result.name);
    }

    // ── Utility ────────────────────────────────────────────────────────

    function currentBranch() {
        return _state.branchPath.length
            ? _state.branchPath[_state.branchPath.length - 1].id
            : null;
    }

    // Ensures a named sub-panel div exists inside #branch-right-col.
    // Used for pnlists-panel and cc-panel which are siblings inside the right col.
    function ensureSubPanel(panelId) {
        const $rightCol = $('#branch-right-col');
        const $existing = $('#' + panelId);

        // If the panel exists but is not a child of the current right col
        // (can happen if the DOM was rebuilt) — remove the orphan
        if ($existing.length && !$.contains($rightCol[0], $existing[0])) {
            $existing.remove();
        }

        if (!$('#' + panelId).length) {
            $rightCol.append('<div id="' + panelId + '"></div>');
        }
    }

    function hideAllTabContent() {
        $('.multistore-tab-content').hide();
    }

    function loadTabPanel(tab) {
        const urls = {
            departments: '/Admin/MultiStore/Departments',
            costcenters: '/Admin/MultiStore/CostCenters',
            packnames: '/Admin/MultiStore/PackNames',
            settings: '/Admin/MultiStore/StoreSettings',
            productgroups: '/Admin/Subsidy/ProductGroups',
            subsidies: '/Admin/Subsidy/Subsidies',
        };
        const url = urls[tab];
        if (!url) return;
        loadPartial(url, { storeId: _storeId }, 'tab-panel-' + tab, function () {
            _rendered[tab] = _storeId;
        });
    }

    function loadPartial(url, params, panelId, onSuccess) {
        const $panel = $('#' + panelId);
        if (_pendingRequests[panelId]) _pendingRequests[panelId].abort();
        $panel.html('<div class="ms-loading-message">' + res('common.loading', 'Loading...') + '</div>');
        _pendingRequests[panelId] = $.get(url, params)
            .done(function (html) {
                $panel.html(html);
                delete _pendingRequests[panelId];
                if (onSuccess) onSuccess();
            })
            .fail(function (xhr) {
                if (xhr.statusText === 'abort') return;
                delete _pendingRequests[panelId];
                $panel.html('<div class="ms-error-message">Failed to load. <a href="javascript:void(0)" onclick="location.reload()">Retry</a></div>');
            });
    }

    function escHtml(str) {
        return String(str || '')
            .replace(/&/g, '&amp;').replace(/</g, '&lt;')
            .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function hasValue(value) {
        return typeof value !== 'undefined' && value !== null;
    }

    // ── Navigation ─────────────────────────────────────────────────────

    function navigate(updates, shouldLoadPanels = true) {
        //ONLY THE RELOADING FORM IS A TEARDOWN. navigate(x, false) just rewrites the state, the hash
        //and the breadcrumb - the callers that pass false do their own loading on the next line and
        //would otherwise ask twice. The breadcrumb tab links and the browse-mode row clicks all use
        //the default, so they are covered here.
        if (shouldLoadPanels && !canLeaveForm()) return;

        Object.assign(_state, updates);
        history.replaceState(null, '', buildHash());
        updateBreadcrumb();

        if (shouldLoadPanels)
            loadPanels();
    }

    function selectBranch(path) {
        //A TEARDOWN: the tree, the breadcrumb and the new-root button all land here. Safe on the
        //programmatic call from the branch save handler - that path re-baselines the form first, so
        //it reads clean and this returns without prompting.
        if (!canLeaveForm()) return;

        _state.branchPath = path;
        _state.listId = null;
        _state.packNameId = null;
        _state.costCenterId = null;
        _state.departmentId = null;
        _state.productGroupId = null;
        _state.subsidyId = null;
        _state.mode = 'browse';
        _state.focusEntity = null;
        _rendered.branchDetail = null; // force reload

        fetchMissingNames(function() {
            history.replaceState(null, '', buildHash());
            updateBreadcrumb();
            loadPanels();
        });
    }

    function goTab(tab) {
        //A TEARDOWN: every tab link and the store breadcrumb land here, and a tab switch resets the
        //state and reloads the panel. Safe on the programmatic call from init(): no form is bound
        //yet, so there is nothing to ask about.
        if (!canLeaveForm()) return;

        _state.tab = tab;
        _state.branchPath = [];
        _state.listId = null;
        _state.packNameId = null;
        _state.costCenterId = null;
        _state.departmentId = null;
        _state.productGroupId = null;
        _state.subsidyId = null;
        _state.mode = 'browse';
        _state.focusEntity = null;

        activateTabLink(tab);
        history.replaceState(null, '', buildHash());
        updateBreadcrumb();
        loadPanels();
    }

    function activateTabLink(tab) {
        $('#store-tabs .nav-link').removeClass('active');
        $('#tab-' + tab).addClass('active');
    }

    function tabExists(tab) {
        return $('#tab-' + tab).length > 0;
    }

    // ── Init ───────────────────────────────────────────────────────────

    function init(storeId, features, resources) {
        _storeId = storeId;
        if (features) _features = features;
        _res = resources || {};
        parseHash();

        //ONE HANDLER FOR EVERY POPUP REFRESH BUTTON ON THIS PAGE.
        //nop's popup convention is to hand the popup a btnId and click that hidden button on close.
        //Each of those buttons used to carry its own <script> whose whole body was one updateTable()
        //call, which is where a large share of this page's inline scripts came from. The button now
        //declares its grid on itself and this delegated handler does the work. Bound to document
        //because the buttons live inside partials that are swapped in and out; .off() first because
        //init() runs once per page load but nothing guarantees it runs only once.
        $(document).off('click.msGridRefresh').on('click.msGridRefresh', '.ms-grid-refresh', function() {
            var grid = $(this).data('grid');
            if (grid) updateTable(grid);
            return false;
        });

        if (!tabExists(_state.tab)) _state.tab = 'info';

        activateTabLink(_state.tab);

        $('#store-tabs .nav-link').on('click', function (e) {
            e.preventDefault();
            goTab($(this).data('tab'));
        });

        if (_state.tab === 'info') {
            updateBreadcrumb();
            loadPanels();
            return;
        }

        fetchMissingNames(function () {
            updateBreadcrumb();
            loadPanels();
        });

        window.addEventListener('hashchange', function () {
            parseHash();

            if (!tabExists(_state.tab)) _state.tab = 'info';

            activateTabLink(_state.tab);

            fetchMissingNames(function () {
                updateBreadcrumb();
                loadPanels();
            });
        });
    }

    return {
        init,
        res,
        jsArg,
        render,
        navigate,
        goTab,
        selectBranch,
        cacheName,
        loadNewChildDetail,
        appendNodeToTree,
        updateNodeInTree,
        enterFocusMode,
        exitFocusMode,
        bindInlineAdd,
        bindGridEmptyState,
        branchDetailOpen,
        reloadPackNameLists: () => loadPackNameListsPanel(currentBranch()),
        //The cost-centre collection is server-rendered now, so adding or deleting one reloads the
        //panel the way packname lists do rather than redrawing a grid.
        reloadCostCenters: () => loadCostCentersPanel(currentBranch()),
        //The address popup calls #btnRefreshBranchAddresses on save; that button reloads the panel
        //now rather than redrawing a grid.
        reloadBranchAddresses: () => loadBranchAddressesPanel(currentBranch()),
        watchForm,
        markFormDirty,
        releaseForm,
        canLeaveForm,
        markSaved,
        flashSavedAfterReload,
        consumeSavedFlash,
        getState: () => _state,
    };
})();