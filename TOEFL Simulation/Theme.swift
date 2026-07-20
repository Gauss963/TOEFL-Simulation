import SwiftUI
#if os(macOS)
import AppKit
#else
import UIKit
#endif

enum TOEFLTheme {
    static let header = Color(red: 0.03, green: 0.22, blue: 0.39)
    static let action = Color(red: 0.02, green: 0.43, blue: 0.66)
    static let aqua = Color(red: 0.02, green: 0.62, blue: 0.67)
    static let gold = Color(red: 0.93, green: 0.60, blue: 0.12)
    static let navy = adaptive(light: (0.03, 0.22, 0.39, 1), dark: (0.60, 0.84, 0.98, 1))
    static let blue = adaptive(light: (0.02, 0.43, 0.66, 1), dark: (0.33, 0.76, 0.96, 1))
    static let teal = adaptive(light: (0.02, 0.50, 0.54, 1), dark: (0.33, 0.84, 0.82, 1))

    #if os(macOS)
    static let canvas = Color(nsColor: .windowBackgroundColor)
    static let surface = Color(nsColor: .controlBackgroundColor)
    static let input = Color(nsColor: .textBackgroundColor)
    static let border = Color(nsColor: .separatorColor)
    #else
    static let canvas = Color(uiColor: .systemGroupedBackground)
    static let surface = Color(uiColor: .secondarySystemGroupedBackground)
    static let input = Color(uiColor: .secondarySystemBackground)
    static let border = Color(uiColor: .separator)
    #endif

    private typealias RGBA = (CGFloat, CGFloat, CGFloat, CGFloat)

    private static func adaptive(light: RGBA, dark: RGBA) -> Color {
        #if os(macOS)
        return Color(nsColor: NSColor(name: nil) { appearance in
            let c = appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? dark : light
            return NSColor(srgbRed: c.0, green: c.1, blue: c.2, alpha: c.3)
        })
        #else
        return Color(uiColor: UIColor { traits in
            let c = traits.userInterfaceStyle == .dark ? dark : light
            return UIColor(red: c.0, green: c.1, blue: c.2, alpha: c.3)
        })
        #endif
    }
}

struct TOEFLPrimaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.headline)
            .foregroundStyle(.white)
            .padding(.horizontal, 22)
            .padding(.vertical, 12)
            .background(configuration.isPressed ? TOEFLTheme.header : TOEFLTheme.action)
            .clipShape(RoundedRectangle(cornerRadius: 7))
    }
}

struct TOEFLSecondaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.headline)
            .foregroundStyle(TOEFLTheme.navy)
            .padding(.horizontal, 18)
            .padding(.vertical, 10)
            .background(configuration.isPressed ? Color.gray.opacity(0.18) : TOEFLTheme.surface)
            .overlay(RoundedRectangle(cornerRadius: 7).stroke(TOEFLTheme.border))
            .clipShape(RoundedRectangle(cornerRadius: 7))
    }
}

extension View {
    func toeflCard() -> some View {
        padding(20)
            .background(TOEFLTheme.surface)
            .clipShape(RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.primary.opacity(0.08)))
            .shadow(color: .black.opacity(0.04), radius: 10, y: 4)
    }
}
