import { currentPhase, type InitiativeSessionDto } from '@/lib/initiativeTypes';

export function PhaseProgress({ session }: { session: InitiativeSessionDto }) {
  const phase = currentPhase(session);
  return (
    <section aria-label="Phase progress">
      <div className="phase-progress">
        {(['Discover', 'Frame', 'Shape', 'Design'] as const).map((step) => (
          <span key={step} className={step === phase ? 'phase-step phase-step-active' : 'phase-step'}>
            {step}
          </span>
        ))}
      </div>
    </section>
  );
}
