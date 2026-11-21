export function localApiBaseUrl(configuredUrl: string, pageHostname: string, isDevelopment: boolean): string {
  const baseUrl = configuredUrl.replace(/\/+$/, '')
  if (!isDevelopment || !/^https?:\/\//i.test(baseUrl)) return baseUrl

  const url = new URL(baseUrl)
  const loopbackHosts = ['localhost', '127.0.0.1']
  if (loopbackHosts.includes(pageHostname) && loopbackHosts.includes(url.hostname) && url.hostname !== pageHostname) {
    url.hostname = pageHostname
    return url.toString().replace(/\/+$/, '')
  }
  return baseUrl
}
