import assert from "node:assert/strict";
import test from "node:test";
import { buildLanUrl } from "./lan-url.ts";

test("builds a phone URL from a valid LAN IPv4 address", () => {
  assert.equal(buildLanUrl(" 192.168.0.105 "), "http://192.168.0.105:7955");
});

test("does not expose a loopback, link-local or invalid address", () => {
  assert.equal(buildLanUrl("127.0.0.1"), null);
  assert.equal(buildLanUrl("169.254.1.10"), null);
  assert.equal(buildLanUrl("300.1.1.1"), null);
  assert.equal(buildLanUrl(null), null);
});
