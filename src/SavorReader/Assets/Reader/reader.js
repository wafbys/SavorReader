(() => {
  'use strict';

  const pages = document.getElementById('pages');
  const loading = document.getElementById('loading');
  const errorBox = document.getElementById('error');
  const progressBar = document.getElementById('progress-bar');
  const bridge = window.chrome && window.chrome.webview;

  const state = {
    book: null,
    settings: null,
    frames: [],
    heights: [],
    offsets: [],
    totalHeight: 0,
    spinePaths: new Map(),
    ready: false,
    measureQueued: false,
    progressTimer: 0,
    selectionTimer: 0,
    lastSelectionText: '',
    loadPromises: []
  };

  const clamp = (value, min, max) => Math.min(max, Math.max(min, value));

  const post = (message) => {
    try {
      bridge?.postMessage(message);
    } catch {
      // The host can navigate away while a message is in flight.
    }
  };

  const encodePath = (path) => path.split('/').map(encodeURIComponent).join('/');

  const pathKey = (path) => {
    try {
      return decodeURIComponent(path).replace(/^\/+/, '').toLowerCase();
    } catch {
      return path.replace(/^\/+/, '').toLowerCase();
    }
  };

  async function init() {
    try {
      const response = await fetch('book.json', { cache: 'no-store' });
      if (!response.ok) {
        throw new Error('无法读取书籍信息。');
      }

      state.book = await response.json();
      state.settings = normalizeSettings(state.book.settings);
      state.spinePaths = new Map(
        (state.book.spine || []).map((href, index) => [pathKey(href), index])
      );

      applyRootSettings();
      buildFrames();
      await Promise.all(state.loadPromises);

      // Give images, fonts, and initial layout one more frame to settle.
      await new Promise((resolve) => setTimeout(resolve, 120));
      scheduleMeasure(true);
      await new Promise((resolve) => setTimeout(resolve, 80));
      measure();

      restoreLocation();
      loading.hidden = true;
      state.ready = true;
      updateProgressBar();
      post({
        type: 'ready',
        chapters: state.frames.length,
        title: state.book.title || ''
      });
      if (state.book.location) {
        sendProgress(true);
      }

      window.addEventListener('scroll', scheduleProgress, { passive: true });
      window.addEventListener('resize', () => {
        scheduleMeasure();
        scheduleProgress();
      }, { passive: true });
      window.addEventListener('beforeunload', () => sendProgress(true));
      bridge?.addEventListener('message', onHostMessage);
    } catch (error) {
      showError(error && error.message ? error.message : String(error));
    }
  }

  function normalizeSettings(raw) {
    const source = raw || {};
    return {
      theme: source.theme === 'night' ? 'night' : 'day',
      font: source.font === 'sans' ? 'sans' : 'serif',
      fontSize: clamp(Number(source.fontSize) || 18, 12, 30),
      lineHeight: clamp(Number(source.lineHeight) || 1.7, 1.2, 2.2),
      margin: clamp(Number(source.margin) || 48, 16, 96)
    };
  }

  function palette() {
    const night = state.settings.theme === 'night';
    return {
      bg: night ? '#121212' : '#faf7f2',
      fg: night ? '#d8d4cf' : '#252322',
      muted: night ? '#a09a94' : '#6b6560',
      accent: night ? '#d6a77a' : '#8b5e3c',
      selection: night ? '#5a4632' : '#f1d9b5'
    };
  }

  function applyRootSettings() {
    const colors = palette();
    const root = document.documentElement;
    root.dataset.theme = state.settings.theme;
    root.style.setProperty('--reader-bg', colors.bg);
    root.style.setProperty('--reader-fg', colors.fg);
    root.style.setProperty('--reader-muted', colors.muted);
    root.style.setProperty('--reader-accent', colors.accent);
    root.style.setProperty('--reader-selection', colors.selection);
  }

  function frameCss() {
    const settings = state.settings;
    const colors = palette();
    const family = settings.font === 'sans'
      ? "'Segoe UI', 'Microsoft YaHei UI', sans-serif"
      : "'Georgia', 'Noto Serif SC', 'SimSun', serif";

    return `
      html, body {
        background: ${colors.bg} !important;
        color: ${colors.fg} !important;
      }
      body {
        max-width: 880px !important;
        margin: 0 auto !important;
        padding: 24px ${settings.margin}px 48px !important;
        font-family: ${family} !important;
        font-size: ${settings.fontSize}px !important;
        line-height: ${settings.lineHeight} !important;
        box-sizing: border-box !important;
        overflow-wrap: break-word !important;
        word-break: normal !important;
      }
      p, li, div, span, td, th, dd, dt, blockquote {
        font-family: inherit !important;
      }
      h1, h2, h3, h4, h5, h6 {
        color: ${colors.fg} !important;
        line-height: 1.35 !important;
      }
      a {
        color: ${colors.accent} !important;
      }
      img, svg, video, canvas {
        max-width: 100% !important;
        height: auto !important;
      }
      table {
        max-width: 100% !important;
      }
      pre, code {
        white-space: pre-wrap !important;
      }
      ::selection {
        background: ${colors.selection} !important;
        color: ${colors.fg} !important;
      }
    `;
  }

  function buildFrames() {
    const spine = state.book.spine || [];
    for (let index = 0; index < spine.length; index++) {
      const frame = document.createElement('iframe');
      frame.className = 'spine-frame';
      frame.dataset.index = String(index);
      frame.setAttribute('sandbox', 'allow-same-origin');
      frame.setAttribute('scrolling', 'no');
      // reader.html and book.json live in __reader/, while spine paths are
      // relative to the EPUB cache root.
      frame.src = `../${encodePath(spine[index])}`;

      const loaded = new Promise((resolve) => {
        const done = () => {
          try {
            injectFrame(frame, index);
          } catch (error) {
            showError(`章节排版失败：${error.message || error}`);
          }
          resolve();
        };
        frame.addEventListener('load', done, { once: true });
        frame.addEventListener('error', resolve, { once: true });
        setTimeout(resolve, 15000);
      });

      state.frames.push(frame);
      state.loadPromises.push(loaded);
      pages.appendChild(frame);
    }
  }

  function injectFrame(frame, index) {
    const doc = frame.contentDocument;
    if (!doc) {
      return;
    }

    // The sandbox already blocks scripts; removing tags keeps the DOM tidy and
    // makes the reader style safer to inject.
    doc.querySelectorAll('script').forEach((element) => element.remove());
    doc.querySelectorAll('meta[http-equiv]').forEach((element) => {
      if ((element.getAttribute('http-equiv') || '').toLowerCase() === 'content-security-policy') {
        element.remove();
      }
    });

    const styleId = 'savor-reader-style';
    let style = doc.getElementById(styleId);
    if (!style) {
      style = doc.createElement('style');
      style.id = styleId;
      (doc.head || doc.documentElement).appendChild(style);
    }
    style.textContent = frameCss();

    doc.addEventListener('click', (event) => handleLink(event, index), true);
    doc.addEventListener('selectionchange', () => scheduleSelection(index), true);
    doc.querySelectorAll('img').forEach((image) => {
      image.addEventListener('load', scheduleMeasure, { once: true });
      image.addEventListener('error', scheduleMeasure, { once: true });
    });

    try {
      const ResizeObserverCtor = doc.defaultView && doc.defaultView.ResizeObserver;
      if (ResizeObserverCtor && doc.body) {
        const observer = new ResizeObserverCtor(() => scheduleMeasure());
        observer.observe(doc.body);
      }
      doc.fonts?.ready.then(scheduleMeasure).catch(() => {});
    } catch {
      // Layout still gets measured on load and timeout below.
    }

    setTimeout(scheduleMeasure, 50);
    setTimeout(scheduleMeasure, 350);
  }

  function scheduleMeasure(immediate) {
    if (immediate) {
      measure();
      return;
    }
    if (state.measureQueued) {
      return;
    }
    state.measureQueued = true;
    requestAnimationFrame(() => {
      state.measureQueued = false;
      measure();
    });
  }

  function measure() {
    let total = 0;
    for (let index = 0; index < state.frames.length; index++) {
      const frame = state.frames[index];
      let height = 0;
      try {
        const doc = frame.contentDocument;
        if (doc) {
          height = Math.max(
            doc.documentElement?.scrollHeight || 0,
            doc.body?.scrollHeight || 0,
            doc.body?.getBoundingClientRect().height || 0
          );
        }
      } catch {
        height = 0;
      }

      if (!Number.isFinite(height) || height < 1) {
        height = 600;
      }
      height = Math.ceil(height);
      const previous = state.heights[index] || 0;
      if (Math.abs(previous - height) > 1 || !frame.style.height) {
        frame.style.height = `${height}px`;
      }
      state.heights[index] = height;
      state.offsets[index] = total;
      total += height;
    }

    state.totalHeight = total;
    pages.style.height = `${total}px`;
    updateProgressBar();
  }

  function currentLocation() {
    const scrollTop = window.scrollY || document.documentElement.scrollTop || 0;
    let index = 0;
    for (let i = 0; i < state.offsets.length; i++) {
      if (scrollTop >= state.offsets[i] - 2) {
        index = i;
      } else {
        break;
      }
    }

    const chapterHeight = Math.max(1, state.heights[index] || 1);
    const chapterRatio = clamp((scrollTop - (state.offsets[index] || 0)) / chapterHeight, 0, 1);
    const maxScroll = Math.max(
      1,
      document.documentElement.scrollHeight - window.innerHeight
    );
    const overall = clamp(scrollTop / maxScroll, 0, 1);
    return { index, chapterRatio, overall };
  }

  function updateProgressBar() {
    if (!state.frames.length) {
      progressBar.style.width = '0%';
      return;
    }
    progressBar.style.width = `${(currentLocation().overall * 100).toFixed(2)}%`;
  }

  function scheduleProgress() {
    updateProgressBar();
    if (state.progressTimer) {
      return;
    }
    state.progressTimer = setTimeout(() => {
      state.progressTimer = 0;
      sendProgress(false);
    }, 250);
  }

  function sendProgress(immediate) {
    if (!state.ready && !immediate) {
      return;
    }
    const location = currentLocation();
    post({
      type: 'progress',
      spineIndex: location.index,
      chapterRatio: Number(location.chapterRatio.toFixed(4)),
      percent: Number(location.overall.toFixed(4))
    });
  }

  function restoreLocation() {
    const location = state.book?.location;
    if (!location) {
      return;
    }

    const index = clamp(Number(location.spineIndex) || 0, 0, Math.max(0, state.frames.length - 1));
    const ratio = clamp(Number(location.chapterRatio) || 0, 0, 1);
    const top = (state.offsets[index] || 0) + (state.heights[index] || 0) * ratio;
    window.scrollTo(0, Math.max(0, top));
  }

  function captureLocation() {
    return currentLocation();
  }

  function restoreTo(location) {
    if (!location || !state.frames.length) {
      return;
    }
    const index = clamp(location.index, 0, state.frames.length - 1);
    const top = (state.offsets[index] || 0) + (state.heights[index] || 0) * location.chapterRatio;
    window.scrollTo(0, Math.max(0, top));
  }

  function goTo(spineIndex, anchor, smooth) {
    if (!state.frames.length) {
      return;
    }

    const index = clamp(Number(spineIndex) || 0, 0, state.frames.length - 1);
    let top = state.offsets[index] || 0;
    if (anchor) {
      top += findAnchorOffset(index, anchor);
    }

    window.scrollTo({
      top: Math.max(0, top),
      behavior: smooth ? 'smooth' : 'auto'
    });
    scheduleProgress();
  }

  function findAnchorOffset(index, anchor) {
    try {
      const doc = state.frames[index]?.contentDocument;
      if (!doc || !anchor) {
        return 0;
      }

      let element = doc.getElementById(anchor) || doc.getElementsByName(anchor)[0];
      if (!element) {
        try {
          const decoded = decodeURIComponent(anchor);
          element = doc.getElementById(decoded) || doc.getElementsByName(decoded)[0];
        } catch {
          element = null;
        }
      }

      if (!element) {
        return 0;
      }

      const rect = element.getBoundingClientRect();
      return Math.max(0, rect.top);
    } catch {
      return 0;
    }
  }

  function handleLink(event, index) {
    const anchorElement = event.target?.closest ? event.target.closest('a') : null;
    if (!anchorElement) {
      return;
    }

    const href = anchorElement.getAttribute('href');
    if (!href) {
      return;
    }

    if (href.startsWith('#')) {
      event.preventDefault();
      goTo(index, href.slice(1), true);
      return;
    }

    if (/^(https?:|mailto:|tel:)/i.test(href)) {
      event.preventDefault();
      post({ type: 'externalLink', url: anchorElement.href || href });
      return;
    }

    try {
      const url = new URL(anchorElement.href || href, state.frames[index].src);
      if (url.origin !== window.location.origin) {
        event.preventDefault();
        post({ type: 'externalLink', url: url.href });
        return;
      }

      const target = state.spinePaths.get(pathKey(url.pathname));
      if (target !== undefined) {
        event.preventDefault();
        const anchor = url.hash ? url.hash.slice(1) : null;
        goTo(target, anchor, true);
      }
    } catch {
      event.preventDefault();
    }
  }

  function scheduleSelection(index) {
    if (state.selectionTimer) {
      clearTimeout(state.selectionTimer);
    }
    state.selectionTimer = setTimeout(() => {
      state.selectionTimer = 0;
      try {
        const selection = state.frames[index]?.contentDocument?.getSelection();
        const text = selection?.toString().trim() || '';
        if (!text || text === state.lastSelectionText) {
          return;
        }
        state.lastSelectionText = text;
        post({
          type: 'selection',
          spineIndex: index,
          text: text.slice(0, 4000)
        });
      } catch {
        // Selection is a best-effort extension point.
      }
    }, 350);
  }

  function onHostMessage(event) {
    const message = event.data;
    if (!message || !message.type) {
      return;
    }

    if (message.type === 'settings') {
      applySettings(message.settings);
    } else if (message.type === 'goto') {
      goTo(message.spineIndex, message.anchor, false);
    }
  }

  function applySettings(raw) {
    if (!state.book) {
      return;
    }

    const previous = captureLocation();
    state.settings = normalizeSettings(raw);
    applyRootSettings();
    for (const frame of state.frames) {
      try {
        const style = frame.contentDocument?.getElementById('savor-reader-style');
        if (style) {
          style.textContent = frameCss();
        }
      } catch {
        // Frame may be gone during navigation.
      }
    }

    scheduleMeasure(true);
    setTimeout(() => {
      measure();
      restoreTo(previous);
      scheduleProgress();
    }, 80);
  }

  function showError(message) {
    loading.hidden = true;
    errorBox.textContent = message;
    errorBox.hidden = false;
    post({ type: 'error', message });
  }

  window.SavorReader = {
    applySettings,
    goTo,
    flush: () => sendProgress(true),
    getLocation: () => currentLocation()
  };

  init();
})();
