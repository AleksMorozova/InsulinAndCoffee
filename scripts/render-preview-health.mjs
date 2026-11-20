import { readFileSync, appendFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';

export function selectDeployment(deployments, sha, number) {
  return deployments.filter(d => d.sha === sha && d.performed_via_github_app?.slug === 'render'
    && d.transient_environment && !d.production_environment
    && d.environment.endsWith(` PR #${number}`))
    .sort((a, b) => b.id - a.id)[0];
}

export function previewUrl(value, slug, number) {
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug)) throw new Error('Invalid Render service slug.');
  const url = new URL(value);
  if (url.origin !== `https://${slug}-pr-${number}.onrender.com`
      || url.username || url.password || url.pathname !== '/' || url.search || url.hash)
    throw new Error('Render reported an unexpected preview URL.');
  return url.origin;
}

export function isHealthy(response, body, sha) {
  return response.status === 200 && body.trim() === 'Healthy'
    && response.headers.get('x-preview-commit') === sha;
}

export async function verify({ repository, number, sha, token, slug, timeoutSeconds,
  fetchImpl = fetch, now = Date.now, wait = sleep, log = console.log }) {
  if (!Number.isInteger(timeoutSeconds) || timeoutSeconds < 1 || timeoutSeconds > 1800)
    throw new Error('PREVIEW_TIMEOUT_SECONDS must be between 1 and 1800.');
  const deadline = now() + timeoutSeconds * 1000;
  let diagnostic = 'Waiting for a Render deployment for the PR head SHA.';
  let url;
  const json = async path => {
    const response = await fetchImpl(`https://api.github.com/repos/${repository}${path}`, {
      headers: { Authorization: `Bearer ${token}`, Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28' },
      signal: AbortSignal.timeout(Math.max(1, Math.min(15000, deadline - now()))), redirect: 'error'
    });
    if (!response.ok) throw new Error(`GitHub metadata request failed: HTTP ${response.status}.`);
    return response.json();
  };
  while (now() < deadline) {
    const pr = await json(`/pulls/${number}`);
    if (pr.state !== 'open' || pr.base.ref !== 'stage' || pr.head.sha !== sha)
      throw new Error('PR is closed, retargeted, or superseded by a newer commit.');
    const deployment = selectDeployment(await json(`/deployments?sha=${sha}&per_page=100`), sha, number);
    if (deployment) {
      const [status] = await json(`/deployments/${deployment.id}/statuses?per_page=1`);
      if (status?.environment_url) url = previewUrl(status.environment_url, slug, number);
      if (status && ['failure', 'error', 'inactive'].includes(status.state))
        throw new Error(`Native Render deployment is ${status.state}. Inspect Render deploy logs.${url ? ` Preview: ${url}` : ''}`);
      diagnostic = `Native Render deployment: ${status?.state ?? 'pending'}.`;
      if (status?.state === 'success') {
        url = previewUrl(status.environment_url, slug, number);
        try {
          // Never forward the GitHub token to the preview or follow redirects.
          const response = await fetchImpl(`${url}/health`, {
            headers: { 'Cache-Control': 'no-cache' }, redirect: 'error',
            signal: AbortSignal.timeout(Math.max(1, Math.min(30000, deadline - now())))
          });
          const body = await response.text();
          if (isHealthy(response, body, sha)) return url;
          diagnostic = `Health HTTP ${response.status}; expected 200 Healthy and the current commit header.`;
        } catch {
          diagnostic = 'Preview health request timed out or could not connect; retrying.';
        }
      }
    }
    log(diagnostic);
    await wait(Math.max(0, Math.min(15000, deadline - now())));
  }
  throw new Error(`Render preview timed out after ${timeoutSeconds}s. ${diagnostic}${url ? ` Preview: ${url}` : ''}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  try {
    const url = await verify({ repository: process.env.GITHUB_REPOSITORY,
      number: event.pull_request.number, sha: event.pull_request.head.sha,
      token: process.env.GH_TOKEN, slug: process.env.RENDER_SERVICE_SLUG,
      timeoutSeconds: Number(process.env.PREVIEW_TIMEOUT_SECONDS || 1200) });
    appendFileSync(process.env.GITHUB_STEP_SUMMARY,
      `Render Preview / Health Check passed\n\nPreview: ${url}\n\nCommit: \`${event.pull_request.head.sha}\`\n\nHTTP 200 Healthy; served commit verified.\n`);
  } catch (error) {
    console.error(error.message);
    appendFileSync(process.env.GITHUB_STEP_SUMMARY, `Render Preview / Health Check failed\n\n${error.message}\n`);
    process.exitCode = 1;
  }
}
