/*
 * The two admin lists of the order approvals: the orders (send the links again, change the approvers) and the approvers
 * (edit, replace, remove).
 *
 * The render functions build buttons that carry no text of a record: a handler reads the record from its row. The
 * actions answer an anonymous object, so its members arrive lowercase: { success, message }. The row's own members
 * come from the grid's model and keep their names.
 *
 * The handlers are delegated from each page's own container, which is rendered with the page and never replaced.
 */
window.CW = window.CW || {};

CW.ApprovalLists = CW.ApprovalLists || (function () {
    'use strict';

    function config() {
        return $('.cw-approval-lists-config');
    }

    function button(cssClass, icon, text) {
        return $('<button type="button" class="btn btn-default btn-sm mr-1 mb-1"></button>')
            .addClass(cssClass)
            .append($('<i aria-hidden="true"></i>').addClass(icon))
            .append(document.createTextNode(' ' + text))
            .prop('outerHTML');
    }

    function rowOf(grid, element) {
        return $(grid).DataTable().row($(element).closest('tr')).data();
    }

    //a dialog's box of approvers, with what it holds and the approvers of the row's store, as the grid last brought them
    function startPicker(selector, grid, emails, storeId, exceptEmail) {
        var answer = $(grid).DataTable().ajax.json();

        CW.ApproverPicker.start($(selector)[0], emails, $.grep((answer && answer.Suggestions) || [], function (approver) {
            return approver.StoreId === storeId && approver.Email !== exceptEmail;
        }));
    }

    function reload(grid) {
        $(grid).DataTable().ajax.reload(null, false);
    }

    //one post at a time: its button waits, so a second click sends nothing. A refusal reloads the list too: the record
    //may have changed under it. Its dialog stays open, with what was typed.
    function post($button, grid, url, data, done) {
        if ($button.prop('disabled'))
            return;

        $button.prop('disabled', true);

        $.ajax({
            cache: false,
            type: 'POST',
            url: url,
            data: addAntiForgeryToken(data),
            success: function (answer) {
                if (answer && answer.success) {
                    done(answer);
                    return;
                }

                display_nop_error({ error: (answer && answer.message) || config().data('errorText') });
                reload(grid);
            },
            error: function () {
                display_nop_error({ error: config().data('errorText') });
            },
            complete: function () {
                $button.prop('disabled', false);
            }
        });
    }

    function bindOrders() {
        var $page = $('#cw-approval-orders').off('.cwapprovals');
        var grid = '#order-approvals-grid';
        var orderId = 0;
        var startedFrom = '';

        if ($page.length === 0)
            return;

        function refresh(answer) {
            displayBarNotification(answer.message, 0, 5000);
            reload(grid);
        }

        $page.on('click.cwapprovals', '.cw-approvals-resend', function () {
            var row = rowOf(grid, this);

            if (confirm(config().data('confirmResend')))
                post($(this), grid, config().data('resendUrl'), { orderId: row.Id }, refresh);
        });

        $page.on('click.cwapprovals', '.cw-approvals-change', function () {
            var row = rowOf(grid, this);
            orderId = row.Id;
            startedFrom = row.WaitingEmails;

            $('#cw-approval-change-order').text(config().data('orderText').replace('{0}', function () { return row.OrderNumber; }));
            startPicker('#cw-approval-change-emails', grid, row.WaitingEmails, row.StoreId);
            $('#cw-approval-change-modal').modal('show');
        });

        $page.on('click.cwapprovals', '#cw-approval-change-save', function () {
            var saved = orderId;

            post($(this), grid, config().data('changeUrl'), {
                orderId: orderId,
                emails: $('#cw-approval-change-emails').val(),
                startedFrom: startedFrom
            }, function (answer) {
                //another order's dialog may be open by now
                if (saved === orderId)
                    $('#cw-approval-change-modal').modal('hide');

                refresh(answer);
            });
        });
    }

    function bindApprovers() {
        var $page = $('#cw-approvers').off('.cwapprovals');
        var grid = '#approvers-grid';
        var approverId = 0;

        if ($page.length === 0)
            return;

        function refresh(answer) {
            displayBarNotification(answer.message, 0, 5000);
            reload(grid);
        }

        //closes the dialog that was saved, unless another approver's is open by now
        function refreshAndClose(modal) {
            var saved = approverId;

            return function (answer) {
                if (saved === approverId)
                    $(modal).modal('hide');

                refresh(answer);
            };
        }

        $page.on('click.cwapprovals', '.cw-approvers-edit', function () {
            var row = rowOf(grid, this);
            approverId = row.Id;

            $('#cw-approver-edit-name').val(row.Name);
            $('#cw-approver-edit-email').val(row.Email);
            $('#cw-approver-edit-modal').modal('show');
        });

        $page.on('click.cwapprovals', '#cw-approver-edit-save', function () {
            post($(this), grid, config().data('updateUrl'), {
                id: approverId,
                name: $('#cw-approver-edit-name').val(),
                email: $('#cw-approver-edit-email').val()
            }, refreshAndClose('#cw-approver-edit-modal'));
        });

        $page.on('click.cwapprovals', '.cw-approvers-replace', function () {
            var row = rowOf(grid, this);
            approverId = row.Id;

            $('#cw-approver-replace-who').text(row.Name + ': ' + row.ApprovesFor);
            $('#cw-approver-replace-name').val('');
            startPicker('#cw-approver-replace-email', grid, '', row.StoreId, row.Email);
            $('#cw-approver-replace-modal').modal('show');
        });

        $page.on('click.cwapprovals', '#cw-approver-replace-save', function () {
            post($(this), grid, config().data('replaceUrl'), {
                id: approverId,
                name: $('#cw-approver-replace-name').val(),
                email: $('#cw-approver-replace-email').val()
            }, refreshAndClose('#cw-approver-replace-modal'));
        });

        $page.on('click.cwapprovals', '.cw-approvers-delete', function () {
            var row = rowOf(grid, this);

            if (confirm(config().data('confirmDelete')))
                post($(this), grid, config().data('deleteUrl'), { id: row.Id }, refresh);
        });
    }

    $(function () {
        bindOrders();
        bindApprovers();
    });

    return {
        renderOrderActions: function (data, type, row) {
            var $cfg = config();

            var view = $('<a class="btn btn-default btn-sm mr-1 mb-1"></a>')
                .attr('href', $cfg.data('viewUrl') + '/' + row.Id)
                .append('<i class="far fa-eye" aria-hidden="true"></i>')
                .append(document.createTextNode(' ' + $cfg.data('viewText')))
                .prop('outerHTML');

            if (!row.IsPending)
                return view;

            return view
                + button('cw-approvals-resend', 'far fa-envelope', $cfg.data('resendText'))
                + button('cw-approvals-change', 'fas fa-user-pen', $cfg.data('changeText'));
        },

        renderApproverActions: function (data, type, row) {
            var $cfg = config();

            return button('cw-approvers-edit', 'fas fa-pencil', $cfg.data('editText'))
                + (row.HasRules ? button('cw-approvers-replace', 'fas fa-right-left', $cfg.data('replaceText')) : '')
                + (row.CanDelete ? button('cw-approvers-delete', 'far fa-trash-can', $cfg.data('deleteText')) : '');
        }
    };
})();
