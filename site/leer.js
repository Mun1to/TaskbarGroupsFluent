/* Lo que comparten todas las páginas de la web: el modo ligero, la barra que se compacta, el
   menú del móvil, las preguntas, el índice de las páginas de leer, las notas de la portada
   con sus flechas, las órdenes que se copian con un clic, las cifras de GitHub y la frase
   que ofrece el otro idioma. Sin dependencias. Las piezas son las de winshotx.com, con su
   porqué en cada una. */

const INGLES = document.documentElement.lang === "en";

/* ------------------------------------------------- cuando la página está tranquila */
// Lo que no hace falta para el primer vistazo (encender la demo, mirar si hay tarjeta gráfica)
// espera a que la página haya cargado y lleve medio segundo sin bajar (como mucho 5 s después
// de cargar): así no se pelea con el primer dibujado ni con el scroll. Avisa con el evento
// `tbgf:quieta` en document y deja `window.tbgfQuieta` en true para quien llegue tarde.
(function () {
  const TIPOS = ["scroll", "wheel", "touchmove", "keydown"];
  let hecho = false, espera = 0;
  const ir = () => {
    if (hecho) return;
    hecho = true;
    TIPOS.forEach((t) => removeEventListener(t, otraVez, true));
    const avisar = () => { window.tbgfQuieta = true; document.dispatchEvent(new Event("tbgf:quieta")); };
    if ("requestIdleCallback" in window) requestIdleCallback(avisar, { timeout: 1000 }); else avisar();
  };
  const otraVez = () => { clearTimeout(espera); espera = setTimeout(ir, 500); };
  const empezar = () => {
    TIPOS.forEach((t) => addEventListener(t, otraVez, { capture: true, passive: true }));
    otraVez();
    setTimeout(ir, 5000);
  };
  if (document.readyState === "complete") empezar();
  else addEventListener("load", empezar, { once: true });
})();

/* ------------------------------------------------- sin tarjeta gráfica, más ligera */
// Sin aceleración por hardware (el ordenador de clase de Munito, muchos de empresa y los
// viejos), el procesador hace lo que hacía la tarjeta: cada desenfoque se recalcula al bajar.
// Se mira UNA vez por página: un contexto WebGL con `failIfMajorPerformanceCaveat` se niega
// cuando solo se dibujaría por software. Entonces se pone <html data-ligero> y el CSS quita lo
// que pesa sin cambiar el diseño. `?ligero=1` lo fuerza y `?ligero=0` lo quita. Las
// animaciones no se tocan.
// Pedir ese contexto en frío tarda 280 a 400 ms y deja el hilo principal parado (medido el
// 15-nov en un portátil con Intel Arc: era el mayor parón del arranque). Por eso se pide en un
// Worker, con un OffscreenCanvas, y la página sigue a lo suyo; solo si el navegador no deja
// WebGL en un Worker se mira aquí, cuando la página ya está quieta.
(function () {
  const raiz = document.documentElement;
  const q = new URLSearchParams(location.search).get("ligero");
  if (q === "1") { raiz.setAttribute("data-ligero", ""); return; }
  if (q === "0") return;
  // Una vez por visita: lo que salió se guarda para las demás páginas de esta pestaña.
  const CLAVE = "tbgf-ligero";
  let visto = null;
  try { visto = sessionStorage.getItem(CLAVE); } catch (e) { /* sin almacenamiento */ }
  if (visto === "1") { raiz.setAttribute("data-ligero", ""); return; }
  if (visto === "0") return;
  const guardar = (v) => { try { sessionStorage.setItem(CLAVE, v); } catch (e) { /* sin almacenamiento */ } };
  const ligero = () => raiz.setAttribute("data-ligero", "");

  // Lo mismo en los dos sitios: «1» es dibujo por software, «0» hay gráfica y «?» no se pudo mirar.
  const mirar = (Lienzo) => {
    try {
      if (typeof WebGLRenderingContext === "undefined") return "?";
      const lienzo = Lienzo();
      const op = { failIfMajorPerformanceCaveat: true };
      const gl = lienzo.getContext("webgl2", op) || lienzo.getContext("webgl", op);
      if (!gl) return "1";
      const soltar = gl.getExtension("WEBGL_lose_context");
      if (soltar) soltar.loseContext();
      return "0";
    } catch (e) {
      return "?";
    }
  };
  const aqui = () => {
    const ir = () => {
      const v = mirar(() => document.createElement("canvas"));
      if (v !== "?") guardar(v);
      if (v === "1") ligero();
    };
    if ("requestIdleCallback" in window) requestIdleCallback(ir, { timeout: 3000 });
    else setTimeout(ir, 1500);
  };
  const enWorker = () => {
    let w;
    try {
      const codigo = `const mirar = ${mirar};
        postMessage(typeof OffscreenCanvas === "undefined" ? "?" : mirar(() => new OffscreenCanvas(1, 1)));`;
      w = new Worker(URL.createObjectURL(new Blob([codigo], { type: "text/javascript" })));
    } catch (e) {
      return aqui();
    }
    w.onmessage = (e) => {
      w.terminate();
      if (e.data === "?") return aqui();
      guardar(e.data);
      if (e.data === "1") ligero();
    };
    w.onerror = () => { w.terminate(); aqui(); };
  };
  // Aun en el Worker, el contexto lo crea el proceso de la gráfica, y ahí compite con el primer
  // dibujado de la página y con el scroll. Se espera a que la página esté tranquila, y 800 ms
  // más para no coincidir con la demo, que se enciende entonces: sin gráfica, el modo ligero
  // llega un par de segundos después de cargar.
  const luego = () => setTimeout(enWorker, 800);
  if (window.tbgfQuieta) luego();
  else document.addEventListener("tbgf:quieta", luego, { once: true });
})();

/* ---------------------------------------------------------- el otro idioma */
// Cada idioma tiene su dirección (/ y /en/): un botón que cambiara los textos con
// JavaScript dejaría a los buscadores viendo solo una. Nunca se redirige solo por el idioma
// del navegador (rompe el botón Atrás y deja a Google en una sola versión). Lo que se hace:
// la PRIMERA vez, si el navegador pide el otro idioma, una frase fina arriba lo ofrece; lo
// que elijas (el enlace, la X o el botón de idioma de la barra) se guarda, y desde entonces
// el arranque del <head> te lleva al tuyo. Sin elección guardada no se mueve nada.
(function () {
  const guardar = (idioma) => { try { localStorage.setItem("tbgf-lang", idioma); } catch (e) { /* sin almacenamiento */ } };
  document.querySelectorAll("a[hreflang]").forEach((a) =>
    a.addEventListener("click", () => guardar(a.getAttribute("hreflang"))));

  const aqui = document.documentElement.lang;
  let elegido = null;
  try { elegido = localStorage.getItem("tbgf-lang"); } catch (e) { return; }
  if (elegido) return;
  const pide = [...(navigator.languages || [navigator.language || ""])]
    .map((l) => String(l).slice(0, 2).toLowerCase())
    .find((l) => l === "es" || l === "en");
  const otra = document.querySelector("nav.barra .idioma");
  if (!pide || pide === aqui || !otra) return;

  const linea = document.createElement("p");
  linea.className = "otro-idioma";
  linea.lang = pide;
  const enlace = document.createElement("a");
  enlace.href = otra.getAttribute("href");
  enlace.hreflang = pide;
  enlace.textContent = pide === "en" ? "Read it in English" : "Leerla en español";
  enlace.addEventListener("click", () => guardar(pide));
  const frase = document.createElement("span");
  frase.textContent = pide === "en" ? "This page is also in English." : "Esta página también está en español.";
  const cerrar = document.createElement("button");
  cerrar.type = "button";
  cerrar.setAttribute("aria-label", pide === "en" ? "Stay in Spanish" : "Seguir en inglés");
  cerrar.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>';
  cerrar.addEventListener("click", () => { guardar(aqui); linea.remove(); });
  linea.append(frase, enlace, cerrar);
  document.querySelector("nav.barra")?.after(linea);
})();

/* ---------------------------------------------------------------- preguntas */
// Una pregunta abierta a la vez, y el cambio de una a otra en un solo movimiento. <details>
// no da nada de esto solo, así que `data-abierta` manda en el CSS y `open` se queda un
// ratito más mientras dura la transición de cierre.
document.querySelectorAll(".faq").forEach((lista) => {
  const items = Array.from(lista.querySelectorAll("details"));
  const ms = parseFloat(getComputedStyle(lista).getPropertyValue("--faq-ms")) || 380;
  const pendientes = new Map();
  const cerrar = (item) => {
    if (!item.open) return;
    item.removeAttribute("data-abierta");
    clearTimeout(pendientes.get(item));
    pendientes.set(item, setTimeout(() => {
      if (!item.hasAttribute("data-abierta")) item.open = false;
      pendientes.delete(item);
    }, ms));
  };
  const abrir = (item) => {
    clearTimeout(pendientes.get(item));
    pendientes.delete(item);
    item.open = true;
    requestAnimationFrame(() => item.setAttribute("data-abierta", ""));
  };
  items.forEach((item) => {
    item.querySelector("summary").addEventListener("click", (evento) => {
      evento.preventDefault();
      const estaAbierta = item.hasAttribute("data-abierta");
      items.forEach((otra) => { if (otra !== item) cerrar(otra); });
      if (estaAbierta) cerrar(item);
      else abrir(item);
    });
  });
});

/* ------------------------------------------------------- el índice de la izquierda */
// La sección en la que estás es la última cuyo título ya ha pasado del primer tercio de la
// ventana; al llegar al final de la página, la última.
(function () {
  const indice = document.querySelector(".leer-indice");
  if (!indice) return;
  const enlaces = [...indice.querySelectorAll('a[href^="#"]')];
  const secciones = enlaces.map((a) => document.getElementById(decodeURIComponent(a.hash.slice(1))));
  if (!secciones.length || secciones.some((s) => !s)) return;
  let vivo = null;
  let pendiente = false;
  const marcar = () => {
    pendiente = false;
    const linea = innerHeight * 0.33;
    let cual = 0;
    secciones.forEach((s, i) => { if (s.getBoundingClientRect().top <= linea) cual = i; });
    if (innerHeight + scrollY >= document.documentElement.scrollHeight - 4) cual = secciones.length - 1;
    const enlace = enlaces[cual];
    if (enlace === vivo) return;
    if (vivo) vivo.removeAttribute("aria-current");
    enlace.setAttribute("aria-current", "true");
    vivo = enlace;
    if (indice.scrollHeight > indice.clientHeight + 1) {
      indice.scrollTop = enlace.offsetTop - indice.clientHeight / 2 + enlace.offsetHeight / 2;
    }
    if (indice.scrollWidth > indice.clientWidth + 1) {
      indice.scrollLeft = enlace.offsetLeft - indice.clientWidth / 2 + enlace.offsetWidth / 2;
    }
  };
  addEventListener("scroll", () => {
    if (pendiente) return;
    pendiente = true;
    requestAnimationFrame(marcar);
  }, { passive: true });
  addEventListener("resize", marcar);
  marcar();
})();

/* --------------------------------------- las pestañas de la comparativa, en el móvil */
// La fila de «¿Cuál usas ahora?» se desliza de lado. Cambiar de panel ya va sin esto (son
// botones de radio y el CSS enseña el suyo); esto trae a la vista la pestaña tocada si estaba
// cortada en el borde.
(function () {
  const fila = document.querySelector(".rivales");
  if (!fila) return;
  fila.addEventListener("change", (e) => {
    const pestana = e.target.closest(".pestana");
    if (!pestana) return;
    const f = fila.getBoundingClientRect();
    const p = pestana.getBoundingClientRect();
    const margen = 20;
    const falta = p.left < f.left + margen ? p.left - f.left - margen : p.right > f.right - margen ? p.right - f.right + margen : 0;
    if (falta) fila.scrollBy({ left: falta, behavior: "smooth" });
  });
})();

/* ----------------------------------------------------------- el menú del móvil */
// Sin JavaScript es un <details>, que se abre y se cierra solo. Con él, al tocar «Menú» la
// píldora de la barra crece hacia abajo con las páginas dentro, y al cerrar se recoge. Al
// abrir, el foco va a la primera página; Escape cierra y lo devuelve a «Menú»; tocar fuera o
// elegir una página también cierra.
document.querySelectorAll("details.menu").forEach((menu) => {
  const nav = menu.closest("nav.barra");
  const pildora = menu.closest(".barra-dentro");
  const boton = menu.querySelector("summary");
  const panel = menu.querySelector(".menu-panel");
  if (!nav || !pildora || !boton || !panel) return;
  menu.classList.add("con-js");
  menu.open = true;
  if (!panel.id) panel.id = "menu-paginas";
  boton.setAttribute("role", "button");
  boton.setAttribute("aria-controls", panel.id);
  boton.setAttribute("aria-expanded", "false");
  let medido = -1;
  const medir = () => {
    const alto = panel.offsetHeight;
    if (alto === medido || !alto) return;
    medido = alto;
    pildora.style.setProperty("--menu-alto", `${alto}px`);
  };
  // El ResizeObserver lo mide en el primer diseño: medirlo aquí forzaba otro a mitad del arranque.
  if ("ResizeObserver" in window) new ResizeObserver(medir).observe(panel);
  else medir();
  const abierto = () => nav.classList.contains("menu-abierto");
  const poner = (abrir, foco) => {
    if (abrir) medir();
    nav.classList.toggle("menu-abierto", abrir);
    menu.classList.toggle("abierto", abrir);
    boton.setAttribute("aria-expanded", String(abrir));
    if (!foco) return;
    if (abrir) panel.querySelector("a")?.focus({ preventScroll: true });
    else boton.focus({ preventScroll: true });
  };
  boton.addEventListener("click", (e) => { e.preventDefault(); poner(!abierto(), !abierto()); });
  panel.querySelectorAll("a").forEach((a) => a.addEventListener("click", () => poner(false, false)));
  addEventListener("keydown", (e) => { if (e.key === "Escape" && abierto()) poner(false, true); });
  document.addEventListener("click", (e) => { if (abierto() && !pildora.contains(e.target)) poner(false, false); });
});

/* --------------------------------------------- la barra se compacta al bajar */
// Como la de cattory.co/es: pasados 40 px de scroll, la píldora se estrecha hasta lo justo de
// lo que lleva dentro (el CSS hace el movimiento, aquí solo se mide y se cambia de estado).
(function () {
  const nav = document.querySelector("nav.barra");
  const pildora = nav && nav.querySelector(".barra-dentro");
  if (!pildora) return;
  const medir = () => {
    const estilo = getComputedStyle(pildora);
    const piezas = [...pildora.children].filter((h) => h.offsetWidth > 0);
    const huecos = parseFloat(estilo.columnGap) * (piezas.length - 1);
    const relleno = parseFloat(estilo.paddingLeft) + parseFloat(estilo.paddingRight);
    const justo = piezas.reduce((total, h) => total + h.getBoundingClientRect().width, 0) + huecos + relleno + 8;
    pildora.style.setProperty("--justo", `${Math.ceil(justo)}px`);
  };
  let compacta = null;
  const mirar = () => {
    const toca = scrollY > 40;
    if (toca === compacta) return;
    compacta = toca;
    nav.classList.toggle("compacta", toca);
  };
  medir();
  mirar();
  requestAnimationFrame(() => requestAnimationFrame(() => nav.classList.add("anima")));
  addEventListener("scroll", mirar, { passive: true });
  addEventListener("resize", medir);
  addEventListener("load", medir);
  if (document.fonts) document.fonts.ready.then(medir);
})();

/* ------------------------------------- las flechas de las notas de la portada */
// Cada nota con `data-flecha` señala un texto. Se miden la nota y el texto en la página y se
// traza una curva de rotulador entre los dos; la punta se abre desde el final exacto del trazo,
// en la dirección en la que llega («esto sigue roto, esta flecha bro omg», Munito, en winshotx).
// Debajo, un halo con el fondo y la misma luz del titular, para que nada que pase por detrás
// parezca chocar con ella. Solo se vuelve a medir cuando cambia el tamaño o llega la letra.
(function () {
  const portada = document.querySelector(".portada");
  const lienzo = portada && portada.querySelector(".garabatos");
  if (!lienzo) return;
  const NS = "http://www.w3.org/2000/svg";
  const nuevo = (tipo, atributos) => {
    const el = document.createElementNS(NS, tipo);
    for (const [k, v] of Object.entries(atributos)) el.setAttribute(k, v);
    return el;
  };
  const luz = nuevo("radialGradient", { id: "garabatos-luz", gradientUnits: "userSpaceOnUse" });
  const paradaFin = nuevo("stop", { class: "luz-fin" });
  luz.append(nuevo("stop", { offset: "0", class: "luz" }), paradaFin);
  const defs = nuevo("defs", {});
  defs.append(luz);
  lienzo.append(defs);
  const flechas = [...document.querySelectorAll(".portada .fan[data-flecha]")].map((nota) => {
    const halo = nuevo("path", { class: "halo" });
    const haloLuz = nuevo("path", { class: "halo-luz", stroke: "url(#garabatos-luz)" });
    const trazo = nuevo("path", { class: "trazo" });
    lienzo.append(halo, haloLuz, trazo);
    return { nota, caminos: [halo, haloLuz, trazo], destino: document.querySelector(nota.dataset.flecha) };
  });

  const copiarLuz = (base) => {
    const antes = getComputedStyle(portada, "::before");
    const m = antes.backgroundImage.match(/radial-gradient\(([\d.]+)% ([\d.]+)% at ([\d.]+)% ([\d.]+)%.*\s([\d.]+)%\)$/);
    if (!m) return false;
    const p = portada.getBoundingClientRect();
    const ancho = p.width;
    const alto = parseFloat(antes.height);
    const arriba = p.top + parseFloat(antes.top) - base.top;
    const [rx, ry, cx, cy, hasta] = m.slice(1).map((v) => parseFloat(v) / 100);
    const x = p.left - base.left + cx * ancho;
    const y = arriba + cy * alto;
    luz.setAttribute("cx", x);
    luz.setAttribute("cy", y);
    luz.setAttribute("r", rx * ancho);
    luz.setAttribute("gradientTransform", `translate(${x} ${y}) scale(1 ${(ry * alto) / (rx * ancho)}) translate(${-x} ${-y})`);
    paradaFin.setAttribute("offset", hasta);
    return true;
  };

  const letra = (el, alFinal) => {
    const textos = [];
    const paseo = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    for (let n = paseo.nextNode(); n; n = paseo.nextNode()) if (n.data.trim()) textos.push(n);
    if (!textos.length) return null;
    const n = alFinal ? textos[textos.length - 1] : textos[0];
    const i = alFinal ? n.data.trimEnd().length - 1 : n.data.search(/\S/);
    const rango = document.createRange();
    rango.setStart(n, i);
    rango.setEnd(n, i + 1);
    return rango.getBoundingClientRect();
  };

  const dibujar = () => {
    if (getComputedStyle(lienzo).display === "none") return;
    const base = lienzo.getBoundingClientRect();
    lienzo.classList.toggle("con-luz", copiarLuz(base));
    const medidas = flechas.map((f) => {
      const t = f.destino && letra(f.destino, f.nota.dataset.punta === "final");
      const n = f.nota.firstElementChild.getBoundingClientRect();
      return { f, t, n, cuerpo: parseFloat(getComputedStyle(f.nota).fontSize) || 22 };
    });
    for (const { f, t, n, cuerpo } of medidas) {
      if (!t) continue;
      const hueco = cuerpo * 0.5;
      const alFinal = f.nota.dataset.punta === "final";
      const fin = [(alFinal ? t.right + hueco : t.left - hueco) - base.left, t.top + t.height * 0.58 - base.top];
      const ini = f.nota.dataset.sale === "arriba"
        ? [n.left + n.width * 0.42 - base.left, n.top - hueco * 0.8 - base.top]
        : [n.right + hueco * 0.8 - base.left, n.top + n.height * 0.42 - base.top];
      const d = [fin[0] - ini[0], fin[1] - ini[1]];
      const largo = Math.hypot(d[0], d[1]);
      if (largo < cuerpo * 2) { f.caminos.forEach((c) => c.removeAttribute("d")); continue; }
      const normal = [-d[1] / largo, d[0] / largo];
      const k = parseFloat(f.nota.dataset.curva) || 0;
      const c1 = [ini[0] + d[0] * 0.28 + normal[0] * k * 1.15 * largo, ini[1] + d[1] * 0.28 + normal[1] * k * 1.15 * largo];
      const c2 = [ini[0] + d[0] * 0.7 + normal[0] * k * 0.85 * largo, ini[1] + d[1] * 0.7 + normal[1] * k * 0.85 * largo];
      const atras = Math.atan2(c2[1] - fin[1], c2[0] - fin[0]);
      const pata = (giro, largoPata) => [fin[0] + Math.cos(atras + giro) * largoPata, fin[1] + Math.sin(atras + giro) * largoPata];
      const p1 = pata(0.47, cuerpo * 0.56);
      const p2 = pata(-0.56, cuerpo * 0.5);
      const r = (p) => `${p[0].toFixed(1)} ${p[1].toFixed(1)}`;
      const camino = `M${r(ini)}C${r(c1)} ${r(c2)} ${r(fin)}M${r(p1)}L${r(fin)}L${r(p2)}`;
      f.caminos.forEach((c) => c.setAttribute("d", camino));
    }
  };
  dibujar();
  new ResizeObserver(dibujar).observe(document.querySelector(".portada > .env"));
  addEventListener("load", dibujar);
  if (document.fonts) document.fonts.ready.then(dibujar);
})();

/* ------------------------------------------- las órdenes se copian con un clic */
// Una orden para pegar en la terminal se pincha entera y va al portapapeles, con su aviso de
// copiado ahí mismo («cuando le des click se copie, ¿no?», Munito, en winshotx). Vale con el
// teclado (Tab e Intro o espacio). Sin JS se queda como texto que se selecciona a mano.
(function () {
  const T = INGLES
    ? { copiar: "Copy", copiado: "Copied", fallo: "Selected: press Ctrl+C" }
    : { copiar: "Copiar", copiado: "Copiado", fallo: "Seleccionado: pulsa Ctrl+C" };
  const piezas = [...document.querySelectorAll("code")].filter((c) => /^winget\s/.test(c.textContent.trim()));
  if (!piezas.length) return;
  const dibujo = (clase, trazo) =>
    `<svg class="${clase}" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${trazo}</svg>`;
  const ICONOS =
    dibujo("ico-copiar", '<rect width="14" height="14" x="8" y="8" rx="2"/><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/>') +
    dibujo("ico-hecho", '<path d="M20 6 9 17l-5-5"/>');
  const oido = document.createElement("span");
  oido.className = "solo-lector";
  oido.setAttribute("aria-live", "polite");
  const globo = document.createElement("span");
  globo.className = "copiado copiado-globo";
  globo.setAttribute("aria-hidden", "true");
  document.body.append(oido, globo);
  let quitarGlobo = 0;

  async function copiar(texto, pieza) {
    try {
      await navigator.clipboard.writeText(texto);
      return true;
    } catch {
      const area = document.createElement("textarea");
      area.value = texto;
      area.setAttribute("readonly", "");
      area.style.cssText = "position:fixed;top:0;left:0;opacity:0;pointer-events:none";
      document.body.appendChild(area);
      area.select();
      let hecho = false;
      try { hecho = document.execCommand("copy"); } catch { hecho = false; }
      area.remove();
      if (hecho) return true;
      const rango = document.createRange();
      rango.setStart(pieza, 0);
      rango.setEndBefore(pieza.querySelector(".ico-copiar"));
      getSelection().removeAllRanges();
      getSelection().addRange(rango);
      return false;
    }
  }
  function ponerGlobo(pieza, frase, punto) {
    const trozos = [...pieza.getClientRects()];
    const r = (punto && trozos.find((q) => punto.y >= q.top && punto.y <= q.bottom)) || trozos[0];
    globo.textContent = frase;
    const medio = globo.offsetWidth / 2 + 8;
    const x = Math.min(innerWidth - medio, Math.max(medio, r.left + r.width / 2));
    globo.style.left = `${x + scrollX}px`;
    globo.style.top = `${r.top + scrollY}px`;
    globo.dataset.visible = "";
    clearTimeout(quitarGlobo);
    quitarGlobo = setTimeout(() => delete globo.dataset.visible, 1600);
  }
  piezas.forEach((pieza) => {
    const texto = pieza.textContent.replace(/\s+/g, " ").trim();
    pieza.classList.add("copiable", "en-frase");
    pieza.setAttribute("role", "button");
    pieza.tabIndex = 0;
    pieza.setAttribute("aria-label", `${T.copiar}: ${texto}`);
    pieza.title = T.copiar;
    pieza.insertAdjacentHTML("beforeend", ICONOS);
    let quitar = 0;
    const hacer = async (punto) => {
      const bien = await copiar(texto, pieza);
      ponerGlobo(pieza, bien ? T.copiado : T.fallo, punto);
      oido.textContent = "";
      oido.textContent = bien ? `${T.copiado}: ${texto}` : T.fallo;
      pieza.dataset.copiado = bien ? "si" : "no";
      clearTimeout(quitar);
      quitar = setTimeout(() => delete pieza.dataset.copiado, bien ? 1600 : 3200);
    };
    pieza.addEventListener("click", (e) => {
      if (String(getSelection()).length && pieza.contains(getSelection().anchorNode)) return;
      e.preventDefault();
      hacer({ x: e.clientX, y: e.clientY });
    });
    pieza.addEventListener("keydown", (e) => {
      if (e.key !== "Enter" && e.key !== " ") return;
      e.preventDefault();
      hacer(null);
    });
  });
})();

/* ------------------------------------------------- las cifras, en vivo de GitHub */
// Descargas y versiones, leídas de la API pública. Se guardan 15 minutos en este navegador
// para no gastar el cupo (60 consultas por hora sin cuenta). Si GitHub no contesta, se quedan
// las escritas en el HTML, medidas el día que se escribieron.
(function () {
  const nDescargas = document.getElementById("n-descargas");
  const nVersiones = document.getElementById("n-versiones");
  if (!nDescargas && !nVersiones) return;
  const REPO = "https://api.github.com/repos/Mun1to/TaskbarGroupsFluent";
  // En español, Intl no separa los miles de un número de cuatro cifras (1972); el HTML trae
  // «1.972», así que se le pide el punto siempre.
  const numero = new Intl.NumberFormat(INGLES ? "en-US" : "es-ES", { useGrouping: "always" });
  const pintar = (d) => {
    if (nDescargas && d.descargas > 0) nDescargas.textContent = numero.format(d.descargas);
    if (nVersiones && d.versiones > 0) nVersiones.textContent = numero.format(d.versiones);
  };
  (async () => {
    try {
      const c = JSON.parse(localStorage.getItem("tbgf-gh2") || "null");
      if (c && Date.now() - c.t < 15 * 60 * 1000 && c.d) return pintar(c.d);
    } catch (e) { /* sin almacenamiento */ }
    const ctrl = "AbortController" in window ? new AbortController() : null;
    const corte = setTimeout(() => ctrl && ctrl.abort(), 8000);
    try {
      const r = await fetch(`${REPO}/releases?per_page=100`, { headers: { Accept: "application/vnd.github+json" }, signal: ctrl?.signal });
      if (!r.ok) return;
      const versiones = (await r.json()).filter((v) => !v.draft && v.published_at);
      if (!versiones.length) return;
      let descargas = 0;
      versiones.forEach((v) => (v.assets || []).forEach((a) => { descargas += a.download_count || 0; }));
      const d = { descargas, versiones: versiones.filter((v) => !v.prerelease).length };
      try { localStorage.setItem("tbgf-gh2", JSON.stringify({ t: Date.now(), d })); } catch (e) { /* sin almacenamiento */ }
      pintar(d);
    } catch (e) {
      /* GitHub no contesta: se quedan las del HTML */
    } finally {
      clearTimeout(corte);
    }
  })();
})();

/* ------------------------------------------------------ las entradas al aparecer */
// El observador añade .in al asomar; la cascada la pone el CSS con --i. Dos redes: sin
// observador o sin movimiento, todo se ve desde el principio; y tras cargar, lo que ya esté a
// la vista se enciende aunque el observador no haya dicho nada. Nunca hay texto oculto esperando.
(function () {
  const items = [...document.querySelectorAll("[data-reveal]")];
  const ver = (el) => el.classList.add("in");
  if (!document.documentElement.classList.contains("motion") || !("IntersectionObserver" in window)) {
    items.forEach(ver);
    return;
  }
  const io = new IntersectionObserver((entradas) => {
    for (const e of entradas) if (e.isIntersecting) { ver(e.target); io.unobserve(e.target); }
  }, { threshold: 0.06, rootMargin: "0px 0px -6% 0px" });
  items.forEach((el) => io.observe(el));
  const rescate = () => items.forEach((el) => {
    if (el.classList.contains("in")) return;
    const r = el.getBoundingClientRect();
    if (r.top < innerHeight && r.bottom > 0) ver(el);
  });
  addEventListener("load", () => setTimeout(rescate, 400));
  setTimeout(rescate, 1500);
  document.querySelectorAll('a[href^="#"]').forEach((a) => a.addEventListener("click", () => {
    const t = document.getElementById(a.getAttribute("href").slice(1));
    if (t) t.querySelectorAll("[data-reveal]").forEach(ver);
  }));
})();
