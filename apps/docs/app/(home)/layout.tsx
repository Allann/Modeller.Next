import { HomeLayout } from 'fumadocs-ui/layouts/home';
import { baseOptions } from '@/lib/layout.shared';

export default function Layout({ children }: LayoutProps<'/'>) {
  return (
    <HomeLayout
      {...baseOptions()}
      nav={{ title: 'Modeller', transparentMode: 'top' }}
      links={[
        { text: 'Why Modeller', url: '/#why-modeller' },
        { text: 'The journey', url: '/#journey' },
        { text: 'System Design', url: '/#system-design' },
        { text: 'Docs', url: '/docs' },
        { text: 'Playground', url: 'https://modeller.website' },
        { type: 'button', text: 'Get started', url: '/docs/getting-started' },
      ]}
    >
      {children}
    </HomeLayout>
  );
}
