/**
 * Genera la página de novedades a partir de _herramientas/versiones.json.
 *
 *   node site/_herramientas/generar-novedades.mjs              escribe site/novedades/index.html
 *   node site/_herramientas/generar-novedades.mjs --comprobar  además, mira en GitHub que no falte ninguna versión
 *
 * La corre generar-en.mjs, que después saca la inglesa (en/changelog/) de los data-en.
 *
 * versiones.json es una lista, de la más nueva a la primera. Cada entrada cuenta una versión o un
 * tramo (`desde` hasta `version`), con su título y sus puntos en los dos idiomas, sacados de las
 * notas de cada release de GitHub. Los puntos ya traen su <b> en la palabra clave. Cada versión
 * nueva se añade arriba, a mano, al publicarla. `--comprobar` falla si en GitHub hay una versión
 * estable que aquí no está.
 */
import { readFile, writeFile } from "node:fs/promises";
import { execFileSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { leerPortada, cromoPara, spritePara } from "./cromo.mjs";

const aqui = dirname(fileURLToPath(import.meta.url));
const raiz = join(aqui, "..");
const BASE = "https://tbgf.munito.dev/";
const REPO = "Mun1to/TaskbarGroupsFluent";
const versiones = JSON.parse(await readFile(join(aqui, "versiones.json"), "utf8"));

const num = (v) => v.split(".").map(Number);
const menor = (a, b) => { const [x, y] = [num(a), num(b)]; for (let i = 0; i < 3; i++) if (x[i] !== y[i]) return x[i] < y[i]; return false; };
const tramo = (v) => v.desde && v.desde !== v.version;

// ---------------------------------------------------------------- comprobar contra GitHub
if (process.argv.includes("--comprobar")) {
  // En esta máquina hay variables que fuerzan los colores, y `gh` los mete dentro del JSON.
  const env = { ...process.env, CLICOLOR_FORCE: "0", FORCE_COLOR: "0", NO_COLOR: "1" };
  const salida = execFileSync("gh", ["api", `repos/${REPO}/releases?per_page=100`, "--paginate", "--jq", ".[] | select((.draft or .prerelease) | not) | .tag_name"], { env, encoding: "utf8" });
  const publicadas = salida.split(/\r?\n/).filter(Boolean).map((t) => t.replace(/^v/, ""));
  const cubierta = (v) => versiones.some((e) => (tramo(e) ? !menor(v, e.desde) && !menor(e.version, v) : e.version === v));
  const faltan = publicadas.filter((v) => !cubierta(v));
  if (faltan.length) {
    console.error(`En GitHub hay versiones que la página de novedades no cuenta: ${faltan.join(", ")}`);
    console.error("Añade su entrada arriba del todo en site/_herramientas/versiones.json.");
    process.exit(1);
  }
  console.log(`Las ${publicadas.length} versiones estables publicadas en GitHub están en la página.`);
}

// ---------------------------------------------------------------- piezas
const escapar = (t) => t.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;");
const MESES_ES = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];
const MESES_EN = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
const fechaEs = (f) => { const [a, m, d] = f.split("-").map(Number); return `${d} de ${MESES_ES[m - 1]} de ${a}`; };
const fechaEn = (f) => { const [a, m, d] = f.split("-").map(Number); return `${d} ${MESES_EN[m - 1]} ${a}`; };

/** Un elemento con su español dentro y su inglés en data-en, que es lo que lee generar-en.mjs. */
const bilingue = (tag, es, en, atributos = "") => {
  if (es === en) return `<${tag}${atributos}>${es}</${tag}>`;
  const html = /<[a-z]/.test(es) || /<[a-z]/.test(en);
  return `<${tag}${atributos}${html ? " data-html" : ""} data-en="${escapar(en)}">${es}</${tag}>`;
};
const idDe = (v) => `v${v.version.replaceAll(".", "-")}`;
const nombres = (v) => tramo(v)
  ? [`Versiones ${v.desde} a ${v.version}`, `Versions ${v.desde} to ${v.version}`]
  : [`Versión ${v.version}`, `Version ${v.version}`];

const entrada = (v) => {
  const enlace = `https://github.com/${REPO}/releases/tag/v${v.version}`;
  const [nombreEs, nombreEn] = nombres(v);
  const lineaEs = `<a href="${enlace}" rel="noopener">${nombreEs}</a> · ${fechaEs(v.fecha)}`;
  const lineaEn = `<a href="${enlace}" rel="noopener">${nombreEn}</a> · ${fechaEn(v.fecha)}`;
  if (v.es.puntos.length !== v.en.puntos.length) throw new Error(`${v.version}: no tiene los mismos puntos en los dos idiomas`);
  const puntos = v.es.puntos.map((p, i) => `    ${bilingue("li", p, v.en.puntos[i])}`).join("\n");
  return `<section id="${idDe(v)}">
  ${bilingue("h2", escapar(v.es.titulo), escapar(v.en.titulo))}
  ${bilingue("p", lineaEs, lineaEn, ' class="version"')}
  <ul class="lista">
${puntos}
  </ul>
</section>`;
};

// ---------------------------------------------------------------- la página
// La barra, el pie y los dibujos salen de la portada (cromo.mjs), como en las demás páginas.
const portada = await leerPortada(raiz);
const { barra, pie } = cromoPara(portada, "novedades");

const ultima = versiones[0];
const descripcion = "Lo que trae cada versión de Taskbar Groups Fluent, de la más nueva a la primera, con el enlace a su publicación en GitHub.";
const indice = versiones
  .map((v) => {
    const es = tramo(v) ? `${v.desde} a ${v.version}` : v.version;
    const en = tramo(v) ? `${v.desde} to ${v.version}` : v.version;
    return `        ${bilingue("a", es, en, ` href="#${idDe(v)}"`)}`;
  })
  .join("\n");

let html = `<!doctype html>
<html lang="es" data-otra="../en/changelog/">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title data-en="What's new in Taskbar Groups Fluent, version by version">Novedades de Taskbar Groups Fluent, versión a versión</title>
<meta name="description" content="${descripcion}">
<link rel="canonical" href="${BASE}novedades/">
<link rel="alternate" hreflang="es" href="${BASE}novedades/">
<link rel="alternate" hreflang="en" href="${BASE}en/changelog/">
<link rel="alternate" hreflang="x-default" href="${BASE}novedades/">
<meta name="color-scheme" content="light dark">
<meta name="theme-color" content="#0b0b0d" media="(prefers-color-scheme: dark)">
<meta name="theme-color" content="#f7f7f9" media="(prefers-color-scheme: light)">
<meta property="og:type" content="article">
<meta property="og:site_name" content="Taskbar Groups Fluent">
<meta property="og:locale" content="es_ES">
<meta property="og:locale:alternate" content="en_US">
<meta property="og:url" content="${BASE}novedades/">
<meta property="og:title" content="Novedades de Taskbar Groups Fluent">
<meta property="og:description" content="${descripcion}">
<meta property="og:image" content="${BASE}social.png?v=1">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:image" content="${BASE}social.png?v=1">
<link rel="icon" href="../assets/icon-256.png" type="image/png">
<link rel="apple-touch-icon" href="../assets/icon-256.png">
<script>
// El dial de movimiento y el idioma elegido, igual que en la portada (ver index.html).
(function () {
  var h = document.documentElement;
  var q = new URLSearchParams(location.search).get("motion");
  var reduce = matchMedia("(prefers-reduced-motion: reduce)").matches;
  var gain = q === "off" ? 0 : q === "full" ? 1 : reduce ? 0.25 : 1;
  h.style.setProperty("--motion-gain", String(gain));
  h.classList.toggle("motion", gain > 0);
  h.classList.toggle("quieto", gain === 0);
  try {
    var elegido = localStorage.getItem("tbgf-lang");
    var otra = h.getAttribute("data-otra");
    if (otra && (elegido === "es" || elegido === "en") && elegido !== h.lang) location.replace(otra + location.search + location.hash);
  } catch (e) {}
})();
</script>
<link rel="stylesheet" href="../estilos.css">
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "BreadcrumbList",
  "itemListElement": [
    { "@type": "ListItem", "position": 1, "name": "Taskbar Groups Fluent", "item": "${BASE}" },
    { "@type": "ListItem", "position": 2, "name": "Novedades", "item": "${BASE}novedades/" }
  ]
}
</script>
</head>
<body>

<!-- Esta página la escribe _herramientas/generar-novedades.mjs a partir de versiones.json: no se
     edita a mano, que se pisa la próxima vez. -->
<svg style="display:none" xmlns="http://www.w3.org/2000/svg"></svg>

${barra}

<main class="leer novedades">
  <div class="env">
    <header class="leer-cabeza">
      ${bilingue("h1", "Novedades", "What's new")}
      ${bilingue(
        "p",
        `Lo que trae cada versión, de la más nueva a la primera. La app se actualiza sola cuando sale una nueva, si tú le dices que sí, y la última es la <b>${ultima.version}</b>.`,
        `What each version brings, newest first. The app updates itself when a new one is out, if you say yes, and the latest is <b>${ultima.version}</b>.`,
        ' class="entradilla"',
      )}
    </header>

    <div class="leer-rejilla con-lado">
      <nav class="leer-indice" aria-label="Versiones" data-aria-en="Versions">
${indice}
      </nav>

      <div class="texto">
${versiones.map(entrada).join("\n\n")}
      </div>

      <figure class="leer-lado">
        <img src="../assets/shot-formas.webp" width="1464" height="1120" loading="lazy" alt="Los cuatro paneles abiertos sobre una barra de tareas real: cuadrícula, lista, columna y fila" data-alt-en="The four panels open over a real taskbar: grid, list, column and row">
        ${bilingue(
          "figcaption",
          "Las cuatro formas del panel, en una barra de tareas real. Cada versión llega sola: al abrirse, la app te ofrece la nueva.",
          "The four panel shapes, on a real taskbar. Each version arrives by itself: when it opens, the app offers you the new one.",
        )}
      </figure>
    </div>
  </div>
</main>

${pie}

<script src="../leer.js"></script>
</body>
</html>
`;
html = html.replace(/<svg style="display:none"[^>]*><\/svg>/, () => spritePara(html, portada.simbolos, "novedades"));
await writeFile(join(raiz, "novedades", "index.html"), html, "utf8");
console.log(`novedades/index.html: ${versiones.length} entradas, la última la ${ultima.version}`);

// ---------------------------------------------------------------- las tres últimas, en la portada
// La portada cuenta las tres últimas entre dos marcas; cada una lleva a su sitio en la página.
const tarjeta = (v) => {
  const [nombreEs, nombreEn] = nombres(v);
  return `      <article class="novedad">
        ${bilingue("p", `${nombreEs} · ${fechaEs(v.fecha)}`, `${nombreEn} · ${fechaEn(v.fecha)}`, ' class="cuando"')}
        <h3>${bilingue("a", escapar(v.es.titulo), escapar(v.en.titulo), ` href="novedades/#${idDe(v)}"`)}</h3>
        ${bilingue("p", v.es.puntos[0], v.en.puntos[0])}
      </article>`;
};
const rutaPortada = join(raiz, "index.html");
const portadaHtml = await readFile(rutaPortada, "utf8");
const bloque = `<!-- novedades:desde (lo escribe generar-novedades.mjs a partir de versiones.json) -->
    <div class="novedades-portada">
${versiones.slice(0, 3).map(tarjeta).join("\n")}
    </div>
    <!-- novedades:hasta -->`;
const nuevaPortada = portadaHtml.replace(/<!-- novedades:desde[\s\S]*?<!-- novedades:hasta -->/, () => bloque);
if (nuevaPortada === portadaHtml && !portadaHtml.includes(bloque)) throw new Error("la portada no tiene las marcas novedades:desde y novedades:hasta");
if (nuevaPortada !== portadaHtml) {
  await writeFile(rutaPortada, nuevaPortada, "utf8");
  console.log("index.html: las tres últimas versiones");
}
