#!/usr/bin/env python3
"""Build the bundled TOEFL vocabulary bank from user-authorized PDF tables.

The PDFs themselves stay outside the repository. This importer retains compact
headword metadata and one study example per entry, merges duplicate headwords,
and optionally enriches pronunciation/English definitions from an existing
offline GRE vocabulary export and ECDICT copy.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

import pdfplumber


SOURCE_FILES = {
    "托福中階500單字.pdf": "托福中階500單字 · 授權來源",
    "托福進階500單字.pdf": "托福進階500單字 · 授權來源",
    "高階500英文難字.pdf": "高階500英文難字 · 授權來源",
}

ENTRY_START = re.compile(r"(?m)(?<!\d)(\d{1,3})\.\s*")
ENTRY_HEADER = re.compile(
    r"^\s*(?P<head>[^\n(]{1,80}?)\s*\((?P<pos>[^)\n]{1,30})\)\s*(?P<zh>.*)",
    re.S,
)
CJK = re.compile(r"[\u3400-\u9fff\uf900-\ufaff]+")
DEFAULT_REVIEWED_GLOSSES = Path(__file__).with_name("reviewed_vocabulary_glosses.json")


@dataclass
class ExtractedEntry:
    word: str
    part_of_speech: str
    chinese: str
    example: str
    source: str


def compact(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()


def normalize_word(raw: str) -> str:
    word = compact(raw).strip(" .,:;-")
    replacements = {
        "l ithe": "lithe",
        "phenomenon 單數": "phenomenon",
        "ornithology/ist": "ornithology",
        "vying with sb for sth": "vie with ... for ...",
        "desist from + ving/ in": "desist from",
        "take + umbrage": "take umbrage",
        "tussle + with": "tussle with",
        "to have a penchant for + ving": "have a penchant for",
        "to be in the pink": "in the pink",
    }
    lowered = word.lower()
    word = replacements.get(lowered, word)
    if word.lower().startswith("to ") and len(word.split()) <= 4:
        word = word[3:]
    word = re.sub(r"\s*\+\s*(?:v-?ing|ving|sb|sth)\b.*$", "", word, flags=re.I)
    return compact(word).lower()


def normalize_part_of_speech(raw: str) -> str:
    value = compact(raw).replace("adj.", "adj").replace("adv.", "adv")
    value = value.replace("ph.片語", "phrase")
    return value.strip(" .")


def clean_chinese(raw: str) -> str:
    value = compact(raw)
    value = re.sub(r"(?<=[\u3400-\u9fff])\s+(?=[\u3400-\u9fff])", "", value)
    value = re.sub(r"^(?:to\s+\w+\s*\([^)]*\)\s*)+", "", value, flags=re.I)
    value = re.sub(r"^[A-Za-z][A-Za-z /-]*\([^)]*\)\s*", "", value)
    return value.strip(" /；")


def clean_example(raw: str) -> str:
    value = raw.replace("⚫", " ").replace("●", " ").replace("", " ").replace("•", " ")
    value = CJK.sub(" ", value)
    value = compact(value)
    value = value.translate(str.maketrans({"。": ".", "，": ",", "？": "?", "！": "!", "／": "/"}))
    value = re.sub(r"\s+([,.;:!?])", r"\1", value)
    value = re.sub(r"(?:\s*[./,]){2,}\s*$", ".", value)
    value = value.strip(" -")
    if len(value) > 360:
        shortened = value[:360].rsplit(" ", 1)[0].rstrip(" ,;:")
        value = shortened + "…"
    return value


def example_chunks(raw: str) -> list[str]:
    return [clean_example(chunk) for chunk in re.split(r"[⚫●•]", raw) if clean_example(chunk)]


def example_for(entry: tuple[str, str, str], chunks: list[str], index: int, count: int) -> str:
    word = normalize_word(entry[0])
    token = next((part for part in re.findall(r"[a-z]+", word) if len(part) >= 4), "")
    stem = token[: max(4, len(token) - 3)]
    if stem:
        matching = [chunk for chunk in chunks if re.search(rf"\b{re.escape(stem)}", chunk, re.I)]
        if matching:
            return matching[0]
    if not chunks:
        return ""
    if len(chunks) == count:
        return chunks[index]
    return chunks[min(round(index * (len(chunks) - 1) / max(count - 1, 1)), len(chunks) - 1)]


def extract_pdf(path: Path, source: str) -> list[ExtractedEntry]:
    entries: list[ExtractedEntry] = []
    with pdfplumber.open(path) as pdf:
        for page in pdf.pages:
            for table in page.extract_tables():
                for row in table:
                    if not row or not row[0]:
                        continue
                    left = row[0]
                    right = row[1] if len(row) > 1 and row[1] else ""
                    starts = list(ENTRY_START.finditer(left))
                    parsed: list[tuple[str, str, str]] = []
                    for index, start in enumerate(starts):
                        end = starts[index + 1].start() if index + 1 < len(starts) else len(left)
                        header = ENTRY_HEADER.match(left[start.end() : end].strip())
                        if header:
                            parsed.append((header["head"], header["pos"], header["zh"]))
                    chunks = example_chunks(right)
                    for index, (head, pos, chinese) in enumerate(parsed):
                        word = normalize_word(head)
                        if not word or not re.search(r"[a-z]", word):
                            continue
                        entries.append(
                            ExtractedEntry(
                                word=word,
                                part_of_speech=normalize_part_of_speech(pos),
                                chinese=clean_chinese(chinese),
                                example=example_for((head, pos, chinese), chunks, index, len(parsed)),
                                source=source,
                            )
                        )
    return entries


def load_gre_vocabulary(directory: Path | None) -> dict[str, dict]:
    if directory is None:
        return {}
    result: dict[str, dict] = {}
    for path in directory.glob("*Vocabulary.json"):
        for entry in json.loads(path.read_text(encoding="utf-8")):
            word = normalize_word(entry.get("word", ""))
            if word:
                result[word] = entry
    return result


def load_ecdict(path: Path | None, targets: set[str]) -> dict[str, dict]:
    if path is None:
        return {}
    result: dict[str, dict] = {}
    with path.open(encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            word = normalize_word(row.get("word", ""))
            if word in targets and word not in result:
                result[word] = row
    return result


def compact_definition(raw: str) -> str:
    value = compact(raw.replace("\\n", "; "))
    value = re.sub(r"\s*;\s*", "; ", value)
    if len(value) > 220:
        value = value[:220].rsplit(" ", 1)[0].rstrip(" ,;:") + "…"
    return value


def incomplete_example(value: str) -> bool:
    if len(value) < 18:
        return True
    return bool(
        re.search(
            r"\b(?:a|an|and|as|at|because|by|for|from|her|his|in|into|is|of|on|or|the|to|was|were|with)$",
            value.rstrip(" ,;:"),
            re.I,
        )
        or re.search(r"['’]s$", value.rstrip(" ,;:"), re.I)
    )


def original_fallback(word: str, definition: str, chinese: str, part_of_speech: str) -> str:
    meaning = definition or chinese or "the referenced vocabulary meaning"
    if part_of_speech.lower().startswith("adj"):
        return f'The passage describes the subject as “{word},” meaning {meaning}.'
    if part_of_speech.lower().startswith(("v", "verb")):
        return f'In this context, “{word}” means {meaning}.'
    return f'The lecture uses “{word}” to refer to {meaning}.'


def load_reviewed_glosses(path: Path | None) -> dict[str, dict]:
    """Manually reviewed Taiwan Chinese senses, ordered by TOEFL frequency."""
    if path is None or not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def apply_reviewed_glosses(entries: list[dict], reviewed: dict[str, dict]) -> list[dict]:
    for entry in entries:
        review = reviewed.get(entry["word"])
        if not review:
            continue
        entry["partOfSpeech"] = review["partOfSpeech"]
        entry["chinese"] = review["chinese"]
        entry["definition"] = review["definition"]
    return entries


def merge_entries(
    extracted: list[ExtractedEntry],
    gre: dict[str, dict],
    ecdict: dict[str, dict],
) -> list[dict]:
    grouped: dict[str, list[ExtractedEntry]] = defaultdict(list)
    for entry in extracted:
        grouped[entry.word].append(entry)

    result: list[dict] = []
    for word, variants in grouped.items():
        gre_entry = gre.get(word, {})
        lexical = ecdict.get(word, {})
        sources = sorted({entry.source for entry in variants})
        chinese_values = list(dict.fromkeys(filter(None, (entry.chinese for entry in variants))))
        chinese = "；".join(chinese_values[:3])
        chinese = chinese or {
            "afford": "負擔得起；提供",
            "in the pink": "健康的；精神飽滿的",
        }.get(word, "")
        pos_values = list(dict.fromkeys(filter(None, (entry.part_of_speech for entry in variants))))
        part_of_speech = "/".join(pos_values[:3])
        definition = compact_definition(
            gre_entry.get("definition", "") or lexical.get("definition", "")
        )
        pronunciation = compact(
            gre_entry.get("pronunciation", "") or lexical.get("phonetic", "")
        )
        synonyms = list(dict.fromkeys(gre_entry.get("synonyms", [])))[:8]
        examples = [entry.example for entry in variants if entry.example]
        demonstrating = [
            example
            for example in examples
            if re.search(rf"(?<![A-Za-z]){re.escape(word.split()[0])}", example, re.I)
        ]
        example = min(demonstrating or examples, key=len, default="")
        if not example:
            example = gre_entry.get("example", "")
        if incomplete_example(example):
            example = original_fallback(word, definition, chinese, part_of_speech)
        elif not re.search(r"[.!?…][\"”’']?$", example):
            example += "."

        result.append(
            {
                "word": word,
                "pronunciation": pronunciation,
                "partOfSpeech": part_of_speech,
                "definition": definition,
                "chinese": chinese,
                "synonyms": synonyms,
                "example": example,
                "sources": sources,
                "isHighFrequency": len(sources) > 1,
            }
        )
    return sorted(result, key=lambda item: item["word"].casefold())


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--references", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--gre-vocabulary", type=Path)
    parser.add_argument("--ecdict", type=Path)
    parser.add_argument("--reviewed-glosses", type=Path, default=DEFAULT_REVIEWED_GLOSSES)
    args = parser.parse_args()

    extracted: list[ExtractedEntry] = []
    for filename, source in SOURCE_FILES.items():
        path = args.references / filename
        if not path.exists():
            raise FileNotFoundError(path)
        extracted.extend(extract_pdf(path, source))

    targets = {entry.word for entry in extracted}
    merged = merge_entries(
        extracted,
        load_gre_vocabulary(args.gre_vocabulary),
        load_ecdict(args.ecdict, targets),
    )
    merged = apply_reviewed_glosses(merged, load_reviewed_glosses(args.reviewed_glosses))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(merged, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Extracted {len(extracted)} rows and wrote {len(merged)} unique vocabulary entries.")


if __name__ == "__main__":
    main()
