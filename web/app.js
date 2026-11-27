// Pages opened from the RoomTrack e-mails: /verify-email, /reset-password, /forgot-password; any other link
// (login, bookings, rooms...) tells the reader to continue in the app.
(() => {
  'use strict';

  const API = 'https://gateway.bravepond-f06cc29c.chilecentral.azurecontainerapps.io/api/v1';

  const MESSAGES = {
    'email_verification.link_expired': 'El enlace de verificación expiró. Pide uno nuevo abajo.',
    'email_verification.link_invalid': 'El enlace de verificación no es válido o ya se usó.',
    'password_reset.link_expired': 'El enlace para restablecer la contraseña expiró. Pide uno nuevo.',
    'password_reset.link_invalid': 'El enlace para restablecer la contraseña no es válido o ya se usó.',
    'password.too_short': 'La contraseña es muy corta: mínimo 15 caracteres para huéspedes y 8 para el personal.',
    'password.too_common': 'Esa contraseña es muy común. Elige otra.',
    'password.breached': 'Esa contraseña apareció en filtraciones de datos. Elige otra.',
    'password.contains_email': 'La contraseña no puede contener tu correo.',
    'password.repetitive': 'La contraseña es demasiado repetitiva.',
  };

  const view = document.getElementById('view');
  const params = new URLSearchParams(location.search);
  const path = location.pathname.replace(/\/+$/, '').toLowerCase();

  function el(tag, attrs = {}, ...children) {
    const node = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs)) {
      if (k === 'class') node.className = v;
      else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
      else node.setAttribute(k, v);
    }
    for (const child of children) node.append(child);
    return node;
  }

  function render(...nodes) {
    view.replaceChildren(...nodes);
  }

  function status(text, kind) {
    return el('p', { class: `status ${kind || ''}`, role: kind === 'error' ? 'alert' : 'status' }, text);
  }

  // The gateway and the services scale to zero: the first request can take up to a minute.
  async function post(endpoint, body) {
    let response;
    try {
      response = await fetch(API + endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify(body),
        signal: AbortSignal.timeout(100000),
      });
    } catch {
      throw new Error('No hay conexión con el servidor de RoomTrack. Inténtalo de nuevo en un momento.');
    }
    if (response.ok) return;
    let problem = {};
    try { problem = await response.json(); } catch { /* not JSON */ }
    const known = MESSAGES[problem.code];
    if (known) throw Object.assign(new Error(known), { code: problem.code });
    const violations = (problem.violations || []).map(v => MESSAGES[v.code]).filter(Boolean);
    if (violations.length) throw new Error([...new Set(violations)].join(' '));
    if (response.status === 429) throw new Error('Demasiados intentos. Espera un momento.');
    if (response.status >= 502) throw new Error('El servidor está arrancando. Inténtalo de nuevo en un momento.');
    throw new Error(problem.detail || problem.title || `Error del servidor (${response.status}).`);
  }

  function busy(button, on, label) {
    button.disabled = on;
    button.textContent = on ? 'Un momento…' : label;
  }

  // ---- /verify-email?token= ---------------------------------------------------------------------------------------
  async function verifyEmail() {
    const token = params.get('token');
    if (!token) return resendForm('Este enlace no trae el código de verificación.');
    render(el('h1', {}, 'Verificando tu correo…'),
      el('p', {}, 'Si el servidor estaba en reposo, puede tardar hasta un minuto.'));
    try {
      await post('/authentication/verify-email', { token });
      render(el('h1', {}, '¡Correo verificado!'),
        status('Ya puedes iniciar sesión en la app RoomTrack.', 'ok'));
    } catch (e) {
      resendForm(e.message);
    }
  }

  function resendForm(problem) {
    const email = el('input', { id: 'email', type: 'email', autocomplete: 'email', required: '' });
    const button = el('button', { type: 'submit' }, 'Enviarme un enlace nuevo');
    const result = el('div');
    const form = el('form', {
      onsubmit: async (ev) => {
        ev.preventDefault();
        busy(button, true);
        result.replaceChildren();
        try {
          await post('/authentication/verify-email/resend', { email: email.value.trim() });
          result.replaceChildren(status('Si ese correo tiene una cuenta sin verificar, te enviamos un enlace nuevo.', 'ok'));
        } catch (e) {
          result.replaceChildren(status(e.message, 'error'));
        } finally {
          busy(button, false, 'Enviarme un enlace nuevo');
        }
      },
    }, el('label', { for: 'email' }, 'Tu correo'), email, button, result);
    render(el('h1', {}, 'No pudimos verificar tu correo'), status(problem, 'error'), form);
  }

  // ---- /reset-password?token= -------------------------------------------------------------------------------------
  function resetPassword() {
    const token = params.get('token');
    if (!token) return forgotPassword('Este enlace no trae el código para restablecer la contraseña.');
    const pass = el('input', { id: 'pass', type: 'password', autocomplete: 'new-password', required: '' });
    const again = el('input', { id: 'again', type: 'password', autocomplete: 'new-password', required: '' });
    const button = el('button', { type: 'submit' }, 'Guardar la contraseña');
    const result = el('div');
    const form = el('form', {
      onsubmit: async (ev) => {
        ev.preventDefault();
        if (pass.value !== again.value) {
          result.replaceChildren(status('Las contraseñas no coinciden.', 'error'));
          return;
        }
        busy(button, true);
        result.replaceChildren();
        try {
          await post('/authentication/password-reset', { token, newPassword: pass.value });
          render(el('h1', {}, 'Contraseña actualizada'),
            status('Ya puedes iniciar sesión en la app RoomTrack con tu nueva contraseña.', 'ok'));
        } catch (e) {
          result.replaceChildren(status(e.message, 'error'));
          busy(button, false, 'Guardar la contraseña');
        }
      },
    },
    el('label', { for: 'pass' }, 'Nueva contraseña'), pass,
    el('label', { for: 'again' }, 'Repítela'), again,
    el('p', { class: 'hint' }, 'Mínimo 15 caracteres para huéspedes y 8 para el personal.'),
    button, result);
    render(el('h1', {}, 'Crea una nueva contraseña'), form);
  }

  // ---- /forgot-password?email= ------------------------------------------------------------------------------------
  function forgotPassword(problem) {
    const email = el('input', { id: 'email', type: 'email', autocomplete: 'email', required: '' });
    email.value = params.get('email') || '';
    const button = el('button', { type: 'submit' }, 'Enviarme el enlace');
    const result = el('div');
    const form = el('form', {
      onsubmit: async (ev) => {
        ev.preventDefault();
        busy(button, true);
        result.replaceChildren();
        try {
          await post('/authentication/password-recovery', { email: email.value.trim() });
          result.replaceChildren(status('Si ese correo tiene una cuenta, te enviamos un enlace para restablecer la contraseña.', 'ok'));
        } catch (e) {
          result.replaceChildren(status(e.message, 'error'));
        } finally {
          busy(button, false, 'Enviarme el enlace');
        }
      },
    }, el('label', { for: 'email' }, 'Tu correo'), email, button, result);
    render(el('h1', {}, 'Restablece tu contraseña'),
      problem ? status(problem, 'error') : el('p', {}, 'Te enviaremos un enlace para crear una nueva.'),
      form);
  }

  // ---- anything else ----------------------------------------------------------------------------------------------
  function openTheApp() {
    render(el('h1', {}, 'Continúa en la app'),
      el('p', {}, 'Abre la app RoomTrack en tu teléfono para iniciar sesión y ver tus reservas, habitaciones y reportes.'),
      el('button', { type: 'button', class: 'link', onclick: () => location.assign('/forgot-password') },
        '¿Olvidaste tu contraseña?'));
  }

  switch (path) {
    case '/verify-email': verifyEmail(); break;
    case '/reset-password': resetPassword(); break;
    case '/forgot-password': forgotPassword(); break;
    default: openTheApp();
  }
})();
