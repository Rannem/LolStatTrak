export const environment = {
  production: true,
  // Production: Caddy reverse-proxies /api and /hubs to the backend over Railway's private network.
  apiBaseUrl: '/api',
  hubBaseUrl: '/hubs',
};
