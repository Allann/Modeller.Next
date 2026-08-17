import { NextResponse } from 'next/server';
import { auth } from './auth';

export const proxy = auth((request) => {
  if (!request.auth?.user) return NextResponse.redirect(new URL('/sign-in', request.url));
  const response = NextResponse.next();
  response.cookies.set('modeller_internal', '1', { domain: '.modeller.website', path: '/', secure: true, sameSite: 'lax', maxAge: 31536000 });
  return response;
});
export const config = { matcher: ['/((?!api/auth|sign-in|_next/static|_next/image|favicon.ico|icon).*)'] };
