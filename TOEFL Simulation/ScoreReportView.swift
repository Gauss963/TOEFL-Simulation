import SwiftUI
import Foundation

struct TOEFLScoreReportView: View {
    let result: TOEFLExamResult
    let heading: String
    let doneTitle: String
    let done: () -> Void

    @State private var selectedSection: TOEFLSectionKind?

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text(heading).font(.title2.bold())
                    Text(result.mode.title).font(.caption).foregroundStyle(.secondary)
                }
                Spacer()
                Button(doneTitle, action: done).buttonStyle(TOEFLPrimaryButtonStyle())
            }
            .padding(.horizontal, 26).padding(.vertical, 16).background(TOEFLTheme.surface)
            Divider()

            ScrollView {
                VStack(alignment: .leading, spacing: 22) {
                    scoreHero
                    sectionScores
                    methodology
                    review
                }
                .frame(maxWidth: 1040, alignment: .leading)
                .padding(30)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(TOEFLTheme.canvas)
    }

    private var scoreHero: some View {
        HStack(spacing: 28) {
            ZStack {
                Circle().stroke(TOEFLTheme.blue.opacity(0.18), lineWidth: 14)
                Circle().trim(from: 0, to: result.overallBand / 6)
                    .stroke(LinearGradient(colors: [TOEFLTheme.action, TOEFLTheme.aqua], startPoint: .top, endPoint: .bottom), style: StrokeStyle(lineWidth: 14, lineCap: .round))
                    .rotationEffect(.degrees(-90))
                VStack(spacing: 2) {
                    Text(String(format: "%.1f", result.overallBand)).font(.system(size: 42, weight: .black, design: .rounded)).foregroundStyle(TOEFLTheme.navy)
                    Text("of 6").font(.caption).foregroundStyle(.secondary)
                }
            }
            .frame(width: 150, height: 150)

            VStack(alignment: .leading, spacing: 10) {
                Text("Practice estimate").font(.title.bold()).foregroundStyle(TOEFLTheme.navy)
                Text("CEFR \(result.cefr) · comparable overall score ~\(result.comparableTotal120)/120")
                    .font(.title3).foregroundStyle(.secondary)
                Text("The 2026 TOEFL overall score is the average of four section bands rounded to the nearest half band. For a single-section practice, this card summarizes that section only.")
                    .foregroundStyle(.secondary)
                Text("Completed \(result.completedAt.formatted(date: .abbreviated, time: .shortened)) · \(duration(result.elapsedSeconds))")
                    .font(.caption).foregroundStyle(.tertiary)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .toeflCard()
    }

    private var sectionScores: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Section performance").font(.title2.bold()).foregroundStyle(TOEFLTheme.navy)
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 220), spacing: 14)], spacing: 14) {
                ForEach(result.sections) { section in
                    Button { selectedSection = selectedSection == section.kind ? nil : section.kind } label: {
                        VStack(alignment: .leading, spacing: 10) {
                            HStack {
                                Label(section.kind.title, systemImage: section.kind.icon).font(.headline)
                                Spacer()
                                Text(String(format: "%.1f", section.band)).font(.title2.bold()).foregroundStyle(TOEFLTheme.navy)
                            }
                            ProgressView(value: section.band, total: 6).tint(TOEFLTheme.action)
                            Text("\(section.answered)/\(section.total) answered" + (section.objectiveTotal > 0 ? " · \(section.correct)/\(section.objectiveTotal) keyed" : ""))
                                .font(.caption).foregroundStyle(.secondary)
                            if let route = section.route { Text(route.title).font(.caption.bold()).foregroundStyle(TOEFLTheme.teal) }
                            if selectedSection == section.kind {
                                Divider()
                                Text(section.rubricDetail).font(.footnote).foregroundStyle(.secondary)
                            }
                        }
                        .frame(maxWidth: .infinity, minHeight: 120, alignment: .topLeading)
                        .toeflCard()
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }

    private var methodology: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("How this estimate works", systemImage: "info.circle.fill").font(.headline).foregroundStyle(TOEFLTheme.navy)
            Text("Reading, Listening, and Build a Sentence use exact answer keys. Adaptive routes constrain the objective estimate. Email and Academic Discussion use a local, disclosed heuristic for completion, development, lexical variety, cohesion, and email conventions. Speaking combines recording completion with your 1–5 rubric self-check.")
                .font(.subheadline).foregroundStyle(.secondary)
            Text("ETS does not publish the secure item calibration or production AI/rater model. These results diagnose practice performance and are not official, predictive, or suitable for admissions.")
                .font(.subheadline.bold()).foregroundStyle(.secondary)
        }
        .toeflCard()
    }

    private var review: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Answer review").font(.title2.bold()).foregroundStyle(TOEFLTheme.navy)
            ForEach(TOEFLSectionKind.allCases) { kind in
                let records = result.reviews.filter { $0.question.section == kind }
                if !records.isEmpty {
                    DisclosureGroup("\(kind.title) · \(records.count) items") {
                        VStack(spacing: 12) {
                            ForEach(records) { record in reviewCard(record) }
                        }
                        .padding(.top, 12)
                    }
                    .font(.headline)
                    .toeflCard()
                }
            }
        }
    }

    private func reviewCard(_ record: TOEFLQuestionReview) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Question \(record.number) · \(record.question.taskType.title)").font(.subheadline.bold())
                    Text(record.question.prompt).font(.subheadline).foregroundStyle(.secondary)
                }
                Spacer()
                statusBadge(record.isCorrect)
            }
            if let audio = record.question.audioScript {
                Text("Transcript: \(audio)").font(.footnote).foregroundStyle(.secondary)
            }
            Text("Your answer: \(answerText(record))").font(.footnote)
            if let sample = record.question.sampleAnswer, !sample.isEmpty {
                Text((record.isCorrect == nil ? "Sample / target: " : "Key: ") + sample)
                    .font(.footnote).foregroundStyle(TOEFLTheme.teal)
            }
            Text(record.question.explanation).font(.caption).foregroundStyle(.secondary)
            Text(record.question.sourceNote).font(.caption2).foregroundStyle(.tertiary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(14)
        .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(TOEFLTheme.border))
    }

    @ViewBuilder
    private func statusBadge(_ correct: Bool?) -> some View {
        if let correct {
            Label(correct ? "Correct" : "Review", systemImage: correct ? "checkmark.circle.fill" : "xmark.circle.fill")
                .font(.caption.bold()).foregroundStyle(correct ? TOEFLTheme.teal : .red)
        } else {
            Label("Rubric-scored", systemImage: "text.badge.checkmark").font(.caption.bold()).foregroundStyle(TOEFLTheme.blue)
        }
    }

    private func answerText(_ record: TOEFLQuestionReview) -> String {
        if let id = record.answer.selectedChoiceID,
           let choice = record.question.choices.first(where: { $0.id == id }) { return choice.text }
        if !record.answer.textEntry.isEmpty { return record.answer.textEntry }
        if !record.answer.orderedTokens.isEmpty { return record.answer.orderedTokens.joined(separator: " ") }
        if !record.answer.writtenResponse.isEmpty { return record.answer.writtenResponse }
        if record.answer.recordingDuration > 0 {
            return String(format: "Recording %.1f sec · self-rating %d/5", record.answer.recordingDuration, record.answer.speakingSelfRating)
        }
        return "No response"
    }

    private func duration(_ seconds: Int) -> String {
        let minutes = seconds / 60
        return minutes > 0 ? "\(minutes)m \(seconds % 60)s" : "\(seconds)s"
    }
}
