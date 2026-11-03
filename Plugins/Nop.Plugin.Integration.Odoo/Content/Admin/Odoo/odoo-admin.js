/*
 * Odoo Integration — admin pages.
 *
 * Configure page (.odoo-configure-config): the Run Diagnostics button.
 * Sync Log page (.odoo-synclog-config): the sync buttons, Delete selected, and the grid's status column.
 *
 * Server values come from the config divs (CLAUDE.md §9.14). The actions answer with C# names as declared:
 * Diagnostics returns a model (OdooUrl, ConnectionStatus, ...), the sync actions { ConnectionStatus, TestResultMessage },
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

    $(function () {
        var $configure = $('.odoo-configure-config');
        if ($configure.length)
            initConfigure($configure);

        var $syncLog = $('.odoo-synclog-config');
        if ($syncLog.length)
            initSyncLog($syncLog);
    });
})();
