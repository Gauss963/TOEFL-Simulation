//
//  TOEFL_SimulationApp.swift
//  TOEFL Simulation
//
//  Created by Gauss on 7/20/26.
//

import SwiftUI

@main
struct TOEFL_SimulationApp: App {
    init() {
        #if DEBUG
        let issues = TOEFLQuestionBank.validate() + TOEFLStudyBank.validate()
        let arguments = ProcessInfo.processInfo.arguments
        if let flag = arguments.firstIndex(of: "-renderWordsDrill"), arguments.indices.contains(flag + 1) {
            WordsDrillView.renderSnapshots(to: URL(fileURLWithPath: arguments[flag + 1]))
            exit(0)
        }
        if arguments.contains("-selfTest") {
            // Prints validation and Listen and Choose a Response rotation results, then exits.
            let report = issues + TOEFLQuestionBank.rotationIssues() + WordsDrillStore.selfTestIssues()
            FileHandle.standardError.write(Data(((report.isEmpty ? ["SELF-TEST PASSED"] : report).joined(separator: "\n") + "\n").utf8))
            exit(report.isEmpty ? 0 : 1)
        }
        assert(issues.isEmpty, issues.joined(separator: "\n"))
        #endif
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
        }
        #if os(macOS)
        .defaultSize(width: 1220, height: 800)
        .windowResizability(.contentMinSize)
        #endif
    }
}
