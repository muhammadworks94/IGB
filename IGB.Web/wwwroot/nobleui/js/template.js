(function ($) {
    'use strict';
    $(function () {
        var body = $('body');
        var mainWrapper = $('.main-wrapper');
        var footer = $('footer');
        var sidebar = $('.sidebar');
        var navbar = $('.navbar').not('.top-navbar');


        // Enable feather-icons with SVG markup
        if (typeof feather !== 'undefined' && typeof feather.replace === 'function') {
            try {
                feather.replace();
            } catch (error) {
                console.warn('Feather Icons replace failed:', error);
            }
        } else {
            console.warn('Feather Icons library not available');
        }


        // initialize clipboard plugin
        if ($('.btn-clipboard').length) {
            // Enabling tooltip to all clipboard buttons
            $('.btn-clipboard').attr('data-bs-toggle', 'tooltip').attr('title', 'Copy to clipboard');

            var clipboard = new ClipboardJS('.btn-clipboard');

            clipboard.on('success', function (e) {
                console.log(e);
                e.trigger.innerHTML = 'copied';
                setTimeout(function () {
                    e.trigger.innerHTML = 'copy';
                    e.clearSelection();
                }, 700)
            });
        }


        // initializing bootstrap tooltip
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
        var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl)
        })


        // initializing bootstrap popover
        var popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'))
        var popoverList = popoverTriggerList.map(function (popoverTriggerEl) {
            return new bootstrap.Popover(popoverTriggerEl)
        })


        // Applying perfect-scrollbar 
        if ($('.sidebar .sidebar-body').length) {
            if (typeof PerfectScrollbar !== 'undefined') {
                const sidebarBodyScroll = new PerfectScrollbar('.sidebar-body');
            } else {
                console.warn('PerfectScrollbar library not available - using native scrolling');
            }
        }


        // Close other submenu in sidebar on opening any
        sidebar.on('show.bs.collapse', '.collapse', function () {
            sidebar.find('.collapse.show').collapse('hide');
        });


        // Sidebar toggle to sidebar-folded
        $('.sidebar-toggler').on('click', function (e) {
            e.preventDefault();
            $('.sidebar-header .sidebar-toggler').toggleClass('active not-active');
            if (window.matchMedia('(min-width: 992px)').matches) {
                e.preventDefault();
                body.toggleClass('sidebar-folded');
            } else if (window.matchMedia('(max-width: 991px)').matches) {
                e.preventDefault();
                body.toggleClass('sidebar-open');
            }
        });


        // Settings sidebar toggle
        $('.settings-sidebar-toggler').on('click', function (e) {
            $('body').toggleClass('settings-open');
        });


        // Sidebar theme settings
        $("input:radio[name=sidebarThemeSettings]").click(function () {
            $('body').removeClass('sidebar-light sidebar-dark');
            $('body').addClass($(this).val());
        })


        // Add active class to nav-link based on url dynamically
        function addActiveClass(element) {
            var href = element.attr('href');
            if (!href) return;

            // Normalize the href (remove leading slash for comparison)
            var normalizedHref = href.replace(/^\//, '');

            // Get the full current path (including controller/action)
            var fullPath = window.location.pathname.replace(/^\//, '');

            // Check for exact match first
            if (normalizedHref === fullPath || href === window.location.pathname) {
                element.parents('.nav-item').last().addClass('active');
                if (element.parents('.sub-menu').length) {
                    element.closest('.collapse').addClass('show');
                    element.addClass('active');
                }
                if (element.parents('.submenu-item').length) {
                    element.addClass('active');
                }
                return;
            }

            // Special handling for root/index
            if (current === "" || current === "index.html") {
                if (href === "/" || normalizedHref === "" || normalizedHref === "index.html" || href.indexOf("Home/Index") !== -1) {
                    element.parents('.nav-item').last().addClass('active');
                    if (element.parents('.sub-menu').length) {
                        element.closest('.collapse').addClass('show');
                        element.addClass('active');
                    }
                }
            }
        }

        // Clear any existing active classes first
        $('.nav li', sidebar).removeClass('active');
        $('.nav li a', sidebar).removeClass('active');
        $('.collapse', sidebar).removeClass('show');

        var current = location.pathname.split("/").slice(-1)[0].replace(/^\/|\/$/g, '');

        // First pass: find exact matches
        var foundExactMatch = false;
        $('.nav li a', sidebar).each(function () {
            var $this = $(this);
            var href = $this.attr('href');

            if (href && (href === window.location.pathname ||
                href.replace(/^\//, '') === window.location.pathname.replace(/^\//, ''))) {
                $this.parents('.nav-item').last().addClass('active');
                if ($this.parents('.sub-menu').length) {
                    $this.closest('.collapse').addClass('show');
                    $this.addClass('active');
                }
                if ($this.parents('.submenu-item').length) {
                    $this.addClass('active');
                }
                foundExactMatch = true;
                return false; // Stop after first exact match
            }
        });

        // If no exact match, try the old logic for partial matches
        if (!foundExactMatch) {
            $('.nav li a', sidebar).each(function () {
                var $this = $(this);
                addActiveClass($this);
            });
        }

        // Handle horizontal menu
        $('.horizontal-menu .nav li a').each(function () {
            var $this = $(this);
            addActiveClass($this);
        })


        //  open sidebar-folded when hover
        $(".sidebar .sidebar-body").hover(
            function () {
                if (body.hasClass('sidebar-folded')) {
                    body.addClass("open-sidebar-folded");
                }
            },
            function () {
                if (body.hasClass('sidebar-folded')) {
                    body.removeClass("open-sidebar-folded");
                }
            });


        // close sidebar when click outside on mobile/table    
        $(document).on('click touchstart', function (e) {
            e.stopPropagation();

            // closing of sidebar menu when clicking outside of it
            if (!$(e.target).closest('.sidebar-toggler').length) {
                var sidebar = $(e.target).closest('.sidebar').length;
                var sidebarBody = $(e.target).closest('.sidebar-body').length;
                if (!sidebar && !sidebarBody) {
                    if ($('body').hasClass('sidebar-open')) {
                        $('body').removeClass('sidebar-open');
                    }
                }
            }
        });


        //Horizontal menu in mobile
        $('[data-toggle="horizontal-menu-toggle"]').on("click", function () {
            $(".horizontal-menu .bottom-navbar").toggleClass("header-toggled");
        });
        // Horizontal menu navigation in mobile menu on click
        var navItemClicked = $('.horizontal-menu .page-navigation >.nav-item');
        navItemClicked.on("click", function (event) {
            if (window.matchMedia('(max-width: 991px)').matches) {
                if (!($(this).hasClass('show-submenu'))) {
                    navItemClicked.removeClass('show-submenu');
                }
                $(this).toggleClass('show-submenu');
            }
        })

        $(window).scroll(function () {
            if (window.matchMedia('(min-width: 992px)').matches) {
                var header = $('.horizontal-menu');
                if ($(window).scrollTop() >= 60) {
                    $(header).addClass('fixed-on-scroll');
                } else {
                    $(header).removeClass('fixed-on-scroll');
                }
            }
        });


        // Prevent body scrolling while sidebar scroll
        $('.sidebar .sidebar-body').hover(function () {
            $('body').addClass('overflow-hidden');
        }, function () {
            $('body').removeClass('overflow-hidden');
        });


    });
})(jQuery);