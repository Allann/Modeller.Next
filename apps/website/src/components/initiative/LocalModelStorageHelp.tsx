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
 * Shown, in place of an error, when the browser would not store the local model. A web page cannot
 * raise its own storage limit, and browsers have no quota setting: the limit comes from deleting site
 * data on close (about 300 MB per site), which the Facilitator can lift for this one site.
 */
export function LocalModelStorageHelp({ quotaBytes }: { quotaBytes: number | null }) {
  const browser = detectBrowser();
  return (
    <div className="local-model-help" role="alert">
      <p className="local-model-help-title">One browser setting is needed to use the local model</p>
      <p>
        The model needs about 1.8 GB of storage in your browser.{' '}
        {quotaBytes === null
          ? 'Your browser did not let this site store it.'
          : `Your browser lets this site store only about ${Math.round(quotaBytes / 1e6)} MB.`}{' '}
        This happens when the browser is set to delete site data when it closes. Free disk space does not change it.
        Do these steps once, to let modeller.website keep its data. Your other sites keep their protection.
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
      <p>Then reload this page and start the local model again. In a private window, this limit always applies.</p>
      <button className="secondary-action" type="button" onClick={() => window.location.reload()}>
        Reload this page
      </button>
    </div>
  );
}
