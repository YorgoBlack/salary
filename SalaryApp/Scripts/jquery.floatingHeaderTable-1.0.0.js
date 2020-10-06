(function ($) {
    function container_scroll($container, $table) {
    }

    function createHelper($table) {
        $container = $table.parent();
        var $helper = $table
            .clone()
            .hide()
            .appendTo($container)
            .attr("data-helper-table", "true")
            .width($table.width())
            .css("position", "fixed")
            .css("left", $table.offset().left)
            .css("margin-left", "0px")
            .css("top", $container.offset().top)
            .css("margin-top", "0px")

        $helper.find("*").removeAttr("id"); // remove ids
        $helper.find("tbody").remove(); // remove tbody
        $helper.find("tr").each(function (i, tr) {
            var $tr = $(tr);
            var $orig_tr = $table.find("thead>tr:nth-child(" + (i + 1) + ")");
            $tr.children("th, td").each(function (j, thd) {
                var $thd = $(thd);
                var $orig_thd = $orig_tr.children("th:nth-child(" + (j + 1) + "), td:nth-child(" + (j + 1) + ")");
                $thd.width($orig_thd.width());
            });
        });

        return $helper;
    }

    var methods = {
        init: function (options) {
            var opts = $.extend({}, $.fn.floatingHeaderTable.defaults, options);

            return this.each(function () {
                var $table = $(this);
                var $container = $table.parent()
                    .scroll(function () {
                        var $helper = $table.data("helper")
                        if ($helper) {
                            var headerIsFullyVisible = $table.offset().top >= $container.offset().top;
                            var tableIsHiddenUp = ($table.offset().top + $table.height()) <= $container.offset().top;
                            if (headerIsFullyVisible || tableIsHiddenUp)
                                $helper.hide();
                            else
                                $helper.show();
                        }
                    })
                    .trigger("scroll");
                if ($table.data("helper"))
                    $table.data("helper").remove();
                $table.data("helper", createHelper($table));
            });
        },

        refresh: function () {
            return this.each(function () {
                var $table = $(this);
                if ($table.data("helper")) {
                    $table.data("helper").remove();
                    $table.data("helper", createHelper($table));
                }
            });
        }
    }

    $.fn.floatingHeaderTable = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        } else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        } else {
            $.error('Method ' + method + ' does not exist on jQuery.floatingHeaderTable');
        }
    }

    $.fn.floatingHeaderTable.defaults = {
    };

})(jQuery);