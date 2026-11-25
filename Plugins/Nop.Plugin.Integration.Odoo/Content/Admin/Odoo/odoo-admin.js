/*
 * Odoo Integration — admin pages.
 *
 * Configure page (.odoo-configure-config): the Run Diagnostics button.
 * Sync Log page (.odoo-synclog-config): the sync buttons, Delete selected, and the grid's status column.
 * Orders page (.odoo-orders-config): Sync selected / Sync all listed, and each row's sync status, shown as its
 * Sync or Cancel in Odoo button when there is something to do.
 *
 * Server values come from the config divs (CLAUDE.md §9.14). The actions answer with C# names as declared:
 * Diagnostics returns a model (OdooUrl, ConnectionStatus, ...), the sync and order actions { ConnectionStatus, TestResultMessage },
 * and [CheckPermission] answers { error } when access is denied.
 */

/**
 * Renders the Status column of the sync events grid (named by RenderCustom, so it must be global).
 */
function renderColumnEventInfo(data) {
    if (data === 'Running') return '<span class="badge bg-warning">Running</span>';
    if (data === 'Done (with errors)') return '<span class="badge bg-danger">Done (errors)</span>';
    if (data === 'Done') return '<span class="badge bg-success">Done</span>';
    return $('<div>').text(data || '').html();
}

/**
 * Renders the Odoo sync column of the Orders page (named by RenderCustom, so it must be global). data is
 * OdooOrderSyncSummary's status text. A row with something to do shows only its button, worded for its status;
 * any other row shows only the status as text.
 */
function renderOdooSyncStatus(data, type, row) {
    // Sorting and searching read the plain status
    if (type !== 'display')
        return data;

    // The buttons carry the order id only; the clicks are handled in initOrders.
    var id = parseInt(row.Id, 10);
    var send = {
        'Not sent': { text: 'Sync', css: 'btn-primary' },
        'Failed': { text: 'Failed: sync again', css: 'btn-danger' },
        'Held': { text: 'Held: sync again', css: 'btn-warning' },
        'In progress': { text: 'Finish sync', css: 'btn-info' }
    }[data] || { text: 'Sync', css: 'btn-primary' };

    if (row.CanSend)
        return '<button type="button" class="btn ' + send.css + ' btn-xs text-nowrap odoo-send-order" data-id="' + id + '">' +
            '<i class="fas fa-paper-plane"></i> ' + send.text + '</button>';
    if (row.CanCancel)
        return '<button type="button" class="btn btn-outline-danger btn-xs text-nowrap odoo-cancel-order" data-id="' + id + '">' +
            '<i class="fas fa-ban"></i> Cancel in Odoo</button>';

    var css = data === 'Sent' ? 'text-success' : 'text-muted';
    return '<span class="text-nowrap ' + css + '">' + $('<div>').text(data || '').html() + '</span>';
}

(function () {
    'use strict';

    function setBadge($badge, text, ok) {
        $badge.text(text)
            .removeClass('badge-success badge-danger')
            .addClass(ok ? 'badge-success' : 'badge-danger');
    }

    // ── Configure page ───────────────────────────────────────────────────────

    function initConfigure($cfg) {
        var $card = $('#odoo-diagnostics-result').hide();
        var fields = ['OdooUrl', 'DatabaseName', 'ApiKeyExists', 'TestResultMessage'];

        $('#btnRunDiagnostics').on('click', function () {
            $card.show();
            var $access = $('#diagnosticsAccessChecks').empty();
            $.each(fields, function (i, name) { $('#diagnostics' + name).text('Loading...'); });
            $('#diagnosticsConnectionStatus').removeClass('badge-success badge-danger').text('Loading...');

            $.ajax({
                url: $cfg.data('diagnosticsUrl'),
                type: 'GET',
                cache: false,
                success: function (data) {
                    $('#diagnosticsOdooUrl').text(data.OdooUrl || 'N/A');
                    $('#diagnosticsDatabaseName').text(data.DatabaseName || 'N/A');
                    $('#diagnosticsApiKeyExists').text(data.ApiKeyExists ? 'Yes' : 'No');
                    setBadge($('#diagnosticsConnectionStatus'), data.ConnectionStatus || data.error || 'N/A',
                        data.ConnectionStatus === 'Connected');
                    $('#diagnosticsTestResultMessage').text(data.TestResultMessage || data.error || 'N/A');
                    $.each(data.AccessChecks || [], function (i, check) {
                        var $badge = $('<span class="badge mr-2">');
                        setBadge($badge, check.Ok ? 'OK' : 'Problem', check.Ok);
                        $access.append($('<li>').append($badge, $('<strong>').text(check.Name + ': '), $('<span>').text(check.Detail)));
                    });
                },
                error: function (xhr, status, error) {
                    $.each(fields, function (i, name) { $('#diagnostics' + name).text('N/A'); });
                    setBadge($('#diagnosticsConnectionStatus'), 'Error', false);
                    $('#diagnosticsTestResultMessage').text('An error occurred while running diagnostics: ' + error);
                }
            });
            return false;
        });
    }

    // ── Sync Log page ────────────────────────────────────────────────────────

    function initSyncLog($cfg) {
        var $card = $('#odoo-sync-result').hide();

        $('.odoo-run-sync').on('click', function () {
            var $btn = $(this);
            $card.show();
            $('#odooSyncStatus').removeClass('badge-success badge-danger').text('Loading...');
            $('#odooSyncMessage').text('Running ' + $btn.data('label') + '...');

            $.ajax({
                url: $btn.data('url'),
                type: 'POST',
                data: addAntiForgeryToken({}),
                success: function (data) {
                    var ok = data.ConnectionStatus === 'Queued';
                    setBadge($('#odooSyncStatus'), ok ? 'OK' : 'Failed', ok);
                    $('#odooSyncMessage').text(data.TestResultMessage || data.error || 'Done');
                    updateTable('#odoo-logevent-grid');
                },
                error: function (xhr, status, error) {
                    setBadge($('#odooSyncStatus'), 'Error', false);
                    $('#odooSyncMessage').text('An error occurred: ' + error);
                }
            });
            return false;
        });

        // The confirmation dialog belongs to nop-action-confirmation; selectedIds is the grid's checkbox state.
        $('#delete-selected-action-confirmation-submit-button').on('click', function () {
            var postData = { selectedIds: selectedIds };
            addAntiForgeryToken(postData);
            $.ajax({
                cache: false,
                type: 'POST',
                url: $cfg.data('deleteSelectedUrl'),
                data: postData,
                traditional: true,
                error: function (jqXHR, textStatus, errorThrown) {
                    showAlert('deleteSelectedFailed', errorThrown);
                },
                complete: function (jqXHR) {
                    if (jqXHR.status === 204) {
                        showAlert('nothingSelectedAlert', $cfg.data('nothingSelected'));
                        return;
                    }
                    updateTable('#odoo-logevent-grid');
                }
            });
            $('#delete-selected-action-confirmation').modal('toggle');
            return false;
        });
    }

    // ── Orders page ──────────────────────────────────────────────────────────

    function initOrders($cfg) {
        var $result = $('#odoo-orders-result');

        // One request at a time: while one runs, every order button is disabled and a further press is ignored,
        // so a second answer can never overwrite the first in the message box. The row buttons are found when
        // the request starts, so rows the grid redraws meanwhile come back enabled.
        var busy = false;
        var $buttons = $();

        function setBusy(value) {
            busy = value;
            if (value)
                $buttons = $('#odoo-send-selected, #odoo-send-all, #odoo-orders-grid .odoo-send-order, #odoo-orders-grid .odoo-cancel-order');
            $buttons.prop('disabled', value);
        }

        // Every order action queues a background run and answers { ConnectionStatus, TestResultMessage },
        // or 204 when nothing was selected.
        function queue(url, data) {
            if (busy)
                return;

            setBusy(true);
            $result.show();
            $('#odooOrdersStatus').removeClass('badge-success badge-danger').text('Loading...');
            $('#odooOrdersMessage').text('');

            $.ajax({
                url: url,
                type: 'POST',
                data: addAntiForgeryToken(data),
                traditional: true,
                success: function (answer, textStatus, jqXHR) {
                    if (jqXHR.status === 204) {
                        setBadge($('#odooOrdersStatus'), 'Nothing selected', false);
                        $('#odooOrdersMessage').text($cfg.data('nothingSelected'));
                        return;
                    }
                    var ok = answer.ConnectionStatus === 'Queued';
                    setBadge($('#odooOrdersStatus'), ok ? 'Queued' : 'Failed', ok);
                    $('#odooOrdersMessage').text(answer.TestResultMessage || answer.error || '');
                },
                error: function (xhr, status, error) {
                    setBadge($('#odooOrdersStatus'), 'Error', false);
                    $('#odooOrdersMessage').text('An error occurred: ' + error);
                },
                complete: function () {
                    setBusy(false);
                }
            });
        }

        // selectedIds is the grid's checkbox state (nop's Table partial).
        $('#odoo-send-selected').on('click', function () {
            queue($cfg.data('sendSelectedUrl'), { selectedIds: selectedIds });
            return false;
        });

        $('#odoo-send-all').on('click', function () {
            queue($cfg.data('sendAllUrl'), {
                StoreId: $('#StoreId').val(),
                SyncState: $('#SyncState').val(),
                PaymentStatusId: $('#PaymentStatusId').val(),
                CreatedFrom: $('#CreatedFrom').val(),
                CreatedTo: $('#CreatedTo').val()
            });
            return false;
        });

        // Cancelling in Odoo cannot be undone from here, so it waits for nop's confirmation dialog
        // (nop-action-confirmation, opened from the row button below). The order it is for is kept here.
        var cancelOrderId = null;
        var $cancelConfirmation = $('#odoo-cancel-order-action-confirmation');

        $('#odoo-cancel-order-action-confirmation-submit-button').off('.odoo').on('click.odoo', function () {
            $cancelConfirmation.modal('hide');
            if (cancelOrderId !== null)
                queue($cfg.data('cancelUrl'), { id: cancelOrderId });
            cancelOrderId = null;
            return false;
        });

        // The grid's rows are redrawn on every page, so the row buttons are delegated from the table, which stays.
        $('#odoo-orders-grid').off('.odoo')
            .on('click.odoo', '.odoo-send-order', function () {
                queue($cfg.data('sendUrl'), { id: $(this).data('id') });
                return false;
            })
            .on('click.odoo', '.odoo-cancel-order', function () {
                cancelOrderId = $(this).data('id');
                $cancelConfirmation.modal('show');
                return false;
            });
    }

    $(function () {
        var $configure = $('.odoo-configure-config');
        if ($configure.length)
            initConfigure($configure);

        var $syncLog = $('.odoo-synclog-config');
        if ($syncLog.length)
            initSyncLog($syncLog);

        var $orders = $('.odoo-orders-config');
        if ($orders.length)
            initOrders($orders);
    });
})();
