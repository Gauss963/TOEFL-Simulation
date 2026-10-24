# Third-party content notices

No third-party source-code package is bundled or linked. The application uses Apple platform frameworks including SwiftUI, Foundation, Combine, and AVFoundation.

## Open English WordNet 2025

Some supplemental vocabulary definitions, pronunciations, and synonyms originate from [Open English WordNet 2025](https://en-word.net/), used under [Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/). The normalized vocabulary records are adapted for compact offline card display.

## ECDICT

Some supplemental vocabulary lexical fields originate from [ECDICT](https://github.com/skywind3000/ECDICT).

MIT License

Copyright (c) 2025 Linwei

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Kokoro-82M recordings

The bundled `WordPronunciations.pack` (one pronunciation clip per headword, `af_heart` voice) and `ExamAudio.pack` (every Listening and Speaking recording, using several North American and British English voices) were synthesized at build time with the [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) model. The model weights and voices are released under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). Neither the model nor any Python, PyTorch, or phonemizer library is bundled or linked; the app only plays the generated AAC audio with AVFoundation.

## Authorized local references

The application bundles normalized vocabulary records derived from the project owner's authorized study sources. The original PDFs and books remain outside the distributed application and repository. See `SOURCES.md` for the inventory and provenance notes.

TOEFL® and ETS® are registered trademarks of ETS. This independent project is not endorsed or approved by ETS.
