/* La demo de la portada y los pasos de las funciones. Solo la carga la portada (las dos
   versiones, / y /en/), y no depende de nada: leer.js va aparte y no hace falta para esto.

   La demo es una pantalla de 1280 x 720 dibujada a tamaño real y reducida entera, con cuatro
   grupos anclados en la barra. Con ratón se abren al pasar por encima o con un clic, según lo
   que se elija; con el dedo, al tocar. El clic derecho saca su menú, como el de Windows. Las
   opciones son las del editor de la app, con las mismas reglas que PanelLayouts.cs: la
   cuadrícula usa las columnas del grupo, y la lista se parte en dos a partir de seis. */

const RAIZ = new URL(".", document.currentScript ? document.currentScript.src : location.href).href;
const EN = document.documentElement.lang === "en";
const GANANCIA = parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--motion-gain")) || 0;
const limitar = (n, a, b) => Math.min(b, Math.max(a, n));

/* ============================================================= la demo === */
(function () {
  const seccion = document.getElementById("pruebalo");
  const pantalla = document.getElementById("pantalla");
  if (!seccion || !pantalla) return;
  const env = seccion.querySelector(".env");
  const hueco = seccion.querySelector(".hueco-demo");
  const monitor = seccion.querySelector(".monitor");
  const esc = document.getElementById("escritorio");
  const panel = document.getElementById("panel");
  const nombre = document.getElementById("panel-nombre");
  const lista = document.getElementById("panel-apps");
  const todas = document.getElementById("panel-todas");
  const lapiz = document.getElementById("panel-lapiz");
  const salto = document.getElementById("salto");
  const aviso = document.getElementById("aviso-demo");
  const pistaTexto = document.getElementById("pista-texto");
  const pistaExtra = document.getElementById("pista-extra");
  const pistaFuera = document.getElementById("pista-demo");
  const zona = document.getElementById("zona-grupos");
  const reloj = document.getElementById("reloj");
  const conRaton = matchMedia("(hover: hover) and (pointer: fine)").matches;
  const movil = matchMedia("(max-width: 860px)");
  // Tamaños de verdad: la pantalla, la barra, el hueco que deja el panel y el margen con el
  // borde de la pantalla, como en PositionOverTaskbar. A pantalla completa en el móvil, la
  // demo no baja de este tamaño (más pequeña no se toca con el dedo): si no cabe entera, se ve
  // la parte de abajo del centro, donde viven la barra y el panel.
  const W = 1280, H = 720, BARRA = 48, HUECO = 8, MARGEN = 4, K_DEDO = 0.69;

  const T = EN
    ? {
        abrir: "Open the group", grupo: "Group", editar: "Edit the group", todas: "Open all",
        lanza: "On your PC, this would open “{app}”",
        lanzaTodas: "On your PC, this would open the {n} apps of “{g}” at once",
        lapiz: "The pencil opens the editor for “{g}” straight away",
        editarMenu: "“Edit group” opens the editor for “{g}” straight away",
        desanclar: "That is how it comes off the taskbar. Here it stays",
        pistaRaton: "Hover over a group on the taskbar",
        pistaClic: "Click a group on the taskbar",
        pistaToque: "Tap a group on the taskbar",
        pistaExtra: "Right-click it for its menu",
        tareas: "Tasks", editarGrupo: "Edit group", desanclarTxt: "Unpin from taskbar",
        verGrande: "See the demo full screen",
        locale: "en-GB",
      }
    : {
        abrir: "Abrir el grupo", grupo: "Grupo", editar: "Editar el grupo", todas: "Abrir todas",
        lanza: "En tu PC, esto abriría «{app}»",
        lanzaTodas: "En tu PC, esto abriría las {n} apps de «{g}» a la vez",
        lapiz: "El lápiz abre el editor de «{g}» sin pasar por la ventana principal",
        editarMenu: "«Editar grupo» abre el editor de «{g}» directamente",
        desanclar: "Así se quita de la barra. Aquí lo dejamos puesto",
        pistaRaton: "Pasa el ratón por un grupo de la barra",
        pistaClic: "Pulsa un grupo de la barra",
        pistaToque: "Toca un grupo de la barra",
        pistaExtra: "Con el clic derecho, su menú",
        tareas: "Tareas", editarGrupo: "Editar grupo", desanclarTxt: "Desanclar de la barra de tareas",
        verGrande: "Ver la demo a pantalla completa",
        locale: "es-ES",
      };

  // Los mismos cuatro grupos de la grabación del README, con apps de verdad y el icono que
  // les saca Windows. cols es la columna que se le pone al grupo en el editor.
  const A = (ico, n) => ({ ico, n });
  const GRUPOS = [
    { n: EN ? "Gaming" : "Juegos", cols: 3, emoji: "🎮", apps: [
      A("steam", "Steam"), A("xbox", "Xbox"), A("minecraft", "Minecraft"),
      A("valorant", "VALORANT"), A("discord", "Discord"), A("solitaire", "Solitaire"),
    ] },
    { n: "Dev", cols: 3, emoji: "💻", apps: [
      A("vscode", "Visual Studio Code"), A("terminal", "Terminal"), A("cursor", "Cursor"),
      A("docker", "Docker Desktop"), A("python", "Python"),
    ] },
    { n: EN ? "Creative" : "Diseño", cols: 3, apps: [
      A("davinci", "DaVinci Resolve"), A("obs", "OBS Studio"), A("gimp", "GIMP"),
      A("inkscape", "Inkscape"), A("paint", "Paint"), A("clipchamp", "Clipchamp"),
    ] },
    { n: EN ? "Office" : "Oficina", cols: 3, apps: [
      A("word", "Word"), A("excel", "Excel"), A("powerpoint", "PowerPoint"),
      A("outlook", "Outlook"), A("onenote", "OneNote"), A("teams", "Teams"),
    ] },
  ];
  const icoApp = (ico) => RAIZ + "assets/apps/" + ico + ".webp";

  const op = { forma: "cuadricula", abrir: conRaton ? "raton" : "clic", nombres: "si", tam: "100", cab: "arriba", tema: "oscuro" };
  let vista = { w: W, h: H }, abierto = null, ultimo = GRUPOS[0], abiertoEn = 0, tocado = false;
  let porRaton = false, menuDe = null, tHover = 0, tAviso = 0, tSalida = 0;

  // El icono del grupo en la barra: su emoji, o sus cuatro primeras apps.
  function icono(g, peq) {
    const s = document.createElement("span");
    s.setAttribute("aria-hidden", "true");
    if (g.emoji) {
      s.className = "emoji" + (peq ? " peq" : "");
      s.textContent = g.emoji;
      return s;
    }
    s.className = "mini" + (peq ? " peq" : "");
    g.apps.slice(0, 4).forEach((a) => {
      const c = document.createElement("img");
      c.src = icoApp(a.ico);
      c.alt = "";
      s.appendChild(c);
    });
    return s;
  }

  GRUPOS.forEach((g) => {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "bt grupo";
    b.setAttribute("aria-expanded", "false");
    b.setAttribute("aria-controls", "panel");
    b.setAttribute("aria-label", T.abrir + " " + g.n);
    b.appendChild(icono(g));
    zona.appendChild(b);
    g.btn = b;

    b.addEventListener("pointerenter", (e) => {
      if (e.pointerType !== "mouse" || op.abrir !== "raton" || menuDe) return;
      clearTimeout(tHover);
      clearTimeout(tSalida);
      // Como la app: espera un instante antes de abrir, salvo que ya haya un panel
      // abierto, en cuyo caso cambia de grupo al momento.
      tHover = setTimeout(() => { porRaton = true; abrir(g); }, abierto ? 0 : 200);
    });
    b.addEventListener("pointerleave", (e) => {
      clearTimeout(tHover);
      if (e.pointerType === "mouse") salir();
    });
    b.addEventListener("click", (e) => {
      e.stopPropagation();
      clearTimeout(tHover);
      if (menuDe) { cerrarMenu(); return; }
      // Si se acaba de abrir al pasar el ratón, el clic no lo cierra de golpe.
      if (abierto === g && performance.now() - abiertoEn > 450) cerrar();
      else { porRaton = false; abrir(g); }
    });
    b.addEventListener("contextmenu", (e) => {
      e.preventDefault();
      e.stopPropagation();
      clearTimeout(tHover);
      menu(g);
    });
  });

  // Abierto al pasar el ratón, se cierra solo cuando el cursor lleva un rato fuera del panel
  // y de su icono: 600 ms, como el vigilante de la app.
  function salir() {
    if (!porRaton || !abierto) return;
    clearTimeout(tSalida);
    tSalida = setTimeout(() => {
      if (porRaton && abierto && !panel.matches(":hover") && !abierto.btn.matches(":hover")) cerrar();
    }, 600);
  }
  panel.addEventListener("pointerenter", () => clearTimeout(tSalida));
  panel.addEventListener("pointerleave", (e) => { if (e.pointerType === "mouse") salir(); });

  // Qué rejilla y qué baldosa toca, como PanelLayouts.Plan.
  function plan(g) {
    const n = g.apps.length, nombres = op.nombres === "si";
    if (op.forma === "fila") return { cls: "p-fila", t: nombres ? "t-nombre" : "t-icono" };
    if (op.forma === "columna") return { cls: "p-columna", t: nombres ? "t-linea" : "t-icono" };
    if (op.forma === "lista") return { cls: "p-cuadricula", t: "t-celda", cols: n > 6 ? 2 : 1 };
    return { cls: "p-cuadricula", t: nombres ? "t-nombre" : "t-icono",
             cols: g.cols || Math.min(Math.max(n, 1), nombres ? 5 : 4) };
  }

  function pintar(g) {
    const p = plan(g);
    nombre.textContent = g.n;
    panel.setAttribute("aria-label", T.grupo + " " + g.n);
    lapiz.setAttribute("aria-label", T.editar + " " + g.n);
    todas.setAttribute("aria-label", T.todas);
    panel.classList.toggle("claro", op.tema === "claro");
    panel.classList.toggle("cab-abajo", op.cab === "abajo");
    panel.classList.toggle("sin-cab", op.cab === "no");
    panel.style.setProperty("--k", Number(op.tam) / 100);
    lista.className = "panel-apps " + p.cls + " " + p.t;
    lista.style.setProperty("--cols", p.cols || 1);
    lista.textContent = "";
    g.apps.forEach((a) => {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "app";
      b.title = a.n;
      const img = document.createElement("img");
      img.className = "ico-app";
      img.src = icoApp(a.ico);
      img.alt = "";
      const s = document.createElement("span");
      s.className = "app-nombre";
      s.textContent = a.n;
      b.append(img, s);
      b.addEventListener("click", (e) => {
        e.stopPropagation();
        avisar(T.lanza.replace("{app}", a.n));
        // El panel de verdad se cierra solo al lanzar una app.
        cerrar(true);
      });
      lista.appendChild(b);
    });
  }

  // El centro de un botón de la barra, en píxeles de la pantalla de mentira.
  function centro(b) {
    let x = b.offsetWidth / 2, e = b;
    while (e && e !== esc) { x += e.offsetLeft; e = e.offsetParent; }
    return x;
  }
  // Lo que se ve de la pantalla: entera, o el centro de abajo a pantalla completa en el móvil.
  function limites() {
    const izq = (W - vista.w) / 2;
    return { izq: izq + MARGEN, der: izq + vista.w - MARGEN };
  }

  // Encima de su botón, sin salirse de lo que se ve; si no cabe (una fila de siete apps en
  // la parte que se ve en el móvil), se encoge.
  function colocar() {
    if (!abierto) return;
    const pw = panel.offsetWidth, ph = panel.offsetHeight, lim = limites();
    const fit = Math.min(1, (lim.der - lim.izq) / pw, (vista.h - BARRA - HUECO - MARGEN) / ph);
    const w = pw * fit;
    const left = Math.max(lim.izq, Math.min(lim.der - w, centro(abierto.btn) - w / 2));
    panel.style.setProperty("--fit", fit.toFixed(4));
    panel.style.left = left.toFixed(1) + "px";
    panel.style.bottom = BARRA + HUECO + "px";
  }

  function abrir(g, forzar) {
    cerrarMenu();
    if (abierto && abierto !== g) {
      abierto.btn.classList.remove("activo");
      abierto.btn.setAttribute("aria-expanded", "false");
    }
    const cambia = forzar || abierto !== g || !panel.classList.contains("visible");
    abierto = ultimo = g;
    abiertoEn = performance.now();
    tocado = true;
    if (cambia) {
      // Vuelve al estado cerrado sin transición, pinta, coloca y anima la entrada otra vez:
      // cada grupo sale desde la barra, como en Windows.
      panel.classList.add("sin-trans");
      panel.classList.remove("visible");
      pintar(g);
      colocar();
      void panel.offsetWidth;
      panel.classList.remove("sin-trans");
      void panel.offsetWidth;
      panel.classList.add("visible");
    }
    g.btn.classList.add("activo");
    g.btn.setAttribute("aria-expanded", "true");
    esc.classList.add("con-panel");
  }

  function cerrar(devolverFoco) {
    if (!abierto) return;
    const g = abierto;
    g.btn.classList.remove("activo");
    g.btn.setAttribute("aria-expanded", "false");
    abierto = null;
    porRaton = false;
    clearTimeout(tSalida);
    panel.classList.remove("visible");
    esc.classList.remove("con-panel");
    if (devolverFoco && panel.contains(document.activeElement)) g.btn.focus({ preventScroll: true });
  }

  // El menú del clic derecho, como el que Windows enseña en un botón anclado: las tareas que
  // registra la app y, debajo, el propio acceso y «Desanclar».
  function menu(g) {
    cerrar();
    tocado = true;
    salto.textContent = "";
    const titulo = document.createElement("div");
    titulo.className = "salto-titulo";
    titulo.textContent = T.tareas;
    salto.appendChild(titulo);
    const item = (ico, texto, accion) => {
      const b = document.createElement("button");
      b.type = "button";
      b.setAttribute("role", "menuitem");
      if (typeof ico === "string") b.innerHTML = '<svg aria-hidden="true"><use href="#' + ico + '"/></svg>';
      else b.appendChild(ico);
      const s = document.createElement("span");
      s.textContent = texto;
      b.appendChild(s);
      b.addEventListener("click", (e) => { e.stopPropagation(); cerrarMenu(); accion(); });
      salto.appendChild(b);
    };
    item("i-abrir-todas", T.todas, () => avisar(T.lanzaTodas.replace("{n}", g.apps.length).replace("{g}", g.n)));
    item("i-pencil", T.editarGrupo, () => avisar(T.editarMenu.replace("{g}", g.n)));
    salto.appendChild(document.createElement("hr"));
    item(icono(g, true), g.n, () => { porRaton = false; abrir(g); });
    item("i-pin-off", T.desanclarTxt, () => avisar(T.desanclar));
    menuDe = g;
    ponerMenu();
    salto.classList.add("visible");
    esc.classList.add("con-panel");
  }
  function ponerMenu() {
    if (!menuDe) return;
    const lim = limites(), w = salto.offsetWidth;
    salto.style.left = Math.max(lim.izq, Math.min(lim.der - w, centro(menuDe.btn) - w / 2)).toFixed(1) + "px";
    salto.style.bottom = BARRA + HUECO + "px";
  }

  function cerrarMenu() {
    if (!menuDe) return;
    menuDe = null;
    salto.classList.remove("visible");
    if (!abierto) esc.classList.remove("con-panel");
  }

  function avisar(msg) {
    aviso.textContent = msg;
    aviso.classList.add("visible");
    clearTimeout(tAviso);
    tAviso = setTimeout(() => aviso.classList.remove("visible"), 2800);
  }

  todas.addEventListener("click", (e) => {
    e.stopPropagation();
    if (!abierto) return;
    const g = abierto;
    avisar(T.lanzaTodas.replace("{n}", g.apps.length).replace("{g}", g.n));
    cerrar(true);
  });
  lapiz.addEventListener("click", (e) => {
    e.stopPropagation();
    if (!abierto) return;
    avisar(T.lapiz.replace("{g}", abierto.n));
    cerrar(true);
  });
  panel.addEventListener("click", (e) => e.stopPropagation());
  salto.addEventListener("click", (e) => e.stopPropagation());
  salto.addEventListener("contextmenu", (e) => e.preventDefault());
  // Un clic en el escritorio vacío cierra, como el panel real al perder el foco.
  esc.addEventListener("click", () => { cerrar(); cerrarMenu(); });
  document.addEventListener("keydown", (e) => {
    if (e.key !== "Escape" || (!abierto && !menuDe)) return;
    e.preventDefault();
    cerrar(true);
    cerrarMenu();
  });

  // Las opciones: cada cambio vuelve a abrir el grupo para que se vea.
  const botones = [...seccion.querySelectorAll("[data-op]")];
  const marcar = () => botones.forEach((o) => o.setAttribute("aria-pressed", op[o.dataset.op] === o.dataset.val ? "true" : "false"));
  botones.forEach((b) => b.addEventListener("click", () => {
    op[b.dataset.op] = b.dataset.val;
    marcar();
    pistas();
    if (b.dataset.op === "abrir") return;
    porRaton = false;
    abrir(abierto || ultimo, true);
  }));

  function pistas() {
    const texto = !conRaton ? T.pistaToque : op.abrir === "raton" ? T.pistaRaton : T.pistaClic;
    pistaTexto.textContent = texto;
    pistaExtra.textContent = conRaton ? T.pistaExtra : "";
    if (pistaFuera) pistaFuera.textContent = conRaton ? texto + ". " + T.pistaExtra + "." : texto + ".";
  }

  /* ---------- el tamaño: entera a lo ancho, y a pantalla completa en el móvil, tocable ---------- */
  let capa = false;
  function medir() {
    let k;
    if (capa) {
      // A pantalla completa se mide el hueco que deja lo demás, no la propia pantalla.
      const borde = monitor.offsetWidth - monitor.clientWidth + parseFloat(getComputedStyle(monitor).paddingLeft) * 2;
      const cw = Math.max(200, hueco.clientWidth - borde);
      const ch = Math.max(160, hueco.clientHeight - borde);
      const entera = Math.min(cw / W, ch / H);
      if (entera >= K_DEDO) {
        k = Math.min(1, entera);
        vista = { w: W, h: H };
      } else {
        k = K_DEDO;
        vista = { w: Math.min(W, Math.max(480, cw / k)), h: Math.min(H, ch / k) };
        k = Math.min(k, cw / vista.w);
      }
      pantalla.style.width = Math.floor(vista.w * k) + "px";
      pantalla.style.height = Math.floor(vista.h * k) + "px";
    } else {
      pantalla.style.removeProperty("width");
      pantalla.style.removeProperty("height");
      vista = { w: W, h: H };
      k = pantalla.clientWidth / W;
      if (!k) return;
    }
    pantalla.style.setProperty("--escala", k.toFixed(5));
    aviso.style.top = H - vista.h + 16 + "px";
    colocar();
    ponerMenu();
  }

  /* En el móvil y sin abrir, la pantalla es un botón y lo de dentro no se alcanza ni con el
     tabulador; a pantalla completa (sin la del navegador, que la anuncia con un aviso
     encima) se maneja con el dedo, la página de debajo queda quieta y Atrás la cierra. */
  const botonCerrar = seccion.querySelector('[data-capa="cerrar"]');
  const botonAbrir = seccion.querySelector('[data-capa="abrir"]');
  let entrada = false, saltarVuelta = false, quien = null, dormidos = [];

  function ponerPapel() {
    const boton = movil.matches && !capa;
    esc.inert = boton;
    if (boton) {
      pantalla.setAttribute("role", "button");
      pantalla.tabIndex = 0;
      pantalla.setAttribute("aria-label", T.verGrande);
    } else {
      pantalla.removeAttribute("role");
      pantalla.removeAttribute("tabindex");
      pantalla.removeAttribute("aria-label");
    }
  }

  function abrirCapa(e) {
    if (capa || !movil.matches) return;
    quien = e && e.currentTarget instanceof HTMLElement ? e.currentTarget : null;
    capa = true;
    // La sección se queda con su alto: la página de debajo no se mueve ni un píxel.
    seccion.style.minHeight = seccion.offsetHeight + "px";
    seccion.classList.add("en-grande");
    document.documentElement.classList.add("capa-abierta");
    env.setAttribute("role", "dialog");
    env.setAttribute("aria-modal", "true");
    dormidos = [...document.body.children].filter((el) => el !== seccion && !el.inert && el.tagName !== "SCRIPT");
    seccion.parentElement !== document.body &&
      dormidos.push(...[...seccion.parentElement.children].filter((el) => el !== seccion && !el.inert));
    dormidos = dormidos.filter((el) => !el.contains(seccion));
    dormidos.forEach((el) => (el.inert = true));
    ponerPapel();
    medir();
    try {
      history.pushState({ capaDemo: true }, "");
      entrada = true;
    } catch (err) {
      entrada = false;
    }
    if (botonCerrar) botonCerrar.focus({ preventScroll: true });
  }

  function quitarCapa() {
    if (!capa) return;
    capa = false;
    seccion.classList.remove("en-grande");
    document.documentElement.classList.remove("capa-abierta");
    seccion.style.removeProperty("min-height");
    env.removeAttribute("role");
    env.removeAttribute("aria-modal");
    dormidos.forEach((el) => (el.inert = false));
    dormidos = [];
    ponerPapel();
    medir();
    if (quien && quien.isConnected) quien.focus({ preventScroll: true });
  }

  // «Cerrar» y Escape: se cierra ya, y se gasta la entrada del historial para que Atrás no
  // haga nada raro luego.
  function cerrarCapa() {
    if (!capa) return;
    const gastar = entrada && history.state && history.state.capaDemo;
    entrada = false;
    quitarCapa();
    if (gastar) {
      saltarVuelta = true;
      history.back();
    }
  }
  addEventListener("popstate", () => {
    if (saltarVuelta) { saltarVuelta = false; return; }
    entrada = false;
    quitarCapa();
  });
  document.addEventListener("keydown", (e) => {
    if (!capa || e.key !== "Escape" || e.defaultPrevented) return;
    e.preventDefault();
    cerrarCapa();
  });
  pantalla.addEventListener("click", (e) => { if (movil.matches && !capa) abrirCapa(e); });
  pantalla.addEventListener("keydown", (e) => {
    if (!movil.matches || capa || (e.key !== "Enter" && e.key !== " ")) return;
    e.preventDefault();
    abrirCapa(e);
  });
  if (botonAbrir) botonAbrir.addEventListener("click", abrirCapa);
  if (botonCerrar) botonCerrar.addEventListener("click", cerrarCapa);
  movil.addEventListener("change", () => { if (!movil.matches) cerrarCapa(); ponerPapel(); medir(); });

  function tic() {
    const d = new Date();
    reloj.innerHTML = d.toLocaleTimeString(T.locale, { hour: "2-digit", minute: "2-digit" }) + "<br>" +
      d.toLocaleDateString(T.locale, { day: "2-digit", month: "2-digit", year: "numeric" });
  }

  marcar();
  pistas();
  ponerPapel();
  medir();
  tic();
  setInterval(tic, 30000);
  const vigia = new ResizeObserver(medir);
  vigia.observe(pantalla);
  if (hueco) vigia.observe(hueco);

  // Al llegar, el primer grupo se abre solo para que se vea qué hace la app.
  setTimeout(() => { if (!tocado) abrir(GRUPOS[0]); }, 900);
})();

/* ================================================== las funciones, paso a paso === */
/* Agrupa, ábrelo y hazlo tuyo: un paso cada 260 px de rueda mientras la sección está pegada
   (GUSTOS, 8: «por cada 2 o 3 scroll ya pasar a la siguiente fase»). La rueda es la del
   navegador: solo se mira por dónde va. Es el motor de winshotx.com. */
(function () {
  const pista = document.getElementById("momentos-pista");
  if (!pista) return;
  const articulos = [...document.querySelectorAll(".momento")];
  const fotos = [...document.querySelectorAll(".momentos-fotos > *")];
  const pasos = [...document.querySelectorAll("#pasos-momento button")];
  const PASO = 260;
  let actual = -1;
  const poner = (i) => {
    if (i === actual) return;
    actual = i;
    articulos.forEach((a, j) => a.classList.toggle("activo", j === i));
    fotos.forEach((f, j) => f.classList.toggle("activa", j === i));
    pasos.forEach((b, j) => (j === i ? b.setAttribute("aria-current", "step") : b.removeAttribute("aria-current")));
    document.dispatchEvent(new CustomEvent("tbgf:paso"));
  };
  let pendiente = false;
  const mirar = () => {
    pendiente = false;
    if (innerWidth <= 860) return poner(0);
    const recorrido = -pista.getBoundingClientRect().top;
    poner(limitar(Math.floor((recorrido + PASO * 0.35) / PASO), 0, articulos.length - 1));
  };
  addEventListener("scroll", () => {
    if (pendiente) return;
    pendiente = true;
    requestAnimationFrame(mirar);
  }, { passive: true });
  addEventListener("resize", mirar);

  // El vacío de arriba y de abajo de lo pegado (estilos.css, .momentos): se mide lo que sobra
  // entre el borde de la pantalla pegada y su contenido, y se deja solo el aire de una
  // sección normal. Se vuelve a medir si cambia el tamaño o llegan las letras.
  const seccion = pista.closest(".momentos");
  const pegado = pista.querySelector(".momentos-pegado");
  const ajustarAire = () => {
    if (!pegado || innerWidth <= 860) {
      seccion.style.removeProperty("--momentos-sube");
      seccion.style.removeProperty("--momentos-baja");
      return;
    }
    const caja = pegado.getBoundingClientRect();
    const hijos = [...pegado.children].map((h) => h.getBoundingClientRect()).filter((r) => r.height);
    if (!hijos.length) return;
    const arriba = Math.min(...hijos.map((r) => r.top)) - caja.top;
    const abajo = caja.bottom - Math.max(...hijos.map((r) => r.bottom));
    const aire = parseFloat(getComputedStyle(document.querySelector(".seccion") || seccion).paddingTop) || 0;
    seccion.style.setProperty("--momentos-sube", Math.max(0, Math.round(arriba - aire)) + "px");
    seccion.style.setProperty("--momentos-baja", Math.max(0, Math.round(abajo - aire)) + "px");
  };
  new ResizeObserver(ajustarAire).observe(pegado || pista);
  if (document.fonts) document.fonts.ready.then(ajustarAire);
  ajustarAire();
  // Los nombres de arriba llevan al paso: bajan la página justo hasta él.
  pasos.forEach((b, i) => b.addEventListener("click", () => {
    const arriba = pista.getBoundingClientRect().top + scrollY;
    scrollTo({ top: arriba + i * PASO + 2, behavior: GANANCIA ? "smooth" : "auto" });
  }));
  mirar();
})();

/* ================================================ el vídeo del panel, con pausa === */
/* La grabación de verdad del panel abriéndose: se pone en marcha cuando se ve (y, en los
   pasos, cuando su paso es el activo), y se para al salir. Dura más de 5 s y va en bucle,
   así que lleva su botón de pausa (WCAG 2.2.2), y quien la pausa la encuentra pausada. Con
   ?motion=off no arranca sola: el botón la pone en marcha. */
(function () {
  const cajas = [...document.querySelectorAll(".video-caja")];
  if (!cajas.length) return;
  const TXT = EN ? { pausa: "Pause the video", sigue: "Play the video" } : { pausa: "Pausar el vídeo", sigue: "Reproducir el vídeo" };
  let pausado = GANANCIA === 0;
  const estado = new Map();

  const toca = (caja) => {
    const v = caja.querySelector("video");
    const fuera = caja.closest(".momentos-fotos");
    const activa = !fuera || caja.classList.contains("activa");
    const visible = estado.get(caja) && getComputedStyle(caja).display !== "none";
    if (!pausado && activa && visible) {
      const p = v.play();
      if (p && p.catch) p.catch(() => {});
    } else v.pause();
  };
  const pintar = () => cajas.forEach((caja) => {
    const b = caja.querySelector(".video-pausa");
    b.setAttribute("aria-label", pausado ? TXT.sigue : TXT.pausa);
    b.setAttribute("aria-pressed", pausado ? "true" : "false");
    b.querySelector("use").setAttribute("href", pausado ? "#i-play" : "#i-pause");
  });
  const todo = () => cajas.forEach(toca);

  const ojo = new IntersectionObserver((vistos) => {
    vistos.forEach((e) => estado.set(e.target, e.isIntersecting));
    todo();
  }, { threshold: 0.35 });
  cajas.forEach((caja) => {
    ojo.observe(caja);
    caja.querySelector(".video-pausa").addEventListener("click", () => {
      pausado = !pausado;
      pintar();
      todo();
    });
  });
  document.addEventListener("tbgf:paso", todo);
  pintar();
})();
