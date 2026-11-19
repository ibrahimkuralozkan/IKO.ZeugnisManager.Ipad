window.browserPackageCrypto = (() => {
    const format = "IKO.ZeugnisManager.ZMPKG";
    const version = 1;
    const iterations = 310000;
    const encoder = new TextEncoder();
    const decoder = new TextDecoder();
    const additionalData = encoder.encode(`${format}|${version}`);

    function fromBase64(value) {
        const binary = atob(value);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        return bytes;
    }

    function toBase64(bytes) {
        let binary = "";
        const chunkSize = 0x8000;
        for (let i = 0; i < bytes.length; i += chunkSize)
            binary += String.fromCharCode(...bytes.subarray(i, i + chunkSize));
        return btoa(binary);
    }

    async function deriveKey(password, salt, count) {
        const material = await crypto.subtle.importKey(
            "raw", encoder.encode(password), "PBKDF2", false, ["deriveKey"]);
        return crypto.subtle.deriveKey(
            { name: "PBKDF2", salt, iterations: count, hash: "SHA-256" },
            material,
            { name: "AES-GCM", length: 256 },
            false,
            ["encrypt", "decrypt"]);
    }

    async function decrypt(packageBase64, password) {
        const envelope = JSON.parse(decoder.decode(fromBase64(packageBase64)));
        if (envelope.Format !== format || envelope.Version !== version)
            throw new Error("Unsupported package format");
        if (envelope.Iterations < 100000 || envelope.Iterations > 2000000)
            throw new Error("Invalid KDF iterations");

        const salt = fromBase64(envelope.Salt);
        const nonce = fromBase64(envelope.Nonce);
        const tag = fromBase64(envelope.Tag);
        const ciphertext = fromBase64(envelope.Ciphertext);
        if (salt.length !== 16 || nonce.length !== 12 || tag.length !== 16)
            throw new Error("Invalid security parameters");

        const encrypted = new Uint8Array(ciphertext.length + tag.length);
        encrypted.set(ciphertext, 0);
        encrypted.set(tag, ciphertext.length);
        const key = await deriveKey(password, salt, envelope.Iterations);
        const plaintext = await crypto.subtle.decrypt(
            { name: "AES-GCM", iv: nonce, additionalData, tagLength: 128 }, key, encrypted);
        return decoder.decode(plaintext);
    }

    async function encrypt(plaintextJson, password) {
        const salt = crypto.getRandomValues(new Uint8Array(16));
        const nonce = crypto.getRandomValues(new Uint8Array(12));
        const key = await deriveKey(password, salt, iterations);
        const combined = new Uint8Array(await crypto.subtle.encrypt(
            { name: "AES-GCM", iv: nonce, additionalData, tagLength: 128 }, key, encoder.encode(plaintextJson)));
        const ciphertext = combined.subarray(0, combined.length - 16);
        const tag = combined.subarray(combined.length - 16);
        const envelope = {
            Format: format, Version: version, Kdf: "PBKDF2-SHA256", Iterations: iterations,
            Salt: toBase64(salt), Nonce: toBase64(nonce), Tag: toBase64(tag), Ciphertext: toBase64(ciphertext)
        };
        return toBase64(encoder.encode(JSON.stringify(envelope)));
    }

    return { encrypt, decrypt };
})();
