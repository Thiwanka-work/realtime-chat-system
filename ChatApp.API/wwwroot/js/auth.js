// ─── Helpers ─────────────────────────────────────────────────────────────────

const isLoginPage    = document.getElementById('login-form')    !== null;
const isRegisterPage = document.getElementById('register-form') !== null;

function showAlert(message, type = 'error') {
  const box = document.getElementById('alert-box');
  if (!box) return;
  box.textContent = message;
  box.className   = `alert ${type}`;
}

function hideAlert() {
  const box = document.getElementById('alert-box');
  if (box) box.className = 'alert hidden';
}

function setFieldError(fieldId, message) {
  const el    = document.getElementById(`${fieldId}-error`);
  const input = document.getElementById(fieldId);
  if (el)    el.textContent = message;
  if (input) {
    if (message) input.classList.add('invalid');
    else         input.classList.remove('invalid');
  }
}

function clearErrors(...fieldIds) {
  fieldIds.forEach(id => setFieldError(id, ''));
  hideAlert();
}

function setLoading(btnId, loading) {
  const btn     = document.getElementById(btnId);
  const text    = btn?.querySelector('.btn-text');
  const spinner = btn?.querySelector('.btn-spinner');
  if (!btn) return;
  btn.disabled = loading;
  text?.classList.toggle('hidden', loading);
  spinner?.classList.toggle('hidden', !loading);
}

// ─── Password Toggle ──────────────────────────────────────────────────────────

const toggleBtn = document.getElementById('toggle-pwd');
if (toggleBtn) {
  toggleBtn.addEventListener('click', () => {
    const pwdInput = document.getElementById('password');
    if (!pwdInput) return;
    const isText        = pwdInput.type === 'text';
    pwdInput.type       = isText ? 'password' : 'text';
    toggleBtn.textContent = isText ? '👁' : '🙈';
  });
}

// ─── Redirect if already logged in ───────────────────────────────────────────

if ((isLoginPage || isRegisterPage) && localStorage.getItem('jwt_token')) {
  window.location.href = '/chat.html';
}

// ─── LOGIN ────────────────────────────────────────────────────────────────────

if (isLoginPage) {
  const form = document.getElementById('login-form');

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    clearErrors('username', 'password');

    const username = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value;

    // Client-side validation
    let valid = true;
    if (!username) { setFieldError('username', 'Username is required.'); valid = false; }
    if (!password) { setFieldError('password', 'Password is required.'); valid = false; }
    if (!valid) return;

    setLoading('login-btn', true);

    try {
      const res = await fetch('/api/auth/login', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ username, password })
      });

      const data = await res.json();

      if (!res.ok) {
        showAlert(data.message || 'Login failed. Please check your credentials.');
        return;
      }

      // Store token and user info
      localStorage.setItem('jwt_token', data.token);
      localStorage.setItem('chat_username', data.username);
      localStorage.setItem('chat_email', data.email);

      // Redirect to chat
      window.location.href = '/chat.html';

    } catch (err) {
      showAlert('Network error. Please check your connection and try again.');
      console.error('Login error:', err);
    } finally {
      setLoading('login-btn', false);
    }
  });
}

// ─── REGISTER ─────────────────────────────────────────────────────────────────

if (isRegisterPage) {
  const form = document.getElementById('register-form');

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    clearErrors('username', 'email', 'password');

    const username = document.getElementById('username').value.trim();
    const email    = document.getElementById('email').value.trim();
    const password = document.getElementById('password').value;

    // Client-side validation
    let valid = true;
    if (!username || username.length < 3) {
      setFieldError('username', 'Username must be at least 3 characters.');
      valid = false;
    }
    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setFieldError('email', 'Please enter a valid email address.');
      valid = false;
    }
    if (!password || password.length < 6) {
      setFieldError('password', 'Password must be at least 6 characters.');
      valid = false;
    }
    if (!valid) return;

    setLoading('register-btn', true);

    try {
      const res = await fetch('/api/auth/register', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ username, email, password })
      });

      const data = await res.json();

      if (!res.ok) {
        showAlert(data.message || data.title || 'Registration failed. Please try again.');
        return;
      }

      // Auto-login after register
      localStorage.setItem('jwt_token', data.token);
      localStorage.setItem('chat_username', data.username);
      localStorage.setItem('chat_email', data.email);

      showAlert('Account created! Redirecting...', 'success');
      setTimeout(() => { window.location.href = '/chat.html'; }, 800);

    } catch (err) {
      showAlert('Network error. Please check your connection and try again.');
      console.error('Register error:', err);
    } finally {
      setLoading('register-btn', false);
    }
  });
}
