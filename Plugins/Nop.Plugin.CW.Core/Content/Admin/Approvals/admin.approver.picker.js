/*
 * The boxes of approvers: they suggest the store's approvers by name or address, and take a new address too.
 *
 * A box is a text input (class cw-approver-picker) whose value is the list of addresses the form posts. What it posts
 * is unchanged, and an untouched box keeps its value to the character: a form that compares itself with what was loaded
 * does not read as changed. Suggestions come with the box (data-approvers), so nothing is asked of the server.
 *
 * Anything typed becomes a tag, an address that cannot be one in red: the server refuses it, and it is never lost.
 */
window.CW = window.CW || {};

CW.ApproverPicker = CW.ApproverPicker || (function () {
    'use strict';

    var separator = ', ';
    var pickers = [];

    function split(text) {
        return $.map((text || '').split(/[,;|\r\n]+/), function (part) {
            return $.trim(part) || null;
        });
    }

    function same(first, second) {
        return first.toLowerCase() === second.toLowerCase();
    }

    function isAddress(text) {
        return /^[^\s@,;|]+@[^\s@,;|]+\.[^\s@,;|]+$/.test(text);
    }

    function label(option, escape) {
        return option.Name && !same(option.Name, option.Email)
            ? escape(option.Name) + ' <span class="cw-approver-email">' + escape(option.Email) + '</span>'
            : escape(option.Email);
    }

    //the option or item with an address, whatever its case, or else with a name only one has; null when there is none
    function find(picker, text) {
        var keys = Object.keys(picker.options);
        var byEmail = keys.filter(function (key) { return same(key, text); });

        if (byEmail.length)
            return byEmail[0];

        var byName = keys.filter(function (key) { return same(picker.options[key].Name || '', text); });

        return byName.length === 1 ? byName[0] : null;
    }

    //what is typed becomes tags: an approver's address the approver, anything else a new one. A box of one approver takes
    //the text as one, several addresses too (the server refuses them), in place of the one it had
    function commit(picker, text) {
        var single = picker.settings.maxItems === 1;

        if (single)
            picker.clear(true);

        $.each(single ? [$.trim(text)] : split(text), function (index, email) {
            var key = find(picker, email);

            if (key === null) {
                picker.addOption({ Name: email, Email: email });
                key = email;
            }

            picker.addItem(key);
        });

        picker.setTextboxValue('');
        picker.refreshOptions(false);
    }

    /*
     * Makes a box a picker, or starts one again with other suggestions.
     *
     * element: the input; emails: what it holds; suggestions: the approvers it suggests
     */
    function start(element, emails, suggestions) {
        if (element.tomselect)
            element.tomselect.destroy();

        //the boxes of a panel the page has replaced, and of this box before
        pickers = $.grep(pickers, function (old) {
            var inUse = old.input.tomselect === old && document.contains(old.input);

            if (!inUse && old.input.tomselect === old)
                old.destroy();

            return inUse;
        });

        var current = split(emails);

        //an address it holds is kept as it is written, with the approver's name
        var options = $.map(current, function (email) {
            var known = $.grep(suggestions, function (suggestion) { return same(suggestion.Email, email); })[0];

            return { Name: known ? known.Name : email, Email: email };
        }).concat($.grep(suggestions, function (suggestion) {
            return !current.some(function (email) { return same(email, suggestion.Email); });
        }));

        element.value = current.join(separator);

        var $element = $(element);

        var picker = new TomSelect(element, {
            valueField: 'Email',
            labelField: 'Email',
            searchField: ['Name', 'Email'],
            sortField: [{ field: 'Name' }, { field: 'Email' }],
            delimiter: separator,
            //what is pasted is split below, whatever separates it
            splitOn: /(?!)/,
            //the suggestions are at hand; a delayed list would answer Enter for what was typed before
            refreshThrottle: 0,
            options: options,
            items: current,
            maxItems: $element.data('maxItems') || null,
            mode: 'multi',
            hideSelected: true,
            closeAfterSelect: true,
            plugins: ['remove_button'],
            create: function (text) {
                return { Name: text, Email: text };
            },
            //an address in other case is an approver already
            createFilter: function (text) {
                return split(text).length === 1 && find(this, $.trim(text)) === null;
            },
            render: {
                option: function (option, escape) {
                    return '<div>' + label(option, escape) + '</div>';
                },
                item: function (option, escape) {
                    return '<div' + (isAddress(option.Email) ? '' : ' class="cw-approver-invalid"') + '>' + label(option, escape) + '</div>';
                },
                option_create: function (typed, escape) {
                    return '<div class="create">' + escape($element.data('addText') || '') + ' <strong>' + escape(typed.input) + '</strong></div>';
                },
                no_results: function () {
                    return null;
                }
            }
        });

        pickers.push(picker);

        var input = picker.control_input;
        var chosen = false; //a suggestion was gone to with the arrows

        input.addEventListener('input', function () {
            chosen = false;
        });

        //before the library's own handler, which takes the first suggestion on Enter: a whole address typed is that
        //address, though an approver's contains it, and an empty box takes nothing. Enter never submits the form around
        //the box. A comma or a semicolon ends an address
        input.addEventListener('keydown', function (event) {
            var typed = picker.inputValue();

            if (event.key === 'ArrowDown' || event.key === 'ArrowUp')
                chosen = true;

            if (event.key === 'Enter') {
                event.preventDefault();

                if (chosen && picker.isOpen) {
                    chosen = false;
                    return;
                }

                //part of a name or an address takes the first suggestion
                if (typed && picker.isOpen && !isAddress(typed))
                    return;

                if (typed)
                    commit(picker, typed);

                event.stopImmediatePropagation();
            }
            else if ((event.key === ',' || event.key === ';') && typed) {
                event.preventDefault();
                event.stopImmediatePropagation();
                commit(picker, typed);
            }
        }, true);

        //pasted text may hold several, one per line too
        input.addEventListener('paste', function (event) {
            var pasted = event.clipboardData ? event.clipboardData.getData('text') : '';

            if (split(pasted).length > 1) {
                event.preventDefault();
                commit(picker, input.value.slice(0, input.selectionStart) + pasted + input.value.slice(input.selectionEnd));
            }
        });

        //a click on a tag, which the library answers by emptying the box
        picker.control.addEventListener('mousedown', function (event) {
            if (picker.inputValue() && $(event.target).closest('.item').length)
                commit(picker, picker.inputValue());
        }, true);

        //an address typed and left, as when Save is pressed; before the library, which empties the box then. Another
        //window taken to the front is not leaving it
        input.addEventListener('blur', function () {
            if (!document.hasFocus() || !picker.inputValue())
                return;

            //a tag on a new line would move the button being pressed from under the mouse: the box grows when it is let go
            picker.control.style.height = picker.control.offsetHeight + 'px';

            $(document).off('.cwapproverpicker').one('mouseup.cwapproverpicker keyup.cwapproverpicker', function () {
                $(document).off('.cwapproverpicker');
                setTimeout(function () {
                    $('.ts-wrapper.cw-approver-picker .ts-control').css('height', '');
                }, 0);
            });

            commit(picker, picker.inputValue());
        }, true);
    }

    //every box not yet a picker, with the suggestions it came with
    function startAll() {
        $('input.cw-approver-picker').each(function () {
            if (!this.tomselect)
                start(this, this.value, $(this).data('approvers') || []);
        });
    }

    $(startAll);

    return {
        start: start,
        startAll: startAll
    };
})();
