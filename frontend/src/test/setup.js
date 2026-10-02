import '@testing-library/jest-dom/vitest';

// O jsdom não implementa localStorage entre recarregamentos de teste, e o
// session.js depende dele. Um stub simples basta para os testes.
if (!('matchMedia' in window)) {
  window.matchMedia = (query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  });
}