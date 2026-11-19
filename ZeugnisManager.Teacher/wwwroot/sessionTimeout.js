window.zmSessionTimeout = (() => {
    const activityEvents = ['pointerdown', 'keydown', 'input', 'wheel', 'touchstart'];
    let reference = null, lastActivity = 0, timeoutMs = 0, warningMs = 0, warned = false, timer = null;

    const touch = () => {
        lastActivity = Date.now();
        if (warned && reference) { warned = false; reference.invokeMethodAsync('Activity'); }
    };

    // Compares wall-clock time so an iPad that slept with the page open still times out on wake.
    const check = () => {
        if (!reference) return;
        const idle = Date.now() - lastActivity;
        if (idle >= timeoutMs) { const expired = reference; stop(); expired.invokeMethodAsync('Expire'); }
        else if (idle >= warningMs && !warned) { warned = true; reference.invokeMethodAsync('Warn'); }
    };

    function start(dotNetReference, timeout, warning) {
        stop();
        reference = dotNetReference; timeoutMs = timeout; warningMs = warning; lastActivity = Date.now();
        activityEvents.forEach(name => document.addEventListener(name, touch, { passive: true, capture: true }));
        document.addEventListener('visibilitychange', check);
        timer = setInterval(check, 10000);
    }

    function stop() {
        activityEvents.forEach(name => document.removeEventListener(name, touch, { capture: true }));
        document.removeEventListener('visibilitychange', check);
        if (timer) clearInterval(timer);
        reference = null; warned = false; timer = null;
    }

    return { start, stop, touch };
})();
