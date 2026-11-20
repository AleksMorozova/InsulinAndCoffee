export function resolveApiUrl(env) {
  if (env.VERCEL_ENV === 'preview' && env.RENDER_PREVIEW_SERVICE_SLUG) {
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(env.RENDER_PREVIEW_SERVICE_SLUG))
      throw new Error('Invalid RENDER_PREVIEW_SERVICE_SLUG.');
    if (!/^[1-9][0-9]*$/.test(env.VERCEL_GIT_PULL_REQUEST_ID || ''))
      throw new Error('Preview API routing requires VERCEL_GIT_PULL_REQUEST_ID. Open a PR and redeploy.');
    return `https://${env.RENDER_PREVIEW_SERVICE_SLUG}-pr-${env.VERCEL_GIT_PULL_REQUEST_ID}.onrender.com/api`;
  }
  return env.API_URL || 'https://insulinandcoffee.onrender.com/api';
}
