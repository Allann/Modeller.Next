import { NextResponse } from 'next/server';

// Local-only API routes (filesystem document read/write, CLI-subprocess
// projection) must be unreachable in a playground deployment even though the
// playground UI never calls them — a public deployment can't rely solely on
// the client not asking. See issue #72's "accidental host filesystem access
// is impossible in public mode" acceptance criterion.
export function localOnlyRouteGuard(): NextResponse | undefined {
  if (process.env.NEXT_PUBLIC_MODELLER_STUDIO_MODE !== 'playground') return undefined;
  return NextResponse.json({ error: 'This route is disabled in playground mode.' }, { status: 404 });
}
