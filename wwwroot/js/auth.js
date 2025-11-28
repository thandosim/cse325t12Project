(function () {
    const listeners = new Set();

    function handleMessage(event) {
        const data = event.data;
        if (!data || typeof data !== 'object') {
            return;
        }

        listeners.forEach(listener => {
            try {
                listener.invokeMethodAsync('HandleExternalAuthMessage', JSON.stringify(data));
            } catch (err) {
                console.error('loadhitchAuth listener failed', err);
            }
        });
    }

    window.loadhitchAuth = {
        startGoogleSignIn(url) {
            const width = 520;
            const height = 620;
            const left = window.screenX + (window.outerWidth - width) / 2;
            const top = window.screenY + (window.outerHeight - height) / 2;
            window.open(url, 'loadhitch-google', `width=${width},height=${height},left=${left},top=${top}`);
        },
        registerHandler(dotNetHelper) {
            if (!dotNetHelper) {
                return;
            }
            if (listeners.size === 0) {
                window.addEventListener('message', handleMessage);
            }
            listeners.add(dotNetHelper);
        },
        unregisterHandler(dotNetHelper) {
            if (!dotNetHelper) {
                return;
            }
            listeners.delete(dotNetHelper);
            if (listeners.size === 0) {
                window.removeEventListener('message', handleMessage);
            }
        }
    };
})();
