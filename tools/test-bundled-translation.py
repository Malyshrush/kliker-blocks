"""Developer smoke test for the bundled offline Argos runtime and model graph."""

import os
import re
from pathlib import Path

import argostranslate.settings as settings
import argostranslate.translate as translation


class OfflineSentencizer:
    def __init__(self, pkg):
        pass

    def split_sentences(self, text):
        return [part for part in re.split(r"(?<=[.!?。！？])\s+|\n+", text) if part] or [text]


translation.StanzaSentencizer = OfflineSentencizer
translation.MiniSBDSentencizer = OfflineSentencizer
bundled = os.environ.get("KLIKER_BUNDLED_MODELS")
if bundled and Path(bundled).is_dir():
    settings.package_dirs.append(Path(bundled))

languages = {language.code: language for language in translation.get_installed_languages()}
samples = {
    "ru": "Привет, мой друг.",
    "en": "Hello, my friend.",
    "fr": "Bonjour, mon ami.",
    "es": "Hola, amigo mío.",
    "zh": "你好，我的朋友。",
    "de": "Hallo, mein Freund.",
}
for source, sample in samples.items():
    for target in samples:
        if source == target:
            continue
        result = languages[source].get_translation(languages[target]).translate(sample)
        if not result.strip():
            raise AssertionError(f"Empty translation: {source} → {target}")
        print(source, target, result, flush=True)
