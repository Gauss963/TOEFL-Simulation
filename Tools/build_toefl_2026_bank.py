#!/usr/bin/env python3
"""Build the TOEFL iBT 2026-format item bank and its audio manifest.

Inputs live in Tools/content2026/ (original items written to the ETS 2026 task specifications):
  academic_passage_*.json, academic_talk_*.json, conversation_*.json, announcement_*.json,
  daily_life_*.json, build_sentence_*.json, email_task_*.json, academic_discussion_*.json,
  choose_response_*.json, repeat_set_*.json, interview_set_*.json, ctest_texts_*.json (Complete the Words
  paragraphs) with ctest_notes_*.json (per-blank part of speech, meaning, and reasoning), legacy_*.json

Outputs:
  TOEFL Simulation/Resources/TOEFL2026Bank.json   items with set structure, read by the app
  Tools/content2026/audio_manifest.json           per-recording speaker segments for
                                                  Tools/generate_exam_audio.py

Complete the Words paragraphs follow the ETS rule: the first sentence is intact; afterwards the
second half of every second word is removed (10 blanks). Blanks are written {kept|missing}.
"""

from __future__ import annotations

import glob
import hashlib
import json
import random
import re
from pathlib import Path

from ctest_rule import ctest

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "Tools" / "content2026"
OUTPUT = ROOT / "TOEFL Simulation" / "Resources" / "TOEFL2026Bank.json"
MANIFEST = CONTENT / "audio_manifest.json"
SOURCE = "Original practice item written to the ETS TOEFL iBT 2026 task specifications; not an official ETS question."

NARRATOR = "am_michael"
# Kokoro voices with the better published quality grades; whisper-style and low-grade voices are excluded.
VOICES = {
    ("us", "female"): ["af_heart", "af_bella", "af_sarah", "af_aoede", "af_kore"],
    ("us", "male"): ["am_fenrir", "am_puck"],
    ("uk", "female"): ["bf_emma", "bf_isabella"],
    ("uk", "male"): ["bm_george", "bm_fable"],
}

items: list[dict] = []
manifest: dict[str, list[dict]] = {}


def load(kind: str) -> list[dict]:
    rows: list[dict] = []
    for path in sorted(glob.glob(str(CONTENT / f"{kind}_*.json"))):
        rows += json.loads(Path(path).read_text(encoding="utf-8"))
    return rows


def load_maps(kind: str) -> list[dict]:
    """Files that hold one JSON object each (id → value) rather than a list of items."""
    return [json.loads(Path(path).read_text(encoding="utf-8")) for path in sorted(glob.glob(str(CONTENT / f"{kind}_*.json")))]


def rng_for(key: str) -> random.Random:
    return random.Random(int(hashlib.sha1(key.encode()).hexdigest()[:12], 16))


def pick_voices(key: str, genders: list[str]) -> list[str]:
    """Distinct voices for each speaker, about 60% North American and 40% British."""
    rng = rng_for(key)
    chosen: list[str] = []
    for gender in genders:
        accent = "us" if rng.random() < 0.6 else "uk"
        pool = [v for v in VOICES[(accent, gender if gender in ("male", "female") else "female")] if v not in chosen]
        chosen.append(rng.choice(pool))
    return chosen


def choices(question: dict) -> tuple[list[dict], str]:
    ids = "ABCD"
    return [{"id": ids[i], "text": text} for i, text in enumerate(question["options"])], ids[question["answer"]]


def base(id: str, section: str, task: str, difficulty: str, prompt: str, **extra) -> dict:
    row = {
        "id": id, "section": section, "taskType": task, "difficulty": difficulty, "prompt": prompt,
        "passage": None, "audioScript": None, "choices": [], "correctChoiceID": None,
        "acceptedTextAnswers": [], "sentenceTokens": [], "correctSentenceTokens": [],
        "responseGuidance": None, "sampleAnswer": None, "explanation": "", "sourceNote": SOURCE,
    }
    row.update({k: v for k, v in extra.items() if v is not None})
    return row


def add_mc_set(set_id: str, section: str, task: str, difficulty: str, questions: list[dict], **shared) -> None:
    for index, question in enumerate(questions):
        options, key = choices(question)
        items.append(base(
            f"{set_id}-{index + 1}", section, task, difficulty, question["prompt"],
            choices=options, correctChoiceID=key, sampleAnswer=question["options"][question["answer"]],
            explanation=question["explanation"], setID=set_id, setIndex=index, **shared,
        ))


# Reading ---------------------------------------------------------------------------------------

notes = {row["id"]: row["blanks"] for row in load("ctest_notes")}
for text in load("ctest_texts"):
    built = ctest(text["text"])
    if not built:
        raise SystemExit(f"{text['id']} yields fewer than 10 blanks")
    passage, blanks = built
    set_id = f"cw-{text['id']}"
    annotations = notes.get(text["id"], [])
    for index, (kept, missing) in enumerate(blanks):
        word = kept + missing
        note = annotations[index] if index < len(annotations) else {}
        if note and note["word"] != word:
            raise SystemExit(f"{text['id']} note {index + 1} is for {note['word']!r}, not {word!r}")
        items.append(base(
            f"{set_id}-{index + 1}", "reading", "completeWords", text["difficulty"],
            "Fill in the missing letters in the paragraph.", passage=passage,
            acceptedTextAnswers=[missing, word], sampleAnswer=word,
            explanation=f"The complete word is “{word}”." + (f" {note['why']}" if note else ""),
            setID=set_id, setIndex=index, setting=text["title"],
            **({"partOfSpeech": note["pos"], "wordGloss": note["zh"], "blankReason": note["why"]} if note else {}),
        ))

for row in load("daily_life"):
    add_mc_set(row["id"], "reading", "readDailyLife", row["difficulty"], row["questions"],
               passage=f"{row['title']}\n\n{row['text']}")

for row in load("academic_passage"):
    add_mc_set(row["id"], "reading", "readAcademicPassage", row["difficulty"], row["questions"],
               passage=f"{row['title']}\n\n{row['passage']}", setting=row["title"])

# Listening -------------------------------------------------------------------------------------

for row in load("choose_response"):
    gender = "female" if rng_for(row["id"]).random() < 0.5 else "male"
    voice = pick_voices(row["id"], [gender])[0]
    answer = next(c["text"] for c in row["choices"] if c["id"] == row["correctChoiceID"])
    items.append(base(
        row["id"], "listening", "listenChooseResponse", row["difficulty"], "Choose the best response.",
        audioScript=row["audioScript"], choices=row["choices"], correctChoiceID=row["correctChoiceID"],
        sampleAnswer=answer, explanation=row["explanation"], setID=row["id"], setIndex=0,
        speakers=[{"role": "speaker", "gender": gender}],
    ))
    manifest[row["id"]] = [{"voice": voice, "text": row["audioScript"]}]

for row in load("conversation"):
    speakers = row["speakers"]
    voices = pick_voices(row["id"], [s["gender"] for s in speakers])
    transcript = "\n".join(f"{speakers[t['speaker']]['name']}: {t['text']}" for t in row["turns"])
    add_mc_set(row["id"], "listening", "listenConversation", row["difficulty"], row["questions"],
               audioScript=transcript, intro=row["intro"],
               speakers=[{"role": s["role"], "name": s["name"], "gender": s["gender"]} for s in speakers])
    manifest[row["id"]] = [{"voice": NARRATOR, "text": row["intro"], "pause": 0.8}] + [
        {"voice": voices[t["speaker"]], "text": t["text"], "pause": 0.45} for t in row["turns"]
    ]

for row in load("announcement"):
    voice = pick_voices(row["id"], [row["speaker"]["gender"]])[0]
    add_mc_set(row["id"], "listening", "listenAnnouncement", row["difficulty"], row["questions"],
               audioScript=row["text"], intro=row["intro"], speakers=[row["speaker"]])
    manifest[row["id"]] = [{"voice": NARRATOR, "text": row["intro"], "pause": 0.8}, {"voice": voice, "text": row["text"], "speed": 0.85}]

for row in load("academic_talk"):
    voice = pick_voices(row["id"], [row["speaker"]["gender"]])[0]
    add_mc_set(row["id"], "listening", "listenAcademicTalk", row["difficulty"], row["questions"],
               audioScript=row["talk"], intro=row["intro"], speakers=[row["speaker"]], setting=row["course"])
    manifest[row["id"]] = [{"voice": NARRATOR, "text": row["intro"], "pause": 0.8}, {"voice": voice, "text": row["talk"], "speed": 0.85}]

# Writing ---------------------------------------------------------------------------------------

# Traditional Chinese grammar notes ({id: note}) and email sample responses ({id: email}) live in
# separate files so they can be added without rewriting the item files.
build_notes_zh: dict[str, str] = {}
for notes_file in load_maps("bs_notes_zh"):
    build_notes_zh.update(notes_file)
email_samples: dict[str, str] = {}
for samples_file in load_maps("email_samples"):
    email_samples.update(samples_file)

for row in load("build_sentence"):
    tokens = row["chunks"] + [row["distractor"]]
    rng_for(row["id"]).shuffle(tokens)
    parts = row["template"].split("___")
    filled = "".join(part + (row["chunks"][i] if i < len(row["chunks"]) else "") for i, part in enumerate(parts))
    filled = re.sub(r"\s+([.?!,])", r"\1", re.sub(r"\s+", " ", filled)).strip()
    filled = filled[0].upper() + filled[1:]
    items.append(base(
        row["id"], "writing", "buildSentence", row["difficulty"], "Make an appropriate sentence.",
        sentenceTokens=tokens, correctSentenceTokens=row["chunks"], sampleAnswer=filled,
        explanation="\n".join(filter(None, [
            row.get("explanation") or "The chunks form the only grammatical reply.",
            row.get("explanation_zh") or build_notes_zh.get(row["id"]),
        ])),
        context=row["context"], sentenceTemplate=row["template"],
    ))

for row in load("email_task"):
    bullets = "\n".join(f"• {b}" for b in row["bullets"])
    items.append(base(
        row["id"], "writing", "writeEmail", row["difficulty"],
        f"{row['scenario']}\n\nWrite an email to {row['to']}. In your email, do the following:\n{bullets}\n\nWrite as much as you can and in complete sentences.",
        passage=f"To: {row['to']}\nSubject: {row['subject']}",
        sampleAnswer=row.get("sample_response") or email_samples.get(row["id"]),
        explanation="Scored 0–5 on the official Write an Email rubric: task completion, elaboration, language range and precision, and social conventions.",
    ))

discussions = load("academic_discussion")
if discussions:
    for row in discussions:
        posts = "\n\n".join(f"{s['name']}: {s['post']}" for s in row["students"])
        items.append(base(
            row["id"], "writing", "academicDiscussion", row["difficulty"],
            f"{row['professor']['name']}: {row['professor_post']}", passage=posts,
            sampleAnswer=row["sample_response"], setting=row["course"],
            explanation="Scored 0–5 on the official Academic Discussion rubric: relevance, elaboration, and language use. An effective response has at least 100 words.",
        ))
else:
    for row in json.loads((CONTENT / "legacy_discussions.json").read_text(encoding="utf-8")):
        items.append(base(row["id"], "writing", "academicDiscussion", row["difficulty"], row["prompt"],
                          passage=row["passage"], sampleAnswer=row["sampleAnswer"], explanation=row["explanation"]))

# Speaking --------------------------------------------------------------------------------------

def repeat_seconds(sentence: str) -> int:
    words = len(sentence.split())
    return 8 if words <= 7 else 9 if words <= 10 else 10 if words <= 13 else 11 if words <= 16 else 12


for row in load("repeat_set"):
    voice = pick_voices(row["id"], [row["speaker"]["gender"]])[0]
    manifest[f"intro-{row['id']}"] = [{"voice": NARRATOR, "text": row["intro"]}]
    for index, sentence in enumerate(row["sentences"]):
        id = f"{row['id']}-{index + 1}"
        items.append(base(
            id, "speaking", "listenRepeat", row["difficulty"], "Listen and repeat.",
            audioScript=sentence, sampleAnswer=sentence, intro=row["intro"],
            explanation="Scored 0–5: exact, intelligible repetition earns 5; meaning captured with minor changes earns 4.",
            setID=row["id"], setIndex=index, setting=row["setting"], stops=row["stops"],
            speakers=[row["speaker"]], responseSeconds=repeat_seconds(sentence),
        ))
        manifest[id] = [{"voice": voice, "text": sentence}]

for row in load("interview_set"):
    voice = pick_voices(row["id"], [row["interviewer"]["gender"]])[0]
    manifest[f"intro-{row['id']}"] = [{"voice": NARRATOR, "text": row["intro"]}]
    for index, question in enumerate(row["questions"]):
        id = f"{row['id']}-{index + 1}"
        items.append(base(
            id, "speaking", "interview", row["difficulty"], "Answer the interviewer's question.",
            audioScript=question, intro=row["intro"],
            explanation="Scored 0–5 on the official interview rubric: relevance, elaboration, fluency, intelligibility, and language use.",
            setID=row["id"], setIndex=index, speakers=[row["interviewer"]], responseSeconds=45,
        ))
        manifest[id] = [{"voice": voice, "text": question}]

# Output ----------------------------------------------------------------------------------------

ids = [item["id"] for item in items]
assert len(ids) == len(set(ids)), "duplicate item ids"
OUTPUT.write_text(json.dumps(items, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=1, sort_keys=True) + "\n", encoding="utf-8")
counts: dict[str, int] = {}
for item in items:
    counts[item["taskType"]] = counts.get(item["taskType"], 0) + 1
print(f"{len(items)} items, {len(manifest)} recordings")
print(counts)
