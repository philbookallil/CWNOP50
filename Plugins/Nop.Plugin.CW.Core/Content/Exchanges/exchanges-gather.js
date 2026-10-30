; (function ($, window, document, undefined) {
    'use strict';

    var cwExchangesGather = window.cwExchangesGather = (function () {

        var options = {};

        // ---------------------------------------------------------------- helpers

        var getMainForm = function () {
            return $('#gather-info-form');
        };

        var getReplacementInputsWrapper = function () {
            return $('.replacements-wrapper', getMainForm());
        };

        var getReplacementWidget = function (orderItemId) {
            return $('.replacement-item-container[data-orderitem-id="' + orderItemId + '"]');
        };

        /**
         * Shows the row's picker link, named for its row: "Choose" before a replacement is picked, "Change" after
         */
        var showChangeLink = function (orderItemId, show, picked) {
            var actions = $('.item-actions', getReplacementWidget(orderItemId)).toggle(!!show);
            var link = $('.edit', actions);
            var productName = link.data('product-name') || '';

            link.text(textOf(picked ? 'changeReplacement' : 'chooseReplacement'))
                .attr('aria-label', textOf(picked ? 'changeReplacementFor' : 'chooseReplacementFor', productName));
        };

        var needsReplacement = function (value) {
            return value === String(options.exchangeType.sameStyle) || value === String(options.exchangeType.differentStyle);
        };

        var typeOf = function (orderItemId) {
            return String($('.exchange-type-selector[data-orderitem-id="' + orderItemId + '"]').val());
        };

        var getNextStepButton = function () {
            return $('#confirmExchangeRequest');
        };

        /**
         * Localised text from the settings bundle, with {0}, {1}... replaced by the given arguments.
         * Falls back to the key name so a missing resource is visible rather than blank.
         */
        var textOf = function (key) {
            var value = (options.text || {})[key];

            if (value === undefined) {
                return key;
            }

            var args = Array.prototype.slice.call(arguments, 1);

            return args.reduce(function (result, arg, index) {
                return result.replace('{' + index + '}', arg);
            }, value);
        };

        var itemValue = function (item) {
            return item.Value !== undefined ? item.Value : item.value;
        };

        var itemText = function (item) {
            return item.Text !== undefined ? item.Text : item.text;
        };

        /**
        * Show or hide by class rather than by inline style. jQuery's show()/hide() write inline
        * display values, which override the responsive stylesheet - see Phase C.
        */
        var toggleSection = function (selector, show) {
            $(selector).toggleClass('is-hidden', !show);
        };

        // ---------------------------------------------------------------- mode selection

        var setExchangeMode = function (mode) {
            $('#exchange_mode').val(mode);
        };

        var clearAllReplacements = function () {
            $('.returned-item', getMainForm()).each(function () {
                var orderItemId = $(this).data('orderitem-id');
                clearReplacement(orderItemId);
                $('.exchange-type-selector[data-orderitem-id="' + orderItemId + '"]').val('0');
            });
        };

        var showExchangeMode = function () {
            setExchangeMode(options.exchangeMode.exchange);

            toggleSection('.creditnote-section', false);
            toggleSection('.exchange-section', true);
            toggleSection('.exchange-table', true);

            refreshNextStepButton();
        };

        var showCreditNoteMode = function () {
            setExchangeMode(options.exchangeMode.creditNote);

            // Nothing is being exchanged, so any replacement already picked is discarded
            clearAllReplacements();

            toggleSection('.exchange-section', false);
            toggleSection('.exchange-table', false);
            toggleSection('.creditnote-section', true);
        };

        // ---------------------------------------------------------------- replacement inputs

        var getReplacementInputs = function (orderItemId) {
            return $('.inputs-wrapper-' + orderItemId, getReplacementInputsWrapper());
        };

        var removeReplacementInputs = function (orderItemId) {
            getReplacementInputs(orderItemId).remove();
        };

        // Produces exactly the field names the server parses - see spec section 3
        var writeReplacementInputs = function (orderItemId, replacement) {
            removeReplacementInputs(orderItemId);

            var wrapper = $('<div class="inputs-wrapper-' + orderItemId + '">');
            wrapper.append($('<input type="hidden">')
                .attr('name', 'replacement_id_' + orderItemId)
                .val(replacement.productId));

            $.each(replacement.productAttributes, function (i, attribute) {
                var fieldName = 'attr_id_' + orderItemId + '_' + replacement.productId + '_' + attribute.attributeId;
                wrapper.append($('<input type="hidden">').attr('name', fieldName).val(attribute.attributeValueId));
            });

            getReplacementInputsWrapper().append(wrapper);
        };

        // ---------------------------------------------------------------- display

        var setReplacementDisplay = function (orderItemId, name, attributes, pictureUrl) {
            var widget = getReplacementWidget(orderItemId);

            $('.item-thumb', widget).remove();

            if (pictureUrl) {
                $('.item-with-thumb', widget).prepend(
                    $('<span class="item-thumb">').append($('<img>').attr({ src: pictureUrl, alt: name })));
            }

            $('.item-name', widget).text(name);

            var attrsContainer = $('.item-attrs', widget).empty();

            // Match the server's format exactly - "Name: Value<br />Name: Value" - so a row written
            // here looks identical to one rendered from AttributesInfo after a validation failure
            if (attributes && attributes.length) {
                $.each(attributes, function (i, attribute) {
                    if (i > 0) {
                        attrsContainer.append($('<br>'));
                    }

                    attrsContainer.append(document.createTextNode(attribute.attributeName + ': ' + attribute.attributeText));
                });
            }
        };

        var clearReplacement = function (orderItemId, labelText) {
            removeReplacementInputs(orderItemId);
            setReplacementDisplay(orderItemId, labelText || textOf('noReplacement'), null);
            showChangeLink(orderItemId, false);
        };

        // ---------------------------------------------------------------- modal

        var currentOrderItemId = null;
        var editState = null;
        var pickerRequest = null;
        var modalInitialised = false;
        var pickerState = { orderItemId: null, excludeProductId: null, keywords: '', pageIndex: 0 };
        var modalMaxWidth = 1000;

        var getModal = function () {
            return $('#change-style-modal');
        };

        var ensureModal = function () {
            if (modalInitialised) {
                return;
            }

            var modal = getModal();

            modal.dialog({
                autoOpen: false,
                modal: true,
                width: Math.min(1000, $(window).width() - 40),
                maxHeight: $(window).height() - 40,
                title: textOf('pickerTitle'),
                // Fires for every close route - the X, Esc, clicking the overlay, or dialog('close')
                close: abandonSelectionIfEmpty
            });

            modal.on('click', '.rp-card-select', function (e) {
                e.preventDefault();
                openProductOptions(pickerState.orderItemId, $(this).data('product-id'), null, true);
            });

            modal.on('click', '.rp-page', function (e) {
                e.preventDefault();
                pickerState.pageIndex = parseInt($(this).data('page'), 10);
                loadProductPage();
            });

            modal.on('submit', '.rp-search-form', function (e) {
                e.preventDefault();
                pickerState.keywords = $('.rp-search-input', modal).val();
                pickerState.pageIndex = 0;
                loadProductPage();
            });

            modal.on('click', '.rp-back', function (e) {
                e.preventDefault();
                loadProductPage();
            });

            // Attribute changed - swap the preview and the colour square, no server call needed
            modal.on('change', '.product-attrs select', function () {
                var select = $(this);
                var pictureUrl = $('option:selected', select).data('picture-url');

                if (pictureUrl) {
                    $('.rp-options-picture', modal).attr('src', pictureUrl);
                    $('.product-picture', modal).val(pictureUrl);
                }

                // Colour squares belong to the colour select alone - running this for Size would
                // hide every square and match none
                if (!select.hasClass('cw-colour-select')) {
                    return;
                }

                var container = $('#color-samples-' + $('.product-id', modal).val(), modal);

                if (container.length) {
                    $('.color-indicator', container).hide();
                    $('.color-sample-' + select.val(), container).show();
                }
            });

            modalInitialised = true;
        };

        var openModal = function () {
            ensureModal();
            getModal().dialog('open');
        };

        var closeModal = function () {
            if (modalInitialised) {
                getModal().dialog('close');
            }
        };

        var sizeAndCentreModal = function () {
            if (!modalInitialised) {
                return;
            }

            getModal()
                .dialog('option', {
                    width: Math.min(modalMaxWidth, $(window).width() - 40),
                    maxHeight: Math.floor($(window).height() * 0.85)
                })
                .dialog('option', 'position', { my: 'center', at: 'center', of: window });
        };

        /** Stage 1 - browse. Different Style only. */
        var openProductBrowser = function (orderItemId, excludeProductId) {
            pickerState = { orderItemId: orderItemId, excludeProductId: excludeProductId, keywords: '', pageIndex: 0 };
            currentOrderItemId = orderItemId;

            openModal();
            loadProductPage();
        };

        /**
         * Starts a picker load: an earlier load still running is dropped, so a late answer cannot land on another row
         */
        var startPickerRequest = function (settings) {
            if (pickerRequest) {
                pickerRequest.abort();
            }

            var request = $.ajax(settings);
            pickerRequest = request;

            return request;
        };

        var isCurrentPickerRequest = function (request) {
            return pickerRequest === request;
        };

        /** Moves the keyboard focus into what the picker just loaded */
        var focusPickerContent = function () {
            $('.modal-content', getModal()).find('input:visible, select:visible, button:visible, a[href]:visible').first().trigger('focus');
        };

        var loadProductPage = function () {
            var modal = getModal();

            modalMaxWidth = 1000;

            $('.modal-content', modal).html('<div class="picker-loading"><span class="picker-spinner"></span>' + textOf('loadingProducts') + '</div>');
            $('.error-wrapper', modal).hide();
            sizeAndCentreModal();

            var request = startPickerRequest({
                cache: false,
                url: options.replacementListUrl,
                data: {
                    orderItemId: pickerState.orderItemId,
                    excludeProductId: pickerState.excludeProductId,
                    keywords: pickerState.keywords,
                    pageIndex: pickerState.pageIndex
                }
            });

            request
                .done(function (html) {
                    if (!isCurrentPickerRequest(request)) {
                        return;
                    }

                    $('.modal-content', modal).html(html);
                    focusPickerContent();
                })
                .fail(function (jqXHR, textStatus, errorThrown) {
                    if (!isCurrentPickerRequest(request) || textStatus === 'abort') {
                        return;
                    }

                    $('.modal-content', modal).empty();
                    showPickerError(textOf('loadProductsError', errorThrown || textStatus));
                })
                .always(function () {
                    if (isCurrentPickerRequest(request)) {
                        sizeAndCentreModal();
                    }
                });
        };

        /**
         * Stage 2 - choose size and colour for one product. Same Style and Edit Exchange Item enter here
         * directly; Different Style arrives via a card in the stage 1 grid.
         * fromBrowser: true only when there is a stage 1 grid behind this, so Back has somewhere to go.
         */
        var openProductOptions = function (orderItemId, productId, returnRequestId, fromBrowser) {
            currentOrderItemId = orderItemId;

            var modal = getModal();

            $('.modal-content', modal).html('<div class="picker-loading"><span class="picker-spinner"></span>' + textOf('loadingOptions') + '</div>');
            $('.error-wrapper', modal).hide();

            modalMaxWidth = 620;

            openModal();
            sizeAndCentreModal();

            var request = startPickerRequest({
                cache: false,
                url: options.replacementOptionsUrl,
                data: { productId: productId, orderItemId: orderItemId, returnRequestId: returnRequestId || null }
            });

            request
                .done(function (html) {
                    if (!isCurrentPickerRequest(request)) {
                        return;
                    }

                    $('.modal-content', modal).html(html);

                    // The partial renders Back hidden - reveal it only when a grid is behind us
                    if (fromBrowser) {
                        $('.rp-back', modal).show();
                    }

                    // Sync the swatch and preview with whatever came preselected
                    $('.cw-colour-select', modal).trigger('change');
                    focusPickerContent();
                })
                .fail(function (jqXHR, textStatus, errorThrown) {
                    if (!isCurrentPickerRequest(request) || textStatus === 'abort') {
                        return;
                    }

                    $('.modal-content', modal).empty();
                    showPickerError(textOf('loadOptionsError', errorThrown || textStatus));
                })
                .always(function () {
                    if (isCurrentPickerRequest(request)) {
                        sizeAndCentreModal();
                    }
                });
        };

        var showPickerError = function (message) {
            $('.error-wrapper', getModal()).show().find('.error-text').text(message);
            sizeAndCentreModal();
        };

        var setPickerBusy = function (busy) {
            var modal = getModal();
            modal.toggleClass('picker-busy', !!busy);
            $('.rp-choose', modal).prop('disabled', !!busy);
        };

        // ---------------------------------------------------------------- selection

        var readProductForm = function (productId) {
            var form = $('#product-details-form-' + productId);
            var attributes = [];

            $('.product-attrs .product-input select', form).each(function () {
                var select = $(this);
                attributes.push({
                    attributeId: select.data('attribute-id'),
                    attributeValueId: select.val(),
                    attributeName: select.data('attribute-name'),
                    attributeText: $('option:selected', select).text()
                });
            });

            return {
                productId: $('.product-id', form).val(),
                productName: $('.product-name', form).val(),
                productPicture: $('.product-picture', form).val(),
                productAttributes: attributes
            };
        };

        var validateProductForm = function (productId) {
            var valid = true;

            $('.product-attrs .product-input select', $('#product-details-form-' + productId)).each(function () {
                if (!$(this).val() || $(this).val() === '0') {
                    valid = false;
                }
            });

            return valid;
        };

        /**
        * What happens once a product is chosen. Set by init() (wizard) or
        * initEditExchangeItem() (return requests list).
        */
        var selectionHandler = null;

        /** Wizard: record the replacement on the form and show it in the row */
        var applySelectionToGatherForm = function (replacement) {
            if (!currentOrderItemId) {
                return;
            }

            writeReplacementInputs(currentOrderItemId, replacement);
            setReplacementDisplay(currentOrderItemId, replacement.productName, replacement.productAttributes, replacement.productPicture);
            showChangeLink(currentOrderItemId, true, true);

            closeModal();
            refreshNextStepButton();
        };

        /** Called by the Choose Product button rendered in _ReplacementProductOptions.cshtml */
        var chooseProduct = function (productId) {
            var form = $('#product-details-form-' + productId);

            // The picker can only collect options it can render as a dropdown. A product with a
            // required option it cannot render - free text, a date, an upload, a rental period, gift
            // card details - would be refused by cart validation after approval, leaving an exchange
            // that can never be completed. Refuse it here with the reason. The product deliberately
            // stays visible: hiding it reads as the product being out of stock.
            // attr(), not data(): jQuery coerces data-* values and would turn a name like "0" false.
            var uncollectable = form.attr('data-uncollectable');

            if (uncollectable) {
                showPickerError(textOf('optionsNotAvailable', uncollectable));
                return;
            }

            if (!validateProductForm(productId)) {
                alert(textOf('selectAllOptions'));
                return;
            }

            var replacement = readProductForm(productId);

            (selectionHandler || applySelectionToGatherForm)(replacement);
        };

        /** The picker closed: a load still running is dropped; the row keeps its type and waits for a product */
        var abandonSelectionIfEmpty = function () {
            editState = null;

            if (pickerRequest) {
                pickerRequest.abort();
                pickerRequest = null;
            }

            if (!currentOrderItemId) {
                return;
            }

            var orderItemId = currentOrderItemId;
            currentOrderItemId = null;

            refreshNextStepButton();

            //back to the row's own link, for the keyboard
            $('.item-actions .edit', getReplacementWidget(orderItemId)).trigger('focus');
        };

        // ---------------------------------------------------------------- exchange type

        /**
         * A new type drops the replacement picked for the old one (a product picked for one type never stands for
         * another). The picker opens from the row's link, not from the list, so moving through the list with the
         * keyboard opens nothing.
         */
        var handleExchangeTypeChange = function (select) {
            var orderItemId = select.data('orderitem-id');
            var value = String(select.val());

            if (value === String(options.exchangeType.exact)) {
                // No picker - the returned item comes back identically
                clearReplacement(orderItemId, textOf('exactExchange'));
            }
            else {
                clearReplacement(orderItemId);
                showChangeLink(orderItemId, needsReplacement(value), false);
            }

            refreshNextStepButton();
        };

        var handleChangeLinkClick = function (link) {
            var orderItemId = link.data('orderitem-id');
            var select = $('.exchange-type-selector[data-orderitem-id="' + orderItemId + '"]');
            var productId = select.data('product-id');
            var value = typeOf(orderItemId);

            if (value === String(options.exchangeType.sameStyle)) {
                openProductOptions(orderItemId, productId, null, false);
            }
            else if (value === String(options.exchangeType.differentStyle)) {
                openProductBrowser(orderItemId, productId);
            }
        };

        // ---------------------------------------------------------------- validation

        /**
         * Returns null when every row is resolved, otherwise a sentence saying what is missing.
         * Type is reported before replacement, because choosing a type is what reveals the picker.
         */
        var whatIsMissing = function () {
            var missingType = 0;
            var missingProduct = 0;

            $('.returned-item', getMainForm()).each(function () {
                var row = $(this);
                var value = String($('.exchange-type-selector', row).val());

                if (!value || value === '0') {
                    missingType++;
                    return;
                }

                // Same and different style both require a chosen replacement
                if ((value === String(options.exchangeType.sameStyle) || value === String(options.exchangeType.differentStyle))
                    && getReplacementInputs(row.data('orderitem-id')).length === 0) {
                    missingProduct++;
                }
            });

            if (missingType > 0) {
                return missingType === 1
                    ? textOf('chooseTypeSingular')
                    : textOf('chooseTypePlural', missingType);
            }

            if (missingProduct > 0) {
                return missingProduct === 1
                    ? textOf('chooseReplacementSingular')
                    : textOf('chooseReplacementPlural', missingProduct);
            }

            return null;
        };

        var refreshNextStepButton = function () {
            var missing = whatIsMissing();
            var hint = $('#next-step-hint');

            getNextStepButton().prop('disabled', !!missing);

            if (missing) {
                hint.text(missing);
                toggleSection('#next-step-hint', true);
            } else {
                hint.empty();
                toggleSection('#next-step-hint', false);
            }
        };

        // ---------------------------------------------------------------- reason -> action

        var loadActionsForReason = function () {
            var reasonSelect = $(options.reasonSelector);
            var actionSelect = $(options.actionSelector);
            var reasonId = reasonSelect.val();

            if (!reasonId || reasonId === '0') {
                return;
            }

            var previous = actionSelect.val();
            actionSelect.prop('disabled', true);

            $.getJSON(options.actionsForReasonUrl, { reasonId: reasonId, storeId: options.storeId })
                .done(function (items) {
                    actionSelect.empty();

                    $.each(items || [], function (i, item) {
                        actionSelect.append($('<option>').val(itemValue(item)).text(itemText(item)));
                    });

                    // Keep the previous choice when it is still offered
                    if (previous && actionSelect.find('option[value="' + previous + '"]').length) {
                        actionSelect.val(previous);
                    }
                })
                .fail(function () {
                    // Leave the existing list rather than emptying it on a transient failure
                    console.error('Could not load return actions for the selected reason.');
                })
                .always(function () {
                    actionSelect.prop('disabled', false);
                });
        };

        // ---------------------------------------------------------------- confirmations panel

        var initConfirmations = function (settings) {
            // Merge rather than replace - this panel renders inside the return requests list, whose
            // own init() runs afterwards, and both contribute to options.text
            options = $.extend(true, options, settings || {});

            var panel = $('#rr-confirms');
            if (panel.length === 0) {
                return;
            }

            var printed = false;

            panel.dialog({
                autoOpen: true,
                modal: true,
                closeOnEscape: false,
                width: Math.min(820, $(window).width() - 40),
                maxHeight: Math.floor($(window).height() * 0.85),
                title: textOf('dialogTitle'),
                position: { my: 'center', at: 'center', of: window },
                beforeClose: function () {
                    if (settings && settings.allowCloseWithoutPrinting) {
                        return true;
                    }

                    if (!printed) {
                        window.alert(textOf('printReminder'));
                        return false;
                    }

                    return true;
                }
            });

            $('.rc-print', panel).on('click', function () {
                printed = true;
            });

            $('.rc-close', panel).on('click', function () {
                panel.dialog('close');
            });

            $(window).on('resize', function () {
                if (panel.dialog('isOpen')) {
                    panel.dialog('option', {
                        width: Math.min(820, $(window).width() - 40),
                        maxHeight: Math.floor($(window).height() * 0.85)
                    }).dialog('option', 'position', { my: 'center', at: 'center', of: window });
                }
            });
        };

        // ---------------------------------------------------------------- edit exchange item

        /** True when at least one attribute differs from what is currently recorded */
        var selectionDiffersFromCurrent = function (replacement) {
            if (!editState || !editState.current) {
                return true;
            }

            var changed = false;

            $.each(replacement.productAttributes, function (i, attribute) {
                var current = editState.current[String(attribute.attributeId)];

                // Unknown attribute - treat as a change rather than silently blocking
                if (current === undefined || String(current) !== String(attribute.attributeValueId)) {
                    changed = true;
                    return false;
                }
            });

            return changed;
        };

        var submitExchangeItemEdit = function (replacement) {
            if (!selectionDiffersFromCurrent(replacement)) {
                showPickerError(textOf('sameCombination'));
                return;
            }

            var payload = { returnRequestId: editState.returnRequestId };

            $.each(replacement.productAttributes, function (i, attribute) {
                payload['selectedAttributes[' + i + '].AttributeId'] = attribute.attributeId;
                payload['selectedAttributes[' + i + '].AttributeValueId'] = attribute.attributeValueId;
            });

            addAntiForgeryToken(payload);

            setPickerBusy(true);

            $.ajax({
                cache: false,
                method: 'POST',
                url: options.editExchangedItemUrl,
                data: payload,
            })
                .done(function (response) {
                    if (response && response.success) {
                        // The reloaded row is the confirmation - a toast would be destroyed by the reload
                        closeModal();
                        window.location.reload();
                        return;
                    }

                    showPickerError((response && response.message) || textOf('updateFailed'));
                    setPickerBusy(false);
                })
                .fail(function (jqXHR, textStatus, errorThrown) {
                    // Deliberately stays open - closing would discard the customer's selection
                    showPickerError(textOf('updateError', errorThrown || textStatus));
                    setPickerBusy(false);
                });
        };

        var initEditExchangeItem = function (settings) {
            options = $.extend(options, settings || {});
            selectionHandler = submitExchangeItemEdit;

            $('.change-return-button').on('click', function (e) {
                e.preventDefault();

                var button = $(this);

                editState = {
                    returnRequestId: parseInt(button.data('returnrequestid'), 10),
                    productId: parseInt(button.data('productid'), 10),
                    current: button.data('currentattrs') || null
                };

                // Same product, every option open - the customer is adjusting their own choice.
                // returnRequestId preselects the attributes already on the exchange; the returned item's id
                // opens its packname's catalogue, as in the wizard.
                openProductOptions(parseInt(button.data('orderitemid'), 10), editState.productId, editState.returnRequestId, false);
            });
        };

        // ---------------------------------------------------------------- init

        var init = function (settings) {
            // Merge, like the other initialisers - a page may call more than one of them
            options = $.extend(true, options, settings || {});
            selectionHandler = applySelectionToGatherForm;

            $(window).on('resize', function () {
                if (modalInitialised && getModal().dialog('isOpen')) {
                    sizeAndCentreModal();
                }
            });

            $('.mode-button').on('click', function (e) {
                e.preventDefault();

                if ($(this).data('mode') === 'exchange') {
                    showExchangeMode();
                } else {
                    showCreditNoteMode();
                }
            });

            getMainForm().on('submit', function (e) {
                var missing = whatIsMissing();

                if ($('#exchange_mode').val() === String(options.exchangeMode.exchange) && missing) {
                    e.preventDefault();
                    alert(missing);
                }
            });

            submitOnce(getMainForm());

            var table = $('.returned-items-list');

            table.on('change', '.exchange-type-selector', function () {
                handleExchangeTypeChange($(this));
            });

            table.on('click', '.item-actions .edit', function (e) {
                e.preventDefault();
                handleChangeLinkClick($(this));
            });

            if (options.reasonSelector && options.actionSelector) {
                initReasonCascade(options);
            }

            // A row's link shows once its type needs a product: "Change" when one is picked (a row restored after a
            // validation failure), "Choose" before
            $('.returned-item', getMainForm()).each(function () {
                var orderItemId = $(this).data('orderitem-id');
                var picked = getReplacementInputs(orderItemId).length > 0;

                showChangeLink(orderItemId, picked || needsReplacement(typeOf(orderItemId)), picked);
            });

            // Reopen where the customer left off when the step is redisplayed after a validation failure
            var restoredMode = $('#exchange_mode').val();

            if (restoredMode === String(options.exchangeMode.exchange)) {
                showExchangeMode();
            } else if (restoredMode === String(options.exchangeMode.creditNote)) {
                // Deliberately not showCreditNoteMode() - that clears replacements, which is right
                // for a button click but would wipe restored state
                setExchangeMode(options.exchangeMode.creditNote);
                toggleSection('.exchange-section', false);
                toggleSection('.exchange-table', false);
                toggleSection('.creditnote-section', true);
            }

            refreshNextStepButton();
        };

        var initReasonCascade = function (settings) {
            options = $.extend(options, settings || {});
            $(options.reasonSelector).on('change', loadActionsForReason);
        };

        /**
         * A form goes once: a second click while the first post is on its way does nothing. The buttons are
         * disabled after the browser has read the clicked one, so its name still goes with the post.
         */
        var submitOnce = function (form) {
            var submitted = false;

            form.on('submit', function (e) {
                if (e.isDefaultPrevented()) {
                    return;
                }

                if (submitted) {
                    e.preventDefault();
                    return;
                }

                submitted = true;

                window.setTimeout(function () {
                    $('button[type="submit"], input[type="submit"]', form).prop('disabled', true);
                }, 0);
            });

            // Coming back with the browser's Back button shows the page as it was left: usable again
            $(window).on('pageshow', function (e) {
                if (e.originalEvent && e.originalEvent.persisted) {
                    submitted = false;
                    $('button[type="submit"], input[type="submit"]', form).prop('disabled', false);
                }
            });
        };

        return {
            init: init,
            initReasonCascade: initReasonCascade,
            submitOnce: submitOnce,
            initConfirmations: initConfirmations,
            initEditExchangeItem: initEditExchangeItem,
            chooseProduct: chooseProduct
        };
    })();

    // The Choose Product button in _ReplacementProduct.cshtml calls this by name
    window.validateSelection = function (productId) {
        cwExchangesGather.chooseProduct(productId);
    };

})(jQuery, window, document);