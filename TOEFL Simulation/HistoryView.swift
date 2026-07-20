import SwiftUI

struct HistoryView: View {
    @ObservedObject var store: TOEFLHistoryStore
    @State private var confirmClear = false
    @State private var selectedResult: TOEFLExamResult?

    var body: some View {
        ZStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    HStack {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Score history")
                                .font(.system(size: 30, weight: .bold, design: .rounded))
                                .foregroundStyle(TOEFLTheme.navy)
                            Text("Practice results stay on this device.").foregroundStyle(.secondary)
                        }
                        Spacer()
                        if !store.results.isEmpty {
                            Button("Clear history", role: .destructive) { confirmClear = true }
                        }
                    }

                    if store.results.isEmpty {
                        ContentUnavailableView("No completed tests", systemImage: "chart.line.uptrend.xyaxis", description: Text("Complete a practice session to create a score report."))
                            .frame(maxWidth: .infinity, minHeight: 420)
                            .toeflCard()
                    } else {
                        ForEach(store.results) { result in historyCard(result) }
                    }
                }
                .frame(maxWidth: 950, alignment: .leading)
                .padding(30)
            }

            if let selectedResult {
                TOEFLScoreReportView(result: selectedResult, heading: "Saved practice report", doneTitle: "Close") {
                    self.selectedResult = nil
                }
                .background(TOEFLTheme.canvas.ignoresSafeArea())
                .transition(.opacity)
                .zIndex(2)
            }
        }
        .navigationTitle("Score History")
        .confirmationDialog("Delete every saved result?", isPresented: $confirmClear, titleVisibility: .visible) {
            Button("Delete all results", role: .destructive) { store.clear() }
        }
    }

    private func historyCard(_ result: TOEFLExamResult) -> some View {
        Button { selectedResult = result } label: {
            HStack(spacing: 18) {
                VStack(alignment: .leading, spacing: 5) {
                    Text(result.mode.title).font(.headline).foregroundStyle(.primary)
                    Text(result.completedAt, format: .dateTime.year().month().day().hour().minute())
                        .font(.caption).foregroundStyle(.secondary)
                    Text(result.sections.map { section in
                        "\(section.kind.title) \(String(format: "%.1f", section.band))"
                    }.joined(separator: " · "))
                        .font(.subheadline).foregroundStyle(.secondary)
                }
                Spacer()
                VStack(alignment: .trailing, spacing: 4) {
                    Text(String(format: "%.1f", result.overallBand)).font(.title.bold()).foregroundStyle(TOEFLTheme.navy)
                    Text("CEFR \(result.cefr) · ~\(result.comparableTotal120)/120").font(.caption).foregroundStyle(.secondary)
                }
                Image(systemName: "chevron.right").foregroundStyle(.tertiary)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .toeflCard()
        }
        .buttonStyle(.plain)
    }
}
