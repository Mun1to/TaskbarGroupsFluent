<div align="center">

<img src="brand/logo.png" width="128" height="128" alt="Logo de Taskbar Groups Fluent" />

# Taskbar Groups Fluent

**Agrupa tus accesos directos en un solo icono de la barra de tareas.** Una reescritura moderna en **WPF / .NET 10** de Taskbar Groups, con interfaz Fluent (estilo WinUI 3).

🌍 Español · [English](README.md)

Web: **[tbgf.munito.dev](https://tbgf.munito.dev)**

<p>
  <a href="https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest">
    <img src="https://img.shields.io/github/v/release/Mun1to/TaskbarGroupsFluent?label=versi%C3%B3n&style=for-the-badge&color=0a84ff" alt="Última versión" />
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/licencia-MIT-0a84ff?style=for-the-badge" alt="Licencia MIT" />
  </a>
  <img src="https://img.shields.io/badge/Windows%2010%20%2F%2011-x64-22c55e?style=for-the-badge" alt="Windows 10/11 x64" />
</p>

<p align="center">
  <a href="https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest/download/TaskbarGroupsFluent-Setup.exe">
    <img src="brand/download-button-es.svg" alt="Descargar Taskbar Groups Fluent" width="320" height="72" />
  </a>
</p>

<img src="docs/screenshots/panel-demo.gif" width="732" alt="Cuatro grupos anclados en una barra de tareas real de Windows 11, abriéndose uno tras otro: cuadrícula, lista, columna vertical y fila" />

<sub>Grabado en una barra de tareas real de Windows 11: cuatro grupos, cada uno con su forma de panel.</sub>

</div>

---

## ✨ Qué hace

Taskbar Groups convierte una carpeta llena de apps en **un solo botón de la barra de tareas**. Al pulsarlo se abre encima un pequeño panel con todas las apps del grupo, así tu barra queda ordenada y tienes todo a un clic.

- 🧩 **Añade cualquier app instalada**: una lista buscable con todo lo instalado, apps de escritorio **y** de Microsoft Store, leída del propio catálogo de apps de Windows. Un botón **Examinar…** cubre lo que no aparezca.
- 🖼️ **Los iconos buenos**: salen del mismo sitio del que los saca el menú Inicio, nítidos y transparentes. Cuando Windows no tiene icono para una app (algunas apps Electron), se usa el del propio ejecutable en vez de un cuadro en blanco.
- 🎨 **Tu propio icono de grupo**: cualquier imagen, recortada y con zoom en el editor integrado, o un **emoji a color**, o…
- 🔲 **…un icono hecho con las apps del grupo**: con un clic, el icono de la barra se hace con las cuatro primeras apps, y se actualiza solo cuando añades o quitas alguna.
- 📌 **Ancla a la barra de tareas**: cada grupo se convierte en su propio botón anclado.
- 🪟 **Cristal de Windows 11, con la forma que quieras**: el panel de un grupo se abre en cuadrícula, en una fila horizontal, en una columna vertical o como lista, con o sin el nombre de las apps, en tres tamaños de icono y con el nombre del grupo arriba o abajo. El editor te enseña cómo queda mientras lo cambias.
- 🖱️ **Abrir al pasar el cursor**: actívalo en la barra rápida y un grupo se abre con solo dejar el cursor encima de su icono, sin hacer clic. Espera un momento antes de abrir, así cruzar la barra no abre nada sin querer; nunca te quita el teclado de donde estabas escribiendo; y se cierra en cuanto alejas el cursor. Viene apagado, porque deja un pequeño vigilante en la bandeja del sistema.
- 📋 **Clic derecho en un grupo anclado** para **Abrir todas** y **Editar grupo**.
- 🚀 **Abrirlas todas de golpe**, desde ese menú o desde el botón de la cabecera del panel.
- 🗂️ **Grupos dentro de grupos**: mete un grupo en otro y se abre en su propio panel.
- ⚙️ **Opciones por app**: cámbiale el nombre a una app dentro del grupo, pásale argumentos o ábrela como administrador.
- 📁 **Apps y carpetas** en el mismo grupo.
- 💾 **Exportar, importar y duplicar**: comparte un grupo en un solo archivo, guarda una copia o empieza un grupo nuevo a partir de otro.
- 🔁 **¿Vienes de Taskbar Groups o de AppGroup?** Importa tus grupos de cualquiera de los dos y sigue como estabas.
- 🔄 **El icono de la barra sigue tus cambios**: cambia el icono de un grupo anclado y su botón se actualiza. Windows guarda esos iconos en caché, así que el Explorador se reinicia una vez; tus otros iconos anclados se quedan donde estaban.
- 🌗 **Una barra rápida abajo en la ventana principal**: claro u oscuro, español o inglés (los dos siguen a Windows si no eliges), abrir al pasar el cursor y la versión que tienes.
- ⬇️ **Instalador y actualizaciones**: se instala con doble clic, y la app te avisa cuando hay una versión nueva y se actualiza cuando tú digas.

---

## 📸 Capturas

<div align="center">
  <img src="docs/screenshots/es/main.png" width="820" alt="Ventana principal con cuatro grupos y la barra rápida abajo" />
</div>

<p align="center"><em>Tus grupos de un vistazo. La barra rápida de abajo tiene el tema, el idioma, abrir al pasar el cursor y la versión, con un ✓ verde cuando estás al día.</em></p>

<div align="center">
  <img src="docs/screenshots/es/editor.png" width="420" alt="Editor de grupo con sus apps y la vista previa del panel" />
</div>

<p align="center"><em>El editor de grupo: nombre, icono, apps y la vista previa del panel tal y como se va a abrir.</em></p>

> 💡 Las imágenes en GitHub se pueden pulsar: abre cualquier captura para verla a tamaño completo.

---

## ⬇️ Descarga e instalación

1. Pulsa el botón **Descargar** de arriba, o usa este enlace directo: **[descargar el instalador](https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest/download/TaskbarGroupsFluent-Setup.exe)**.
2. Ejecuta `TaskbarGroupsFluent-Setup.exe`. Se instala solo para tu usuario (sin permisos de administrador) y, si falta el **.NET 10 Desktop Runtime**, lo descarga e instala.
3. Abre **Taskbar Groups** desde el menú Inicio.

> ¿Vas a instalarlo en un PC sin internet? `TaskbarGroupsFluent-Setup-Offline.exe`, en la [página de Releases](https://github.com/Mun1to/TaskbarGroupsFluent/releases/latest), lleva .NET dentro.

> Windows puede avisar de un *"editor desconocido"*, porque la app no está firmada con un certificado de pago. Pulsa **Más información → Ejecutar de todas formas**.

## 🔄 Actualizaciones

Al abrirse, la app mira en GitHub si hay una versión nueva. Abajo a la derecha de la ventana principal sale la versión que tienes, con un ✓ verde si estás al día (púlsalo para volver a mirar). Si hay una nueva pone **Actualizar ahora**: un clic la descarga, la instala y vuelve a abrir la app.

---

## 🚀 Cómo se usa

1. Pulsa **Añadir grupo** y dale un **nombre** y un **icono** (una imagen, un emoji, o **Apps** para hacerlo con las apps del grupo).
2. Añade accesos con **Aplicación** (cualquier app instalada de la lista buscable, o *Examinar…*), **Carpeta** o **Grupo**.
3. Elige cómo se ve su panel en **Panel** y pulsa **Guardar grupo**.
4. En la tarjeta del grupo, pulsa **Anclar a la barra de tareas**. La app abre la carpeta con el acceso del grupo resaltado: haz clic derecho en él, luego *Mostrar más opciones* y luego *Anclar a la barra de tareas*. Windows 11 no deja que las apps se anclen solas, así que este último paso lo haces tú.
5. Pulsa el botón anclado para abrir el panel con tus apps, o haz clic derecho para **Abrir todas** y **Editar grupo**.

---

## 🛠️ Compilar desde el código

Requiere el [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/Mun1to/TaskbarGroupsFluent.git
cd TaskbarGroupsFluent
dotnet build TaskbarGroupsFluent.sln -c Release
```

Ejecuta el proyecto `TaskbarGroups.App`. Para generar la carpeta que empaqueta el instalador:

```bash
dotnet publish src/TaskbarGroups.App -c Release -r win-x64 --self-contained false -o dist/TaskbarGroupsFluent
dotnet publish src/TaskbarGroups.Hover -c Release -r win-x64 --self-contained false -o dist/TaskbarGroupsFluent
dotnet publish src/TaskbarGroups.Background -c Release -r win-x64 --self-contained false -o dist/TaskbarGroupsFluent/Background
```

El instalador se genera con [Inno Setup](https://jrsoftware.org/isinfo.php) a partir de [`installer/TaskbarGroupsFluent.iss`](installer/TaskbarGroupsFluent.iss).

### 🧱 Cómo funciona

| Proyecto | Qué hace |
| --- | --- |
| `TaskbarGroups.Core` | La lógica sin interfaz: modelo de datos, catálogo de apps de Windows e iconos, interop del shell, rutas |
| `TaskbarGroups.App` | La app Fluent: ventana principal, editor de grupo, selectores de apps y emojis, editor de recorte, actualizador |
| `TaskbarGroups.Background` | El panel sin bordes que sale encima de la barra de tareas |
| `TaskbarGroups.Hover` | Vigilante opcional para abrir al pasar el cursor, vive en la bandeja del sistema |

Cada grupo anclado es un acceso directo al panel con el nombre del grupo como argumento y su propio AppUserModelID, que es lo que hace que cada grupo sea un botón aparte en la barra.

Lo del cursor no es algo que Windows ofrezca: un botón de la barra no avisa de nada cuando el cursor pasa por encima, así que el vigilante lee los botones de la barra con UI Automation y compara la posición del cursor con ellos. Solo lo hace mientras el cursor está de verdad sobre la barra, y guarda en caché lo que lee, que es lo que lo mantiene en una fracción de un uno por ciento de un núcleo cuando no pasa nada.

Que abra rápido pide un truco más. Arrancar un panel en frío significa cargar .NET, luego WPF, y luego pintar una ventana por primera vez: casi un segundo, por poco que haga el panel. Así que cuando el cursor se acerca a la barra, el vigilante carga uno por adelantado y lo deja escondido, listo para salir en unos cincuenta milisegundos; un panel cargado ocupa memoria de verdad, así que se suelta en cuanto el cursor lleva un rato lejos.

## 🙏 Créditos

Construido sobre el trabajo de:

- [tjackenpacken/taskbar-groups](https://github.com/tjackenpacken/taskbar-groups), la app original.
- [PikeNote/taskbar-groups-pike-beta](https://github.com/PikeNote/taskbar-groups-pike-beta), el fork de la comunidad cuya estructura sirvió de base a esta reescritura.
- [WPF-UI](https://github.com/lepoco/wpfui), la librería de controles Fluent, y [SkiaSharp](https://github.com/mono/SkiaSharp), para los emojis a color.

## 🔍 No te fíes, compruébalo

El código abierto solo sirve si alguien lo lee, y casi nadie lo hace. Así que en vez de
pedirte que te fíes de este proyecto, aquí tienes el texto para comprobarlo: pásaselo a tu
propia IA junto con este repositorio y en unos minutos tendrás un informe de seguridad en tu
idioma, aunque no sepas programar.

**[Abre AI-AUDIT.md](AI-AUDIT.md)** y pégalo en Claude Code, Codex, Cursor, Copilot o lo que
uses. Es el mismo texto en todos los repositorios públicos de aquí, así que puedes comparar.

## 📜 Licencia

[MIT](LICENSE), igual que los proyectos de arriba.
