/**
 * Los datos de la comparativa de la portada («Frente a frente»): Taskbar Groups Fluent y otras siete.
 *
 * De aquí sale la sección entera con generar-comparativa.mjs, que la escribe en index.html
 * (y generar-en.mjs la pasa luego al inglés). Cambiar un dato es cambiarlo aquí y correr
 * `node site/_herramientas/generar-en.mjs`.
 *
 * Comprobado el 15 de noviembre de 2026 en las fuentes primarias de cada una (la API de GitHub,
 * su código, sus README y notas de versión, los manifiestos de winget-pkgs y las páginas de
 * Microsoft). El detalle, con la fuente de cada celda, está en comparativa.md, al lado. Lo que no
 * sale en ninguna fuente oficial va como «Sin dato oficial» (nv), nunca como «No», y esa fila no
 * se cuenta en esa pestaña. Lo que no tiene sentido para una (instalar Windows) va como «na».
 *
 * Caduca: estrellas, versiones y fechas cambian. Antes de tocar una cifra, vuelve a pasar los
 * `gh api` de comparativa.md.
 *
 * Cada valor es [estado, español, inglés]; estado: si | no | medio | nv | na.
 */

export const HERRAMIENTAS = [
  { id: "tgf", es: "Taskbar Groups Fluent", en: "Taskbar Groups Fluent" },
  {
    id: "appgroup", es: "AppGroup", en: "AppGroup",
    resumen: {
      es: "La alternativa viva más conocida: gratis, MIT y con versión del 26 de octubre de 2026.",
      en: "The best-known alternative that is still alive: free, MIT, with a release from 26 October 2026.",
    },
    fuentes: [["el README de AppGroup", "AppGroup's README", "https://github.com/iandiv/AppGroup#readme"]],
  },
  {
    id: "windows11", es: "Windows 11 sin instalar nada", en: "Windows 11 with nothing installed", corto: "Windows 11", cortoEn: "Windows 11",
    gana: { es: "Lo que gana Windows 11 solo", en: "Where Windows 11 alone wins" },
    resumen: {
      es: "Anclar cada app a la barra, o juntarlas en una carpeta del menú Inicio, que se hace soltando una app encima de otra.",
      en: "Pinning each app to the taskbar, or putting them together in a Start menu folder, made by dropping one app on top of another.",
    },
    fuentes: [
      ["la página de Microsoft sobre las carpetas del Inicio", "Microsoft's page on Start menu folders", "https://www.microsoft.com/en-us/windows/learning-center/start-menu-folders"],
      ["el blog de Windows Insider de la compilación 22557", "the Windows Insider blog for build 22557", "https://blogs.windows.com/windows-insider/2022/02/16/announcing-windows-11-insider-preview-build-22557/"],
    ],
  },
  {
    id: "original", es: "Taskbar Groups", en: "Taskbar Groups", corto: "Taskbar Groups", cortoEn: "Taskbar Groups",
    resumen: {
      es: "El original de tjackenpacken, del que viene esta app: gratis y MIT, con su última versión, de prueba, de enero de 2021.",
      en: "The original by tjackenpacken, which this app comes from: free and MIT, with its last version, a pre-release, from January 2021.",
    },
    fuentes: [["el repositorio de Taskbar Groups", "the Taskbar Groups repository", "https://github.com/tjackenpacken/taskbar-groups"]],
  },
  {
    id: "pike", es: "Taskbar Groups pike-beta", en: "Taskbar Groups pike-beta", corto: "pike-beta", cortoEn: "pike-beta",
    resumen: {
      es: "El fork de PikeNote, en WinGet, gratis y MIT, con su última versión de enero de 2023.",
      en: "PikeNote's fork, on WinGet, free and MIT, with its last version from January 2023.",
    },
    fuentes: [["el repositorio de pike-beta", "the pike-beta repository", "https://github.com/PikeNote/taskbar-groups-pike-beta"]],
  },
  {
    id: "barras10", es: "las barras de herramientas de Windows 10", en: "Windows 10 toolbars", corto: "Barras de Windows 10", cortoEn: "Windows 10 toolbars",
    gana: { es: "Lo que ganan las barras de Windows 10", en: "Where the Windows 10 toolbars win" },
    resumen: {
      es: "Lo que mucha gente usaba antes: una carpeta entera puesta en la barra. Windows 11 ya no las trae, y Windows 10 está sin soporte desde el 14 de octubre de 2025.",
      en: "What many people used before: a whole folder placed on the taskbar. Windows 11 no longer has them, and Windows 10 has been out of support since 14 October 2025.",
    },
    fuentes: [
      ["la documentación de Microsoft sobre la barra de tareas", "Microsoft's taskbar documentation", "https://learn.microsoft.com/en-us/windows/win32/shell/taskbar"],
      ["el fin del soporte de Windows 10", "the Windows 10 end of support notice", "https://support.microsoft.com/en-us/windows/windows-10-support-has-ended-on-october-14-2025-2ca8b313-1946-43d3-b55c-2b95b107f281"],
      ["las especificaciones de Windows 11", "the Windows 11 specifications", "https://www.microsoft.com/en-us/windows/windows-11-specifications"],
    ],
  },
  {
    id: "explorerpatcher", es: "ExplorerPatcher", en: "ExplorerPatcher",
    resumen: {
      es: "La vía gratis para tener la barra de Windows 10, con sus barras de herramientas, en Windows 11. GPL-2.0.",
      en: "The free way to get the Windows 10 taskbar, with its toolbars, on Windows 11. GPL-2.0.",
    },
    fuentes: [
      ["el repositorio de ExplorerPatcher", "the ExplorerPatcher repository", "https://github.com/valinet/ExplorerPatcher"],
      ["su wiki", "its wiki", "https://github.com/valinet/ExplorerPatcher/wiki/All-features"],
    ],
  },
  {
    id: "windhawk", es: "Windhawk con Taskbar Folder Menus", en: "Windhawk with Taskbar Folder Menus", corto: "Windhawk", cortoEn: "Windhawk",
    gana: { es: "Lo que gana Windhawk con su mod", en: "Where Windhawk with its mod wins" },
    resumen: {
      es: "Un mod de Windhawk que pone en la barra de Windows 11 botones que abren una carpeta como menú.",
      en: "A Windhawk mod that puts buttons on the Windows 11 taskbar that open a folder as a menu.",
    },
    fuentes: [
      ["el mod Taskbar Folder Menus", "the Taskbar Folder Menus mod", "https://github.com/ramensoftware/windhawk-mods/blob/main/mods/taskbar-folder-menus.wh.cpp"],
      ["Windhawk", "Windhawk", "https://github.com/ramensoftware/windhawk"],
    ],
  },
];

const S = (es = "Sí", en = "Yes") => ["si", es, en];
const N = (es = "No", en = "No") => ["no", es, en];
const M = (es, en) => ["medio", es, en];
const NV = ["nv", "Sin dato oficial", "No official info"];
const NA = ["na", "", ""];
const SOLO_INGLES = N("Solo inglés", "English only");

// `vende`: dicha como lo que hace Taskbar Groups Fluent, para su lista. `es`/`en`: la fila dicha como
// cosa, para «Las dos». `pierde`: donde gana la otra (la fila solo cuenta si la otra dice sí).
// `sinEmpate`: no se cuenta en «Las dos» (que las dos funcionen en Windows 11 no le dice nada a nadie).
export const FILAS = [
  {
    id: "hover", es: "abrir al pasar el ratón", en: "opening on hover", icono: "i-pointer",
    vende: { es: "Se abre con solo pasar el ratón", en: "Opens just by hovering" },
    v: {
      tgf: S("Sí, si lo enciendes", "Yes, if you switch it on"), appgroup: N(), windows11: NV,
      original: N("Con un clic", "With a click"), pike: N("Con un clic", "With a click"), barras10: NV, explorerpatcher: NV,
      windhawk: N("Con un clic; al pasar, solo las subcarpetas", "With a click; only subfolders open on hover"),
    },
  },
  {
    id: "carpeta", es: "un grupo que enseña una carpeta", en: "a group that shows a folder", icono: "i-folder",
    vende: { es: "Un grupo enseña lo que hay en una carpeta, o la Papelera", en: "A group shows what is in a folder, or the Recycle Bin" },
    v: {
      tgf: S(), appgroup: N(), windows11: N("Sus carpetas se hacen con apps, no con una del disco", "Its folders are made of apps, not of a folder on disk"),
      original: N("Una carpeta entra como un acceso más", "A folder goes in as one more shortcut"), pike: N("Una carpeta entra como un acceso más", "A folder goes in as one more shortcut"),
      barras10: S("Es su forma de trabajar", "That is how they work"), explorerpatcher: M("Con una barra de herramientas de carpeta", "With a folder toolbar"),
      windhawk: S("Carpetas, unidades y el Panel de control", "Folders, whole drives and the Control Panel"),
    },
  },
  {
    id: "subgrupos", es: "grupos dentro de grupos", en: "groups inside groups", icono: "i-layers",
    vende: { es: "Grupos dentro de grupos, al lado como un submenú", en: "Groups inside groups, beside it like a submenu" },
    v: {
      tgf: S(), appgroup: S("Cada uno en su propio panel", "Each one in its own panel"), windows11: NV, original: NV, pike: NV,
      barras10: NV, explorerpatcher: NV, windhawk: S("Como subcarpetas en cascada", "As cascading subfolders"),
    },
  },
  {
    id: "store", es: "apps de la Microsoft Store", en: "Microsoft Store apps", icono: "i-store",
    vende: { es: "Apps de la Microsoft Store y web apps del navegador", en: "Microsoft Store apps and browser web apps" },
    v: {
      tgf: S(), appgroup: S("También PWA y accesos de Steam", "PWAs and Steam shortcuts too"), windows11: S(),
      original: S("Por su acceso directo", "Through their shortcut"), pike: S(), barras10: NV, explorerpatcher: NV, windhawk: NV,
    },
  },
  {
    id: "admin", es: "instalar sin permisos de administrador", en: "installing without administrator rights", icono: "i-shield",
    vende: { es: "Se instala sin permisos de administrador", en: "Installs without administrator rights" },
    v: {
      tgf: S("Solo para tu usuario", "For your user only"), appgroup: S("Con su versión portable", "With its portable version"), windows11: NA,
      original: S("Se descomprime y se ejecuta", "You unzip it and run it"), pike: S("Un solo .exe portable", "A single portable .exe"), barras10: NA,
      explorerpatcher: N("Pide elevación al instalar", "Asks for elevation to install"), windhawk: N("Pide elevación al instalar", "Asks for elevation to install"),
    },
  },
  {
    id: "viva", es: "seguir con versiones nuevas", en: "still getting new versions", icono: "i-refresh", sinEmpate: true,
    vende: { es: "Sigue viva, con versiones nuevas", en: "Still alive, with new versions" },
    v: {
      tgf: S("La última, del 14 de noviembre de 2026", "The latest from 14 November 2026"), appgroup: S("La última, del 26 de octubre de 2026", "The latest from 26 October 2026"),
      windows11: NA, original: N("La última, de prueba, de enero de 2021", "The last one, a pre-release, from January 2021"),
      pike: N("La última, de enero de 2023", "The last one from January 2023"),
      barras10: N("Windows 10 está sin soporte desde el 14/10/2025", "Windows 10 has been out of support since 14/10/2025"),
      explorerpatcher: S("La última, del 29 de octubre de 2026", "The latest from 29 October 2026"), windhawk: S("El mod, del 8 de octubre de 2026", "The mod, from 8 October 2026"),
    },
  },
  {
    id: "win11", es: "funcionar en Windows 11", en: "working on Windows 11", icono: "i-monitor", sinEmpate: true,
    vende: { es: "Funciona en Windows 11", en: "Works on Windows 11" },
    v: {
      tgf: S(), appgroup: S(), windows11: NA, original: NV, pike: NV,
      barras10: N("Windows 11 ya no las trae", "Windows 11 no longer has them"), explorerpatcher: S(), windhawk: S(),
    },
  },
  {
    id: "win10", es: "funcionar también en Windows 10", en: "working on Windows 10 too", icono: "i-monitor", sinEmpate: true,
    vende: { es: "Funciona también en Windows 10", en: "Works on Windows 10 too" },
    v: {
      tgf: S("Desde la 1809", "From 1809 on"), appgroup: S(), windows11: NA, original: NV, pike: NV, barras10: NA,
      explorerpatcher: NV, windhawk: N("El mod, solo Windows 11", "The mod is Windows 11 only"),
    },
  },
  {
    id: "monitores", es: "salir en cualquier monitor", en: "showing on any monitor", icono: "i-monitor",
    vende: { es: "Sale en cualquier monitor, encima de su icono", en: "Shows on any monitor, above its icon" },
    v: {
      tgf: S(), appgroup: NV, windows11: NA, original: NV, pike: NV, barras10: NV, explorerpatcher: NV,
      windhawk: N("Solo en la barra principal", "Only on the main taskbar"),
    },
  },
  {
    id: "icono", es: "su propio icono anclado", en: "its own pinned icon", icono: "i-image",
    vende: { es: "Cada grupo es un icono anclado, con su propio dibujo", en: "Each group is a pinned icon with its own picture" },
    v: {
      tgf: S("Una imagen, un emoji o sus apps", "An image, an emoji or its apps"), appgroup: S(), windows11: NV, original: S(), pike: S(),
      barras10: NA, explorerpatcher: NA, windhawk: M("Botones de texto o emoji, juntos en un sitio de la barra", "Text or emoji buttons, all together in one spot on the taskbar"),
    },
  },
  {
    id: "tiendas", es: "estar en WinGet y en la Store", en: "being on WinGet and the Store", icono: "i-terminal",
    vende: { es: "En WinGet y en la Microsoft Store", en: "On WinGet and the Microsoft Store" },
    v: {
      tgf: S(), appgroup: N("En ninguna de las dos", "On neither"), windows11: NA, original: N("En ninguna de las dos", "On neither"),
      pike: M("En WinGet, no en la Store", "On WinGet, not on the Store"), barras10: NA,
      explorerpatcher: M("En WinGet, no en la Store", "On WinGet, not on the Store"), windhawk: M("En WinGet, no en la Store", "On WinGet, not on the Store"),
    },
  },
  {
    id: "idiomas", es: "hablar español", en: "speaking Spanish", icono: "i-languages",
    vende: { es: "Habla español, inglés, ruso y chino", en: "Speaks Spanish, English, Russian and Chinese" },
    v: { tgf: S(), appgroup: SOLO_INGLES, windows11: NA, original: SOLO_INGLES, pike: SOLO_INGLES, barras10: NA, explorerpatcher: NA, windhawk: NA },
  },
  {
    id: "noBarra", es: "dejar la barra de Windows como está", en: "leaving the Windows taskbar as it is", icono: "i-layout",
    vende: { es: "No sustituye la barra de Windows", en: "Does not replace the Windows taskbar" },
    v: {
      tgf: S(), appgroup: S(), windows11: NA, original: S(), pike: S(), barras10: NA,
      explorerpatcher: N("La cambia por la de Windows 10; su wiki pide no usarla en equipos de trabajo", "Swaps it for the Windows 10 one; its wiki asks you not to use it on work machines"),
      windhawk: S(),
    },
  },
  {
    id: "net", es: "un .NET con soporte", en: "a supported .NET", icono: "i-shield", sinEmpate: true,
    vende: { es: "Va en .NET 10, con soporte hasta 2028", en: "Runs on .NET 10, supported until 2028" },
    v: { tgf: S(), appgroup: N(".NET 8, sin soporte desde el 10/11/2026", ".NET 8, out of support since 10/11/2026"), windows11: NA, original: NA, pike: NA, barras10: NA, explorerpatcher: NA, windhawk: NA },
  },

  // ---------------------------------------------------- donde gana la otra
  {
    id: "estrellas", pierde: true, icono: "i-star",
    vende: { es: "Más estrellas en GitHub", en: "More stars on GitHub" },
    v: {
      tgf: ["si", "38", "38"], appgroup: S("788; esta, 38", "788; this one, 38"), original: S("2.795; esta, 38", "2,795; this one, 38"),
      pike: S("165; esta, 38", "165; this one, 38"), explorerpatcher: S("34.326; esta, 38", "34,326; this one, 38"), windhawk: S("9.842, las de Windhawk; esta, 38", "9,842, Windhawk's; this one, 38"),
      windows11: NA, barras10: NA,
    },
  },
  {
    id: "teclas", pierde: true, icono: "i-keyboard",
    vende: { es: "Abre las apps del panel con las teclas 1 a 0", en: "Opens the panel's apps with keys 1 to 0" },
    v: { original: S(), pike: S() },
  },
  {
    id: "sinNet", pierde: true, icono: "i-download",
    vende: { es: "No pide instalar .NET", en: "Needs no .NET installed" },
    v: { original: S("Usa el .NET Framework que trae Windows", "Uses the .NET Framework Windows already has"), pike: S("Usa el .NET Framework que trae Windows", "Uses the .NET Framework Windows already has") },
  },
  {
    id: "portable", pierde: true, icono: "i-package",
    vende: { es: "Tiene versión portable", en: "Has a portable version" },
    v: { appgroup: S("Un .zip", "A .zip"), original: S("Un .zip", "A .zip"), pike: S("Un solo .exe", "A single .exe") },
  },
  {
    id: "card", pierde: true, icono: "i-layout",
    vende: { es: "Un diseño «Card», como los Ajustes rápidos de Windows 11", en: "A «Card» layout, like Windows 11 Quick Settings" },
    v: { appgroup: S() },
  },
  {
    id: "nada", pierde: true, icono: "i-download",
    vende: { es: "No hay que instalar nada", en: "Nothing to install" },
    v: { windows11: S("Viene con Windows", "It comes with Windows"), barras10: S("Vienen con Windows 10", "They come with Windows 10") },
  },
  {
    id: "arrastre", pierde: true, icono: "i-folder",
    vende: { es: "Una carpeta del Inicio se hace con un arrastre", en: "A Start folder is made with one drag" },
    v: { windows11: S() },
  },
  {
    id: "carpetaVista", pierde: true, icono: "i-folder",
    vende: { es: "Deja una carpeta entera a la vista en la barra", en: "Leaves a whole folder in view on the taskbar" },
    v: { barras10: S() },
  },
  {
    id: "barraEntera", pierde: true, icono: "i-layout",
    vende: { es: "Devuelve la barra de Windows 10 entera, con su menú Inicio", en: "Brings back the whole Windows 10 taskbar, with its Start menu" },
    v: { explorerpatcher: S() },
  },
  {
    id: "masIdiomas", pierde: true, icono: "i-languages",
    vende: { es: "Habla más idiomas", en: "Speaks more languages" },
    v: {
      windows11: S("Los de Windows", "Those of Windows"), barras10: S("Los de Windows", "Those of Windows"),
      explorerpatcher: S("20 en su barra; esta, 4", "20 on its taskbar; this one, 4"), windhawk: S("28 en Windhawk; el mod, en inglés", "28 in Windhawk; the mod, in English"),
    },
  },
  {
    id: "sinAnclar", pierde: true, icono: "i-pin-off",
    vende: { es: "No hay que anclar nada: los botones salen solos", en: "Nothing to pin: the buttons appear by themselves" },
    v: { windhawk: S() },
  },
];

export const TEXTOS = {
  titulo: { es: "Frente a frente", en: "Head to head" },
  entradilla: {
    es: "Lo que hace Taskbar Groups Fluent y no hacen las otras, y donde gana alguna de ellas. Todo sale de la web, el código o las notas de cada una, con el enlace debajo.",
    en: "What Taskbar Groups Fluent does and the others do not, and where one of them wins. All of it comes from each one's website, code or release notes, linked below.",
  },
  fan: {
    es: "AppGroup tiene más estrellas. <b>Abrir al pasar, no.</b>",
    en: "AppGroup has more stars. <b>Opening on hover, it doesn't.</b>",
  },
  pregunta: { es: "¿Cuál usas ahora?", en: "Which one do you use now?" },
  gana: { es: "Lo que gana Taskbar Groups Fluent", en: "Where Taskbar Groups Fluent wins" },
  ganaOtra: { es: "Lo que gana {x}", en: "Where {x} wins" },
  lasDos: { es: "Las dos:", en: "Both:" },
  nada: { es: "Nada que diga una fuente oficial.", en: "Nothing an official source says." },
  fuentes: {
    es: "Comprobado el 15 de noviembre de 2026 en {f}. «Sin dato oficial» no cuenta ni a favor ni en contra. Si algo ha cambiado, <a href=\"https://github.com/Mun1to/TaskbarGroupsFluent/issues\" rel=\"noopener\">avísame</a> y se corrige.",
    en: "Checked on 15 November 2026 in {f}. «No official info» counts neither for nor against. If something has changed, <a href=\"https://github.com/Mun1to/TaskbarGroupsFluent/issues\" rel=\"noopener\">tell me</a> and it gets fixed.",
  },
};
