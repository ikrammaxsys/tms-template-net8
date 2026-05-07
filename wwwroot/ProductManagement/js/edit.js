import apiRoutes from "../../js/apiRoutes.js";
import { showSuccess, showError } from "../../js/notifications.js";

(function () {
    var $ = window.jQuery;
    var apiClient = window.apiClient;

    var requiredFields = [
        { id: 'Name', label: 'Product Name', type: 'input' },
        { id: 'Sku', label: 'SKU', type: 'input' },
        { id: 'Price', label: 'Price', type: 'input' }
    ];

    var config = { productId: 0, urls: {} };
    var urls = {};
    var routeProductId = 0;

    function refreshConfig() {
        config = window.productManagementEditConfig || { productId: 0, urls: {} };
        urls = config.urls || {};
        routeProductId = config.productId;
    }

    function setUiTextInputValue(id, value) {
        var el = document.getElementById(id);
        if (!el) return;
        var str = value == null ? '' : String(value);
        el.setAttribute('value', str);
        try { el.value = str; } catch (e) { /* ignore */ }
        var root = el.shadowRoot;
        if (root) {
            var inp = root.querySelector('input');
            if (inp) {
                inp.value = str;
                inp.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
                inp.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
            }
        }
        el.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
        el.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    }

    function getUiTextInputValue(id) {
        var el = document.getElementById(id);
        if (!el) return '';
        if (el.shadowRoot) {
            var inp = el.shadowRoot.querySelector('input');
            if (inp) return inp.value == null ? '' : String(inp.value);
        }
        if (typeof el.value !== 'undefined' && el.value !== null) return String(el.value);
        var jq = $('#' + id);
        return jq.length ? jq.val() || '' : '';
    }

    function getUiDropdownValue(id) {
        var el = document.getElementById(id);
        if (!el) return '';
        if (typeof el.value !== 'undefined' && el.value !== null && String(el.value).trim() !== '') {
            return String(el.value);
        }
        if (el.shadowRoot) {
            var sel = el.shadowRoot.querySelector('select');
            if (sel && sel.value) return String(sel.value);
            var hid = el.shadowRoot.querySelector('input[type="hidden"]');
            if (hid) return hid.value == null ? '' : String(hid.value);
        }
        var a = el.getAttribute('value');
        return a == null ? '' : String(a);
    }

    function setUiDropdownValue(id, value) {
        var el = document.getElementById(id);
        if (!el) return;
        var str = value == null ? '' : String(value);
        function apply() {
            el.setAttribute('value', str);
            try { el.value = str; } catch (e) { /* ignore */ }
            if (el.shadowRoot) {
                var sel = el.shadowRoot.querySelector('select');
                if (sel) {
                    sel.value = str;
                    sel.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
                    sel.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
                }
                var hid = el.shadowRoot.querySelector('input[type="hidden"]');
                if (hid) {
                    hid.value = str;
                    hid.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
                    hid.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
                }
            }
            el.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
            el.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
        }
        apply();
        [50, 150, 400, 800].forEach(function (ms) { setTimeout(apply, ms); });
    }

    function normalizeStatusForSelect(raw) {
        var s = (raw == null ? '' : String(raw)).trim().toLowerCase();
        if (s === 'inactive') return 'Inactive';
        return 'Active';
    }

    function getStatusValue() {
        var raw = getUiDropdownValue('Status');
        var t = raw == null ? '' : String(raw).trim();
        if (t === '') return '';
        return normalizeStatusForSelect(t);
    }

    function setStatusDropdownValue(raw) {
        setUiDropdownValue('Status', normalizeStatusForSelect(raw));
    }

    function clearFieldError(field) {
        $('#' + field.id).css('border', '');
        $('#required-field-error-' + field.id).remove();
    }

    function clearDropdownError(id) {
        $('#required-field-error-' + id).remove();
        $('#' + id).css('border', '');
    }

    function loadProductAndShowForm() {
        if (!apiClient) {
            $('#editLoading').hide();
            $('#editError').show().text('API client failed to load.');
            return;
        }

        if (!routeProductId) {
            $('#editLoading').hide();
            $('#editError').show().text('Invalid product ID.');
            return;
        }

        var detailUrl = apiRoutes.products.show.replace('{id}', routeProductId);
        apiClient.get(detailUrl).then(function (res) {
            $('#editLoading').hide();

            if (res.ok && res.data && res.data.data) {
                var p = res.data.data;
                $('#productEditForm').show();

                function applyLoadedValues() {
                    setUiTextInputValue('Name', p.name || '');
                    setUiTextInputValue('Sku', p.sku || '');
                    setUiTextInputValue('Price', p.price != null ? String(p.price) : '');
                    setUiTextInputValue('Description', p.description || '');
                    setStatusDropdownValue(p.status);
                }

                requestAnimationFrame(function () { requestAnimationFrame(applyLoadedValues); });
                setTimeout(applyLoadedValues, 120);
            } else {
                $('#editError').show();
                var msg = (res.data && (res.data.message || res.data.title)) || 'Failed to load product.';
                showError(msg);
            }
        }).catch(function () {
            $('#editLoading').hide();
            $('#editError').show();
            showError('An error occurred. Please try again.');
        });
    }

    function submitUpdate() {
        var name = getUiTextInputValue('Name');
        var sku = getUiTextInputValue('Sku');
        var price = getUiTextInputValue('Price');
        var status = getStatusValue();
        var description = getUiTextInputValue('Description');

        requiredFields.forEach(function (field) { clearFieldError(field); });
        clearDropdownError('Status');

        var missing = false;
        requiredFields.forEach(function (field) {
            var value = getUiTextInputValue(field.id);
            if (!value) {
                var $el = $('#' + field.id);
                $el.css('border', '1px solid red');
                missing = true;
                $el.after(
                    '<small class="text-danger" id="required-field-error-' +
                        field.id + '"> * ' + field.label + ' is required</small>'
                );
            }
        });
        if (!status) {
            missing = true;
            var $st = $('#Status');
            if ($st.length) {
                $st.css('border', '1px solid red');
                $st.after(
                    '<small class="text-danger d-block mt-1" id="required-field-error-Status"> * Status is required</small>'
                );
            }
        }

        if (missing) {
            showError('Please fill in all required fields.');
            return;
        }

        var payload = {
            name: name,
            sku: sku,
            price: Number(price),
            status: status,
            description: description
        };

        var updateUrl = apiRoutes.products.update;
        apiClient.put(updateUrl, payload, { id: routeProductId }).then(function (res) {
            if (res.ok) {
                showSuccess('Product updated successfully.').then(function () {
                    if (window.spaNavigate) { window.spaNavigate(urls.index || '/ProductManagement'); }
                    else { window.location.href = urls.index || '/ProductManagement'; }
                });
            } else {
                var msg = (res.data && (res.data.message || res.data.title)) || 'An error occurred. Please try again.';
                showError(msg);
            }
        }).catch(function () {
            showError('An error occurred. Please try again.');
        });
    }

    function onProductEditDocumentClick(e) {
        if (!document.getElementById('productEditForm')) return;
        var path = typeof e.composedPath === 'function' ? e.composedPath() : [];
        for (var i = 0; i < path.length; i++) {
            var el = path[i];
            if (el && el.id === 'btnCancel') {
                e.preventDefault();
                var dest = urls.detail || '/ProductManagement/Detail/' + routeProductId;
                if (window.spaNavigate) { window.spaNavigate(dest); }
                else { window.location.href = dest; }
                return;
            }
        }
    }

    function initEditPage() {
        if (!document.getElementById('productEditForm')) return;
        var editLoading = document.getElementById('editLoading');
        if (!editLoading) return;
        refreshConfig();
        var pageMarker = document.getElementById('editContent') || editLoading;
        var initKey = String(routeProductId || '');
        if (pageMarker.getAttribute('data-spa-init-key') === initKey) return;
        pageMarker.setAttribute('data-spa-init-key', initKey);
        loadProductAndShowForm();

        if (window.__productEditDocumentClickHandler) {
            document.removeEventListener('click', window.__productEditDocumentClickHandler, true);
        }
        window.__productEditDocumentClickHandler = onProductEditDocumentClick;
        document.addEventListener('click', window.__productEditDocumentClickHandler, true);

        $('#btnSubmit').on('click', function () { this.blur(); });

        $('#btnConfirmUpdate').on('click', function () {
            submitUpdate();
        });

        $('#productEditForm').on('submit', function (e) {
            e.preventDefault();
            submitUpdate();
        });

        requiredFields.forEach(function (field) {
            var $el = $('#' + field.id);
            if ($el.length) {
                $el.on('change input', function () {
                    $(this).removeClass('is-invalid');
                    $(this).css({ 'border-color': '', 'background-color': '' });
                    $('#required-field-error-' + field.id).remove();
                });
            }
        });

        var statusEl = document.getElementById('Status');
        if (statusEl) {
            statusEl.addEventListener('change', function () { clearDropdownError('Status'); });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initEditPage);
    } else {
        initEditPage();
    }
    if (window.__productEditMountedHandler) {
        window.removeEventListener('app:main-mounted', window.__productEditMountedHandler);
    }
    window.__productEditMountedHandler = initEditPage;
    window.addEventListener('app:main-mounted', window.__productEditMountedHandler);
})();
