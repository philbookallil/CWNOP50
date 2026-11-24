/* The approver's page: opens or closes every person of a large order at once. The page works without it. */
(function () {
    function start() {
        var button = document.querySelector('.order-approval-expand');

        if (!button)
            return;

        var groups = Array.prototype.slice.call(document.querySelectorAll('details.order-approval-group'));

        function anyClosed() {
            return groups.some(function (group) { return !group.open; });
        }

        function showText() {
            button.textContent = button.getAttribute(anyClosed() ? 'data-expand-text' : 'data-collapse-text');
        }

        button.addEventListener('click', function () {
            var open = anyClosed();

            groups.forEach(function (group) { group.open = open; });
            showText();
        });

        //opened or closed one by one
        groups.forEach(function (group) { group.addEventListener('toggle', showText); });

        button.hidden = false;
        showText();
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', start);
    else
        start();
})();
