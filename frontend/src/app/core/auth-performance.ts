// Local browser diagnostics only. Fixed names and numeric/status data, never identities or URLs.
// Keep only the latest measurement of each stage to bound memory across repeated sign-ins.
export function recordAuthTiming(stage: string, start: number, outcome: string): void {
  const name = `iac.auth.${stage}`;
  performance.clearMeasures(name);
  performance.measure(name, { start, duration: performance.now() - start, detail: { outcome } });
}
