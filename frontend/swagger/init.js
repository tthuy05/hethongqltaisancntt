// Local distribution only. No online validator, persisted token or external API target.
window.SwaggerUIBundle({
  url: '/openapi/v1.json',
  dom_id: '#swagger-ui',
  presets: [window.SwaggerUIBundle.presets.apis],
  layout: 'BaseLayout',
  deepLinking: true,
  docExpansion: 'list',
  defaultModelsExpandDepth: -1,
  displayRequestDuration: true,
  persistAuthorization: false,
  queryConfigEnabled: false,
  validatorUrl: null,
  withCredentials: false,
  requestInterceptor(request) {
    const url = new URL(request.url, window.location.origin);
    if (url.origin !== window.location.origin) throw new Error('Swagger chỉ được gọi API cùng origin.');
    return request;
  },
});
