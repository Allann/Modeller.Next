'use client';

import { CopyLinkButton } from './CopyLinkButton';

/** Matches both modeller.website and www.modeller.website in Chrome's and Edge's site lists. */
const SITE_PATTERN = '[*.]modeller.website';

type Browser = 'edge' | 'chrome' | 'other';

function detectBrowser(): Browser {
  const agent = typeof navigator === 'undefined' ? '' : navigator.userAgent;
  if (agent.includes('Edg/')) return 'edge';
  if (agent.includes('Chrome/')) return 'chrome';
  return 'other';
}

function SettingsAddress({ address }: { address: string }) {
  return (
    <>
      <code>{address}</code> <CopyLinkButton url={address} label="settings address" />
    </>
  );
}

function ChromeSteps() {
  return (
    <ol>
      <li>Open a new tab, and paste this address into it: <SettingsAddress address="chrome://settings/content/siteData" /></li>
      <li>Next to <strong>Allowed to save data on your device</strong>, click <strong>Add</strong>.</li>
      <li>Type <code>{SITE_PATTERN}</code>, then click <strong>Add</strong>.</li>
    </ol>
  );
}

function EdgeSteps() {
  return (
    <ol>
      <li>Open a new tab, and paste this address into it: <SettingsAddress address="edge://settings/clearBrowsingDataOnClose" /></li>
      <li>Next to <strong>Don&rsquo;t clear</strong>, click <strong>Add</strong>.</li>
      <li>Type <code>{SITE_PATTERN}</code>, then click <strong>Add</strong>.</li>
    </ol>
  );
}

/**
 * Shown when the browser's per-site storage limit is smaller than the local model. A web page cannot
 * raise its own limit, and the browser has no quota setting: the limit comes from deleting site data on
 * close (about 300 MB per site), which the Facilitator can lift for this one site.
 */
export function LocalModelStorageHelp({ message }: { message: string }) {
  const browser = detectBrowser();
  return (
    <div className="local-model-help" role="alert">
      <p><strong>{message}</strong></p>
      <p>
        Your browser is set to delete site data when it closes. Then it lets each site store only about 300 MB, however
        much disk space is free. You can lift this limit for modeller.website only. Your other sites keep their protection.
      </p>
      {browser !== 'edge' && (
        <>
          {browser === 'other' && <p className="local-model-help-browser">Chrome:</p>}
          <ChromeSteps />
        </>
      )}
      {browser !== 'chrome' && (
        <>
          {browser === 'other' && <p className="local-model-help-browser">Edge:</p>}
          <EdgeSteps />
        </>
      )}
      <p>
        Then reload this page and start the local model again. A private window always has this limit.
      </p>
      <button className="secondary-action" type="button" onClick={() => window.location.reload()}>
        Reload this page
      </button>
    </div>
  );
}
