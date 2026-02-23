import { useState, useEffect, useRef, useCallback } from 'react';

interface UseVerticalResizeOptions {
  initialHeight: number;
  minHeight: number;
  maxHeight: number;
  storageKey?: string;
  direction?: 'down' | 'up';
}

export function useVerticalResize({
  initialHeight,
  minHeight,
  maxHeight,
  storageKey,
  direction = 'down',
}: UseVerticalResizeOptions) {
  const [height, setHeight] = useState(() => {
    if (storageKey) {
      const saved = localStorage.getItem(storageKey);
      if (saved) {
        const parsed = parseInt(saved, 10);
        return Math.max(minHeight, Math.min(maxHeight, parsed));
      }
    }
    return initialHeight;
  });

  const isDragging = useRef(false);
  const startY = useRef(0);
  const startHeight = useRef(0);

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    isDragging.current = true;
    startY.current = e.clientY;
    startHeight.current = height;
    e.preventDefault();
  }, [height]);

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isDragging.current) return;
      const delta = direction === 'down'
        ? e.clientY - startY.current
        : startY.current - e.clientY;
      const newHeight = Math.max(minHeight, Math.min(maxHeight, startHeight.current + delta));
      setHeight(newHeight);
      if (storageKey) {
        localStorage.setItem(storageKey, String(newHeight));
      }
    };

    const handleMouseUp = () => {
      isDragging.current = false;
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [minHeight, maxHeight, storageKey, direction]);

  return { height, handleMouseDown };
}
