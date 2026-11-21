// Use real browser viewport/media/container queries without depending on the host window's minimum width.
export async function inResponsiveFrame(host: HTMLElement, check: (viewport: Window) => void): Promise<void> {
  const parent = host.parentNode;
  const next = host.nextSibling;
  const frame = document.createElement('iframe');
  frame.title = 'Responsive layout test';
  frame.style.cssText = 'position:fixed;left:0;top:0;width:390px;height:844px;border:0;opacity:0;pointer-events:none';
  const loaded = new Promise<void>(resolve => frame.onload = () => resolve());
  frame.srcdoc = '<!doctype html><html><head></head><body></body></html>';
  document.body.appendChild(frame);
  try {
    await loaded;
    const viewport = frame.contentWindow;
    const target = frame.contentDocument;
    if (!viewport || !target) throw new Error('Responsive test frame unavailable');
    const styles: Promise<void>[] = [];
    for (const source of Array.from(document.head.querySelectorAll('style,link[rel="stylesheet"]'))) {
      if (source instanceof HTMLLinkElement) {
        const link = target.createElement('link'); link.rel = 'stylesheet'; link.href = source.href;
        styles.push(new Promise<void>((resolve, reject) => { link.onload = () => resolve(); link.onerror = () => reject(new Error('Responsive test stylesheet failed')); }));
        target.head.appendChild(link);
      } else target.head.appendChild(source.cloneNode(true));
    }
    await Promise.all(styles);
    target.body.appendChild(host);
    for (const [width, height] of [[1440, 1000], [1024, 900], [390, 844]]) {
      frame.style.width = `${width}px`; frame.style.height = `${height}px`;
      await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      expect(viewport.innerWidth).toBe(width);
      check(viewport);
    }
  } finally {
    if (parent) parent.insertBefore(host, next);
    frame.remove();
  }
}
