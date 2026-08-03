'use client';

import { useEffect, useRef, useState } from 'react';

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
  const scrollerRef = useRef<HTMLDivElement>(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);

  useEffect(() => {
    const scroller = scrollerRef.current;
    if (!scroller) return;

    const updateScrollState = () => {
      setCanScrollLeft(scroller.scrollLeft > 0);
      setCanScrollRight(scroller.scrollLeft + scroller.clientWidth < scroller.scrollWidth - 1);
    };

    updateScrollState();
    scroller.addEventListener('scroll', updateScrollState);
    // Tabs opening/closing changes scrollWidth without necessarily firing a
    // scroll event, so a size observer is needed too, not just the listener.
    const resizeObserver = new ResizeObserver(updateScrollState);
    resizeObserver.observe(scroller);

    return () => {
      scroller.removeEventListener('scroll', updateScrollState);
      resizeObserver.disconnect();
    };
  }, [openPaths]);

  const scrollBy = (delta: number) => {
    scrollerRef.current?.scrollBy({ left: delta, behavior: 'smooth' });
  };

  return (
    <div className="tabbar-wrap">
      {canScrollLeft && (
        <button className="tabbar-scroll-btn left" aria-label="Scroll tabs left" onClick={() => scrollBy(-160)}>
          ‹
        </button>
      )}
      <div className="tabbar" ref={scrollerRef}>
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
      {canScrollRight && (
        <button className="tabbar-scroll-btn right" aria-label="Scroll tabs right" onClick={() => scrollBy(160)}>
          ›
        </button>
      )}
    </div>
  );
}
