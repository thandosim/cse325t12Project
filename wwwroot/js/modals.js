window.modalUtils = {
    trapFocus(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return () => {};

        const focusableSelectors = [
            'a[href]', 'area[href]', 'input:not([disabled])', 'select:not([disabled])',
            'textarea:not([disabled])', 'button:not([disabled])', 'iframe', 'object', 'embed',
            '[tabindex]:not([tabindex="-1"])', '[contenteditable=true]'
        ];

        const getFocusable = () => Array.from(modal.querySelectorAll(focusableSelectors.join(',')))
            .filter(el => el.offsetParent !== null);

        const focusFirst = () => {
            const items = getFocusable();
            if (items.length > 0) {
                items[0].focus();
            }
        };

        const handler = (e) => {
            if (e.key === 'Escape') {
                modal.dispatchEvent(new CustomEvent('modalEscape', { bubbles: true }));
            }
            if (e.key !== 'Tab') return;

            const focusable = getFocusable();
            if (focusable.length === 0) {
                e.preventDefault();
                return;
            }

            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            const active = document.activeElement;

            if (e.shiftKey) {
                if (active === first) {
                    e.preventDefault();
                    last.focus();
                }
            } else {
                if (active === last) {
                    e.preventDefault();
                    first.focus();
                }
            }
        };

        document.addEventListener('keydown', handler);
        setTimeout(focusFirst, 0);

        return () => document.removeEventListener('keydown', handler);
    }
};
