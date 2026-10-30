(function ($) {
    'use strict';

    var cfg = {};

    function loadBar() {
        $('.subsidy-bar-container').load(cfg.barUrl);
    }

    //A group that becomes exhausted is collapsed, and one that stops being exhausted is opened again; a group whose
    //state is unchanged keeps whatever the customer did with its arrow
    function syncGroupState(id) {
        var $group = $('#product-group-' + id);
        var exhausted = $group.find('.pg-header-badge.exhausted').length > 0;

        if (String($group.data('exhausted')) === String(exhausted))
            return;

        $group.data('exhausted', exhausted);

        var $products = $group.find('.products-container');
        var $toggle = $group.find('.toggle-icon');

        if (exhausted) {
            $products.slideUp();
            $toggle.removeClass('expanded').addClass('collapsed');
        } else {
            $products.slideDown();
            $toggle.removeClass('collapsed').addClass('expanded');
        }
    }

    function loadHeaders() {
        $.getJSON(cfg.headersUrl, function (data) {
            if (!data) return;
            $.each(data, function (id, html) {
                $('#product-group-header-' + id + ' .product-group-header-info').html(html);
                syncGroupState(id);
            });
        });
    }

    //the subsidy bar's top-up dialog refreshes the headers through this name when it closes
    window.refreshProductGroupHeaders = loadHeaders;

    function getViewMode() {
        var $sel = $('[data-viewmode].selected');
        return $sel.length > 0 ? $sel.data('viewmode') : 'grid';
    }

    function applyViewClass(viewMode) {
        $('.product-groups-container')
            .toggleClass('productgroups-list', viewMode === 'list')
            .toggleClass('productgroups-grid', viewMode !== 'list');

        //nop's grid and list styles act on each group's product-grid / product-list
        $('.product-groups-container .product-grid, .product-groups-container .product-list')
            .toggleClass('product-list', viewMode === 'list')
            .toggleClass('product-grid', viewMode !== 'list');
    }

    function loadItems() {
        var viewMode = getViewMode();
        $('.product-groups-container').load(cfg.itemsUrl + '?viewMode=' + viewMode, function () {
            applyViewClass(viewMode);
            setTimeout(configProductDetailsLink, 100);
        });
    }

    function configProductDetailsLink() {
        // Redirect product image/title link clicks to the add-to-cart button
        // so customers don't navigate away from the catalog page for simple products
        $(".product-details-link").each(function () {
            var $item = $(this).closest('.product-item');
            var $qtyWrapper = $item.find('.qty-wrapper');
            if ($item.length > 0 && $qtyWrapper.length === 0) {
                var $addBtn = $item.find('.product-box-add-to-cart-button');
                if ($addBtn.length > 0) {
                    $(this).attr("href", "javascript:void(0);")
                        .attr("title", $addBtn.text());
                    $(this).find('img').attr("title", $addBtn.text());
                    $(this).on('click', function (e) {
                        e.stopPropagation();
                        e.preventDefault();
                        $addBtn.click();
                        return false;
                    });
                }
            }
        });
    }

    $(document).ready(function () {
        cfg = window.subsidyCatalog || {};

        // View mode toggle
        $('[data-viewmode]').on('click', function () {
            if (!$(this).hasClass('selected')) {
                $('[data-viewmode]').removeClass('selected');
                $(this).addClass('selected');
                applyViewClass(getViewMode());
            }
            return false;
        });

        $(document).on('click', '.toggle-icon', function () {
            $(this).closest('.product-group').find('.products-container').slideToggle();
            $(this).toggleClass('collapsed expanded');
        });

        // Any change to the cart moves the credit in cart and the credit available
        $(document).on('cwCartChangedEvent', function () {
            loadBar();
            loadHeaders();
        });

        // Apply initial view class
        applyViewClass(getViewMode());
    });

})(jQuery);