import test from 'node:test';
import assert from 'node:assert/strict';
import { selectDeployment, previewUrl, isHealthy, verify } from './render-preview-health.mjs';

const sha = 'a'.repeat(40);
const url = 'https://insulinandcoffee-pr-35.onrender.com';
const deployment = { id: 2, sha, performed_via_github_app: { slug: 'render' },
  transient_environment: true, production_environment: false, environment: 'branch - Service PR #35' };
test('only the current commit and native Render PR deployment are selected', () => {
  assert.equal(selectDeployment([{ ...deployment, sha: 'old' }, deployment], sha, 35), deployment);
  assert.equal(selectDeployment([{ ...deployment, performed_via_github_app: { slug: 'vercel' } }], sha, 35), undefined);
  assert.equal(selectDeployment([deployment], sha, 36), undefined);
});
test('reported URL must be the verified service and PR origin', () => {
  assert.equal(previewUrl(url, 'insulinandcoffee', 35), url);
  for (const bad of ['https://evil.example', `${url}.evil.example`, `${url}/wrong`, url.replace('35', '34')])
    assert.throws(() => previewUrl(bad, 'insulinandcoffee', 35));
});
test('health requires 200, Healthy and the served head commit', () => {
  assert.equal(isHealthy(new Response('Healthy', { headers: { 'x-preview-commit': sha } }), 'Healthy', sha), true);
  assert.equal(isHealthy(new Response('Healthy'), 'Healthy', sha), false);
  assert.equal(isHealthy(new Response('Healthy', { headers: { 'x-preview-commit': 'old' } }), 'Healthy', sha), false);
  assert.equal(isHealthy(new Response('', { status: 401 }), '', sha), false);
  assert.equal(isHealthy(new Response('<html>'), '<html>', sha), false);
});

function fixture({ states = ['success'], health = 200, servedSha = sha, closed = false, head = sha, missing = false } = {}) {
  let time = 0;
  let attempts = 0;
  return {
    repository: 'owner/repo', number: 35, sha, token: 'synthetic', slug: 'insulinandcoffee', timeoutSeconds: 40,
    now: () => time, wait: async ms => { time += ms; }, log: () => {},
    fetchImpl: async (target, options) => {
      if (target.endsWith('/health')) {
        assert.equal(options.headers.Authorization, undefined);
        assert.equal(options.redirect, 'error');
        return new Response('Healthy', { status: health, headers: { 'x-preview-commit': servedSha } });
      }
      let body;
      if (target.includes('/pulls/')) body = { state: closed ? 'closed' : 'open', base: { ref: 'stage' }, head: { sha: head } };
      else if (target.includes('/statuses')) body = [{ state: states[Math.min(attempts++, states.length - 1)], environment_url: url }];
      else body = missing ? [] : [deployment];
      return new Response(JSON.stringify(body));
    }
  };
}
test('deploying waits, then ready succeeds without redeploying', async () => {
  assert.equal(await verify(fixture({ states: ['in_progress', 'success'] })), url);
});
test('failed deployment fails clearly', async () => {
  await assert.rejects(verify(fixture({ states: ['failure'] })), /deployment is failure/);
});
test('missing deployment, bad health and stale served commit time out', async () => {
  for (const config of [{ missing: true }, { health: 401 }, { servedSha: 'old' }])
    await assert.rejects(verify(fixture(config)), /timed out/);
});
test('closed PR and subsequent commit cannot pass an obsolete check', async () => {
  await assert.rejects(verify(fixture({ closed: true })), /closed.*superseded/);
  await assert.rejects(verify(fixture({ head: 'new' })), /superseded/);
});
