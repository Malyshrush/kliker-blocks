"""Build the offline German help pack from reviewed English help text."""

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
source = json.loads((root / "tools" / "en-help-source.json").read_text(encoding="utf-8"))
languages = {language.code: language for language in translation.get_installed_languages()}
translator = languages["en"].get_translation(languages["de"])


def translate(text):
    return "\n".join(translator.translate(part) if part.strip() else ""
                     for part in text.split("\n"))


result = {"language": "de", "overview": translate(source["overview"]),
          "sections": {}, "blocks": {}}
for section, value in source["sections"].items():
    result["sections"][section] = translate(value)
    print(f"Section: {section}", flush=True)
for index, (kind, value) in enumerate(source["blocks"].items(), 1):
    result["blocks"][kind] = translate(value)
    if index % 10 == 0:
        print(f"Blocks: {index}/{len(source['blocks'])}", flush=True)

(root / "Languages" / "de-help.json").write_text(
    json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("German help saved", flush=True)
