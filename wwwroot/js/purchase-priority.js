document.addEventListener('DOMContentLoaded', function () {
    const copyBtn = document.getElementById('copyMassEntryBtn');
    const textarea = document.getElementById('massEntryText');
    if (!copyBtn || !textarea) return;

    copyBtn.addEventListener('click', async function () {
        try {
            await navigator.clipboard.writeText(textarea.value);
            const originalText = copyBtn.textContent;
            copyBtn.textContent = 'Copied!';
            setTimeout(function () {
                copyBtn.textContent = originalText;
            }, 1500);
        } catch (err) {
            textarea.select();
            document.execCommand('copy');
        }
    });
});
