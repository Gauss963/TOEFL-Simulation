#!/bin/zsh
# Regenerates golden.json by compiling COPIES of the Mac app's Swift sources (never the originals)
# with a small command-line harness. Run on a Mac with Xcode:  ./generate.sh
set -euo pipefail
HERE="${0:A:h}"
REPO="${HERE:h:h:h:h:h}"
SRC="$REPO/TOEFL Simulation"
WORK="$(mktemp -d)"
trap "rm -rf \"$WORK\"" EXIT
for f in Models.swift QuestionBank.swift ExamSession.swift SpeakingScoring.swift WritingScoring.swift VocabularyBank.swift AudioSupport.swift; do
  # Resources load from $RES instead of the app bundle.
  sed -e 's|Bundle\.main\.url(|Bundle(path: ProcessInfo.processInfo.environment["RES"]!)!.url(|g' \
      -e 's|bundle: Bundle = \.main|bundle: Bundle = Bundle(path: ProcessInfo.processInfo.environment["RES"]!)!|' \
      "$SRC/$f" > "$WORK/$f"
done
# Plain types that live in view files.
sed -n '/^enum ReadingQuestionType/,/^}/p' "$SRC/ReadingDrillView.swift" > "$WORK/ReadingQuestionType.swift"
{ echo 'import Foundation'; sed -n '/^struct CTestParagraph/,/^}/p' "$SRC/ExamView.swift"; } > "$WORK/CTestParagraph.swift"
{ echo 'import Foundation'; echo 'enum GuideHarness {'; sed -n '/static func meaningGroups/,/^    }/p' "$SRC/SpeakingGuideView.swift"; echo '}'; } > "$WORK/Guide.swift"
cp "$HERE/main.swift" "$WORK/main.swift"
[[ -d /Applications/Xcode.app ]] && export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
xcrun swiftc -D DEBUG -O -parse-as-library -swift-version 5 -default-isolation MainActor -framework AppKit -o "$WORK/harness" "$WORK"/*.swift
RES="$SRC/Resources" "$WORK/harness" "$HERE/../golden.json"
echo "Wrote $HERE/../golden.json"
