/*
 * The approval panel of the branch and department forms: its details show while approval is required.
 *
 * Delegated from document, and bound once: the forms of Manage Store are loaded by AJAX into different panels, long
 * after this script ran, and the standalone branch page holds its form in the page. The document is the only ancestor
 * the three share.
 */
(function () {
    'use strict';

    $(document).off('change.cwapprovalrule').on('change.cwapprovalrule', '.cw-approval-rule-required input[type="checkbox"]', function () {
        $(this).closest('.cw-approval-rule').find('.cw-approval-rule-details').toggle(this.checked);
    });
})();
