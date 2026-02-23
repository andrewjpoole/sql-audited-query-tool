import { useState, useEffect, useRef, useCallback } from 'react';

interface UseHorizontalResizeOptions {
  initialWidth: number;
  minWidth: number;
  maxWidth: number;
  storageKey?: string;
  direction?: 'right' | 'left';
}

export function useHorizontalResize({
  initialWidth,
  minWidth,
  maxWidth,
  storageKey,
  direction = 'right',
}: UseHorizontalResizeOptions) {
  const [width, setWidth] = useState(() => {
    if (storageKey) {
      const saved = localStorage.getItem(storageKey);
      if (saved) {
        const parsed = parseInt(saved, 10);
        return Math.max(minWidth, Math.min(maxWidth, parsed));
      }
    }
    return initialWidth;
  });

  const isDragging = useRef(false);
  const startX = useRef(0);
  const startWidth = useRef(0);

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    isDragging.current = true;
    startX.current = e.clientX;
    startWidth.current = width;
    e.preventDefault();
  }, [width]);

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isDragging.current) return;
      const delta = direction === 'right' 
        ? e.clientX - startX.current 
        : startX.current - e.clientX;
      const newWidth = Math.max(minWidth, Math.min(maxWidth, startWidth.current + delta));
      setWidth(newWidth);
      if (storageKey) {
        localStorage.setItem(storageKey, String(newWidth));
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
  }, [minWidth, maxWidth, storageKey, direction]);

  return { width, handleMouseDown };
}
