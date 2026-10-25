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
        if ProcessInfo.processInfo.arguments.contains("-selfTest") {
            // Prints validation and Listen and Choose a Response rotation results, then exits.
            let report = issues + TOEFLQuestionBank.rotationIssues()
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
