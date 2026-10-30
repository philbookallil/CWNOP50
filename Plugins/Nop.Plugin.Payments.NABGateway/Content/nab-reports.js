// Client-side helpers for the NAB XML payment reports (Payments + Failed Transactions).
// Relies on the globals nabDetailsUrl, nabOrderEditUrl and nabCustomerEditUrl defined in the views.

function nabEscapeHtml(value) {
    if (value === null || typeof value === 'undefined') {
        return '';
    }
    return $('<div/>').text(value).html();
}

// DataTables column renderers ------------------------------------------------

function renderOrderLink(data, type, row) {
    if (!data) {
        return '<span>N/A</span>';
    }
    var url = nabOrderEditUrl + '/' + data;
    return '<a href="' + url + '" title="Go to linked order">' + nabEscapeHtml(data) + '</a>';
}

function renderCustomerLink(data, type, row) {
    var email = nabEscapeHtml(data);
    if (row.CustomerId) {
        var url = nabCustomerEditUrl + '/' + row.CustomerId;
        return '<a href="' + url + '" title="View customer">' + email + '</a>';
    }
    return '<span>' + email + '</span>';
}

function renderViewButton(data, type, row) {
    return '<a class="btn btn-default nab-view-details" href="javascript:void(0)" data-id="' + row.Id + '">' +
        '<i class="fas fa-eye"></i></a>';
}

// Detail modal ---------------------------------------------------------------

function nabSetValue(container, css, value) {
    var text = (value === null || typeof value === 'undefined' || value === '') ? ' - ' : value;
    $('.' + css, container).text(text);
}

function nabShowDetails(id) {
    var modal = $('#view-transaction-details');

    // reset
    $('.details-value', modal).text(' - ');
    $('.nab-raw-xml', modal).text(' - ');

    modal.modal('show');

    $.ajax({
        cache: false,
        type: 'GET',
        url: nabDetailsUrl,
        data: { id: id },
        success: function (data) {
            if (!data || data.error) {
                alert('Error loading transaction details: ' + (data ? data.error : 'unknown'));
                return;
            }
            nabSetValue(modal, 'nab-order-guid', data.OrderGuid);
            nabSetValue(modal, 'nab-user-email', data.UserEmail);
            nabSetValue(modal, 'nab-user-name', data.UserFullName);
            nabSetValue(modal, 'nab-approved', data.Approved ? 'Yes' : 'No');
            nabSetValue(modal, 'nab-amount', data.Amount);
            nabSetValue(modal, 'nab-message-id', data.MessageId);
            nabSetValue(modal, 'nab-txn-id', data.TxnId);
            nabSetValue(modal, 'nab-auth-id', data.AuthId);
            nabSetValue(modal, 'nab-response-code', data.ResponseCode);
            nabSetValue(modal, 'nab-response-text', data.ResponseText);
            nabSetValue(modal, 'nab-settlement-date', data.SettlementDate);
            nabSetValue(modal, 'nab-masked-pan', data.MaskedPan);
            nabSetValue(modal, 'nab-card-type', data.CardType);
            nabSetValue(modal, 'nab-purchase-order', data.PurchaseOrderNo);
            nabSetValue(modal, 'nab-created-on', data.CreatedOn);
            $('.nab-raw-xml', modal).text(data.RawResponseXml || ' - ');
        },
        error: function () {
            alert('Error loading transaction details.');
        }
    });
}

$(document).on('click', '.nab-view-details', function () {
    nabShowDetails($(this).data('id'));
});
