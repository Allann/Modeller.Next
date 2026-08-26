'use client';

// Drives the generation preview panel's tab-vs-split breakpoint (issue #135). Deliberately measures
// a specific element's own rendered width via ResizeObserver rather than the viewport (matchMedia
// on `(min-width: ...)`, or raw `window.innerWidth`) — a user can have a narrower devtools-docked
// window on a big monitor, and the thing that actually needs to fit two panels side by side is the
// panel group, not the browser window.
import { useEffect, useState, type RefObject } from 'react';

export function useElementWidthBreakpoint(elementRef: RefObject<HTMLElement | null>, thresholdPx: number): boolean {
  const [isAtOrAboveThreshold, setIsAtOrAboveThreshold] = useState(false);

  useEffect(() => {
    const element = elementRef.current;
    if (!element) return;

    const update = (width: number) => setIsAtOrAboveThreshold(width >= thresholdPx);
    update(element.getBoundingClientRect().width);

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) update(entry.contentRect.width);
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [elementRef, thresholdPx]);

  return isAtOrAboveThreshold;
}
