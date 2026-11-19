// Registers the service worker and lets the user decide when a newly published
// version is activated. A new worker stays in the "waiting" state until the user
// confirms, so an open package is never closed by an unexpected reload.
window.zmPwaUpdate = (() => {
    const checkIntervalMs = 30 * 60 * 1000;
    let registration = null, reference = null, available = false, reloading = false;

    const notify = () => {
        if (available && reference) reference.invokeMethodAsync('UpdateAvailable');
    };

    const markAvailable = () => { available = true; notify(); };

    const check = () => {
        if (registration && navigator.onLine !== false) registration.update().catch(() => { });
    };

    function track(reg) {
        registration = reg;
        // Without a controller this is the very first install, not an update.
        if (reg.waiting && navigator.serviceWorker.controller) markAvailable();
        reg.addEventListener('updatefound', () => {
            const worker = reg.installing;
            if (!worker) return;
            worker.addEventListener('statechange', () => {
                if (worker.state === 'installed' && navigator.serviceWorker.controller) markAvailable();
            });
        });
        setInterval(check, checkIntervalMs);
        document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') check(); });
    }

    if ('serviceWorker' in navigator) {
        // A new worker asks open pages whether they can show the update prompt.
        // Pages of older versions do not answer, and the worker then activates on its own.
        navigator.serviceWorker.addEventListener('message', event => {
            if (event.data && event.data.type === 'ZM_UPDATE_PING' && event.ports[0]) {
                event.ports[0].postMessage({ type: 'ZM_UPDATE_PONG' });
            }
        });
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (reloading) window.location.reload();
        });
        navigator.serviceWorker.register('service-worker.js').then(track).catch(error => console.warn('Service worker registration failed', error));
    }

    function connect(dotNetReference) { reference = dotNetReference; notify(); }
    function disconnect() { reference = null; }

    function isFileOpen() {
        return !!(window.zmSessionTimeout && window.zmSessionTimeout.isActive());
    }

    function apply() {
        reloading = true;
        const waiting = registration && registration.waiting;
        // Another window may already have activated the new version.
        if (waiting) waiting.postMessage({ type: 'SKIP_WAITING' });
        else window.location.reload();
    }

    return { connect, disconnect, isFileOpen, apply };
})();
