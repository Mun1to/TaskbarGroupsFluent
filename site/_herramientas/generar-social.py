"""Las tarjetas que salen al compartir un enlace de la web: social.png (español) y social-en.png.

    python site/_herramientas/generar-social.py

1200 x 630, el tamaño que piden Open Graph y X. A la izquierda, el logo, el titular de la
portada y lo que es; a la derecha, un panel de verdad (el de «Juegos» de assets/shot-formas.webp,
abierto sobre una barra de tareas real). Las letras son las de Windows (Segoe UI): se genera en
Windows y las dos imágenes se suben hechas. Si cambia el titular, se vuelve a correr.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

RAIZ = Path(__file__).resolve().parent.parent
FUENTES = Path("C:/Windows/Fonts")
W, H = 1200, 630
FONDO = (11, 11, 13)
AZUL = (76, 194, 255)
TEXTO = (233, 233, 238)
SUAVE = (171, 172, 184)

TEXTOS = {
    "social.png": ("Diez iconos en la barra.", "Ahora solo uno.", "Agrupa tus apps en un solo botón de la barra de tareas.", "Gratis y de código abierto · Windows 10 y 11 · WinGet y Microsoft Store"),
    "social-en.png": ("Ten icons on the taskbar.", "Now just one.", "Group your apps into a single taskbar button.", "Free and open source · Windows 10 and 11 · WinGet and Microsoft Store"),
}


def letra(nombre, tam):
    return ImageFont.truetype(str(FUENTES / nombre), tam)


def fondo():
    img = Image.new("RGB", (W, H), FONDO)
    luz = Image.new("RGB", (W, H), FONDO)
    d = ImageDraw.Draw(luz)
    d.ellipse((560, -120, 1320, 640), fill=(10, 60, 110))
    d.ellipse((-260, 330, 520, 900), fill=(14, 34, 60))
    return Image.blend(img, luz.filter(ImageFilter.GaussianBlur(140)), 0.9)


def panel():
    # El cuarto de arriba a la izquierda de shot-formas: el panel «Juegos» en cuadrícula, con su barra.
    foto = Image.open(RAIZ / "assets" / "shot-formas.webp").convert("RGB")
    trozo = foto.crop((112, 226, 482, 560))
    trozo = trozo.resize((int(trozo.width * 1.12), int(trozo.height * 1.12)), Image.LANCZOS)
    mascara = Image.new("L", trozo.size, 0)
    ImageDraw.Draw(mascara).rounded_rectangle((0, 0, trozo.width - 1, trozo.height - 1), 18, fill=255)
    return trozo, mascara


def tarjeta(nombre, linea1, linea2, bajada, pie):
    img = fondo()
    trozo, mascara = panel()
    x, y = W - trozo.width - 56, (H - trozo.height) // 2 + 10
    sombra = Image.new("L", (W, H), 0)
    ImageDraw.Draw(sombra).rounded_rectangle((x + 6, y + 18, x + trozo.width + 6, y + trozo.height + 18), 22, fill=170)
    img.paste((0, 0, 0), (0, 0), sombra.filter(ImageFilter.GaussianBlur(24)))
    img.paste(trozo, (x, y), mascara)
    ImageDraw.Draw(img).rounded_rectangle((x, y, x + trozo.width - 1, y + trozo.height - 1), 18, outline=(255, 255, 255, 40), width=1)

    d = ImageDraw.Draw(img)
    logo = Image.open(RAIZ / "assets" / "logo.png").convert("RGBA").resize((52, 52), Image.LANCZOS)
    img.paste(logo, (64, 60), logo)
    d.text((130, 70), "Taskbar Groups Fluent", font=letra("segoeuib.ttf", 30), fill=TEXTO)

    # El titular, lo más grande que quepa en el hueco de la izquierda del panel.
    hueco = x - 64 - 40
    tam = 60
    while tam > 30 and max(d.textlength(t, font=letra("segoeuib.ttf", tam)) for t in (linea1, linea2)) > hueco:
        tam -= 2
    grande = letra("segoeuib.ttf", tam)
    d.text((64, 190), linea1, font=grande, fill=TEXTO)
    d.text((64, 190 + int(tam * 1.18)), linea2, font=grande, fill=AZUL)
    tam_b = 25
    while tam_b > 16 and d.textlength(bajada, font=letra("segoeui.ttf", tam_b)) > hueco:
        tam_b -= 1
    d.text((64, 190 + int(tam * 2.36) + 26), bajada, font=letra("segoeui.ttf", tam_b), fill=SUAVE)
    d.text((64, 532), pie, font=letra("segoeui.ttf", 21), fill=SUAVE)
    img.save(RAIZ / nombre, optimize=True)
    print(f"{nombre}: {img.size[0]} x {img.size[1]}")


for nombre, (a, b, c, e) in TEXTOS.items():
    tarjeta(nombre, a, b, c, e)
