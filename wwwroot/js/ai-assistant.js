(function () {
    const panel = document.getElementById('ai-chat-panel');
    const fab = document.getElementById('ai-chat-fab');
    const closeBtn = document.getElementById('ai-chat-close');
    const sendBtn = document.getElementById('ai-chat-send');
    const input = document.getElementById('ai-chat-input');
    const messagesEl = document.getElementById('ai-chat-messages');
    const typingEl = document.getElementById('ai-chat-typing');
    const exportPdf = document.getElementById('ai-export-pdf');
    const exportXlsx = document.getElementById('ai-export-xlsx');

    if (!fab || !panel) return;

    let conversationId = null;

    function getAuthToken() {
        // Try localStorage first
        let t = localStorage.getItem('token');
        if (t) return t;

        // Fallback to cookie
        const match = document.cookie.match(new RegExp('(^| )jwt_token=([^;]+)'));
        if (match) return decodeURIComponent(match[2]);

        return null;
    }

    async function apiFetch(url, options = {}) {
        const token = getAuthToken();
        const headers = {
            'Content-Type': 'application/json',
            ...(options.headers || {})
        };

        if (token) {
            headers['Authorization'] = 'Bearer ' + token;
        }

        const res = await fetch(url, {
            ...options,
            credentials: 'include',
            headers: headers
        });

        if (res.status === 401) {
            throw new Error('Your session has expired. Please log in again.');
        }

        const data = await res.json().catch(() => ({}));
        if (!res.ok) {
            throw new Error(data.error || res.statusText || 'Request failed');
        }

        return data;
    }

    function appendBubble(role, text) {
        const wrap = document.createElement('div');
        wrap.className = 'mb-3 ' + (role === 'user' ? 'text-end' : 'text-start');
        const bubble = document.createElement('div');
        bubble.className = 'd-inline-block px-3 py-2 rounded-3 shadow-sm ' + (role === 'user' ? 'bg-primary text-white' : 'bg-light border');
        bubble.style.maxWidth = '92%';
        bubble.style.textAlign = 'left';
        bubble.innerHTML = escapeHtml(text).replace(/\n/g, '<br/>');
        wrap.appendChild(bubble);
        messagesEl.appendChild(wrap);
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }

    function escapeHtml(s) {
        return s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function setTyping(on) {
        typingEl.classList.toggle('d-none', !on);
    }

    fab.addEventListener('click', () => panel.classList.toggle('d-none'));
    closeBtn.addEventListener('click', () => panel.classList.add('d-none'));

    sendBtn.addEventListener('click', async () => {
        const text = (input.value || '').trim();
        if (!text) return;
        input.value = '';
        appendBubble('user', text);
        setTyping(true);
        try {
            const data = await apiFetch('/api/ai/chat/message', {
                method: 'POST',
                body: JSON.stringify({ conversationId: conversationId, message: text })
            });
            conversationId = data.conversationId;
            appendBubble('assistant', data.reply || '');
        } catch (e) {
            appendBubble('assistant', '⚠️ ' + e.message);
        } finally {
            setTyping(false);
        }
    });

    input.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendBtn.click();
        }
    });

    async function exportReport(body) {
        try {
            const token = getAuthToken();
            const res = await fetch('/api/ai/reports/export', {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': token ? 'Bearer ' + token : ''
                },
                body: JSON.stringify(body)
            });

            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                alert(err.error || 'Export failed');
                return;
            }

            const blob = await res.blob();
            const cd = res.headers.get('Content-Disposition');
            let name = 'download';
            if (cd && cd.includes('filename=')) name = cd.split('filename=')[1].replace(/"/g, '').trim();
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = name;
            a.click();
            URL.revokeObjectURL(url);
        } catch (e) {
            alert('Export failed: ' + e.message);
        }
    }

    if (exportPdf) exportPdf.addEventListener('click', () => exportReport({ reportType: 'executive_summary', format: 'pdf' }));
    if (exportXlsx) exportXlsx.addEventListener('click', () => exportReport({ reportType: 'fee_defaulters', format: 'xlsx' }));
})();
