import apiRoutes from "../../js/apiRoutes.js";
import { showSuccess, showError } from "../../js/notifications.js";

(function () {
    var $ = window.jQuery;
    var apiClient = window.apiClient;

    var config = { productId: 0, urls: {} };
    var urls = {};
    var productId = 0;

    function refreshConfig() {
        config = window.productManagementDetailConfig || { productId: 0, urls: {} };
        urls = config.urls || {};
        productId = config.productId;
    }

    document.addEventListener(
        'click',
        function (e) {
            if (!document.getElementById('detailName')) return;
            var path = typeof e.composedPath === 'function' ? e.composedPath() : [];
            for (var i = 0; i < path.length; i++) {
                var el = path[i];
                if (!el || !el.id) continue;
                if (el.id === 'btnBack') {
                    e.preventDefault();
                    if (window.spaNavigate) { window.spaNavigate(urls.index || '/ProductManagement'); }
                    else { window.location.href = urls.index || '/ProductManagement'; }
                    return;
                }
            }
        },
        true
    );

    function hideConfirmationModal() {
        var modalEl = document.getElementById('confirmation-modal');
        if (!modalEl || typeof bootstrap === 'undefined' || !bootstrap.Modal) return;
        var modal = bootstrap.Modal.getInstance(modalEl);
        if (!modal) return;
        var focusTarget = document.getElementById('btnDelete');
        if (focusTarget && focusTarget.focus) { focusTarget.focus(); }
        else { document.body.setAttribute('tabindex', '-1'); document.body.focus(); }
        modal.hide();
    }

    function loadProductDetail() {
        if (!productId) {
            document.getElementById('detailLoading').style.display = 'none';
            document.getElementById('detailError').style.display = 'block';
            document.getElementById('detailError').textContent = 'Invalid product ID.';
            return;
        }

        var detailUrl = apiRoutes.products.show.replace('{id}', productId);

        apiClient.get(detailUrl).then(function (res) {
            var $loading = $('#detailLoading');
            var $content = $('#detailContent');
            var $error = $('#detailError');

            $loading.hide();

            if (res.ok && res.data && res.data.data) {
                var p = res.data.data;
                $('#detailName').text(p.name || '-');
                $('#detailSku').text(p.sku || '-');
                $('#detailPrice').text(p.price != null ? Number(p.price).toFixed(2) : '-');
                $('#detailStatus').text(p.status || '-');
                $('#detailDescription').text(p.description || '-');

                $('#btnEdit').attr('href', urls.edit || ('/ProductManagement/Edit/' + productId));
                $content.show();
            } else {
                $error.show();
                var msg = (res.data && (res.data.message || res.data.title)) || 'Failed to load product.';
                showError(msg);
            }
        }).catch(function () {
            $('#detailLoading').hide();
            $('#detailError').show();
            showError('An error occurred. Please try again.');
        });
    }

    function deleteProduct() {
        var deleteUrl = apiRoutes.products.delete.replace('{id}', productId);
        apiClient.delete(deleteUrl).then(function (res) {
            hideConfirmationModal();

            if (res.ok) {
                showSuccess('Product deleted successfully.').then(function () {
                    if (window.spaNavigate) { window.spaNavigate(urls.index || '/ProductManagement'); }
                    else { window.location.href = urls.index || '/ProductManagement'; }
                });
            } else {
                var msg = (res.data && (res.data.message || res.data.title)) || 'Failed to delete product.';
                showError(msg);
            }
        }).catch(function () {
            hideConfirmationModal();
            showError('An error occurred. Please try again.');
        });
    }

    function initDetailPage() {
        if (!document.getElementById('detailName')) return;
        var detailLoading = document.getElementById('detailLoading');
        if (!detailLoading) return;
        refreshConfig();
        var pageMarker = document.getElementById('detailContent') || detailLoading;
        var initKey = String(productId || '');
        if (pageMarker.getAttribute('data-spa-init-key') === initKey) return;
        pageMarker.setAttribute('data-spa-init-key', initKey);

        loadProductDetail();

        $('#btnConfirmDelete').on('click', function () {
            deleteProduct();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initDetailPage);
    } else {
        initDetailPage();
    }
    if (window.__productDetailMountedHandler) {
        window.removeEventListener('app:main-mounted', window.__productDetailMountedHandler);
    }
    window.__productDetailMountedHandler = initDetailPage;
    window.addEventListener('app:main-mounted', window.__productDetailMountedHandler);
})();
