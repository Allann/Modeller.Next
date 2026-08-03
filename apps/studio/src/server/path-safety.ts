// Port of Modeller.Cli's CliApplication.IsWorkspaceRelative (src/Modeller.Cli/CliApplication.cs).
// Checked host-OS-independently (not via Node's path.isAbsolute) since the .NET
// original rejects drive-letter prefixes regardless of which OS is running.
export function isWorkspaceRelative(value: string): boolean {
  if (isRooted(value)) return false;
  const normalized = value.replace(/\\/g, '/');
  return !normalized.split('/').some((segment) => segment.length > 0 && segment === '..');
}

function isRooted(value: string): boolean {
  if (value.startsWith('/') || value.startsWith('\\')) return true;
  if (value.length >= 2 && value[1] === ':' && /[A-Za-z]/.test(value[0])) return true;
  return false;
}
