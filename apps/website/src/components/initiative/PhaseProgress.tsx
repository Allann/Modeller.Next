export type CockpitStep = 'DiscoverFrame' | 'Shape' | 'Finalize';

const steps: { id: CockpitStep; label: string }[] = [
  { id: 'DiscoverFrame', label: '1. Discover & Frame' },
  { id: 'Shape', label: '2. Shape' },
  { id: 'Finalize', label: '3. Finalize' },
];

export function PhaseProgress({ activeStep, onSelect }: { activeStep: CockpitStep; onSelect: (step: CockpitStep) => void }) {
  return (
    <nav aria-label="Initiative steps">
      <div className="phase-progress">
        {steps.map((step) => (
          <button
            type="button"
            key={step.id}
            className={step.id === activeStep ? 'phase-step phase-step-active' : 'phase-step'}
            aria-current={step.id === activeStep ? 'step' : undefined}
            onClick={() => onSelect(step.id)}
          >
            {step.label}
          </button>
        ))}
      </div>
    </nav>
  );
}
