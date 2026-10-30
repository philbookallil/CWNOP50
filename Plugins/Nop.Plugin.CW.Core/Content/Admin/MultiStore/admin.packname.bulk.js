var Admin = Admin || {};

//THE BULK ACTIONS OF THE PACKNAME GRIDS: the store's PackNames tab and the grid inside an open packname list. Each grid
//keeps a selection of its own, kept across its pages and cleared when its search changes. nop's own master checkbox is
//not used: it keeps one selectedIds for the whole page, and this page holds several grids at once.
//
//var, not let or const, for everything a nested function reads: the admin bundle is minified, and the minifier can move
//a block-scoped declaration out from under a hoisted function that reads it.
Admin.PackNameBulk = (function () {
    var _grids = {};

    function res(key, fallback) {
        return Admin.StoreNav.res(key, fallback);
    }

    function format(text) {
        var args = Array.prototype.slice.call(arguments, 1);

        return String(text).replace(/\{(\d+)\}/g, function (match, index) {
            return args[index] !== undefined ? args[index] : match;
        });
    }

    function escapeAttribute(text) {
        return String(text)
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function serverError() {
        display_nop_error({ error: res('common.serverError', 'A server error occurred. Please try again.') });
    }

    //DATATABLES RENDER CALLBACK, named in the grid's column as Admin.PackNameBulk.render.check. The value is the row's id,
    //a number, so nothing needs escaping.
    var render = {
        check: function (data, type, row) {
            var label = format(res('bulk.selectRow', 'Select {0}'), row && row.Name ? row.Name : data);

            return '<input type="checkbox" class="pn-bulk-check" value="' + data + '" aria-label="' + escapeAttribute(label) + '" />';
        }
    };

    function count(state) {
        return Object.keys(state.selected).length;
    }

    function filterValues(state) {
        var values = {};

        $.each(state.options.filters || {}, function (name, selector) {
            var $input = $(selector);

            if ($input.length)
                values[name] = $input.val();
        });

        return values;
    }

    //The toolbar is inside the partial's markup, so it is looked up each time rather than kept.
    function refresh(state) {
        var $toolbar = $(state.options.toolbar);
        var selected = count(state);

        $toolbar.find('.pn-bulk-count').text(selected
            ? format(res('bulk.selected', '{0} selected'), selected)
            : res('bulk.noneSelected', 'Tick packnames to act on several at once'));

        $toolbar.find('.pn-bulk-action').prop('disabled', state.busy || selected === 0);
        $toolbar.find('.pn-bulk-clear').prop('hidden', selected === 0);
        $toolbar.find('.pn-bulk-export').prop('disabled', state.busy);
        $toolbar.find('.pn-bulk-export-label').text(selected
            ? res('bulk.exportSelected', 'Export selected to Excel')
            : res('bulk.export', 'Export to Excel'));
    }

    function setBusy(state, busy) {
        state.busy = busy;
        refresh(state);
    }

    //The checkboxes of the page on screen follow the selection, and the page checkbox is ticked when every row is.
    function syncPage(state) {
        var $boxes = $(state.options.grid).find('tbody .pn-bulk-check');
        var all = $boxes.length > 0;

        $boxes.each(function () {
            this.checked = state.selected.hasOwnProperty(String(this.value));
            all = all && this.checked;
        });

        $(state.options.grid + '_wrapper .pn-bulk-page').prop('checked', all);
        refresh(state);
    }

    function select(state, box, checked) {
        var id = String(box.value);

        if (!checked) {
            delete state.selected[id];
            return;
        }

        var row = $(state.options.grid).DataTable().row($(box).closest('tr')).data() || {};

        state.selected[id] = { manager: row.Manager === true, customer: row.IsCustomer === true };
    }

    function clear(state) {
        state.selected = {};
        syncPage(state);
    }

    //The packnames just acted on leave every grid's selection, not only the acting grid's: the store's tab and an open
    //list show the same packnames.
    function forgetEverywhere(ids) {
        $.each(_grids, function (grid, state) {
            $.each(ids, function (index, id) {
                delete state.selected[String(id)];
            });

            refresh(state);
        });
    }

    //Every packname grid on the page is redrawn, not only the one acted from: a move changes two lists, and the store's
    //tab shows every list's packnames. The page stays where it is.
    function reloadGrids() {
        $('#packnames-grid, table[id^="multistorepacknames-"]').each(function () {
            if ($.fn.dataTable.isDataTable(this))
                $(this).DataTable().ajax.reload(null, false);
        });
    }

    function dialog(id, title) {
        var $dialog = $('#' + id);

        if (!$dialog.length) {
            $dialog = $('<div class="modal fade pn-bulk-dialog" tabindex="-1" role="dialog" aria-hidden="true">'
                + '<div class="modal-dialog" role="document"><div class="modal-content">'
                + '<div class="modal-header"><h5 class="modal-title" id="' + id + '-title"></h5>'
                + '<button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button></div>'
                + '<div class="modal-body"></div>'
                + '<div class="modal-footer"></div>'
                + '</div></div></div>')
                .attr('id', id)
                .attr('aria-labelledby', id + '-title')
                .appendTo('body');
        }

        $dialog.find('.modal-title').text(title);
        $dialog.find('.modal-body').empty();
        $dialog.find('.modal-footer').empty();

        return $dialog;
    }

    function showResult(result) {
        var $dialog = dialog('pn-bulk-result-dialog', res('bulk.resultTitle', 'Bulk action'));
        var $body = $dialog.find('.modal-body');

        $('<p>').text(format(res('bulk.result.changed', '{0} packname(s) changed.'), result.changed || 0)).appendTo($body);

        if (result.unchanged)
            $('<p>').text(format(res('bulk.result.unchanged', '{0} packname(s) were already that way.'), result.unchanged)).appendTo($body);

        if (result.skipped && result.skipped.length) {
            $('<p>').text(format(res('bulk.result.skipped', '{0} packname(s) skipped:'), result.skipped.length)).appendTo($body);

            var $list = $('<ul class="pn-bulk-skipped">').appendTo($body);

            $.each(result.skipped, function (index, skip) {
                $('<li>').text(skip.name + ': ' + skip.reason).appendTo($list);
            });
        }

        if (result.notProcessed)
            $('<p class="text-danger">').text(result.stoppedMessage || '').appendTo($body);

        $('<button type="button" class="btn btn-primary" data-dismiss="modal">')
            .text(res('bulk.close', 'Close'))
            .appendTo($dialog.find('.modal-footer'));

        $dialog.modal('show');
    }

    function post(state, action, ids, targetListId) {
        if (state.busy)
            return;

        var data = {
            StoreId: state.options.storeId,
            BulkAction: action,
            PackNameIds: ids.join(','),
            TargetPackNameListId: targetListId || 0
        };

        addAntiForgeryToken(data);
        setBusy(state, true);

        $.ajax({ type: 'POST', url: state.options.bulkUrl, data: data })
            .done(function (result) {
                setBusy(state, false);

                if (!result || typeof result !== 'object') {
                    serverError();
                    return;
                }

                //a refusal changed nothing, so the selection stays for another try
                if (result.success === false) {
                    display_nop_error({ error: result.message || result.error || res('common.serverError', 'A server error occurred. Please try again.') });
                    return;
                }

                clear(state);
                forgetEverywhere(ids);
                reloadGrids();

                //the counts on the list rows of the open branch are drawn with the lists
                if (state.options.packNameListId && (action === 'Move' || action === 'Delete'))
                    Admin.StoreNav.reloadPackNameLists();

                showResult(result);
            })
            .fail(function () {
                setBusy(state, false);
                serverError();
            });
    }

    function confirmDelete(state, ids) {
        var managers = 0;
        var customers = 0;

        $.each(ids, function (index, id) {
            var row = state.selected[id] || {};

            if (row.manager) managers++;
            if (row.customer) customers++;
        });

        var text = format(res('bulk.confirmDelete', 'Delete {0} packname(s)? Their subsidy assignments and cart lines are removed too.'), ids.length);

        if (managers)
            text += '\n' + format(res('bulk.confirmDelete.managers', '{0} of them are managers.'), managers);

        if (customers)
            text += '\n' + format(res('bulk.confirmDelete.customers', '{0} of them have a customer account; the accounts are kept.'), customers);

        return confirm(text);
    }

    function openMoveDialog(state, ids) {
        setBusy(state, true);

        $.get(state.options.moveTargetsUrl, { storeId: state.options.storeId })
            .done(function (targets) {
                setBusy(state, false);

                if (!$.isArray(targets)) {
                    serverError();
                    return;
                }

                //from a list's own grid, that list is not a place to move to
                var choices = $.grep(targets, function (target) {
                    return Number(target.id) !== Number(state.options.packNameListId);
                });

                if (!choices.length) {
                    display_nop_error({ error: res('bulk.move.noTargets', 'This store has no other switched-on list to move packnames to.') });
                    return;
                }

                var $dialog = dialog('pn-bulk-move-dialog', res('bulk.move.title', 'Move packnames to another list'));
                var $body = $dialog.find('.modal-body');
                var $footer = $dialog.find('.modal-footer');

                $('<p>').text(format(res('bulk.move.intro', 'Move {0} packname(s) to:'), ids.length)).appendTo($body);

                var $select = $('<select class="form-control">').appendTo($body);

                $.each(choices, function (index, target) {
                    $('<option>').val(target.id).text(target.text).appendTo($select);
                });

                //the footer's buttons are new on every opening, so their handlers never pile up
                $('<button type="button" class="btn btn-default" data-dismiss="modal">')
                    .text(res('bulk.cancel', 'Cancel'))
                    .appendTo($footer);

                $('<button type="button" class="btn btn-primary">')
                    .text(res('bulk.move.confirm', 'Move'))
                    .appendTo($footer)
                    .one('click', function () {
                        var targetListId = $select.val();

                        //posted once the dialog has gone, so the result dialog never opens over a closing one
                        $dialog.one('hidden.bs.modal', function () {
                            post(state, 'Move', ids, targetListId);
                        }).modal('hide');
                    });

                $dialog.modal('show');
            })
            .fail(function () {
                setBusy(state, false);
                serverError();
            });
    }

    function run(state, action) {
        var ids = Object.keys(state.selected);

        if (!ids.length || state.busy)
            return;

        if (ids.length > state.options.max) {
            display_nop_error({ error: format(res('bulk.tooMany', 'Select at most {0} packnames at a time.'), state.options.max) });
            return;
        }

        if (action === 'Move') {
            openMoveDialog(state, ids);
            return;
        }

        if (action === 'Delete' && !confirmDelete(state, ids))
            return;

        post(state, action, ids, 0);
    }

    //THE FILE IS FETCHED, not navigated to, so a refusal or an error shows a message and Manage Store stays as it is. It
    //carries the filters the grid last searched with (what is on screen, not what is typed and not searched yet); with
    //ticked packnames it carries them too, and only they are exported.
    function exportXlsx(state) {
        if (state.busy)
            return;

        var fields = $.extend({}, state.lastFilters || filterValues(state));

        fields.StoreId = state.options.storeId;
        fields.PackNameListId = state.options.packNameListId || 0;
        fields.selectedIds = Object.keys(state.selected).join(',');
        fields.__RequestVerificationToken = $('input[name=__RequestVerificationToken]').first().val();

        var body = new URLSearchParams();

        $.each(fields, function (name, value) {
            if (value !== undefined && value !== null)
                body.append(name, value);
        });

        setBusy(state, true);

        //sent as an AJAX request, so a feature switch or permission refusal answers JSON instead of redirecting to another page
        fetch(state.options.exportUrl, { method: 'POST', body: body, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) {
                var type = response.headers.get('Content-Type') || '';

                if (!response.ok || type.indexOf('spreadsheetml') === -1) {
                    //a refusal answers JSON; anything else (an error page, a redirect to another page) is an error
                    return (type.indexOf('json') !== -1 ? response.json() : Promise.resolve(null)).then(function (answer) {
                        throw new Error(answer && (answer.message || answer.error) || res('common.serverError', 'A server error occurred. Please try again.'));
                    });
                }

                var disposition = response.headers.get('Content-Disposition') || '';
                var match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
                var fileName = match ? decodeURIComponent(match[1]) : 'packnames.xlsx';

                return response.blob().then(function (blob) {
                    var url = URL.createObjectURL(blob);
                    var link = $('<a>', { href: url, download: fileName }).hide().appendTo('body');

                    link[0].click();

                    setTimeout(function () {
                        URL.revokeObjectURL(url);
                        link.remove();
                    }, 1000);
                });
            })
            .then(function () {
                setBusy(state, false);
            }, function (error) {
                setBusy(state, false);
                display_nop_error({ error: error && error.message ? error.message : res('common.serverError', 'A server error occurred. Please try again.') });
            });
    }

    /**
     * Binds the bulk selection, actions and export of one packname grid. Called by the grid's partial every time it
     * renders.
     * options: panel (the stable container the partial is rendered into), grid (the table selector), toolbar (the
     * toolbar selector), storeId, packNameListId (0 for the store's grid), bulkUrl, exportUrl, moveTargetsUrl, max, and
     * filters (posted name -> input selector).
     */
    function bind(options) {
        var state = {
            options: options,
            selected: {},
            signature: null,
            lastFilters: null,
            busy: false
        };

        _grids[options.grid] = state;

        var $panel = options.panel;
        var $grid = $(options.grid);

        //THE PANEL OUTLIVES THE PARTIAL, so its handlers are taken off before they are bound again. The table is new on
        //every render; its handlers are bound on the node itself because nop builds the DataTable after this script runs.
        $panel.off('.pnbulk');
        $grid.off('.pnbulk');

        //a request made for a different search clears the selection: what was ticked may no longer be in the grid
        $grid.on('preXhr.dt.pnbulk', function () {
            var signature = JSON.stringify(filterValues(state));

            if (state.signature !== null && signature !== state.signature && count(state)) {
                state.selected = {};
                refresh(state);
            }

            state.signature = signature;
            state.lastFilters = filterValues(state);
        });

        $grid.on('draw.dt.pnbulk', function () {
            syncPage(state);
        });

        $panel.on('change.pnbulk', '.pn-bulk-check', function () {
            select(state, this, this.checked);
            syncPage(state);
        });

        $panel.on('change.pnbulk', '.pn-bulk-page', function () {
            var checked = this.checked;

            $grid.find('tbody .pn-bulk-check').each(function () {
                this.checked = checked;
                select(state, this, checked);
            });

            syncPage(state);
        });

        $panel.on('click.pnbulk', options.toolbar + ' .pn-bulk-action', function () {
            run(state, String($(this).data('bulkAction')));
        });

        $panel.on('click.pnbulk', options.toolbar + ' .pn-bulk-clear', function () {
            clear(state);
        });

        $panel.on('click.pnbulk', options.toolbar + ' .pn-bulk-export', function () {
            exportXlsx(state);
        });

        refresh(state);
    }

    /**
     * Takes a packname out of a grid's selection, after it was deleted from its own row.
     */
    function unselect(grid, id) {
        var state = _grids[grid];

        if (!state)
            return;

        delete state.selected[String(id)];
        refresh(state);
    }

    return {
        render: render,
        bind: bind,
        unselect: unselect
    };
})();