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
