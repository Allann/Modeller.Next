import { nativeImage, type NativeImage } from 'electron';

// Reuses the app's own generated favicon (src/app/icon.tsx, served at /icon) as the native
// BrowserWindow icon, rather than committing a separate binary icon asset that could drift from it
// — Electron's default window icon otherwise shows through on every window (main and detached
// panels alike).
export async function fetchAppIcon(port: number): Promise<NativeImage | undefined> {
  try {
    const response = await fetch(`http://localhost:${port}/icon`);
    if (!response.ok) return undefined;
    const image = nativeImage.createFromBuffer(Buffer.from(await response.arrayBuffer()));
    return image.isEmpty() ? undefined : image;
  } catch {
    return undefined;
  }
}
