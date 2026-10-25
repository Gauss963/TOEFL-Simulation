#!/usr/bin/env python3
"""The Complete the Words deletion rule, shared by the bank builder and content checks.

ETS rule: the first sentence stays intact; afterwards the second half of every second word is
removed until there are 10 blanks. One-letter words are skipped.

    python3 Tools/ctest_rule.py Tools/content2026/ctest_texts_2.json [notes.json]

prints each paragraph's blanks (kept letters + missing letters) and, given a notes file, checks
that its annotations list exactly those words in order.
"""

from __future__ import annotations

import json
import re
import sys


def ctest(text: str) -> tuple[str, list[tuple[str, str]]] | None:
    """Returns the passage with blanks written {kept|missing}, and the (kept, missing) pairs."""
    match = re.match(r"(.+?[.!?])\s+(.*)", text.strip(), re.S)
    if not match:
        return None
    first, rest = match.groups()
    blanks: list[tuple[str, str]] = []
    eligible = 0
    out: list[str] = []
    for token in rest.split(" "):
        core_match = re.match(r"^([\"'(]*)([A-Za-z]+)([^A-Za-z]*)$", token)
        if core_match and len(blanks) < 10 and len(core_match.group(2)) >= 2:
            lead, core, trail = core_match.groups()
            eligible += 1
            if eligible % 2 == 0:
                keep = len(core) // 2
                blanks.append((core[:keep], core[keep:]))
                out.append(f"{lead}{{{core[:keep]}|{core[keep:]}}}{trail}")
                continue
        out.append(token)
    if len(blanks) < 10:
        return None
    return first + " " + " ".join(out), blanks


def main() -> None:
    texts = json.load(open(sys.argv[1], encoding="utf-8"))
    notes = {}
    if len(sys.argv) > 2:
        notes = {row["id"]: row["blanks"] for row in json.load(open(sys.argv[2], encoding="utf-8"))}
    problems = 0
    for row in texts:
        built = ctest(row["text"])
        if not built:
            print(f"{row['id']}: FEWER THAN 10 BLANKS")
            problems += 1
            continue
        words = [kept + missing for kept, missing in built[1]]
        print(f"{row['id']}: " + ", ".join(f"{kept}[{missing}]" for kept, missing in built[1]))
        if notes:
            got = [blank.get("word") for blank in notes.get(row["id"], [])]
            if got != words:
                print(f"  NOTES MISMATCH: expected {words}, found {got}")
                problems += 1
            for blank in notes.get(row["id"], []):
                if not all(blank.get(key) for key in ("word", "pos", "zh", "why")):
                    print(f"  INCOMPLETE NOTE: {blank}")
                    problems += 1
    print("OK" if problems == 0 else f"{problems} PROBLEM(S)")


if __name__ == "__main__":
    main()
