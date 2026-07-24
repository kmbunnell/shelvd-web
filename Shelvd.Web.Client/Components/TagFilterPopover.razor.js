export function trapFocus(dialogElement) {
    function getFocusable() {
        return Array.from(dialogElement.querySelectorAll('input, button:not([disabled])'));
    }

    dialogElement.addEventListener('keydown', e => {
        if (e.key !== 'Tab') {
            return;
        }

        const focusable = getFocusable();
        if (focusable.length === 0) {
            return;
        }

        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (e.shiftKey && document.activeElement === first) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    });
}
