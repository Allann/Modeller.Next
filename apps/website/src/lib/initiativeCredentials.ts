// Issue #146: the two role-scoped credentials a session issues at creation time. The bearer of
// authority is always the URL query parameter on a session's sharable link (?credential=...) — that
// is what the acceptance criteria call "the link" and what a copy-pasted URL actually carries to
// another browser. sessionStorage here is only a same-tab convenience so the Facilitator cockpit can
// re-display the Domain Expert's link (and survive a reload) without asking the caller to re-paste
// it; it mirrors the existing agentApiKey.ts pattern and shares its caveat — kept in this browser
// tab only, not retrievable from a different tab/device that never saw the original create response.

const facilitatorPrefix = 'modeller:initiative:credential:facilitator:';
const domainExpertPrefix = 'modeller:initiative:credential:domain-expert:';

export function saveInitiativeCredentials(initiativeId: string, facilitatorCredential: string, domainExpertCredential: string) {
  if (typeof window === 'undefined') return;
  window.sessionStorage.setItem(`${facilitatorPrefix}${initiativeId}`, facilitatorCredential);
  window.sessionStorage.setItem(`${domainExpertPrefix}${initiativeId}`, domainExpertCredential);
}

export function loadFacilitatorCredential(initiativeId: string): string {
  if (typeof window === 'undefined') return '';
  return window.sessionStorage.getItem(`${facilitatorPrefix}${initiativeId}`) ?? '';
}

export function loadDomainExpertCredential(initiativeId: string): string {
  if (typeof window === 'undefined') return '';
  return window.sessionStorage.getItem(`${domainExpertPrefix}${initiativeId}`) ?? '';
}
