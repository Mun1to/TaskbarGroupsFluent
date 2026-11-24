# Comparativa «¿Cuál usas ahora?»: alternativas a Taskbar Groups Fluent

**Comprobado el 15/11/2026** (fecha del sistema, `date`: dom 15/11/2026 21:42). Todo sale de
fuentes primarias: la API de GitHub (`gh api`), el código fuente de cada repositorio, sus README
y notas de versión, los manifiestos de `microsoft/winget-pkgs`, `winget search` contra las
fuentes `winget` y `msstore`, y las páginas de Microsoft.

> **Caduca.** Estrellas, versiones, fechas de la última versión, idiomas y precios cambian con
> cada versión. Antes de publicar una cifra, vuelve a pasar los `gh api` de cada sección.

**Criterio.** «Sí» y «No» solo cuando lo dice una fuente oficial o se ve en el código. Lo que
no está publicado va como «Sin dato oficial», nunca como «No». «Medio» es que lo hace a medias
y la celda dice cómo. «No aparece» en WinGet o la Store es el resultado de `winget search` el
15/11/2026.

## Elegidas (7) y por qué

1. **Taskbar Groups**, el original: la base de TGF, 2.795 estrellas.
2. **Taskbar Groups pike-beta**: el fork del que salió TGF, en WinGet.
3. **AppGroup**: la alternativa viva más conocida, 788 estrellas, versión del 26/10/2026.
4. **Windows 11 sin instalar nada**: anclar cada app y las carpetas del menú Inicio.
5. **Barras de herramientas de Windows 10**: lo que mucha gente usaba antes; Windows 11 no las trae.
6. **ExplorerPatcher**: la vía gratis para tener esas barras en Windows 11 (34.326 estrellas).
7. **Windhawk con el mod Taskbar Folder Menus**: botones en la barra de Windows 11 que abren una carpeta como menú.

Las que se miraron y se quedan fuera, con su motivo, están en «Descartadas», al final.

---

## 0. Taskbar Groups Fluent (la referencia)

| Dato | Valor | Fuente |
|---|---|---|
| Precio y licencia | Gratis, MIT, sin versión de pago | [README](https://github.com/Mun1to/TaskbarGroupsFluent#readme), `gh api repos/Mun1to/TaskbarGroupsFluent` |
| Última versión | v1.10.4, 14/11/2026 | `gh release list -R Mun1to/TaskbarGroupsFluent` |
| Estrellas | 38 | `gh api repos/Mun1to/TaskbarGroupsFluent` |
| Windows 11 | Sí, y Windows 10 1809 o posterior, 64 bits | README, «Install» |
| Agrupa apps detrás de un icono | Sí | README, «What it does» |
| Abre al pasar el ratón | Sí, opcional (apagado de fábrica) | README |
| Apps de la Microsoft Store | Sí, y apps web de un navegador | README |
| Un grupo enseña una carpeta | Sí, también el Panel de control, Este equipo, la Papelera o todas tus apps | README |
| Grupos dentro de grupos | Sí, se abren al lado como un submenú | README |
| Sin permisos de administrador | Sí, instala solo para tu usuario | README; `PrivilegesRequired=lowest` en `installer/TaskbarGroupsFluent.iss`; manifiesto de WinGet 1.10.4 con `Scope: user` |
| WinGet / Store | Sí (`Mun1to.TaskbarGroupsFluent` 1.10.4) / Sí (`9PCZDXWFT6SN`) | `winget search` en `winget` y `msstore` |
| Idiomas | 4: inglés, español, ruso y chino simplificado | README; `src/TaskbarGroups.App/Localization/Strings.{en,es,ru,zh-Hans}.xaml` |
| Requisito | .NET 10 Desktop Runtime: el instalador lo baja si falta y el Offline lo lleva dentro | README |
| Varios monitores | Sí, el panel sale en la pantalla del icono | README |

---

## 1. Taskbar Groups (el original, tjackenpacken)

| Dato | Valor | Fuente |
|---|---|---|
| Precio y licencia | Gratis, MIT | [repo](https://github.com/tjackenpacken/taskbar-groups), `gh api repos/tjackenpacken/taskbar-groups` |
| Última versión | v0.2.0.0, 05/01/2021, marcada como prerelease (todas sus versiones lo están). Último commit de su rama principal: 05/01/2021 | `gh api repos/tjackenpacken/taskbar-groups/releases` y `/commits` (`releases/latest` da 404 porque no hay ninguna estable) |
| Estrellas | 2.795 | API de GitHub |
| Windows 11 | Sin dato oficial: su README no lo nombra y su última versión es nueve meses anterior a Windows 11 (salió el 05/10/2021) | README; [blog de Windows, 04/10/2021](https://blogs.windows.com/windowsexperience/2021/10/04/windows-11-a-new-era-for-the-pc-begins-today/) |
| Agrupa apps detrás de un icono | Sí | README |
| Abre al pasar el ratón | No: se abre con un clic en el grupo anclado y su código no vigila el cursor (sin `SetWindowsHookEx`, UI Automation ni `GetCursorPos`) | README, «Creating your first group»; código bajado con `gh api .../tarball` |
| Apps de la Microsoft Store | Sí, por su acceso directo («shortcuts leading to the windows store apps») | README |
| Un grupo enseña una carpeta | No: una carpeta entra como un acceso más que la abre; el panel solo pinta los accesos guardados del grupo | README («drag and drop .exe, .lnk, or folders»); `main/Forms/frmMain.cs`, `LoadCategory` |
| Grupos dentro de grupos | Sin dato oficial | No sale en README ni en notas |
| Sin permisos de administrador | Sí: se descomprime un .zip y se ejecuta; no lleva manifiesto que pida elevación | README; árbol del repo |
| WinGet / Store | No aparece / No aparece | `winget search`; no hay carpeta `manifests/t/tjackenpacken` en winget-pkgs |
| Idiomas | Inglés: no hay archivos de traducción en su código | árbol del repo |
| Atajos | Teclas 1 a 0 para abrir las diez primeras apps y Ctrl+Enter para abrirlas todas | README, «Program Shortcuts» |
| Requisito | .NET Framework 4.7.2, que Windows 10 1803 en adelante y Windows 11 ya traen (o su 4.8, compatible) | `main/client.csproj`; [Microsoft Learn, versiones de .NET Framework](https://learn.microsoft.com/en-us/dotnet/framework/install/versions-and-dependencies) |

Nota: la API da `pushed_at` 04/07/2024, pero el último commit de la rama principal es del
05/01/2021; el empuje de 2024 no cambió la rama que se descarga.

---

## 2. Taskbar Groups pike-beta (PikeNote)

| Dato | Valor | Fuente |
|---|---|---|
| Precio y licencia | Gratis, MIT | [repo](https://github.com/PikeNote/taskbar-groups-pike-beta), API |
| Última versión | v0.2.2.0, 10/01/2023 (estable). Último empuje: 13/02/2023 | `gh api repos/PikeNote/taskbar-groups-pike-beta/releases/latest` |
| Estrellas | 165 | API |
| Windows 11 | Sin dato oficial: su README y sus notas no nombran ninguna versión de Windows | README, [notas de la v0.2.2.0](https://github.com/PikeNote/taskbar-groups-pike-beta/releases/tag/v0.2.2.0) |
| Agrupa apps detrás de un icono | Sí, en la barra o en el escritorio | README; descripción del repo |
| Abre al pasar el ratón | No: su programa de fondo solo tiene los grupos cargados para abrir antes; el «hover color» de sus notas es el color de una app al pasar por encima dentro del panel | Notas de la v0.2.2.0; `backgroundClient/Forms/bkgProcess.cs` y `frmMain.cs` |
| Apps de la Microsoft Store | Sí (sus notas arreglan iconos de apps UWP y juegos de la Store) | Notas de v0.2.1.0 y v0.2.2.0 |
| Un grupo enseña una carpeta | No: igual que el original, el panel solo pinta los accesos guardados | `backgroundClient/Forms/frmMain.cs`, `LoadCategory` |
| Grupos dentro de grupos | Sin dato oficial | No sale en README ni en notas |
| Sin permisos de administrador | Sí: un solo .exe portable con `requestedExecutionLevel level="asInvoker"` | README; `main/app.manifest` |
| WinGet / Store | Sí (`PikeNote.TaskbarGroups` 0.2.2.0, tipo portable) / No aparece | `winget show PikeNote.TaskbarGroups`; `winget search --source msstore` |
| Idiomas | Inglés: no hay archivos de traducción en su código | árbol del repo |
| Atajos | Teclas 1 a 0 y abrir todas, heredadas del original | `backgroundClient/Forms/frmMain.cs` (`keyList`); notas de la v0.2.2.0 («Alt+Enter shortcut to open all») |
| Requisito | .NET Framework 4.7.2, ya incluido en Windows (ver arriba) | `*.csproj` |

---

## 3. AppGroup (iandiv)

| Dato | Valor | Fuente |
|---|---|---|
| Precio y licencia | Gratis, MIT (donaciones en Ko-fi) | [README](https://github.com/iandiv/AppGroup#readme), API |
| Última versión | v1.6.0, 26/10/2026. Último empuje: 04/11/2026 | `gh api repos/iandiv/AppGroup/releases/latest` |
| Estrellas | 788 | API |
| Windows 11 | Sí, y Windows 10 1809 o posterior | README, «Installation» y FAQ |
| Agrupa apps detrás de un icono | Sí | README |
| Abre al pasar el ratón | No: su README no lo trae y en su código el único gancho del ratón mira clics (`WM_LBUTTONDOWN`) para cerrar el panel | README; `AppGroup/PopupWindow.xaml.cs`, `MouseHookCallback` |
| Apps de la Microsoft Store | Sí, también PWA (por acceso directo) y accesos de Steam (`.url`) | README, «Apps and shortcuts» y FAQ |
| Un grupo enseña una carpeta | No: su README no lo trae y en su código el único selector de carpetas es el de importar desde Taskbar Groups | README; `AppGroup/MainWindow.xaml.cs` (`FolderPicker`) |
| Grupos dentro de grupos | Sí («Subgroups»), cada uno en su propio panel | README |
| Sin permisos de administrador | Sí con su versión portable (.zip); del instalador, sin dato oficial | README, tabla de variantes; `app.manifest` sin `requestedExecutionLevel` |
| WinGet / Store | No aparece / No aparece | `winget search AppGroup` en `winget` y `msstore` |
| Idiomas | Inglés: los textos están escritos en inglés en el código, sin archivos de traducción | `AppGroup/SettingsDialog.xaml`; árbol del repo |
| Requisito | .NET 8 (la versión «Bundled» lo lleva dentro). .NET 8 se quedó sin soporte el 10/11/2026 | README; [política de soporte de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) |
| Extras propios | Versión portable, diseño «Card» como los Ajustes rápidos de Windows 11, icono en gris, importar con vista previa | README, «Features» |

---

## 4. Windows 11 sin instalar nada (anclar cada app y carpetas del Inicio)

| Dato | Valor | Fuente |
|---|---|---|
| Precio y licencia | Viene con Windows 11 | |
| Mantenida | Sí, con Windows. Las carpetas del Inicio llegaron en la compilación Insider 22557 (16/02/2022) | [blog de Windows Insider](https://blogs.windows.com/windows-insider/2022/02/16/announcing-windows-11-insider-preview-build-22557/) |
| Estrellas | No aplica | |
| Windows 11 | Sí | |
| Agrupa apps detrás de un icono | Anclar: No, cada app anclada es su propio botón. Carpetas del Inicio: viven dentro del menú Inicio. **Sin comprobar:** el Learning Center de Microsoft dice que una carpeta del Inicio se puede anclar a la barra («Right-click the folder... Select Pin to taskbar. The folder will appear on your taskbar»), pero no dice qué sale al pulsarla, y el anuncio de las carpetas no lo menciona | [Learning Center, «How to create Start menu folders», 28/09/2023](https://www.microsoft.com/en-us/windows/learning-center/start-menu-folders); blog de Insider 22557 |
| Abre al pasar el ratón | Sin dato oficial | |
| Apps de la Microsoft Store | Sí: el Inicio enseña todas las apps instaladas, y el propio ejemplo de Microsoft agrupa la Calculadora y Fotos | Learning Center, misma página |
| Un grupo enseña una carpeta | No: una carpeta del Inicio se hace soltando una app anclada encima de otra, no con una carpeta del disco | Learning Center; blog de Insider 22557 («drag an app on top of another») |
| Grupos dentro de grupos | Sin dato oficial | |
| Sin permisos de administrador | No hace falta instalar nada | |
| WinGet / Store | No aplica | |
| Idiomas | Los de Windows | |

---

## 5. Barras de herramientas de la barra de tareas de Windows 10

| Dato | Valor | Fuente |
|---|---|---|
| Precio y licencia | Viene con Windows 10 | |
| Mantenida | Windows 10 está sin soporte desde el 14/10/2025; las actualizaciones de seguridad extendidas (ESU) para particulares llegan hasta el 12/10/2027 | [Microsoft, fin del soporte de Windows 10](https://support.microsoft.com/en-us/windows/windows-10-support-has-ended-on-october-14-2025-2ca8b313-1946-43d3-b55c-2b95b107f281) |
| Estrellas | No aplica | |
| Windows 11 | No. Ojo: Microsoft no lo dice con estas palabras; su lista de cambios de la barra en Windows 11 dice «Apps can no longer customize areas of the Taskbar». Que faltan lo confirman las herramientas que las devuelven (ExplorerPatcher, Taskbar Folder Menus: «recreating the most useful part of the classic taskbar toolbar workflow») | [Especificaciones de Windows 11, «Feature deprecations»](https://www.microsoft.com/en-us/windows/windows-11-specifications) |
| Agrupa apps detrás de un icono | Medio: una barra de herramientas pone una carpeta entera en la barra. Microsoft la documenta como parte de la barra («Toolbars (optional)», que se añaden desde el menú del clic derecho); cómo se ve y cómo se pliega, sin dato oficial | [Win32, «Taskbar», ms.date 31/05/2018](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar) |
| Abre al pasar el ratón | Sin dato oficial | |
| Apps de la Microsoft Store | Sin dato oficial | |
| Un grupo enseña una carpeta | Sí, es su forma de trabajar («Nueva barra de herramientas» pide una carpeta). Solo lo describe una pregunta de usuario en Microsoft Q&A, no un documento de Microsoft | [Microsoft Q&A, 05/09/2024](https://learn.microsoft.com/en-us/answers/questions/2041095/how-to-add-folders-in-the-taskbar-in-win11) |
| Grupos dentro de grupos | Sin dato oficial | |
| Sin permisos de administrador | No hace falta instalar nada | |
| WinGet / Store | No aplica | |
| Idiomas | Los de Windows | |

---

## 6. ExplorerPatcher (valinet)

| Dato | Valor | Fuente |
|---|---|---|
| Precio y licencia | Gratis, GPL-2.0 | [repo](https://github.com/valinet/ExplorerPatcher), API |
| Última versión | 26100.9711.71.1, 29/10/2026 | `gh api repos/valinet/ExplorerPatcher/releases/latest` |
| Estrellas | 34.326 | API |
| Windows 11 | Sí: su tabla da soporte de 22H2 (22621.1343 en adelante) a 25H2 (26200) en el canal normal | [wiki, «ExplorerPatcher's taskbar implementation»](https://github.com/valinet/ExplorerPatcher/wiki/ExplorerPatcher's-taskbar-implementation) |
| Agrupa apps detrás de un icono | Medio: no agrupa por sí mismo; devuelve la barra de Windows 10 y con ella sus barras de herramientas (su wiki tiene el ajuste «Show separators between taskbar toolbars») | README, paso 2; [wiki, «All features»](https://github.com/valinet/ExplorerPatcher/wiki/All-features) |
| Abre al pasar el ratón | Sin dato oficial | |
| Apps de la Microsoft Store | Sin dato oficial | |
| Un grupo enseña una carpeta | Medio: con una barra de herramientas de carpeta, como en Windows 10 (mismo ajuste) | wiki, «All features» |
| Grupos dentro de grupos | Sin dato oficial | |
| Sin permisos de administrador | No: «It will automatically prompt for elevation» | README, «How to?» |
| WinGet / Store | Sí (`valinet.ExplorerPatcher`) / No aparece | `winget search`; `manifests/v/valinet/ExplorerPatcher` |
| Idiomas | 20 en su barra reimplementada | wiki, «Languages» |
| Aviso propio | «Please DO NOT use the reimplemented taskbar on WORK machines»; «Crashes can happen at any time»; una actualización de Windows puede romperla | wiki, «Warning» |

---

## 7. Windhawk con el mod Taskbar Folder Menus

| Dato | Valor | Fuente |
|---|---|---|
| Precio y licencia | Gratis; Windhawk es GPL-3.0 | [Windhawk](https://github.com/ramensoftware/windhawk), API |
| Última versión | Windhawk 1.7.3 estable, 08/12/2025 (hay una 2.0.0-alpha.8 de prueba, 25/10/2026). El mod: v2.1, 08/10/2026; existe desde el 23/07/2026 (v0.7) | `gh api repos/ramensoftware/windhawk/releases`; historial de `mods/taskbar-folder-menus.wh.cpp` en [windhawk-mods](https://github.com/ramensoftware/windhawk-mods/blob/main/mods/taskbar-folder-menus.wh.cpp) |
| Estrellas | Windhawk: 9.842. El mod no tiene repo propio | API |
| Windows 11 | Sí, solo Windows 11 («Windows 10 is not supported»), y solo en la barra principal («This mod targets the primary Windows 11 taskbar») | README del mod |
| Agrupa apps detrás de un icono | Medio: cada botón abre una carpeta como menú; son botones compactos con texto o emoji, todos juntos en un sitio de la barra («Folder buttons form one grouped toolbar at a shared taskbar position»), no iconos anclados | README del mod |
| Abre al pasar el ratón | No: el botón se abre con un clic; solo las subcarpetas se despliegan al pasar («Click a small button to browse a folder... Subfolders expand on hover») | README del mod y su tabla de ajustes |
| Apps de la Microsoft Store | Sin dato oficial | |
| Un grupo enseña una carpeta | Sí: carpetas, unidades enteras, el Escritorio o el Panel de control (destinos `shell:`) | README del mod |
| Grupos dentro de grupos | Sí, como subcarpetas en cascada, sin límite de profundidad de fábrica | README del mod, `Behavior.MaxDepth` |
| Sin permisos de administrador | No: Windhawk pide elevación | manifiesto de WinGet de `RamenSoftware.Windhawk` 1.7.3, `ElevationRequirement: elevationRequired` |
| WinGet / Store | Sí (`RamenSoftware.Windhawk`) / No aparece | `winget search` |
| Idiomas | Windhawk 1.7.3: 28. El mod, en inglés | carpetas `locales/*/translation.json` en la etiqueta `v1.7.3` |

---

## Descartadas, y por qué

* **StartAllBack.** De pago: 4,99 $ con 30 días de prueba según la página de compra a la que
  enlaza (su registro de cambios dice que la prueba subió a 100 días en la 3.3.3). Versión
  3.9.28 del 12/10/2026, en WinGet (`StartIsBack.StartAllBack`, con instalación por usuario o
  para todo el equipo) y en la Store (`XPFMHKP3QHRQRH`). **Se queda fuera porque su web no habla
  de barras de herramientas en ningún sitio**: que las devuelve solo lo dicen foros de usuarios
  (ElevenForum, AskWoody). Fuentes: [startallback.com](https://www.startallback.com/),
  [startisback.com](https://www.startisback.com/), `winget search`. Como pestaña, casi todas sus
  filas serían «Sin dato oficial».
* **7+ Taskbar Tweaker.** Su web dice «Windows 11 isn't supported», y lo suyo es agrupar y
  combinar las ventanas abiertas, no accesos. [tweaker.ramensoftware.com](https://tweaker.ramensoftware.com/)
* **Stardock Fences.** Organiza el escritorio, no la barra: «Automatically organize your desktop
  apps, files, and folders». [stardock.com/products/fences](https://www.stardock.com/products/fences/)
* **FanFolder.** Enseña los últimos archivos de una carpeta en abanico desde la barra; licencia
  propietaria y 4 estrellas: poco conocida. `winget show OleBhartvigsen.FanFolder`.
* **Repos pequeños** que salen al buscar «taskbar group» en GitHub (MaGrouper, Dockaroo,
  AppStack, TaskbarGroupsPlus y otros): de 0 a 2 estrellas cada uno. No son «conocidos».

---

## Resumen por alternativa

### Taskbar Groups (original)
* **Qué hace TGF que esta no:** abre al pasar el ratón y enseña el contenido de una carpeta, cosas
  que el original no hace (README y código). Está viva (versión del 14/11/2026 frente a una
  prerelease de enero de 2021), en WinGet y en la Store, y en 3 idiomas frente a solo inglés.
* **Dónde gana esta:** 2.795 estrellas frente a 38. Abre las apps del panel con las teclas 1 a 0,
  que el panel de TGF no tiene (su `OnKey` solo atiende Escape y la tecla de menú). No pide
  instalar .NET: va con el .NET Framework que ya trae Windows.

### Taskbar Groups pike-beta
* **Qué hace TGF que esta no:** abrir al pasar, un grupo que enseña una carpeta, la Store, 3
  idiomas, y sigue mantenida (la pike no saca versión desde el 10/01/2023).
* **Dónde gana esta:** es un solo .exe portable, sin instalar nada y sin .NET aparte; conserva
  las teclas 1 a 0 para abrir apps.

### AppGroup
* **Qué hace TGF que esta no:** abrir al pasar el ratón y un grupo que enseña una carpeta (AppGroup
  no tiene ninguna de las dos, ni en su README ni en su código). Está en WinGet y en la Store,
  donde AppGroup no aparece, y habla 3 idiomas frente a solo inglés. TGF va en .NET 10 (soporte
  hasta el 14/11/2028); AppGroup, en .NET 8, sin soporte desde el 10/11/2026.
* **Dónde gana esta:** 788 estrellas, unas 20 veces las de TGF. Tiene versión portable, que TGF no
  publica (sus releases son Setup y Setup Offline), y extras propios como el diseño «Card» o el
  icono en gris.

### Windows 11 sin instalar nada
* **Qué hace TGF que esta no:** un solo botón en la barra que abre un panel con las apps del grupo,
  al pulsar o al pasar; un grupo que enseña una carpeta del disco o un lugar de Windows; grupos
  dentro de grupos.
* **Dónde gana esta:** no se instala nada ni corre nada más; las carpetas del Inicio se hacen con un
  arrastre. Pendiente: Microsoft dice que una carpeta del Inicio se ancla a la barra (ver
  «No comprobado»).

### Barras de herramientas de Windows 10
* **Qué hace TGF que esta no:** funciona en Windows 11, que ya no trae las barras de herramientas, y
  Windows 10 está sin soporte desde el 14/10/2025. Abre al pasar y admite grupos dentro de grupos.
* **Dónde gana esta:** en Windows 10 viene puesta, sin instalar nada, y deja una carpeta entera a la
  vista en la barra.

### ExplorerPatcher
* **Qué hace TGF que esta no:** no toca el Explorador ni sustituye la barra de Windows, se instala sin
  permisos de administrador y está en la Store. ExplorerPatcher avisa en su wiki de que su barra no
  se use en equipos de trabajo y de que puede colgarse en cualquier momento.
* **Dónde gana esta:** 34.326 estrellas, devuelve la barra de Windows 10 entera (también el menú
  Inicio de Windows 10) y su barra viene en 20 idiomas.

### Windhawk con Taskbar Folder Menus
* **Qué hace TGF que esta no:** cada grupo es un icono anclado con su propio icono y sale en cualquier
  monitor; el mod va solo en la barra principal y con botones de texto o emoji. TGF funciona también
  en Windows 10, se instala sin administrador y abre al pasar; el botón del mod se abre con un clic.
* **Dónde gana esta:** no hay que anclar nada, los botones salen solos en la barra; enseña unidades
  enteras y el Panel de control como menús del propio Windows. Windhawk tiene 9.842 estrellas y 28
  idiomas.

---

## No comprobado (dicho en voz alta)

1. **Si una carpeta del Inicio de Windows 11 se puede anclar de verdad a la barra, y qué sale al
   pulsarla.** Lo dice el Learning Center de Microsoft (28/09/2023), pero no el anuncio de las
   carpetas. No se probó.
2. **Que StartAllBack devuelva las barras de herramientas.** Su web no lo dice; solo foros.
3. **El instalador de AppGroup:** si pide administrador o no. La versión portable no lo pide.
4. **Que Microsoft quitó las barras de herramientas en Windows 11:** no hay una frase oficial que
   lo diga con esas palabras; la más cercana es «Apps can no longer customize areas of the Taskbar».
5. **Windows 11 en el original y en la pike:** ninguno lo declara; no se probaron.
6. **Grupos dentro de grupos en el original y en la pike, y apps de la Store en las barras de
   Windows 10, ExplorerPatcher y el mod de Windhawk:** sin dato oficial.
7. **Idiomas de Windows** (pestañas 4 y 5): no busqué la cifra oficial.
