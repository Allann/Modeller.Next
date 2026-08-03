import path from 'node:path';
import { fileURLToPath } from 'node:url';

const dirname = path.dirname(fileURLToPath(import.meta.url));

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // Scope Turbopack to this app — without this it infers a shared workspace
  // root with the sibling docs site (both have their own package-lock.json)
  // and incorrectly pulls in the docs app's root-level proxy.ts.
  turbopack: {
    root: dirname,
  },
};

export default nextConfig;
