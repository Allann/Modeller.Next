const keyPrefix = 'modeller:initiative:agent-key:';

export function saveAgentApiKey(initiativeId: string, apiKey: string) {
  if (typeof window === 'undefined' || !apiKey) return;
  window.sessionStorage.setItem(`${keyPrefix}${initiativeId}`, apiKey);
}

export function loadAgentApiKey(initiativeId: string): string {
  if (typeof window === 'undefined') return '';
  return window.sessionStorage.getItem(`${keyPrefix}${initiativeId}`) ?? '';
}
