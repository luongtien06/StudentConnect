/**
 * TDMUEcoSystem - Standardized Notification & Dialog System (AppDialog)
 * Chuẩn hóa hệ thống thông báo / hộp thoại toàn hệ thống (ASP.NET MVC 5)
 */
(function (window, $) {
    'use strict';

    var AppDialog = {
        /**
         * Kiểm tra trạng thái đăng nhập phía Client
         */
        isLoggedIn: function () {
            if (typeof window.isUserLoggedIn !== 'undefined') {
                return Boolean(window.isUserLoggedIn);
            }
            return false;
        },

        /**
         * Tạo URL quay lại an toàn (chỉ chấp nhận internal local url)
         */
        getSafeReturnUrl: function () {
            try {
                var path = window.location.pathname || '/';
                var search = window.location.search || '';
                var full = path + search;
                if (full.indexOf('//') !== 0 && full.indexOf('http') !== 0 && full.indexOf('\\') !== 0) {
                    return encodeURIComponent(full);
                }
            } catch (e) { }
            return encodeURIComponent('/');
        },

        /**
         * 1. HỘP THOẠI YÊU CẦU ĐĂNG NHẬP
         * @param {Object} options { title, message, returnUrl, onCancel }
         */
        requireLogin: function (options) {
            options = options || {};
            var title = options.title || 'Bạn cần đăng nhập';
            var message = options.message || 'Vui lòng đăng nhập để sử dụng chức năng này.';
            var safeReturnUrl = options.returnUrl || this.getSafeReturnUrl();

            // Nếu đang có một modal requireLogin đang mở, tránh chồng lặp
            if (typeof Swal !== 'undefined' && Swal.isVisible() && Swal.getPopup() && Swal.getPopup().classList.contains('swal2-require-login-popup')) {
                return Promise.resolve({ isConfirmed: false, isDismissed: true });
            }

            if (typeof Swal === 'undefined') {
                if (confirm(title + '\n\n' + message + '\n\nBấm OK để đăng nhập ngay.')) {
                    window.location.href = '/User/Login?returnUrl=' + safeReturnUrl;
                }
                return Promise.resolve();
            }

            return Swal.fire({
                title: title,
                text: message,
                icon: 'info',
                showCancelButton: true,
                confirmButtonText: '<i class="fa-solid fa-arrow-right-to-bracket me-1"></i> Đăng nhập ngay',
                cancelButtonText: 'Để sau',
                reverseButtons: true,
                focusConfirm: true,
                allowOutsideClick: true,
                allowEscapeKey: true,
                customClass: {
                    popup: 'swal2-popup swal2-require-login-popup',
                    confirmButton: 'swal2-confirm btn-sc-login-confirm',
                    cancelButton: 'swal2-cancel btn-sc-login-cancel'
                }
            }).then(function (result) {
                if (result.isConfirmed) {
                    window.location.href = '/User/Login?returnUrl=' + safeReturnUrl;
                } else if (typeof options.onCancel === 'function') {
                    options.onCancel();
                }
                return result;
            });
        },

        /**
         * Kiểm tra đăng nhập trước khi thực thi hành động.
         * Nếu chưa đăng nhập: Tự động mở hộp thoại yêu cầu đăng nhập và trả về false.
         */
        ensureLoggedIn: function (message, title) {
            if (!this.isLoggedIn()) {
                this.requireLogin({ message: message, title: title });
                return false;
            }
            return true;
        },

        /**
         * 2. TOAST THÔNG BÁO GỌN NHẸ (Góc trên màn hình, không chặn tương tác)
         */
        toastSuccess: function (message, title) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'success',
                title: title ? '<b>' + title + '</b> ' + message : message,
                showConfirmButton: false,
                timer: 2200,
                timerProgressBar: true
            });
        },

        toastError: function (message, title) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'error',
                title: title ? '<b>' + title + '</b> ' + message : message,
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true
            });
        },

        toastWarning: function (message, title) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'warning',
                title: title ? '<b>' + title + '</b> ' + message : message,
                showConfirmButton: false,
                timer: 2500,
                timerProgressBar: true
            });
        },

        toastInfo: function (message, title) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'info',
                title: title ? '<b>' + title + '</b> ' + message : message,
                showConfirmButton: false,
                timer: 2200,
                timerProgressBar: true
            });
        },

        /**
         * 3. HỘP THOẠI CHI TIẾT (Modal trung tâm)
         */
        success: function (message, title) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                icon: 'success',
                title: title || 'Thành công',
                text: message,
                confirmButtonText: 'Đóng'
            });
        },

        error: function (message, title) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                icon: 'error',
                title: title || 'Đã xảy ra lỗi',
                text: message || 'Không thể hoàn tất thao tác. Vui lòng thử lại sau.',
                confirmButtonText: 'Đã hiểu'
            });
        },

        warning: function (message, title) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                icon: 'warning',
                title: title || 'Chú ý',
                text: message,
                confirmButtonText: 'Đã hiểu'
            });
        },

        info: function (message, title) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                icon: 'info',
                title: title || 'Thông báo',
                text: message,
                confirmButtonText: 'Đóng'
            });
        },

        /**
         * 4. HỘP THOẠI XÁC NHẬN (CONFIRM)
         * Trả về Promise resolving boolean (true: Đồng ý, false: Hủy)
         * @param {Object} options { title, message, confirmText, cancelText, isDanger }
         */
        confirm: function (options) {
            options = options || {};
            var isDanger = options.isDanger !== false;
            if (typeof Swal === 'undefined') {
                return Promise.resolve(window.confirm(options.title + (options.message ? '\n' + options.message : '')));
            }

            return Swal.fire({
                title: options.title || 'Bạn có chắc chắn?',
                text: options.message || '',
                icon: options.icon || (isDanger ? 'warning' : 'question'),
                showCancelButton: true,
                confirmButtonText: options.confirmText || (isDanger ? 'Xác nhận' : 'Đồng ý'),
                cancelButtonText: options.cancelText || 'Hủy bỏ',
                reverseButtons: true,
                focusCancel: isDanger,
                confirmButtonColor: isDanger ? '#ef4444' : '#00c8ff',
                cancelButtonColor: 'rgba(255,255,255,0.08)'
            }).then(function (result) {
                return Boolean(result.isConfirmed);
            });
        },

        /**
         * 5. TRẠNG THÁI ĐANG XỬ LÝ (LOADING)
         */
        loading: function (message) {
            if (typeof Swal === 'undefined') return;
            return Swal.fire({
                title: message || 'Đang xử lý...',
                allowOutsideClick: false,
                allowEscapeKey: false,
                showConfirmButton: false,
                didOpen: function () {
                    Swal.showLoading();
                }
            });
        },

        closeLoading: function () {
            if (typeof Swal !== 'undefined' && Swal.isVisible() && Swal.isLoading()) {
                Swal.close();
            }
        },

        /**
         * 6. XỬ LÝ LỖI AJAX CHUẨN HÓA
         */
        handleAjaxError: function (xhr, defaultMsg) {
            this.closeLoading();
            if (!xhr) {
                this.error(defaultMsg || 'Không thể kết nối đến máy chủ.');
                return;
            }
            if (xhr.status === 401) {
                this.requireLogin({ message: 'Phiên làm việc đã hết hạn hoặc bạn chưa đăng nhập. Vui lòng đăng nhập lại.' });
            } else if (xhr.status === 403) {
                this.warning('Bạn không có quyền thực hiện thao tác này.');
            } else if (xhr.status === 0) {
                this.toastError('Mất kết nối mạng. Vui lòng kiểm tra đường truyền.');
            } else {
                var serverMsg = defaultMsg;
                try {
                    var json = JSON.parse(xhr.responseText);
                    if (json && json.message) serverMsg = json.message;
                } catch (e) { }
                this.error(serverMsg || 'Có lỗi xảy ra trong quá trình xử lý.');
            }
        }
    };

    // Đăng ký toàn cục
    window.AppDialog = AppDialog;

    // Thay thế an toàn cho alert() mặc định của trình duyệt
    window.alert = function (msg) {
        if (!msg) return;
        var str = String(msg);
        var lower = str.toLowerCase();
        if (lower.indexOf('vui lòng đăng nhập') !== -1 || lower.indexOf('yêu cầu đăng nhập') !== -1 || lower.indexOf('bạn cần đăng nhập') !== -1) {
            AppDialog.requireLogin({ message: str });
        } else {
            AppDialog.info(str);
        }
    };

    // Global jQuery AJAX Interceptor tự động bắt requireLogin và HTTP 401
    if ($) {
        $(document).ajaxSuccess(function (event, xhr, settings, data) {
            if (data && typeof data === 'object') {
                if (data.requireLogin === true) {
                    AppDialog.requireLogin({ message: data.message });
                }
            }
        });

        $(document).ajaxError(function (event, xhr, settings, thrownError) {
            if (settings && settings.ignoreGlobalError) return;
            if (xhr && xhr.status === 401) {
                AppDialog.requireLogin({ message: 'Phiên làm việc đã hết hạn hoặc bạn chưa đăng nhập. Vui lòng đăng nhập lại.' });
            }
        });
    }

})(window, window.jQuery);
