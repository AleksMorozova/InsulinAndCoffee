import test from 'node:test';
import assert from 'node:assert/strict';
import { resolveApiUrl } from './resolve-api-url.mjs';

test('production and existing explicit API URL are preserved', () => {
  assert.equal(resolveApiUrl({}), 'https://insulinandcoffee.onrender.com/api');
  assert.equal(resolveApiUrl({ VERCEL_ENV: 'production', API_URL: 'https://existing.example/api',
    RENDER_PREVIEW_SERVICE_SLUG: 'insulinandcoffee', VERCEL_GIT_PULL_REQUEST_ID: '35' }), 'https://existing.example/api');
});
test('opted-in preview resolves its own PR number instead of inherited production URL', () => {
  for (const number of ['35', '36']) {
    assert.equal(resolveApiUrl({ VERCEL_ENV: 'preview', RENDER_PREVIEW_SERVICE_SLUG: 'insulinandcoffee',
      VERCEL_GIT_PULL_REQUEST_ID: number, API_URL: 'https://production.example/api' }),
    `https://insulinandcoffee-pr-${number}.onrender.com/api`);
  }
});
test('opted-in preview fails clearly without a valid PR or service slug', () => {
  assert.throws(() => resolveApiUrl({ VERCEL_ENV: 'preview', RENDER_PREVIEW_SERVICE_SLUG: 'insulinandcoffee' }));
  assert.throws(() => resolveApiUrl({ VERCEL_ENV: 'preview', RENDER_PREVIEW_SERVICE_SLUG: 'https://evil', VERCEL_GIT_PULL_REQUEST_ID: '35' }));
});
