/**
 * El banco de pruebas de la web: la sirve en local, la abre en un Chromium propio (el de
 * Playwright, nunca el navegador de nadie) y repasa lo que se rompe sin avisar.
 *
 *   cd site/_herramientas && pnpm install && pnpm exec playwright install chromium
 *   node site/_herramientas/probar-web.mjs            todo
 *   node site/_herramientas/probar-web.mjs --sin-red  sin comprobar los enlaces de fuera
 *
 * Lo que mira, en cada página y en los dos idiomas, a 1920, 1440, un iPhone 13 y 360 de ancho,
 * en claro y en oscuro: errores en la consola, peticiones que fallan, que nada se salga a lo
 * ancho, imágenes que no cargan y, en el móvil, que lo que se toca mida 44 px. Y además: la demo
 * (pasar, clic, clic derecho, Escape), la demo a pantalla completa en el móvil con Atrás, la
 * comparativa por pestañas, las preguntas, el menú del móvil, la válvula del movimiento, el
 * idioma guardado, el hreflang y el canonical de cada página, los enlaces internos, la 404,
 * la dirección vieja de la privacidad, lo que pesa la portada y su LCP y CLS, y que los
 * enlaces de descarga lleven a algo que existe. Sale con 1 si algo falla. Es el de winshotx.com.
 */
import { createServer } from "node:http";
import { readFile, stat, readdir } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, extname, join, normalize, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium, devices } from "playwright";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");
const SIN_RED = process.argv.includes("--sin-red");
const fallos = [];
const mal = (que) => { fallos.push(que); console.log(`  ✗ ${que}`); };
const bien = (que) => console.log(`  ✓ ${que}`);

/* --------------------------------------------------------------- el servidor, como Pages */
const TIPOS = { ".html": "text/html; charset=utf-8", ".css": "text/css", ".js": "text/javascript", ".json": "application/json",
  ".png": "image/png", ".webp": "image/webp", ".mp4": "video/mp4", ".svg": "image/svg+xml", ".txt": "text/plain; charset=utf-8", ".xml": "application/xml" };
const servidor = createServer(async (pet, res) => {
  let ruta = decodeURIComponent(new URL(pet.url, "http://x").pathname);
  if (ruta.includes("_herramientas")) ruta = "/no-existe";
  let archivo = normalize(join(raiz, ruta));
  if (!archivo.startsWith(raiz)) { res.writeHead(403).end(); return; }
  try { if ((await stat(archivo)).isDirectory()) archivo = join(archivo, "index.html"); } catch {}
  if (!existsSync(archivo)) {
    res.writeHead(404, { "content-type": TIPOS[".html"] }).end(await readFile(join(raiz, "404.html")));
    return;
  }
  const datos = await readFile(archivo);
  const tipo = TIPOS[extname(archivo)] ?? "application/octet-stream";
  // El vídeo se pide a trozos: sin Range, Chromium corta la descarga y lo apunta como fallo.
  const rango = /bytes=(\d+)-(\d*)/.exec(pet.headers.range ?? "");
  if (rango) {
    const ini = Number(rango[1]), fin = rango[2] ? Number(rango[2]) : datos.length - 1;
    res.writeHead(206, { "content-type": tipo, "content-range": `bytes ${ini}-${fin}/${datos.length}`, "accept-ranges": "bytes", "content-length": fin - ini + 1 });
    res.end(datos.subarray(ini, fin + 1));
    return;
  }
  res.writeHead(200, { "content-type": tipo, "accept-ranges": "bytes" }).end(datos);
});
await new Promise((ok) => servidor.listen(0, "127.0.0.1", ok));
const BASE = `http://127.0.0.1:${servidor.address().port}/`;
console.log(`Sirviendo ${raiz} en ${BASE}`);

const PAGINAS = ["", "en/", "novedades/", "en/changelog/", "soporte/", "en/support/", "privacidad/", "en/privacy/"];
const ANCHOS = [
  { nombre: "1920", viewport: { width: 1920, height: 1080 } },
  { nombre: "1440", viewport: { width: 1440, height: 900 } },
  { nombre: "iPhone 13", ...devices["iPhone 13"] },
  { nombre: "360", viewport: { width: 360, height: 780 }, isMobile: true, hasTouch: true, deviceScaleFactor: 2 },
];
const navegador = await chromium.launch();

/* ------------------------------------------------------------ cada página, cada ancho y tema */
console.log("\nPáginas, anchos y temas");
for (const ancho of ANCHOS) {
  for (const tema of ["light", "dark"]) {
    const { nombre, ...opciones } = ancho;
    const ctx = await navegador.newContext({ ...opciones, colorScheme: tema, locale: "es-ES" });
    // Sin idioma guardado y sin la frase del otro idioma: cada página se mira tal cual.
    await ctx.addInitScript(() => { try { localStorage.removeItem("tbgf-lang"); } catch {} });
    for (const pagina of PAGINAS) {
      const page = await ctx.newPage();
      const donde = `/${pagina} a ${nombre} en ${tema === "light" ? "claro" : "oscuro"}`;
      const errores = [];
      page.on("console", (m) => { if (m.type() === "error") errores.push(m.text()); });
      page.on("pageerror", (e) => errores.push(e.message));
      // El vídeo se descarga a trozos y Chromium corta el que queda a medias al cerrar la página:
      // eso no es un fallo. Lo demás que no llega, sí.
      page.on("requestfailed", (r) => {
        if (r.url().startsWith("https://api.github.com")) return;
        if (/\.mp4(\?|$)/.test(r.url()) && r.failure()?.errorText === "net::ERR_ABORTED") return;
        errores.push(`falla ${r.url()} ${r.failure()?.errorText ?? ""}`);
      });
      page.on("response", (r) => { if (r.status() >= 400 && r.url().startsWith(BASE)) errores.push(`${r.status()} ${r.url()}`); });
      await page.goto(BASE + pagina, { waitUntil: "load" });
      await page.waitForTimeout(400);
      const r = await page.evaluate((movil) => {
        const fuera = document.documentElement.scrollWidth - innerWidth;
        const rotas = [...document.images].filter((i) => i.complete && i.naturalWidth === 0 && i.getBoundingClientRect().width > 0).map((i) => i.src);
        const sinAlt = [...document.images].filter((i) => !i.hasAttribute("alt")).map((i) => i.src);
        const chicos = !movil ? [] : [...document.querySelectorAll("a, button, summary, label.pestana")].filter((e) => {
          const b = e.getBoundingClientRect();
          if (!b.width || getComputedStyle(e).visibility === "hidden" || e.closest("[inert], .escritorio")) return false;
          // Un enlace dentro de una frase es texto, no un botón (WCAG 2.5.8, «inline»).
          if (e.tagName === "A" && e.closest("p, li, .prueba span, .pie-abajo")) return false;
          return b.height < 43.5;
        }).map((e) => `${e.tagName.toLowerCase()} «${e.textContent.trim().slice(0, 24)}» ${Math.round(e.getBoundingClientRect().height)} px`);
        return { fuera, rotas, sinAlt, chicos };
      }, Boolean(opciones.isMobile));
      if (errores.length) mal(`${donde}: ${errores.join(" | ")}`);
      if (r.fuera > 0) mal(`${donde}: se sale ${r.fuera} px a lo ancho`);
      if (r.rotas.length) mal(`${donde}: imágenes que no cargan: ${r.rotas.join(", ")}`);
      if (r.sinAlt.length) mal(`${donde}: imágenes sin alt: ${r.sinAlt.join(", ")}`);
      if (r.chicos.length) mal(`${donde}: menos de 44 px para el dedo: ${r.chicos.slice(0, 6).join(", ")}`);
      await page.close();
    }
    await ctx.close();
    bien(`${nombre} en ${tema === "light" ? "claro" : "oscuro"}: ${PAGINAS.length} páginas repasadas`);
  }
}

/* ------------------------------------------------------------------------------ la demo */
console.log("\nLa demo");
{
  const ctx = await navegador.newContext({ viewport: { width: 1440, height: 900 }, locale: "es-ES" });
  const page = await ctx.newPage();
  await page.goto(BASE, { waitUntil: "load" });
  await page.locator("#pruebalo").scrollIntoViewIfNeeded();
  await page.waitForTimeout(1200); // el primer grupo se abre solo a los 900 ms
  const visible = () => page.evaluate(() => document.getElementById("panel").classList.contains("visible"));
  if (await visible()) bien("el primer grupo se abre solo al llegar"); else mal("la demo no abre el primer grupo al llegar");
  await page.locator(".bt.grupo").nth(2).hover();
  await page.waitForTimeout(500);
  const nombre = await page.locator("#panel-nombre").textContent();
  if (nombre === "Diseño") bien("pasar el ratón abre el grupo de debajo"); else mal(`pasar el ratón abrió «${nombre}», no «Diseño»`);
  await page.locator('[data-op="forma"][data-val="lista"]').click();
  if (await page.locator("#panel-apps.t-celda").count()) bien("la pestaña «Lista» cambia el panel"); else mal("la pestaña «Lista» no cambia el panel");
  await page.locator(".bt.grupo").first().click({ button: "right" });
  await page.waitForTimeout(300);
  if (await page.locator("#salto.visible").count()) bien("el clic derecho saca el menú del grupo"); else mal("el clic derecho no saca el menú");
  await page.keyboard.press("Escape");
  await page.waitForTimeout(300);
  if (!(await page.locator("#salto.visible").count()) && !(await visible())) bien("Escape lo cierra todo"); else mal("Escape no cierra el menú o el panel");
  await ctx.close();
}

/* ------------------------------------------------------ la demo a pantalla completa, móvil */
{
  const ctx = await navegador.newContext({ ...devices["iPhone 13"], locale: "es-ES" });
  const page = await ctx.newPage();
  await page.goto(BASE, { waitUntil: "load" });
  await page.locator('[data-capa="abrir"]').scrollIntoViewIfNeeded();
  await page.locator('[data-capa="abrir"]').tap();
  await page.waitForTimeout(500);
  const abierta = await page.evaluate(() => document.documentElement.classList.contains("capa-abierta"));
  const k = await page.evaluate(() => parseFloat(getComputedStyle(document.getElementById("pantalla")).getPropertyValue("--escala")));
  if (abierta && k >= 0.6) bien(`en el móvil se abre a pantalla completa, a ${k} (se toca con el dedo)`); else mal(`la pantalla completa del móvil: abierta ${abierta}, escala ${k}`);
  await page.locator(".bt.grupo").nth(1).tap();
  await page.waitForTimeout(500);
  if (await page.locator("#panel.visible").count()) bien("tocar un grupo lo abre"); else mal("tocar un grupo en la pantalla completa no lo abre");
  await page.goBack();
  await page.waitForTimeout(400);
  const cerrada = await page.evaluate(() => !document.documentElement.classList.contains("capa-abierta") && location.pathname === "/");
  if (cerrada) bien("Atrás la cierra y se queda en la portada"); else mal("Atrás no cierra la pantalla completa");
  await ctx.close();
}

/* ------------------------------------------------- comparativa, preguntas, menú y válvula */
console.log("\nComparativa, preguntas, menú, válvula e idioma");
{
  const ctx = await navegador.newContext({ viewport: { width: 1440, height: 900 }, locale: "es-ES" });
  const page = await ctx.newPage();
  await page.goto(BASE, { waitUntil: "load" });
  const pestanas = await page.locator('.rivales input[name="rival"]').evaluateAll((l) => l.map((i) => i.value));
  let todas = true;
  for (const valor of pestanas) {
    await page.locator(`.rivales input[value="${valor}"]`).check({ force: true });
    const vistos = await page.evaluate(() => [...document.querySelectorAll(".frente")].filter((f) => getComputedStyle(f).display !== "none").map((f) => f.id));
    if (vistos.length !== 1 || vistos[0] !== `frente-${valor}`) { todas = false; mal(`la pestaña «${valor}» enseña ${vistos.join(", ") || "nada"}`); }
  }
  if (todas) bien(`las ${pestanas.length} pestañas de la comparativa enseñan cada una la suya`);
  await page.locator("#preguntas summary").first().click();
  await page.waitForTimeout(500);
  if (await page.locator("#preguntas details[data-abierta]").count()) bien("una pregunta se abre"); else mal("las preguntas no se abren");
  await page.goto(BASE + "?motion=off", { waitUntil: "load" });
  const gain = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--motion-gain").trim());
  if (gain === "0") bien("?motion=off deja el dial a 0"); else mal(`?motion=off deja el dial a ${gain}`);
  await ctx.close();

  const movil = await navegador.newContext({ viewport: { width: 360, height: 780 }, isMobile: true, hasTouch: true, locale: "es-ES" });
  const m = await movil.newPage();
  await m.goto(BASE, { waitUntil: "load" });
  await m.locator("details.menu summary").tap();
  await m.waitForTimeout(600);
  if (await m.locator("nav.barra.menu-abierto").count()) bien("el menú del móvil crece de la barra"); else mal("el menú del móvil no se abre");
  await movil.close();

  // Lo elegido se respeta: con «en» guardado, la portada española lleva a la inglesa. Sin nada
  // guardado no se redirige nunca, aunque el navegador esté en inglés.
  const ingles = await navegador.newContext({ locale: "en-US" });
  const p = await ingles.newPage();
  await p.goto(BASE, { waitUntil: "load" });
  if (new URL(p.url()).pathname === "/") bien("un navegador en inglés sin elección no se redirige"); else mal(`un navegador en inglés se redirigió a ${p.url()}`);
  await p.evaluate(() => localStorage.setItem("tbgf-lang", "en"));
  await p.goto(BASE + "soporte/", { waitUntil: "load" });
  await p.waitForTimeout(300);
  if (new URL(p.url()).pathname === "/en/support/") bien("con «en» guardado, /soporte/ lleva a /en/support/"); else mal(`con «en» guardado, /soporte/ acabó en ${p.url()}`);
  await ingles.close();
}

/* ------------------------------------------------------- la 404 y la privacidad de antes */
console.log("\nLa 404 y la dirección vieja de la privacidad");
{
  const ctx = await navegador.newContext({ locale: "es-ES" });
  const page = await ctx.newPage();
  const r404 = await page.goto(BASE + "no-existe/", { waitUntil: "load" });
  if (r404.status() === 404 && (await page.locator("h1").textContent()).includes("no existe")) bien("la 404 sale en español"); else mal("la 404 no sale bien");
  await page.goto(BASE + "en/no-such-page/", { waitUntil: "load" });
  if ((await page.locator("h1").textContent()).includes("does not exist")) bien("la 404 dentro de /en/ sale en inglés"); else mal("la 404 de /en/ no se traduce");
  await page.goto(BASE + "privacy.html", { waitUntil: "load" });
  await page.waitForTimeout(300);
  if (new URL(page.url()).pathname === "/privacidad/") bien("privacy.html (la de la Store) lleva a la privacidad en español con el navegador en español");
  else mal(`privacy.html acabó en ${page.url()}`);
  await ctx.close();
}

/* ----------------------------------------------------- lo que pesa la portada, LCP y CLS */
console.log("\nLo que pesa la portada");
for (const [nombre, opciones] of [["1440", { viewport: { width: 1440, height: 900 } }], ["iPhone 13", devices["iPhone 13"]]]) {
  const ctx = await navegador.newContext(opciones);
  const page = await ctx.newPage();
  let bytes = 0;
  page.on("response", async (r) => { try { if (r.url().startsWith(BASE)) bytes += (await r.body()).length; } catch {} });
  await page.addInitScript(() => {
    window.__lcp = 0; window.__cls = 0;
    new PerformanceObserver((l) => { for (const e of l.getEntries()) window.__lcp = e.startTime; }).observe({ type: "largest-contentful-paint", buffered: true });
    new PerformanceObserver((l) => { for (const e of l.getEntries()) if (!e.hadRecentInput) window.__cls += e.value; }).observe({ type: "layout-shift", buffered: true });
  });
  await page.goto(BASE, { waitUntil: "networkidle" });
  await page.waitForTimeout(1500);
  const { lcp, cls } = await page.evaluate(() => ({ lcp: Math.round(window.__lcp), cls: window.__cls }));
  const kb = Math.round(bytes / 1024);
  const linea = `${nombre}: ${kb} KB al cargar, LCP ${lcp} ms en local, CLS ${cls.toFixed(3)}`;
  if (cls > 0.1) mal(`${linea} (el CLS pasa de 0,1)`); else if (kb > 1500) mal(`${linea} (pesa más de 1,5 MB)`); else bien(linea);
  await ctx.close();
}
/* -------------------------------------------------------------- los enlaces de fuera */
// Las descargas, con una petición normal; la Store y el café, abiertos en el navegador, porque
// la Store contesta 403 a lo que no es un navegador.
if (!SIN_RED) {
  console.log("\nLos enlaces de descarga, de la Store y del café");
  for (const url of [
    "https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest/download/TaskbarGroupsFluent-Setup.exe",
    "https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest/download/TaskbarGroupsFluent-Setup-Offline.exe",
  ]) {
    try {
      const r = await fetch(url, { method: "HEAD", redirect: "follow" });
      if (r.ok) bien(`${url} → ${r.status}`); else mal(`${url} → ${r.status}`);
    } catch (e) {
      mal(`${url} → ${e.message}`);
    }
  }
  const ctx = await navegador.newContext();
  const page = await ctx.newPage();
  for (const url of ["https://apps.microsoft.com/detail/9PCZDXWFT6SN", "https://buymeacoffee.com/munito"]) {
    try {
      const r = await page.goto(url, { waitUntil: "domcontentloaded", timeout: 30000 });
      if (r && r.ok()) bien(`${url} → ${r.status()}`); else mal(`${url} → ${r ? r.status() : "sin respuesta"}`);
    } catch (e) {
      mal(`${url} → ${e.message.split("\n")[0]}`);
    }
  }
  await ctx.close();
}
await navegador.close();
servidor.close();

/* ------------------------------------------------------ hreflang, canonical y enlaces internos */
console.log("\nhreflang, canonical y enlaces internos");
const SITIO = "https://tbgf.munito.dev/";
const PARES = [["", "en/"], ["novedades/", "en/changelog/"], ["soporte/", "en/support/"], ["privacidad/", "en/privacy/"]];
for (const [es, en] of PARES) {
  for (const [yo, idioma] of [[es, "es"], [en, "en"]]) {
    const html = await readFile(join(raiz, yo, "index.html"), "utf8");
    const canonical = html.match(/<link rel="canonical" href="([^"]+)">/)?.[1];
    const alt = Object.fromEntries([...html.matchAll(/<link rel="alternate" hreflang="([^"]+)" href="([^"]+)">/g)].map((m) => [m[1], m[2]]));
    const lang = html.match(/<html lang="([a-z]+)"/)?.[1];
    const ok = canonical === SITIO + yo && alt.es === SITIO + es && alt.en === SITIO + en && alt["x-default"] === SITIO + es && lang === idioma;
    if (ok) bien(`/${yo}: canonical propio, hreflang de ida y vuelta y lang="${idioma}"`); else mal(`/${yo}: canonical ${canonical}, hreflang ${JSON.stringify(alt)}, lang ${lang}`);
    if (idioma === "en" && /data-en=/.test(html)) mal(`/${yo}: quedan textos sin traducir (data-en)`);
  }
}
async function htmlDe(carpeta) {
  const fuera = [];
  for (const e of await readdir(carpeta, { withFileTypes: true })) {
    if (e.name.startsWith("_") || e.name === "node_modules") continue;
    const r = join(carpeta, e.name);
    if (e.isDirectory()) fuera.push(...(await htmlDe(r)));
    else if (e.name.endsWith(".html")) fuera.push(r);
  }
  return fuera;
}
let rotos = 0;
for (const archivo of await htmlDe(raiz)) {
  const html = await readFile(archivo, "utf8");
  const nombre = relative(raiz, archivo).replaceAll("\\", "/");
  const base = nombre === "404.html" ? raiz : dirname(archivo);
  for (const [, href] of html.matchAll(/\s(?:href|src)="([^"#?:]+)(?:[?#][^"]*)?"/g)) {
    if (/^(mailto|https?|data)/.test(href)) continue;
    let destino = href.startsWith("/") ? join(raiz, href) : join(base, href);
    if (href.endsWith("/")) destino = join(destino, "index.html");
    if (!existsSync(destino)) { rotos++; mal(`${nombre}: enlace roto a ${href}`); }
  }
  for (const [, ancla] of html.matchAll(/href="#([^"]+)"/g)) {
    if (!html.includes(`id="${ancla}"`)) { rotos++; mal(`${nombre}: ancla #${ancla} sin destino`); }
  }
}
if (!rotos) bien("todos los enlaces internos y las anclas llevan a algo que existe");

console.log(fallos.length ? `\n${fallos.length} fallos.` : "\nTodo en orden.");
process.exit(fallos.length ? 1 : 0);
