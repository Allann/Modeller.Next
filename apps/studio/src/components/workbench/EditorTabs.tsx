'use client';

export function EditorTabs({
  openPaths,
  activePath,
  onSelect,
  onClose,
}: {
  openPaths: string[];
  activePath: string | undefined;
  onSelect: (path: string) => void;
  onClose: (path: string) => void;
}) {
  return (
    <div className="tabbar">
      {openPaths.map((path) => (
        <div key={path} className={`tab${path === activePath ? ' active' : ''}`} onClick={() => onSelect(path)}>
          <span>{path.split('/').pop()}</span>
          <button
            aria-label={`Close ${path}`}
            onClick={(event) => {
              event.stopPropagation();
              onClose(path);
            }}
          >
            ×
          </button>
        </div>
      ))}
    </div>
  );
}
