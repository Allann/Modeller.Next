// The playground's "bounded browser/session workspace" (issue #72): a draft
// lives in sessionStorage — tab-scoped, cleared when the tab closes — never
// on any server or local disk. It is explicitly not a durable local
// workspace; the UI must say so until #73's download feature exists.
import { EXAMPLE_ORDERING_CONFIGURATION, EXAMPLE_ORDERING_DOCUMENTS } from './example-ordering';
import { EXAMPLE_CHILD_CARE_CONFIGURATION, EXAMPLE_CHILD_CARE_DOCUMENTS } from './example-child-care';
import { EPHEMERAL_IDENTITY, type ConfigurationDto, type IdentityDto, type WorkspaceDocumentDto } from './api-client';

const STORAGE_KEY = 'modeller-playground-draft-v1';

export interface PlaygroundDraft {
  documents: WorkspaceDocumentDto[];
  configuration: ConfigurationDto;
  // Ephemeral until the first successful export/download (issue #73) — from then on this carries
  // the durable registry, so a later edit-then-download or repeat download reuses the same ids
  // instead of a fresh ephemeral draft minting new ones every analyze call.
  identity: IdentityDto;
}

function pristineDraft(): PlaygroundDraft {
  const childCare = typeof window !== 'undefined' && new URLSearchParams(window.location.search).get('example') === 'child-care';
  const documents = childCare ? EXAMPLE_CHILD_CARE_DOCUMENTS : EXAMPLE_ORDERING_DOCUMENTS;
  const configuration = childCare ? EXAMPLE_CHILD_CARE_CONFIGURATION : EXAMPLE_ORDERING_CONFIGURATION;
  return {
    documents: documents.map((document) => ({ ...document })),
    configuration: { ...configuration },
    identity: EPHEMERAL_IDENTITY,
  };
}

function isPlaygroundDraft(value: unknown): value is PlaygroundDraft {
  if (!value || typeof value !== 'object') return false;
  const draft = value as Partial<PlaygroundDraft>;
  return (
    Array.isArray(draft.documents) &&
    draft.documents.length > 0 &&
    typeof draft.configuration === 'object' &&
    typeof draft.identity === 'object' &&
    draft.identity !== null
  );
}

export function loadDraft(): PlaygroundDraft {
  if (typeof window === 'undefined') return pristineDraft();
  try {
    const requestedExample = new URLSearchParams(window.location.search).get('example');
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return pristineDraft();
    const parsed: unknown = JSON.parse(raw);
    // An explicit example must not be hidden by an unrelated draft, but a refresh of
    // that same example must preserve the reader's edits.
    if (requestedExample === 'child-care') {
      return isPlaygroundDraft(parsed) && parsed.configuration.profile === 'child-care-csharp'
        ? parsed
        : pristineDraft();
    }
    return isPlaygroundDraft(parsed) ? parsed : pristineDraft();
  } catch {
    return pristineDraft();
  }
}

export function saveDraft(draft: PlaygroundDraft): void {
  if (typeof window === 'undefined') return;
  window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(draft));
}

export function resetToExample(): PlaygroundDraft {
  const draft = pristineDraft();
  saveDraft(draft);
  return draft;
}
