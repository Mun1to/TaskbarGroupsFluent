/**
 * Tells the search engines the site changed, without waiting for them to come by.
 *
 *   node site/_herramientas/avisar-buscadores.mjs
 *
 * Uses IndexNow, the protocol shared by Bing, Yandex, Naver, Seznam and the rest of
 * indexnow.org/searchengines.json: it sends the list of URLs and the key published on the
 * site itself, and they check the key exists before taking notice. Google does NOT use
 * IndexNow: that one goes through Search Console.
 *
 * The key is public by design (it sits in a URL) and its file is site/<key>.txt. If that
 * file is deleted, IndexNow stops accepting the notices.
 *
 * It is the last step of .github/workflows/pages.yml, after the deploy, and that step has
 * continue-on-error: a failure here never stops a release of the site.
 */
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const KEY = "6038864393be12d5f983ae93030e9538";

const site = join(dirname(fileURLToPath(import.meta.url)), "..");
const domain = readFileSync(join(site, "CNAME"), "utf8").trim();

// The URLs come from the sitemap, which is already the good list. Writing them here again
// means the day a page is added, this script notifies too little and still says it went well.
const sitemap = readFileSync(join(site, "sitemap.xml"), "utf8");
const urls = [...sitemap.matchAll(/<loc>([^<]+)<\/loc>/g)].map(([, u]) => u.trim());
if (!urls.length) {
  console.error("sitemap.xml has no URLs.");
  process.exit(1);
}
const foreign = urls.filter((u) => !u.startsWith(`https://${domain}/`));
if (foreign.length) {
  console.error(`The sitemap has URLs from another domain: ${foreign.join(", ")}`);
  process.exit(1);
}

const body = {
  host: domain,
  key: KEY,
  keyLocation: `https://${domain}/${KEY}.txt`,
  urlList: urls,
};

// First check the key is published, because otherwise the notice is rejected without saying
// why. GitHub Pages can take a few seconds to serve a fresh deploy, so it retries for a while.
let published = "";
for (let attempt = 1; attempt <= 12; attempt++) {
  try {
    const r = await fetch(`${body.keyLocation}?t=${Date.now()}`, { signal: AbortSignal.timeout(15000) });
    if (r.ok) published = (await r.text()).trim();
  } catch {}
  if (published === KEY) break;
  await new Promise((done) => setTimeout(done, 10000));
}
if (published !== KEY) {
  console.error(`The key is not published at ${body.keyLocation}. Deploy the site first and try again.`);
  process.exit(1);
}

const response = await fetch("https://api.indexnow.org/indexnow", {
  method: "POST",
  headers: { "Content-Type": "application/json; charset=utf-8" },
  body: JSON.stringify(body),
  signal: AbortSignal.timeout(30000),
});

// 200 and 202 both mean "received"; 202 means the key is still being validated.
// Neither means "indexed".
if (response.status === 200 || response.status === 202) {
  console.log(`Notified ${urls.length} URLs (response ${response.status}).`);
  for (const u of urls) console.log("  " + u);
} else {
  console.error(`IndexNow answered ${response.status}: ${await response.text()}`);
  process.exit(1);
}
