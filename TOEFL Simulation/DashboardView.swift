import SwiftUI

struct DashboardView: View {
    let history: [TOEFLExamResult]
    let start: (ExamMode) -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 26) {
                hero
                if let latest = history.first { latestResult(latest) }
                fullTestCard
                practiceCards
                formatOverview
                accuracyNotice
            }
            .frame(maxWidth: 1080, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Practice Tests")
    }

    private var hero: some View {
        HStack(alignment: .center, spacing: 28) {
            VStack(alignment: .leading, spacing: 12) {
                Text("Practice the TOEFL iBT 2026 format")
                    .font(.system(size: 34, weight: .bold, design: .rounded))
                    .foregroundStyle(TOEFLTheme.navy)
                Text("Two-stage adaptive Reading and Listening, current task types, timed delivery, local recording, and transparent practice scoring for Mac and iPad.")
                    .font(.title3)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
                Label("\(TOEFLQuestionBank.allQuestions.count) questions · \(TOEFLStudyBank.words.count) vocabulary entries", systemImage: "books.vertical.fill")
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(TOEFLTheme.teal)
            }
            Spacer(minLength: 12)
            ZStack {
                RoundedRectangle(cornerRadius: 24)
                    .fill(LinearGradient(colors: [TOEFLTheme.action, TOEFLTheme.aqua], startPoint: .topLeading, endPoint: .bottomTrailing))
                VStack(spacing: 6) {
                    Text("2026")
                        .font(.system(size: 34, weight: .black, design: .rounded))
                    Text("CURRENT FORMAT")
                        .font(.caption.bold())
                        .tracking(1.2)
                }
                .foregroundStyle(.white)
            }
            .frame(width: 170, height: 120)
            .accessibilityLabel("Current 2026 format")
        }
        .toeflCard()
    }

    private func latestResult(_ result: TOEFLExamResult) -> some View {
        HStack(spacing: 20) {
            VStack(alignment: .leading, spacing: 5) {
                Text("Latest practice estimate").font(.headline)
                Text(result.completedAt, format: .dateTime.year().month().day().hour().minute())
                    .font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
            scorePill("Overall", String(format: "%.1f", result.overallBand))
            scorePill("CEFR", result.cefr)
            scorePill("Comparable", "\(result.comparableTotal120)")
        }
        .toeflCard()
    }

    private var fullTestCard: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 7) {
                    Label("Full-length simulation", systemImage: "rectangle.and.pencil.and.ellipsis")
                        .font(.title2.bold()).foregroundStyle(TOEFLTheme.navy)
                    Text("Reading → Listening → Writing → Speaking · 120 items · about 90 minutes of section time; allow approximately 2 hours overall")
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Button("Start full test") { start(.fullLength) }
                    .buttonStyle(TOEFLPrimaryButtonStyle())
            }
            Divider()
            HStack(spacing: 10) {
                Label("Two-stage adaptive", systemImage: "arrow.triangle.branch")
                Label("Timed sections", systemImage: "timer")
                Label("Microphone recording", systemImage: "mic")
                Label("Score report", systemImage: "chart.bar.doc.horizontal")
            }
            .font(.subheadline)
            .foregroundStyle(.secondary)
        }
        .toeflCard()
    }

    private var practiceCards: some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 230), spacing: 14)], spacing: 14) {
            ForEach([ExamMode.readingPractice, .listeningPractice, .writingPractice, .speakingPractice]) { mode in
                let kind = mode.sectionKinds[0]
                VStack(alignment: .leading, spacing: 12) {
                    Image(systemName: kind.icon)
                        .font(.title2).foregroundStyle(TOEFLTheme.blue)
                    Text(mode.title).font(.headline)
                    Text(mode.subtitle).font(.subheadline).foregroundStyle(.secondary)
                    Spacer(minLength: 4)
                    Button("Start") { start(mode) }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                }
                .frame(maxWidth: .infinity, minHeight: 168, alignment: .topLeading)
                .toeflCard()
            }
        }
    }

    private var formatOverview: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Official 2026 section blueprint").font(.title2.bold()).foregroundStyle(TOEFLTheme.navy)
            Grid(alignment: .leading, horizontalSpacing: 18, verticalSpacing: 11) {
                GridRow {
                    Text("Section").bold(); Text("Task types").bold(); Text("Items").bold(); Text("Base time").bold()
                }
                Divider().gridCellColumns(4)
                ForEach(TOEFLSectionKind.allCases) { kind in
                    GridRow {
                        Label(kind.title, systemImage: kind.icon).font(.subheadline.bold())
                        Text(taskSummary(kind)).font(.subheadline).foregroundStyle(.secondary)
                        Text("\(kind.officialItemCount)").monospacedDigit()
                        Text("\(kind.officialMinutes) min").monospacedDigit()
                    }
                }
            }
        }
        .toeflCard()
    }

    private var accuracyNotice: some View {
        Label {
            Text("Practice estimates are not official TOEFL scores. ETS uses secure calibrated forms plus AI and certified raters; this app uses answer keys and a disclosed local rubric.")
        } icon: {
            Image(systemName: "info.circle.fill").foregroundStyle(TOEFLTheme.gold)
        }
        .font(.footnote)
        .foregroundStyle(.secondary)
        .padding(.horizontal, 4)
    }

    private func scorePill(_ label: String, _ value: String) -> some View {
        VStack(spacing: 2) {
            Text(value).font(.title2.bold()).monospacedDigit().foregroundStyle(TOEFLTheme.navy)
            Text(label).font(.caption2).foregroundStyle(.secondary)
        }
        .frame(minWidth: 82)
        .padding(.vertical, 9)
        .background(TOEFLTheme.blue.opacity(0.09), in: RoundedRectangle(cornerRadius: 9))
    }

    private func taskSummary(_ kind: TOEFLSectionKind) -> String {
        switch kind {
        case .reading: "Complete Words · Daily Life · Academic Passage"
        case .listening: "Response · Conversation · Announcement · Academic Talk"
        case .writing: "Build Sentence · Email · Academic Discussion"
        case .speaking: "Listen & Repeat · Interview"
        }
    }
}
