//
//  ContentView.swift
//  TOEFL Simulation
//
//  Created by Gauss on 7/20/26.
//

import SwiftUI

enum TOEFLAppPage: String, CaseIterable, Identifiable {
    case dashboard
    case wordsDrill
    case study
    case history
    case resources

    var id: String { rawValue }
    var title: String {
        switch self {
        case .dashboard: "Practice Tests"
        case .wordsDrill: "Complete the Words"
        case .study: "Vocabulary"
        case .history: "Score History"
        case .resources: "Official Resources"
        }
    }
    var icon: String {
        switch self {
        case .dashboard: "rectangle.and.pencil.and.ellipsis"
        case .wordsDrill: "character.cursor.ibeam"
        case .study: "text.book.closed"
        case .history: "chart.line.uptrend.xyaxis"
        case .resources: "checkmark.seal"
        }
    }
}

struct ContentView: View {
    @StateObject private var history = TOEFLHistoryStore()
    @State private var selection: TOEFLAppPage? = .dashboard
    @State private var activeExamMode: ExamMode?

    init() {
        #if DEBUG
        let args = ProcessInfo.processInfo.arguments
        if args.contains("-openStudy") || args.contains("-openVocabulary") {
            _selection = State(initialValue: .study)
        }
        if args.contains("-openWordsDrill") { _selection = State(initialValue: .wordsDrill) }
        if args.contains("-startReadingPractice") { _activeExamMode = State(initialValue: .readingPractice) }
        if args.contains("-startFullPractice") { _activeExamMode = State(initialValue: .fullLength) }
        #endif
    }

    var body: some View {
        #if os(macOS)
        Group {
            if let activeExamMode { examDestination(activeExamMode) }
            else { rootContent }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(TOEFLTheme.canvas)
        #else
        rootContent
            .fullScreenCover(item: $activeExamMode, content: examDestination)
        #endif
    }

    private var rootContent: some View {
        NavigationSplitView {
            VStack(spacing: 0) {
                brand
                List(TOEFLAppPage.allCases, selection: $selection) { page in
                    Label(page.title, systemImage: page.icon)
                        .tag(page)
                        .padding(.vertical, 4)
                }
                .listStyle(.sidebar)
                Text("Independent TOEFL® practice simulator")
                    .font(.caption2).foregroundStyle(.secondary).padding(14)
            }
            .navigationSplitViewColumnWidth(min: 220, ideal: 255, max: 310)
        } detail: {
            Group {
                switch selection ?? .dashboard {
                case .dashboard: DashboardView(history: history.results) { activeExamMode = $0 }
                case .wordsDrill: WordsDrillView()
                case .study: VocabularyView()
                case .history: HistoryView(store: history)
                case .resources: ResourcesView()
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(TOEFLTheme.canvas)
        }
        .navigationSplitViewStyle(.balanced)
    }

    @ViewBuilder
    private func examDestination(_ mode: ExamMode) -> some View {
        TOEFLExamContainerView(mode: mode) { result in
            history.add(result)
            activeExamMode = nil
            selection = .history
        } onExit: {
            activeExamMode = nil
        }
    }

    private var brand: some View {
        HStack(spacing: 11) {
            ZStack {
                RoundedRectangle(cornerRadius: 9).fill(TOEFLTheme.action).frame(width: 40, height: 40)
                Text("T").font(.system(size: 22, weight: .bold, design: .serif)).foregroundStyle(.white)
            }
            VStack(alignment: .leading, spacing: 1) {
                Text("TOEFL Simulator").font(.headline)
                Text("2026 · Mac + iPad").font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
        }
        .padding(16)
    }
}

#Preview {
    ContentView()
}
