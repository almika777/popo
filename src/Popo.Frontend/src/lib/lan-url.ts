const ipv4Pattern = /^(\d{1,3}\.){3}\d{1,3}$/;

export function buildLanUrl(host: string | null | undefined): string | null {
  const normalizedHost = host?.trim();
  if (!normalizedHost || !ipv4Pattern.test(normalizedHost)) {
    return null;
  }

  const octets = normalizedHost.split(".").map(Number);
  if (octets.some((octet) => !Number.isInteger(octet) || octet < 0 || octet > 255)) {
    return null;
  }

  if (
    octets[0] === 0 ||
    octets[0] === 127 ||
    (octets[0] === 169 && octets[1] === 254)
  ) {
    return null;
  }

  return `http://${normalizedHost}:7955`;
}
