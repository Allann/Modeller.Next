import NextAuth from 'next-auth';
import GitHub from 'next-auth/providers/github';

function allowedAccountIds(): Set<string> {
  return new Set((process.env.ADMIN_GITHUB_ACCOUNT_IDS ?? '').split(',').map((id) => id.trim()).filter(Boolean));
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  trustHost: true,
  providers: [GitHub],
  callbacks: {
    signIn({ account }) {
      return account?.provider === 'github' && typeof account.providerAccountId === 'string' && allowedAccountIds().has(account.providerAccountId);
    },
    authorized({ auth: session }) { return Boolean(session?.user); },
  },
  pages: { signIn: '/sign-in' },
});
