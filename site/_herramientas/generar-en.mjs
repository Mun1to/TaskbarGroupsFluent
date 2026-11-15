/**
 * Genera la versión inglesa de la web a partir de la española, y pone las huellas.
 *
 *   node site/_herramientas/generar-en.mjs
 *
 * Cada idioma necesita su propia dirección: un botón que cambia los textos con JavaScript deja
 * a Google y a los rastreadores de las IA viendo solo una versión. Los textos ingleses ya viven
 * en los atributos data-en de cada página, así que aquí solo hay que aplicarlos. Es el script de
 * winshotx.com, con las páginas de Taskbar Groups Fluent.
 *
 * Páginas: index.html -> en/index.html, privacidad/ -> en/privacy/, soporte/ -> en/support/ y
 * novedades/ -> en/changelog/ (esta la escribe antes generar-novedades.mjs). El despliegue
 * (.github/workflows/pages.yml) lo corre y para si cambia algo sin commitear.
 */
import { readFile, writeFile, mkdir, readdir } from "node:fs/promises";
import { createHash } from "node:crypto";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { sincronizarCromo } from "./cromo.mjs";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");
const BASE = "https://tbgf.munito.dev/";

// Primero la página de novedades, que sale de versiones.json, y la comparativa de la portada.
await import("./generar-novedades.mjs");
await import("./generar-comparativa.mjs");

// Y la barra de arriba, el pie y los dibujos de la privacidad, el soporte y la 404, que se
// copian de la portada.
for (const archivo of await sincronizarCromo(raiz)) console.log(`${archivo}: barra y pie de la portada`);

/** Los data-en llevan las marcas escapadas: aquí se devuelven a su forma. */
const desescapar = (t) =>
  t.replaceAll("&lt;", "<").replaceAll("&gt;", ">").replaceAll("&quot;", '"').replaceAll("&#39;", "'").replaceAll("&amp;", "&");

/** Lo que es igual en todas las páginas: aplicar los data-en, los aria-label y las fotos. */
function traducir(html) {
  const traducible = /<([a-z0-9]+)((?:[^>"]|"[^"]*")*?\sdata-en="([^"]*)"(?:[^>"]|"[^"]*")*)>([\s\S]*?)<\/\1>/g;
  html = html.replace(traducible, (_todo, tag, atributos, ingles) => {
    const limpios = atributos.replace(/\sdata-en="(?:[^"]*)"/, "").replace(/\sdata-html(?==|\s|$)/, "");
    return `<${tag}${limpios}>${desescapar(ingles)}</${tag}>`;
  });

  // Los aria-label son invisibles para quien revisa la página: si no se traducen aquí, se
  // quedan en español sin que nadie lo note.
  html = html.replace(/\saria-label="[^"]*"((?:[^>"]|"[^"]*")*?)\sdata-aria-en="([^"]*)"/g, (_t, medio, ingles) => ` aria-label="${ingles}"${medio}`);
  html = html.replace(/\sdata-aria-en="([^"]*)"((?:[^>"]|"[^"]*")*?)\saria-label="[^"]*"/g, (_t, ingles, medio) => `${medio} aria-label="${ingles}"`);
  html = html.replace(/\sdata-aria-en="[^"]*"/g, "");

  // Las fotos con texto dentro tienen una por idioma: la inglesa va al lado en data-src-en, y
  // su texto alternativo en data-alt-en.
  for (const atributo of ["src", "href", "alt"]) {
    const conIngles = new RegExp(`\\s${atributo}="[^"]*"((?:[^>"]|"[^"]*")*?)\\sdata-${atributo}-en="([^"]*)"`, "g");
    html = html.replace(conIngles, (_t, medio, ingles) => ` ${atributo}="${ingles}"${medio}`);
  }

  // Las páginas se llaman distinto en cada idioma: los enlaces relativos que van a ellas
  // cambian de nombre. Los absolutos los pone cada página.
  html = html.replace(/href="((?:\.\.\/)*)novedades\/(#[^"]*)?"/g, 'href="$1changelog/$2"');
  html = html.replace(/href="((?:\.\.\/)*)soporte\/(#[^"]*)?"/g, 'href="$1support/$2"');
  html = html.replace(/href="((?:\.\.\/)*)privacidad\/(#[^"]*)?"/g, 'href="$1privacy/$2"');

  html = html.replace('<html lang="es"', '<html lang="en"');
  html = html.replace('<meta property="og:locale" content="es_ES">', '<meta property="og:locale" content="en_US">');
  html = html.replace('<meta property="og:locale:alternate" content="en_US">', '<meta property="og:locale:alternate" content="es_ES">');
  html = html.replaceAll(`${BASE}social.png`, `${BASE}social-en.png`);
  return html;
}

/* ------------------------------------------------------------------------- las huellas */
// GitHub Pages sirve el HTML con 10 minutos de caché y lo demás con más: sin la huella del
// archivo en su dirección, quien ya había entrado se lleva la página nueva con el CSS viejo.
// El texto se pasa a saltos de Unix antes de la huella, para que Windows y el servidor den la misma.
const huellaTexto = async (ruta) =>
  createHash("sha256").update((await readFile(ruta, "utf8")).replaceAll("\r\n", "\n")).digest("hex").slice(0, 8);
const huellaBinaria = async (ruta) => createHash("sha256").update(await readFile(ruta)).digest("hex").slice(0, 8);

const CODIGO = ["estilos.css", "demo.js", "leer.js"];
const huellas = Object.fromEntries(await Promise.all(CODIGO.map(async (a) => [a, await huellaTexto(join(raiz, a))])));

async function archivosDe(carpeta) {
  const fuera = [];
  for (const e of await readdir(carpeta, { withFileTypes: true })) {
    const ruta = join(carpeta, e.name);
    if (e.isDirectory()) fuera.push(...(await archivosDe(ruta)));
    else fuera.push(ruta);
  }
  return fuera;
}
const huellasRecursos = Object.fromEntries(
  await Promise.all(
    (await archivosDe(join(raiz, "assets")))
      .filter((r) => /\.(webp|png|mp4)$/.test(r))
      .map(async (r) => [relative(join(raiz, "assets"), r).replaceAll("\\", "/"), await huellaBinaria(r)]),
  ),
);

function versionar(html) {
  return html
    .replace(/((?:\/|(?:\.\.\/)*))(estilos\.css|demo\.js|leer\.js)(\?v=[a-f0-9]+)?(?=")/g, (_t, subir, archivo) => `${subir}${archivo}?v=${huellas[archivo]}`)
    // Solo las rutas relativas o desde la raíz, detrás de una comilla: las absolutas del
    // JSON-LD y de las tarjetas sociales no se tocan.
    .replace(/(["'])((?:\/|(?:\.\.\/)*))assets\/([a-z0-9/-]+\.(?:webp|png|mp4))(\?v=[a-f0-9]+)?/g, (_t, comilla, subir, recurso) => {
      if (!huellasRecursos[recurso]) throw new Error(`la página pide assets/${recurso} y no existe`);
      return `${comilla}${subir}assets/${recurso}?v=${huellasRecursos[recurso]}`;
    });
}

/**
 * El bloque FAQPage sale de las preguntas que la página ENSEÑA: escrito a mano se separa en
 * cuanto alguien añade o quita una, y Google descalifica el bloque entero si marca algo que no
 * está a la vista.
 */
function construirFaq(html, idioma) {
  if (!html.includes("data-faq")) return html;
  const seccion = html.match(/<section id="preguntas"[^>]*>([\s\S]*?)<\/section>/);
  if (!seccion) throw new Error("no aparece la sección de preguntas");
  const soloTexto = (s) => desescapar(s.replace(/<[^>]+>/g, " ")).replace(/\s+/g, " ").trim();
  const pares = [...seccion[1].matchAll(/<h3[^>]*>([\s\S]*?)<\/h3>[\s\S]*?<p[^>]*>([\s\S]*?)<\/p>/g)];
  if (!pares.length) throw new Error("la sección de preguntas no tiene ningún par h3 + p");
  const bloque = {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    inLanguage: idioma,
    mainEntity: pares.map(([, pregunta, respuesta]) => ({
      "@type": "Question",
      name: soloTexto(pregunta),
      acceptedAnswer: { "@type": "Answer", text: soloTexto(respuesta) },
    })),
  };
  return html.replace(/<script type="application\/ld\+json" data-faq>[\s\S]*?<\/script>/, `<script type="application/ld+json" data-faq>
${JSON.stringify(bloque, null, 2)}
</script>`);
}

/**
 * Las descripciones viven en atributos y no pueden llevar data-en: aquí va la frase española
 * ENTERA y su traducción. Cambiar una coma en el HTML deja la clave sin casar y la página
 * inglesa con la frase en español, sin que nada falle; por eso, al final, se comprueba que
 * TODAS las claves se hayan usado.
 */
const ATRIBUTOS = {
  'content="Agrupa tus accesos directos en un solo botón de la barra de tareas de Windows 10 y 11. Lo pulsas, o pasas el ratón, y se abre un panel con todas tus apps, con el cristal de Windows 11. Gratis, de código abierto, en WinGet y en la Microsoft Store."':
    'content="Group your shortcuts into a single taskbar button on Windows 10 and 11. Click it, or just hover, and a panel opens with all your apps, in Windows 11 glass. Free, open source, on WinGet and the Microsoft Store."',
  'content="Taskbar Groups Fluent · Diez iconos en la barra, ahora solo uno"':
    'content="Taskbar Groups Fluent · Ten icons on the taskbar, now just one"',
  'content="Agrupa tus apps en un solo botón de la barra de tareas de Windows. Lo pulsas, o pasas el ratón, y se abre un panel con todas. Gratis y de código abierto."':
    'content="Group your apps into a single taskbar button on Windows. Click it, or just hover, and a panel opens with all of them. Free and open source."',
  'content="Taskbar Groups Fluent: un icono de la barra de tareas que abre un panel con las apps del grupo"':
    'content="Taskbar Groups Fluent: one taskbar icon that opens a panel with the apps in the group"',
  'content="Cómo pedir ayuda o avisar de un fallo de Taskbar Groups Fluent, lo que se pregunta siempre, dónde guarda tus grupos y cómo se desinstala del todo."':
    'content="How to get help or report a bug in Taskbar Groups Fluent, the questions that always come up, where it keeps your groups and how to uninstall it completely."',
  'content="Soporte de Taskbar Groups Fluent"': 'content="Taskbar Groups Fluent support"',
  'content="Pedir ayuda, avisar de un fallo, dónde están tus grupos y cómo se desinstala."':
    'content="Getting help, reporting a bug, where your groups are and how to uninstall it."',
  'content="Taskbar Groups Fluent no recoge datos personales: ni cuentas, ni telemetría, ni anuncios. Lo único que hace por internet es preguntar a GitHub si hay una versión nueva."':
    'content="Taskbar Groups Fluent collects no personal data: no accounts, no telemetry, no ads. The only thing it does online is ask GitHub whether there is a new version."',
  'content="Privacidad de Taskbar Groups Fluent"': 'content="Taskbar Groups Fluent privacy"',
  'content="No recoge datos personales. Lo único que hace por internet es preguntar a GitHub si hay una versión nueva."':
    'content="It collects no personal data. The only thing it does online is ask GitHub whether there is a new version."',
  'content="Lo que trae cada versión de Taskbar Groups Fluent, de la más nueva a la primera, con el enlace a su publicación en GitHub."':
    'content="What each version of Taskbar Groups Fluent brings, newest first, with a link to its release on GitHub."',
  'content="Novedades de Taskbar Groups Fluent"': "content=\"What's new in Taskbar Groups Fluent\"",
};

/** Lo que cambia de cada página: sus direcciones propias y lo que sube un nivel. */
const sube = (de, a) => ({
  [`href="${de}estilos.css`]: `href="${a}estilos.css`,
  [`src="${de}leer.js`]: `src="${a}leer.js`,
  [`src="${de}assets/`]: `src="${a}assets/`,
  [`href="${de}assets/`]: `href="${a}assets/`,
});
const PAGINAS = [
  {
    origen: "index.html",
    destino: ["en", "index.html"],
    propios: {
      [`<link rel="canonical" href="${BASE}">`]: `<link rel="canonical" href="${BASE}en/">`,
      [`<meta property="og:url" content="${BASE}">`]: `<meta property="og:url" content="${BASE}en/">`,
      [`"url": "${BASE}"`]: `"url": "${BASE}en/"`,
      'data-otra="en/"': 'data-otra="../"',
      ...sube("", "../"),
      'src="demo.js': 'src="../demo.js',
      '<a class="idioma" id="idioma" href="en/" hreflang="en" lang="en" data-corto="EN" aria-label="English">English</a>':
        '<a class="idioma" id="idioma" href="../" hreflang="es" lang="es" data-corto="ES" aria-label="Español">Español</a>',
    },
  },
  {
    origen: join("privacidad", "index.html"),
    destino: ["en", "privacy", "index.html"],
    propios: {
      [`<link rel="canonical" href="${BASE}privacidad/">`]: `<link rel="canonical" href="${BASE}en/privacy/">`,
      [`<meta property="og:url" content="${BASE}privacidad/">`]: `<meta property="og:url" content="${BASE}en/privacy/">`,
      [`"item": "${BASE}" }`]: `"item": "${BASE}en/" }`,
      [`"name": "Privacidad", "item": "${BASE}privacidad/" }`]: `"name": "Privacy", "item": "${BASE}en/privacy/" }`,
      'data-otra="../en/privacy/"': 'data-otra="../../privacidad/"',
      ...sube("../", "../../"),
      '<a class="idioma" id="idioma" href="../en/privacy/" hreflang="en" lang="en" data-corto="EN" aria-label="English">English</a>':
        '<a class="idioma" id="idioma" href="../../privacidad/" hreflang="es" lang="es" data-corto="ES" aria-label="Español">Español</a>',
    },
  },
  {
    origen: join("soporte", "index.html"),
    destino: ["en", "support", "index.html"],
    propios: {
      [`<link rel="canonical" href="${BASE}soporte/">`]: `<link rel="canonical" href="${BASE}en/support/">`,
      [`<meta property="og:url" content="${BASE}soporte/">`]: `<meta property="og:url" content="${BASE}en/support/">`,
      [`"item": "${BASE}" }`]: `"item": "${BASE}en/" }`,
      [`"name": "Soporte", "item": "${BASE}soporte/" }`]: `"name": "Support", "item": "${BASE}en/support/" }`,
      'data-otra="../en/support/"': 'data-otra="../../soporte/"',
      ...sube("../", "../../"),
      '<a class="idioma" id="idioma" href="../en/support/" hreflang="en" lang="en" data-corto="EN" aria-label="English">English</a>':
        '<a class="idioma" id="idioma" href="../../soporte/" hreflang="es" lang="es" data-corto="ES" aria-label="Español">Español</a>',
    },
  },
  {
    origen: join("novedades", "index.html"),
    destino: ["en", "changelog", "index.html"],
    propios: {
      [`<link rel="canonical" href="${BASE}novedades/">`]: `<link rel="canonical" href="${BASE}en/changelog/">`,
      [`<meta property="og:url" content="${BASE}novedades/">`]: `<meta property="og:url" content="${BASE}en/changelog/">`,
      [`"item": "${BASE}" }`]: `"item": "${BASE}en/" }`,
      [`"name": "Novedades", "item": "${BASE}novedades/" }`]: `"name": "What's new", "item": "${BASE}en/changelog/" }`,
      'data-otra="../en/changelog/"': 'data-otra="../../novedades/"',
      ...sube("../", "../../"),
      '<a class="idioma" id="idioma" href="../en/changelog/" hreflang="en" lang="en" data-corto="EN" aria-label="English">English</a>':
        '<a class="idioma" id="idioma" href="../../novedades/" hreflang="es" lang="es" data-corto="ES" aria-label="Español">Español</a>',
    },
  },
];

/** Qué claves de ATRIBUTOS ha encontrado de verdad alguna página. */
const usadas = new Set();

for (const pagina of PAGINAS) {
  const rutaOrigen = join(raiz, pagina.origen);
  let original = await readFile(rutaOrigen, "utf8");

  // La página española es el origen: su bloque de preguntas y sus huellas se ponen aquí mismo.
  const puesto = versionar(construirFaq(original, "es"));
  if (puesto !== original) {
    await writeFile(rutaOrigen, puesto, "utf8");
    original = puesto;
    console.log(`${pagina.origen}: actualizado`);
  }

  let html = construirFaq(traducir(original), "en");
  for (const [es, en] of Object.entries(ATRIBUTOS)) {
    if (html.includes(es)) usadas.add(es);
    html = html.replaceAll(es, en);
  }
  for (const [de, a] of Object.entries(pagina.propios)) {
    if (!html.includes(de)) throw new Error(`${pagina.origen}: no aparece ${de}`);
    html = html.replaceAll(de, a);
  }

  const salida = join(raiz, ...pagina.destino);
  await mkdir(dirname(salida), { recursive: true });
  await writeFile(salida, html, "utf8");
  const pendientes = (html.match(/data-en=/g) ?? []).length;
  console.log(`${pagina.destino.join("/")} generado${pendientes ? ` (quedan ${pendientes} sin traducir)` : ""}`);
}

// La 404 no tiene versión inglesa (se traduce sola si la dirección empieza por /en/), pero
// carga el CSS como las demás, con su huella.
{
  const ruta404 = join(raiz, "404.html");
  const antes = await readFile(ruta404, "utf8");
  const despues = versionar(antes);
  if (despues !== antes) {
    await writeFile(ruta404, despues, "utf8");
    console.log("404.html: actualizado");
  }
}

// Una clave que no aparece en ninguna página es una frase que se ha quedado en español al
// otro lado, y sin esto no se entera nadie.
const huerfanas = Object.keys(ATRIBUTOS).filter((es) => !usadas.has(es));
if (huerfanas.length) {
  console.error("Estas frases ya no están en el HTML español, así que su traducción no se aplica:");
  for (const es of huerfanas) console.error(`  ${es}`);
  console.error("Cambia la clave en ATRIBUTOS por el texto que hay ahora, o quítala.");
  process.exit(1);
}
