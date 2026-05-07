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

    var config = window.productManagementCreateConfig || { urls: {} };
    var urls = config.urls || {};

    document.addEventListener(
        'click',
        function (e) {
            if (!document.getElementById('productCreateForm')) return;
            var path = typeof e.composedPath === 'function' ? e.composedPath() : [];
            for (var i = 0; i < path.length; i++) {
                var el = path[i];
                if (el && el.id === 'btnCancel') {
                    e.preventDefault();
                    if (window.spaNavigate) { window.spaNavigate(urls.index || '/ProductManagement'); }
                    else { window.location.href = urls.index || '/ProductManagement'; }
                    return;
                }
            }
        },
        true
    );

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

    function normalizeStatusForSubmit(raw) {
        var t = raw == null ? '' : String(raw).trim();
        if (t === '') return '';
        if (t.toLowerCase() === 'inactive') return 'Inactive';
        return 'Active';
    }

    function getStatusValue() {
        return normalizeStatusForSubmit(getUiDropdownValue('Status'));
    }

    function clearFieldError(field) {
        $('#required-field-error-' + field.id).remove();
        $('#' + field.id).css('border', '');
    }

    function clearDropdownError(id) {
        $('#required-field-error-' + id).remove();
        $('#' + id).css('border', '');
    }

    function submitProduct() {
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

        apiClient.post(apiRoutes.products.store, payload).then(function (res) {
            if (res.ok) {
                showSuccess('Product created successfully.').then(function () {
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

    $(document).ready(function () {
        $('#btnSubmit').on('click', function () { this.blur(); });

        $('#btnConfirm').on('click', function () {
            submitProduct();
        });

        $('#productCreateForm').on('submit', function (e) {
            e.preventDefault();
            submitProduct();
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
            statusEl.addEventListener('change', function () {
                clearDropdownError('Status');
            });
        }
    });
})();
