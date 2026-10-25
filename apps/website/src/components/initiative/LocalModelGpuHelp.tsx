'use client';

import { detectBrowser, SettingsAddress } from './LocalModelStorageHelp';

/**
 * Shown, in place of an error, when the browser supports WebGPU but gives this site no GPU. On a
 * computer with a good GPU, the usual cause is a GPU process crash (for example, a driver reset or
 * update): Chrome then uses software rendering until it restarts, and blocks WebGPU on that. So the
 * restart comes first, and the less usual causes come after it.
 */
export function LocalModelGpuHelp({ gatewayUsable }: { gatewayUsable: boolean }) {
  const scheme = detectBrowser() === 'edge' ? 'edge' : 'chrome';
  return (
    <div className="local-model-help" role="alert">
      <p className="local-model-help-title">Your browser is not using your GPU at the moment</p>
      <p>
        The local model needs the GPU (graphics card) in your computer, but your browser does not give it to this
        site. Usually, a restart of the browser fixes this.
      </p>
      <p>To fix it now:</p>
      <ol>
        <li>
          Open a new tab, and paste this address into it: <SettingsAddress address={`${scheme}://restart`} /> The
          browser closes and opens again, and your tabs come back.
        </li>
        <li>
          Open this address: <SettingsAddress address={`${scheme}://gpu`} /> Find the <strong>WebGPU</strong> line.
          It must show <strong>Hardware accelerated</strong>.
        </li>
        <li>Reload this page, and start the local model again.</li>
      </ol>
      <p>If WebGPU does not show Hardware accelerated:</p>
      <ul>
        <li>
          If the GPU list shows only <strong>Microsoft Basic Render Driver</strong>, Windows does not use your graphics
          driver. Restart the computer, or install the graphics driver again.
        </li>
        <li>
          Graphics acceleration can be off. Open <SettingsAddress address={`${scheme}://settings/system`} /> Turn
          on <strong>Use graphics acceleration when available</strong>, then click <strong>Relaunch</strong>.
        </li>
        <li>A remote desktop or a virtual machine often has no GPU. Use a browser on the computer itself.</li>
      </ul>
      {gatewayUsable && <p>You can also use the AI Gateway.</p>}
      <button className="secondary-action" type="button" onClick={() => window.location.reload()}>
        Reload this page
      </button>
    </div>
  );
}
