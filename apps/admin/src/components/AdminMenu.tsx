import { signOut } from '@/auth';
import Link from 'next/link';

const routes = [
  { href: '/', label: 'Analytics' },
  { href: '/redis', label: 'Redis inventory' },
] as const;

export function AdminMenu() {
  return <nav className="admin-menu" aria-label="Admin">
    <div className="admin-menu-routes">
      {routes.map((route) => <Link href={route.href} key={route.href}>{route.label}</Link>)}
    </div>
    <form action={async () => { 'use server'; await signOut({ redirectTo: '/sign-in' }); }}>
      <button>Sign out</button>
    </form>
  </nav>;
}
