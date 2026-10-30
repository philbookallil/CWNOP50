(function () {
    var $page = $('.packname-import-page');

    if ($page.length === 0)
        return;

    var $cfg = $page.find('.packname-import-config');
    var $form = $page.find('#packname-import-form');
    var $file = $page.find('#packname-import-file');
    var $drop = $page.find('.packname-import-drop');
    var $dropEmpty = $drop.find('.packname-import-drop-empty');
    var $dropFile = $drop.find('.packname-import-drop-file');
    var $dropWorking = $drop.find('.packname-import-drop-working');
    var $importButton = $page.find('.packname-import-button');
    var $message = $page.find('.packname-import-message');
    var $result = $page.find('.packname-import-result');
    var excelIcon = $page.find('.packname-import-template use').attr('href');

    var labels = {
        rows: $cfg.data('labelRows'),
        created: $cfg.data('labelCreated'),
        customers: $cfg.data('labelCustomers'),
        welcomeEmails: $cfg.data('labelWelcomeEmails'),
        lists: $cfg.data('labelLists'),
        failed: $cfg.data('labelFailed'),
        partlyTitle: $cfg.data('labelPartlyTitle'),
        partly: $cfg.data('labelPartly'),
        problemRows: $cfg.data('labelProblemRows'),
        fixHint: $cfg.data('labelFixHint'),
        row: $cfg.data('labelRow'),
        rowsToFix: $cfg.data('labelRowsToFix'),
        listsUsed: $cfg.data('labelListsUsed'),
        downloadReport: $cfg.data('labelDownloadReport'),
        requestFailed: $cfg.data('labelRequestFailed'),
        working: $cfg.data('labelWorking'),
        noFile: $cfg.data('labelNoFile'),
        notExcel: $cfg.data('labelNotExcel')
    };

    function text(value) {
        return $('<span>').text(value == null ? '' : String(value)).html();
    }

    //puts every value into the {0}, {1}... placeholders of a label
    function format(label) {
        var values = Array.prototype.slice.call(arguments, 1);

        return String(label).replace(/\{(\d+)\}/g, function (match, index) {
            return values[index] != null ? values[index] : match;
        });
    }

    function fileSize(bytes) {
        if (bytes < 1024)
            return bytes + ' B';

        if (bytes < 1024 * 1024)
            return Math.round(bytes / 1024) + ' KB';

        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }

    function showMessage(message) {
        $message.text(message || '').prop('hidden', !message);
    }

    function clearResult() {
        $result.prop('hidden', true).removeClass('is-success is-failed is-partly').empty();
    }

    //the drop area shows either the prompt or the chosen file
    function showChosenFile() {
        var file = $file[0].files && $file[0].files[0];

        $drop.toggleClass('has-file', !!file);
        $dropEmpty.prop('hidden', !!file);
        $dropFile.prop('hidden', !file);

        if (file) {
            $dropFile.find('.packname-import-file-name').text(file.name);
            $dropFile.find('.packname-import-file-size').text(fileSize(file.size));
        }
    }

    function setWorking(working) {
        $drop.toggleClass('is-working', working);
        $dropWorking.prop('hidden', !working);

        if (working) {
            $dropEmpty.prop('hidden', true);
            $dropFile.prop('hidden', true);
        } else {
            showChosenFile();
        }

        $importButton.prop('disabled', working).text(working ? labels.working : $importButton.data('label'));
    }

    function head(title, lines) {
        var html = '<div class="packname-import-result-head"><span class="packname-import-result-mark" aria-hidden="true"></span><div>'
            + '<h2 class="packname-import-result-title">' + text(title) + '</h2>';

        $.each(lines, function (index, line) {
            if (line)
                html += '<p>' + text(line) + '</p>';
        });

        return html + '</div></div>';
    }

    //what the import created: all of it, or the rows before the one it stopped at
    function renderCreated(data) {
        var html = '';
        var facts = [];

        if (data.customersCreated > 0) {
            facts.push(format(labels.customers, data.customersCreated));
            facts.push(format(labels.welcomeEmails, data.welcomeEmailsQueued));
        }

        if (data.packNameListsCreated > 0)
            facts.push(format(labels.lists, data.packNameListsCreated));

        if (facts.length > 0) {
            html += '<ul class="packname-import-facts">';
            $.each(facts, function (index, fact) {
                html += '<li>' + text(fact) + '</li>';
            });
            html += '</ul>';
        }

        if (data.lists && data.lists.length > 0) {
            html += '<div><h3 class="packname-import-subtitle">' + text(labels.listsUsed) + '</h3><ul class="packname-import-lists">';
            $.each(data.lists, function (index, list) {
                html += '<li><span>' + text(list.name) + '</span><strong>' + text(list.count) + '</strong></li>';
            });
            html += '</ul></div>';
        }

        return html;
    }

    function renderRowErrors(rowErrors) {
        var html = '<div><h3 class="packname-import-subtitle">' + text(labels.rowsToFix) + '</h3><ul class="packname-import-row-errors">';

        $.each(rowErrors, function (index, rowError) {
            html += '<li><span class="packname-import-row-number">' + text(format(labels.row, rowError.row)) + '</span><ul>';
            $.each(rowError.messages, function (messageIndex, message) {
                html += '<li>' + text(message) + '</li>';
            });
            html += '</ul></li>';
        });

        return html + '</ul></div>';
    }

    //the summary of a successful import, the file and row problems of a refused one, or both for an import
    //stopped part way
    function render(data) {
        var rowErrors = data.rowErrors || [];
        var fileErrors = data.fileErrors || [];
        var stopped = !data.success && data.firstRowNotImported > 0;
        var rows = data.rowCount > 0 ? format(labels.rows, data.rowCount) : '';
        var html;
        var state;

        if (data.success) {
            state = 'is-success';
            html = head(format(labels.created, data.packNamesCreated), [rows])
                + '<div class="packname-import-result-body">' + renderCreated(data) + '</div>';
        } else if (stopped) {
            state = 'is-partly';
            html = head(format(labels.partlyTitle, data.firstRowNotImported), [format(labels.partly, data.firstRowNotImported)])
                + '<div class="packname-import-result-body">'
                + '<h3 class="packname-import-subtitle">' + text(format(labels.created, data.packNamesCreated)) + '</h3>'
                + renderCreated(data);
        } else {
            state = 'is-failed';
            html = head(labels.failed, [
                rowErrors.length > 0 ? format(labels.problemRows, rowErrors.length, data.rowCount) : '',
                rowErrors.length > 0 ? labels.fixHint : ''
            ]) + '<div class="packname-import-result-body">';

            if (data.hasErrorReport)
                html += '<div><button type="submit" form="packname-import-form" class="packname-import-report">'
                    + (excelIcon ? '<svg class="cw-icon" aria-hidden="true"><use href="' + text(excelIcon) + '"></use></svg>' : '')
                    + '<span>' + text(labels.downloadReport) + '</span></button></div>';
        }

        if (!data.success) {
            if (fileErrors.length > 0) {
                html += '<ul class="packname-import-file-errors">';
                $.each(fileErrors, function (index, error) {
                    html += '<li>' + text(error) + '</li>';
                });
                html += '</ul>';
            }

            if (rowErrors.length > 0)
                html += renderRowErrors(rowErrors);

            html += '</div>';
        }

        $result.addClass(state).html(html).prop('hidden', false).trigger('focus');
    }

    //the chosen file goes up with the form's own antiforgery token; the form stays on the page, so the same
    //file can then be posted again for the error report
    $importButton.on('click', function () {
        var file = $file[0].files && $file[0].files[0];

        showMessage('');
        clearResult();

        if (!file) {
            showMessage(labels.noFile);
            $file.trigger('focus');
            return;
        }

        if (!/\.xlsx$/i.test(file.name)) {
            showMessage(labels.notExcel);
            return;
        }

        var formData = new FormData($form[0]);

        setWorking(true);

        $.ajax({
            cache: false,
            url: $cfg.data('importUrl'),
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (data) {
                //anything but the import's own answer (a signed-out session answers with a page)
                if (!data || typeof data !== 'object') {
                    showMessage(labels.requestFailed);
                    return;
                }

                //refused before the import started: the file (message), or the store's switch or the access (error)
                if (data.message || data.error) {
                    showMessage(data.message || data.error);
                    return;
                }

                render(data);

                //a file that has been imported is not offered again, so a second click cannot import it twice
                if (data.success)
                    $file.val('');
            },
            error: function () {
                showMessage(labels.requestFailed);
            },
            complete: function () {
                setWorking(false);
            }
        });
    });

    $importButton.data('label', $importButton.text());

    $file.on('change', function () {
        showMessage('');
        clearResult();
        showChosenFile();
    });

    //the file input covers the whole drop area, so a dropped file lands in it; this only marks the area
    $file.on('dragenter dragover', function () {
        $drop.addClass('is-dragging');
    });

    $file.on('dragleave drop', function () {
        $drop.removeClass('is-dragging');
    });

    showChosenFile();
})();