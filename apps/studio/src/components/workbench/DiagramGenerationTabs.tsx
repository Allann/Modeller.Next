'use client';

// Narrow-viewport layout for the generation preview panel (issue #135, extended to local Studio):
// Diagram view and the generation preview share one Panel, switched by a small tab bar. Both
// children stay mounted at all times (toggled with `display: none`, not conditional rendering) so
// switching tabs doesn't tear down and rebuild the Monaco diff editor or the graph canvas — only
// their visibility changes.
export type DiagramGenerationTab = 'diagram' | 'generation';

export function DiagramGenerationTabs({
  active,
  onChange,
  diagram,
  generation,
}: {
  active: DiagramGenerationTab;
  onChange: (tab: DiagramGenerationTab) => void;
  diagram: React.ReactNode;
  generation: React.ReactNode;
}) {
  return (
    <div className="diagram-generation-tabs">
      <div className="diagram-generation-tabbar" role="tablist" aria-label="Diagram and generation preview">
        <button
          type="button"
          role="tab"
          aria-selected={active === 'diagram'}
          className={`diagram-generation-tab${active === 'diagram' ? ' active' : ''}`}
          onClick={() => onChange('diagram')}
        >
          Diagram
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={active === 'generation'}
          className={`diagram-generation-tab${active === 'generation' ? ' active' : ''}`}
          onClick={() => onChange('generation')}
        >
          Generated files
        </button>
      </div>
      <div className="diagram-generation-tab-panel" style={{ display: active === 'diagram' ? 'flex' : 'none' }}>
        {diagram}
      </div>
      <div className="diagram-generation-tab-panel" style={{ display: active === 'generation' ? 'flex' : 'none' }}>
        {generation}
      </div>
    </div>
  );
}
