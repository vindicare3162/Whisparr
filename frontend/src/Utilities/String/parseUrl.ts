import qs from 'qs';

// See: https://developer.mozilla.org/en-US/docs/Web/API/HTMLHyperlinkElementUtils
const anchor = document.createElement('a');

interface ParsedUrl {
  hash: string;
  host: string;
  hostname: string;
  href: string;
  origin: string;
  pathname: string;
  port: string;
  protocol: string;
  search: string;
  isAbsolute: boolean;
  params: Record<string, string>;
}

export default function parseUrl(url: string): ParsedUrl {
  anchor.href = url;

  // The `origin`, `password`, and `username` properties are unavailable in
  // Opera Presto. We synthesize `origin` if it's not present. While `password`
  // and `username` are ignored intentionally.
  const properties: ParsedUrl = {
    hash: anchor.hash,
    host: anchor.host,
    hostname: anchor.hostname,
    href: anchor.href,
    origin: anchor.origin,
    pathname: anchor.pathname,
    port: anchor.port,
    protocol: anchor.protocol,
    search: anchor.search,
    isAbsolute: /^[\w:]*\/\//.test(url),
    params: {},
  };

  if (properties.search) {
    // Remove leading ? from querystring before parsing.
    properties.params = qs.parse(properties.search.substring(1)) as Record<
      string,
      string
    >;
  }

  return properties;
}
