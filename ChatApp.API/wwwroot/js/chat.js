const token = localStorage.getItem('jwt_token');
const currentUsername = localStorage.getItem('chat_username');
const currentUserEmail = localStorage.getItem('chat_email');
// Assuming we store currentUserId on login (will parse from JWT if not available)
let currentUserId = parseJwt(token)?.sub; 

if (!token || !currentUsername) {
  window.location.href = '/index.html';
}

function parseJwt(token) {
    if (!token) return null;
    try {
        const base64Url = token.split('.')[1];
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(atob(base64).split('').map(function(c) {
            return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
        }).join(''));
        return JSON.parse(jsonPayload);
    } catch (e) {
        return null;
    }
}

// ─── DOM References ───────────────────────────────────────────────────────────
const statusDot       = document.getElementById('status-dot');
const statusText      = document.getElementById('status-text');
const currentUserEl   = document.getElementById('current-user');
const messagesEl      = document.getElementById('messages-container');
const messageInput    = document.getElementById('message-input');
const sendBtn         = document.getElementById('send-btn');
const messageForm     = document.getElementById('message-form');
const logoutBtn       = document.getElementById('logout-btn');

const searchInput     = document.getElementById('search-input');
const searchBtn       = document.getElementById('search-btn');
const searchResults   = document.getElementById('search-results');
const recentChatsList = document.getElementById('recent-chats-list');

const chatPlaceholder = document.getElementById('chat-placeholder');
const activeChat      = document.getElementById('active-chat');
const activeUserName  = document.getElementById('active-user-name');
const activeUserStatus= document.getElementById('active-user-status');
const typingIndicator = document.getElementById('typing-indicator');

let connection = null;
let activeChatId = null;
let activeOtherUser = null;
let typingTimeout = null;
let recentChats = [];

// Init UI
currentUserEl.textContent = `👤 ${currentUsername}`;

// ─── API Calls ────────────────────────────────────────────────────────────────

async function fetchRecentChats() {
    try {
        const res = await fetch('/api/chats', {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) {
            recentChats = await res.json();
            renderRecentChats();
        }
    } catch (e) { console.error('Failed to fetch chats', e); }
}

async function fetchChatMessages(chatId) {
    try {
        const res = await fetch(`/api/chats/${chatId}/messages`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) {
            const messages = await res.json();
            renderMessages(messages);
            if (connection) {
                connection.invoke('MarkAsRead', chatId);
            }
        }
    } catch (e) { console.error('Failed to fetch messages', e); }
}

async function searchUsers(email) {
    try {
        const res = await fetch(`/api/users/search?email=${encodeURIComponent(email)}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) {
            const users = await res.json();
            renderSearchResults(users);
        }
    } catch (e) { console.error('Search failed', e); }
}

async function getOrCreateChat(userId, userObj) {
    try {
        const res = await fetch(`/api/chats/with/${userId}`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) {
            const data = await res.json();
            openChat(data.chatId, userObj);
            fetchRecentChats(); // refresh list
        }
    } catch (e) { console.error('Failed to create chat', e); }
}

// ─── UI Rendering ─────────────────────────────────────────────────────────────

function renderRecentChats() {
    recentChatsList.innerHTML = '';
    if (recentChats.length === 0) {
        recentChatsList.innerHTML = '<li class="chat-item"><div class="chat-item-name">No recent chats</div></li>';
        return;
    }
    
    recentChats.forEach(chat => {
        const li = document.createElement('li');
        li.className = `chat-item ${activeChatId === chat.id ? 'active' : ''}`;
        li.onclick = () => openChat(chat.id, chat.otherUser);
        
        const lastMsgText = chat.lastMessage ? chat.lastMessage.content : 'No messages yet';
        const timeText = chat.lastMessage ? formatTime(chat.lastMessage.sentAt) : '';
        const unreadBadge = chat.unreadCount > 0 ? `<span class="chat-item-unread">${chat.unreadCount}</span>` : '';
        const onlineDot = chat.otherUser.isOnline ? '🟢 ' : '';

        li.innerHTML = `
            <div class="chat-item-header">
                <span class="chat-item-name">${onlineDot}${escapeHtml(chat.otherUser.username)}</span>
                <span class="chat-item-time">${timeText}</span>
            </div>
            <div class="chat-item-body">
                <span class="chat-item-last-msg">${escapeHtml(lastMsgText)}</span>
                ${unreadBadge}
            </div>
        `;
        recentChatsList.appendChild(li);
    });
}

function renderSearchResults(users) {
    searchResults.innerHTML = '';
    searchResults.classList.remove('hidden');
    
    if (users.length === 0) {
        searchResults.innerHTML = '<div style="padding: 10px; color: var(--text-muted)">No users found.</div>';
        return;
    }

    users.forEach(u => {
        const div = document.createElement('div');
        div.className = 'search-result-item';
        div.innerHTML = `
            <div>
                <strong>${escapeHtml(u.username)}</strong><br>
                <small>${escapeHtml(u.email)}</small>
            </div>
            <button class="btn-primary" style="padding: 4px 8px; width: auto; margin:0;" onclick='getOrCreateChat(${u.id}, ${JSON.stringify(u)})'>Chat</button>
        `;
        searchResults.appendChild(div);
    });
}

function openChat(chatId, otherUser) {
    activeChatId = chatId;
    activeOtherUser = otherUser;
    
    chatPlaceholder.classList.add('hidden');
    activeChat.classList.remove('hidden');
    searchResults.classList.add('hidden');
    searchInput.value = '';
    
    activeUserName.textContent = otherUser.username;
    updateOnlineStatusUI(otherUser.isOnline);
    
    messageInput.disabled = false;
    sendBtn.disabled = false;
    
    renderRecentChats(); // update active state
    fetchChatMessages(chatId);
}

function updateOnlineStatusUI(isOnline) {
    if (!activeOtherUser) return;
    activeOtherUser.isOnline = isOnline;
    activeUserStatus.textContent = isOnline ? 'Online' : 'Offline';
    activeUserStatus.className = `active-user-status ${isOnline ? 'online' : ''}`;
}

function renderMessages(messages) {
    messagesEl.innerHTML = '';
    messages.forEach(m => {
        appendMessage(m);
    });
    scrollToBottom();
}

function appendMessage(msg) {
    const isSelf = String(msg.senderId) === String(currentUserId);
    const time = formatTime(msg.sentAt);
    
    const row = document.createElement('div');
    row.className = `msg-row${isSelf ? ' self' : ''}`;
    row.dataset.msgId = msg.id;

    let statusHtml = '';
    if (isSelf) {
        statusHtml = `<span class="msg-status ${msg.isRead ? 'seen' : ''}">${msg.isRead ? '✓✓' : '✓'}</span>`;
    }

    row.innerHTML = `
        <div class="msg-bubble">
            ${escapeHtml(msg.content)}
            <div style="text-align: right; margin-top: 4px; font-size: 0.7rem; color: var(--text-muted);">
                ${time} ${statusHtml}
            </div>
        </div>
    `;

    messagesEl.appendChild(row);
}

function scrollToBottom() {
    messagesEl.scrollTop = messagesEl.scrollHeight;
}

// ─── SignalR Connection ───────────────────────────────────────────────────────

function setStatus(state) {
    statusDot.className = `status-dot ${state}`;
    const labels = { connecting: 'Connecting...', connected: 'Connected', disconnected: 'Disconnected' };
    statusText.textContent = labels[state] || state;
}

async function startConnection() {
    setStatus('connecting');

    connection = new signalR.HubConnectionBuilder()
        .withUrl(`/chatHub?access_token=${encodeURIComponent(token)}`)
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveMessage', (messageDto) => {
        // If message is for active chat, append it and mark as read
        if (activeChatId === messageDto.chatId) {
            appendMessage(messageDto);
            scrollToBottom();
            connection.invoke('MarkAsRead', activeChatId);
        }
        
        // Always refresh recent chats to update last message & unread counts
        fetchRecentChats();
    });

    connection.on('UserOnlineStatus', (userId, isOnline) => {
        // Update active chat header
        if (activeOtherUser && activeOtherUser.id === userId) {
            updateOnlineStatusUI(isOnline);
        }
        // Update recent chats list dots
        const chat = recentChats.find(c => c.otherUser.id === userId);
        if (chat) {
            chat.otherUser.isOnline = isOnline;
            renderRecentChats();
        }
    });

    connection.on('UserTyping', (chatId, senderId) => {
        if (activeChatId === chatId) {
            typingIndicator.classList.remove('hidden');
            clearTimeout(typingTimeout);
            typingTimeout = setTimeout(() => {
                typingIndicator.classList.add('hidden');
            }, 3000);
        }
    });

    connection.on('MessagesRead', (chatId, readerId) => {
        if (activeChatId === chatId) {
            // Update all my messages to double checkmark
            const statuses = messagesEl.querySelectorAll('.msg-row.self .msg-status');
            statuses.forEach(s => {
                s.textContent = '✓✓';
                s.classList.add('seen');
            });
        }
    });

    connection.onclose(() => setStatus('disconnected'));

    try {
        await connection.start();
        setStatus('connected');
        fetchRecentChats();
    } catch (err) {
        setStatus('disconnected');
        console.error('SignalR error:', err);
    }
}

// ─── Events ───────────────────────────────────────────────────────────────────

searchBtn.addEventListener('click', () => {
    const q = searchInput.value.trim();
    if (q) searchUsers(q);
});

messageInput.addEventListener('input', () => {
    if (activeChatId && connection) {
        connection.invoke('Typing', activeChatId).catch(console.error);
    }
});

messageForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const content = messageInput.value.trim();
    if (!content || !connection || !activeChatId) return;

    try {
        messageInput.value = '';
        await connection.invoke('SendMessage', activeChatId, content);
    } catch (err) {
        console.error('SendMessage error:', err);
    }
});

logoutBtn.addEventListener('click', async () => {
    if (connection) {
        try { await connection.stop(); } catch (_) {}
    }
    localStorage.clear();
    window.location.href = '/index.html';
});

// Helpers
function formatTime(isoString) {
    try {
        const d = new Date(isoString);
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } catch { return ''; }
}

function escapeHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

// Boot
startConnection();
