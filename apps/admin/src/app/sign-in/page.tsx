import { signIn } from '@/auth';
export default function SignInPage() {
  return <main className="sign-in"><h1>Modeller Admin</h1><p>This dashboard is private.</p><form action={async () => { 'use server'; await signIn('github', { redirectTo: '/' }); }}><button type="submit">Sign in with GitHub</button></form></main>;
}
