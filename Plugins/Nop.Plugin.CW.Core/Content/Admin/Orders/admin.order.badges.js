var Admin = Admin || {};

//E and R badges on nop's admin order grids. The grids are nop's: nothing here changes how they load, page
//or sort. After each draw the script asks for the flags of the orders on the page — one request — and
//appends the badges to the order-number cell. Labels and the URL come from the config div the
//OrderListBadges widget renders.
Admin.OrderBadges = Admin.OrderBadges || (function () {
    //Sales -> Orders, and the Orders tab of a customer
    var GRID_IDS = ['orders-grid', 'order-grid'];

    function config() {
        return $('.cw-order-badges-config').first();
    }

    function badge(text, title, colour) {
        return $('<span/>', {
            'class': 'grid-report-item ' + colour + ' cw-order-badge',
            title: title,
            text: text
        });
    }

    function onDraw(e, settings) {
        if (!settings || !settings.nTable || GRID_IDS.indexOf(settings.nTable.id) < 0)
            return;

        var $cfg = config();

        if (!$cfg.length)
            return;

        var api = new $.fn.dataTable.Api(settings);

        var column = -1;
        settings.aoColumns.forEach(function (c, i) {
            if (c.mData === 'CustomOrderNumber')
                column = i;
        });

        if (column < 0)
            return;

        var rowByOrderId = {};
        var orderIds = [];

        api.rows({ page: 'current' }).every(function (rowIdx) {
            var data = this.data();

            if (data && data.Id) {
                rowByOrderId[data.Id] = rowIdx;
                orderIds.push(data.Id);
            }
        });

        if (!orderIds.length)
            return;

        var postData = { orderIds: orderIds };
        addAntiForgeryToken(postData);

        $.ajax({
            cache: false,
            type: 'POST',
            url: $cfg.data('url'),
            data: postData,
            traditional: true
        }).done(function (flags) {
            (flags || []).forEach(function (f) {
                var rowIdx = rowByOrderId[f.OrderId];

                //the grid may have redrawn while the request was out: only touch a row still showing that order
                if (rowIdx === undefined || !api.row(rowIdx).data() || api.row(rowIdx).data().Id !== f.OrderId)
                    return;

                var $cell = $(api.cell(rowIdx, column).node());
                $cell.find('.cw-order-badge').remove();

                if (f.HasExchange)
                    $cell.append(' ').append(badge($cfg.data('exchangeText'), $cfg.data('exchangeTitle'), 'yellow'));

                if (f.HasReturn)
                    $cell.append(' ').append(badge($cfg.data('returnText'), $cfg.data('returnTitle'), 'blue'));
            });
        });
    }

    //draw bubbles to the document in DataTables 2, so one delegated handler serves both grids, however late
    //they are created
    $(document).off('draw.dt.cwOrderBadges').on('draw.dt.cwOrderBadges', onDraw);

    return {};
})();