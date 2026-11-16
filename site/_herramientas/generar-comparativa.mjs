/**
 * Escribe la comparativa de la portada («Frente a frente») a partir de comparativa.mjs.
 *
 *   node site/_herramientas/generar-comparativa.mjs     reescribe <section id="comparativa"> de site/index.html
 *
 * La corre generar-en.mjs antes de sacar la página inglesa, así que basta con correr esa.
 *
 * Una pestaña por alternativa («¿Cuál usas ahora?»), y en cada una: qué es, lo que gana Taskbar
 * Groups Fluent frente a esa, lo que gana esa, lo que hacen las dos y de dónde sale. Las pestañas
 * son botones de radio: cambian de panel sin JavaScript (con :has() en el CSS) y con las flechas
 * del teclado. Todo el texto de las siete está en el HTML, también el de las pestañas que no se
 * ven, para quien lee la página sin ejecutar nada. Es el patrón de winshotx.com.
 */
import { readFile, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { HERRAMIENTAS, FILAS, TEXTOS } from "./comparativa.mjs";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");
const NUESTRA = HERRAMIENTAS[0];
const OTRAS = HERRAMIENTAS.slice(1);
const IDIOMAS = ["es", "en"];
const PROPIA = ["el README de esta app", "this app's README", "https://github.com/Mun1to/TaskbarGroupsFluent#readme"];

/* ----------------------------------------------------------------- en los dos idiomas */
// Cada texto va en español y, si cambia, con su inglés en data-en, que es lo que lee
// generar-en.mjs. El elemento que lleva data-en no puede llevar dentro otro de su misma
// etiqueta: generar-en.mjs lo cierra en el primer cierre que encuentra.
const escapar = (t) => t.replaceAll("&", "&amp;").replaceAll('"', "&quot;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
function el(etiqueta, atributos, texto) {
  const { es, en } = texto;
  const marcas = /<[a-z]/i.test(es) || /<[a-z]/i.test(en);
  const ingles = es === en ? "" : `${marcas ? " data-html" : ""} data-en="${escapar(en)}"`;
  return `<${etiqueta}${atributos ? ` ${atributos}` : ""}${ingles}>${es}</${etiqueta}>`;
}
const dos = (f) => Object.fromEntries(IDIOMAS.map((L) => [L, f(L)]));
const nombre = (h, L, corto = false) => (corto && h.corto ? (L === "en" ? h.cortoEn : h.corto) : h[L]);
const txt = (v, L) => v[L === "en" ? 2 : 1];
// Tras dos puntos va en minúscula, salvo un nombre propio o unas siglas.
const PROPIOS = /^(Windows|WinGet|Windhawk|Steam|English|PWA|[A-Z]{2,}|\.NET|\d)/;
const minuscula = (s) => (PROPIOS.test(s) ? s : s.charAt(0).toLowerCase() + s.slice(1));
// Listas de tres o más con punto y coma: sus cosas ya llevan comas dentro.
function lista(cosas, L) {
  if (cosas.length <= 1) return cosas.join("");
  if (cosas.length === 2) return cosas.join(L === "en" ? " and " : " y ");
  return cosas.slice(0, -1).join("; ") + (L === "en" ? "; and " : "; y ") + cosas.at(-1);
}
const icono = (id) => `<svg aria-hidden="true" class="frente-ic"><use href="#${id}"/></svg>`;
const enlaces = (fuentes, L) =>
  lista(fuentes.map(([es, en, url]) => `<a href="${url}" rel="noopener">${L === "en" ? en : es}</a>`), L);

/* --------------------------------------------------------------------- las piezas */
function panel(rival) {
  const id = rival.id;
  const gana = [], pierde = [], empatan = [];
  for (const f of FILAS) {
    const b = f.v[id]?.[0];
    if (!b || b === "nv" || b === "na") continue; // sin dato oficial o sin sentido aquí: no cuenta
    if (f.pierde) { if (b === "si") pierde.push(f); continue; }
    const a = f.v[NUESTRA.id]?.[0];
    if (a === "si" && b !== "si") gana.push(f);
    else if (a === "si" && b === "si" && !f.sinEmpate) empatan.push(f);
  }

  const linea = (f) => {
    const dicho = dos((L) => `${nombre(rival, L, true)}: ${minuscula(txt(f.v[id], L))}`);
    return `              <li>${icono(f.icono)}<div>${el("b", "", dos((L) => f.vende[L]))}${el("span", "", dicho)}</div></li>`;
  };
  const tituloOtra = rival.gana ?? dos((L) => TEXTOS.ganaOtra[L].replace("{x}", nombre(rival, L)));
  const empate = empatan.length
    ? `\n        ${el("p", 'class="frente-empatan"', dos((L) => `<b>${TEXTOS.lasDos[L]}</b> ${lista(empatan.map((f) => f[L]), L)}.`))}`
    : "";
  const fuentes = dos((L) => TEXTOS.fuentes[L].replace("{f}", enlaces([...rival.fuentes, PROPIA], L)));

  return `      <div class="frente" id="frente-${id}">
        ${el("h3", 'class="frente-nombre"', dos((L) => nombre(rival, L, true)))}
        ${el("p", 'class="frente-resumen"', rival.resumen)}
        <div class="frente-dos">
          <div>
            ${el("h3", 'class="frente-sub"', TEXTOS.gana)}
            <ul class="frente-lista">
${gana.map(linea).join("\n") || `              ${el("li", 'class="frente-nada"', TEXTOS.nada)}`}
            </ul>
          </div>
          <div>
            ${el("h3", 'class="frente-sub"', tituloOtra)}
            <ul class="frente-lista pierde">
${pierde.map(linea).join("\n") || `              ${el("li", 'class="frente-nada"', TEXTOS.nada)}`}
            </ul>
          </div>
        </div>${empate}
        ${el("p", 'class="fuentes"', fuentes)}
      </div>`;
}

export function seccion() {
  const pestana = (h, i) =>
    `      <label class="pestana"><input type="radio" name="rival" value="${h.id}"${i === 0 ? " checked" : ""}>${el("span", "", dos((L) => nombre(h, L, true)))}</label>`;
  return `<section id="comparativa" class="seccion comparativa">
  <div class="env">
    <div class="cabeza-seccion">
      ${el("h2", "", TEXTOS.titulo)}
      ${el("p", 'class="entradilla"', TEXTOS.entradilla)}
      <div class="fan suelta" style="--giro:3deg">${el("p", "", TEXTOS.fan)}</div>
    </div>

    ${el("p", 'class="rival-pregunta" id="rival-pregunta"', TEXTOS.pregunta)}
    <div class="pestanas rivales" role="radiogroup" aria-labelledby="rival-pregunta">
${OTRAS.map(pestana).join("\n")}
    </div>
    <div class="frentes">
${OTRAS.map(panel).join("\n")}
    </div>
  </div>
</section>`;
}

/* ------------------------------------------------------------ escribirla en la portada */
const ruta = join(raiz, "index.html");
const portada = await readFile(ruta, "utf8");
const trozo = /<section id="comparativa"[\s\S]*?<\/section>/;
if (!trozo.test(portada)) throw new Error('index.html no tiene <section id="comparativa">');
const nueva = portada.replace(trozo, () => seccion());
// Cada icono que se usa tiene que estar en el sprite: uno que falta no da error, sale un hueco.
const sinDibujo = [...new Set([...nueva.matchAll(/<use href="#([^"]+)"/g)].map((m) => m[1]))].filter((d) => !nueva.includes(`<symbol id="${d}"`));
if (sinDibujo.length) throw new Error(`a la portada le faltan estos iconos en el sprite: ${sinDibujo.join(", ")}`);
if (nueva !== portada) {
  await writeFile(ruta, nueva);
  console.log("index.html: la comparativa, desde comparativa.mjs");
}
