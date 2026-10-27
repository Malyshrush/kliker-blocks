"""Generate a German UI draft from the complete English UI map using bundled Argos.

The generated file is a mechanical localization artifact, then checked by KLIKER's
localization audit. Placeholder-bearing strings fall back to English if altered.
"""

import json
import re
from pathlib import Path

import argostranslate.translate as translation


class OfflineSentencizer:
    def __init__(self, pkg):
        pass

    def split_sentences(self, text):
        return [part for part in re.split(r"(?<=[.!?。！？])\s+|\n+", text) if part] or [text]


translation.StanzaSentencizer = OfflineSentencizer
translation.MiniSBDSentencizer = OfflineSentencizer
root = Path(__file__).resolve().parent.parent
english = {}
for name in ("en.json", "en-extra.json"):
    english.update(json.loads((root / "Languages" / name).read_text(encoding="utf-8"))["translations"])

source = (root / "BlockUiTranslations.cs").read_text(encoding="utf-8")
data = source.split('private const string Data = """', 1)[1].split('""";', 1)[0]
for row in data.splitlines():
    if not row.strip():
        continue
    columns = row.split("|")
    if len(columns) != 5:
        raise ValueError(f"Unexpected block UI row: {row[:80]}")
    english.setdefault(columns[0].replace(r"\n", "\n"), columns[1].replace(r"\n", "\n"))

languages = {language.code: language for language in translation.get_installed_languages()}
translator = languages["en"].get_translation(languages["de"])
placeholders = re.compile(r"\$\{[^}]*\}|\{\d+\}")
german = {}
for index, (russian, value) in enumerate(english.items(), 1):
    if not value.strip():
        german[russian] = value
        continue
    try:
        candidate = "\n".join(translator.translate(line) if line.strip() else ""
                              for line in value.split("\n"))
        if (sorted(placeholders.findall(candidate)) != sorted(placeholders.findall(value))
                or re.search(r"[А-Яа-яЁё]", candidate)):
            candidate = value
        german[russian] = candidate or value
    except Exception as error:
        print(f"Translation fallback at {index}: {error}", flush=True)
        german[russian] = value
    if index % 50 == 0:
        print(f"Translated {index}/{len(english)} UI strings", flush=True)

manual = {
    "Запись журнала в текстовый файл…": "Protokoll in Textdatei schreiben…",
    "Записать действия в журнал": "Blockaktionen protokollieren",
    "Журнал выполнения": "Ausführungsprotokoll",
    "Файл": "Datei",
    "Редактор": "Editor",
    "Блоки": "Blöcke",
    "Захват": "Aufnahme",
    "Макрос": "Makro",
    "Сценарий": "Szenario",
    "Назначения мыши": "Maustastenbelegung",
    "Справка": "Hilfe",
    "Открыть…": "Öffnen…",
    "Сохранить…": "Speichern…",
    "Снимок области…": "Bereich aufnehmen…",
    "Показать журнал": "Protokoll anzeigen",
    "Пауза": "Pause",
    "Таймаут": "Zeitüberschreitung",
    "Тело": "Rumpf",
    "Описание": "Beschreibung",
    "Следить за курсором": "Cursor verfolgen",
    "Сдвиг мыши": "Maus relativ bewegen",
    "Кнопка мыши": "Maustaste",
    "Нажать клавишу": "Taste drücken",
    "Клавиша: удержание": "Taste halten",
    "Ввести текст": "Text eingeben",
    "Буфер обмена": "Zwischenablage setzen",
    "Прочитать буфер": "Zwischenablage lesen",
    "Файл ↔ массив": "Datei ↔ Array",
    "Перевести текст": "Text übersetzen",
    "Перевести массив": "Array übersetzen",
    "Ввод без прерывания": "Eingaben gruppieren",
    "Задать переменную": "Variable setzen",
    "Объявить переменную": "Variable definieren",
    "Удалить переменную": "Variable löschen",
    "Задать по имени": "Variable über Namen setzen",
    "Элемент массива": "Array-Element",
    "Операция с массивом": "Array-Operation",
    "Разделить строку": "Zeichenfolge aufteilen",
    "Если переменная": "Wenn Variable",
    "Пока условие": "Solange Bedingung erfüllt",
    "Выбор": "Fallunterscheidung",
    "Если курсор смещён": "Wenn Cursor bewegt",
    "Если цвет пикселя": "Wenn Pixelfarbe übereinstimmt",
    "Ждать цвет пикселя": "Auf Pixelfarbe warten",
    "Если клавиша нажата": "Wenn Taste gedrückt",
    "Найти цвет": "Farbe suchen",
    "Найти изображение": "Bild suchen",
    "Снимок экрана": "Bildschirmfoto",
    "Запомнить экран": "Bildschirm zwischenspeichern",
    "Цвет из снимка": "Farbe aus Aufnahme lesen",
    "Подсчитать цвет": "Farbpixel zählen",
    "Контрольная сумма снимка": "Prüfsumme der Aufnahme",
    "Заменить цвет в снимке": "Farbe in Aufnahme ersetzen",
    "Найти цвет в снимке": "Farbe in Aufnahme suchen",
    "Найти изображение в снимке": "Bild in Aufnahme suchen",
    "Все пиксели цвета": "Alle Farbpixel",
    "Все совпадения картинки": "Alle Bildtreffer",
    "Сохранить снимок": "Aufnahme speichern",
    "К найденной точке": "Zum Fundpunkt bewegen",
    "Если окно найдено": "Wenn Fenster gefunden",
    "Найти HWND окна": "Fenster-Handle suchen",
    "Действие с окном": "Fensteraktion",
    "Данные элемента": "Elementdaten",
    "Задать текст элемента": "Elementtext setzen",
    "Послать сообщение окну": "Nachricht an Fenster senden",
    "Принять сообщение": "Nachricht empfangen",
    "Запустить файл": "Datei starten",
    "Запустить сценарий": "Szenario starten",
    "Повторить": "Wiederholen",
    "Вызвать подпрограмму": "Unterprogramm aufrufen",
    "Начало подпрограммы": "Unterprogramm-Beginn",
    "Возврат из подпрограммы": "Rückkehr aus Unterprogramm",
    "Конец ветки": "Zweig-Ende",
    "Сообщение": "Protokollmeldung",
    "Файл журнала": "Protokolldatei",
    "Очистить журнал": "Protokoll leeren",
    "Воспроизвести WAV": "WAV abspielen",
    "Озвучить текст": "Text vorlesen",
    "Звуковой сигнал": "Signalton",
    "Запросить текст": "Texteingabe abfragen",
    "Вопрос с кнопками": "Frage mit Schaltflächen",
    "Выбрать вариант": "Option auswählen",
    "О программе": "Über KLIKER",
    "Старт": "Start",
    "Стоп": "Stopp",
    "Далее": "Weiter",
    "Да": "Ja",
    "Нет": "Nein",
    "Сохранить": "Speichern",
    "Отмена": "Abbrechen",
    "Встроенный": "Integriert",
}
german.update(manual)
(root / "Languages" / "de.json").write_text(
    json.dumps({"language": "de", "translations": german}, ensure_ascii=False, indent=2) + "\n",
    encoding="utf-8",
)
print(f"Saved {len(german)} German UI strings", flush=True)
