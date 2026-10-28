import SwiftUI

/// Shared layout pieces for the study guides (cards, fact grids, step lists, phrase banks, mistake
/// lists, and checklists).
@MainActor
enum Guide {

    static func card<Content: View>(_ title: String, icon: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Label(title, systemImage: icon).font(.title3.bold()).foregroundStyle(TOEFLTheme.navy)
            content()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .toeflCard()
    }

    static func facts(_ rows: [(String, String)]) -> some View {
        Grid(alignment: .leading, horizontalSpacing: 16, verticalSpacing: 8) {
            ForEach(rows, id: \.0) { row in
                GridRow {
                    Text(row.0).font(.subheadline.bold()).foregroundStyle(TOEFLTheme.teal)
                    Text(row.1).font(.subheadline).fixedSize(horizontal: false, vertical: true)
                }
            }
        }
    }

    static func structure(_ steps: [(String, String, String, String)]) -> some View {
        VStack(alignment: .leading, spacing: 14) {
            ForEach(steps, id: \.0) { step in
                HStack(alignment: .top, spacing: 12) {
                    Text(step.0)
                        .font(.headline).foregroundStyle(.white)
                        .frame(width: 28, height: 28)
                        .background(TOEFLTheme.action, in: Circle())
                    VStack(alignment: .leading, spacing: 4) {
                        Text(step.1).font(.headline)
                        Text(step.2).font(.subheadline).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                        if !step.3.isEmpty {
                            Text(step.3)
                                .font(.subheadline.italic())
                                .padding(8)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 6))
                                .fixedSize(horizontal: false, vertical: true)
                        }
                    }
                }
            }
        }
    }

    static func template(_ text: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(text)
                .font(.system(.body, design: .monospaced))
                .textSelection(.enabled)
                .padding(14)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
                .overlay(RoundedRectangle(cornerRadius: 8).stroke(TOEFLTheme.border))
                .fixedSize(horizontal: false, vertical: true)
            Text("可選取文字複製；模板只是骨架，內容一定要依題目情境改寫，不要原封不動背誦。")
                .font(.caption).foregroundStyle(.secondary)
        }
    }

    static func phrases(_ rows: [(String, String)]) -> some View {
        Grid(alignment: .leading, horizontalSpacing: 16, verticalSpacing: 8) {
            ForEach(rows, id: \.1) { row in
                GridRow {
                    Text(row.0).font(.subheadline.bold()).foregroundStyle(TOEFLTheme.teal)
                    Text(row.1).font(.subheadline).textSelection(.enabled).fixedSize(horizontal: false, vertical: true)
                }
            }
        }
    }

    static func phraseGroup(_ title: String, _ items: [String]) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            Text(title).font(.subheadline.bold()).foregroundStyle(TOEFLTheme.teal)
            ForEach(items, id: \.self) { item in
                Label(item, systemImage: "quote.opening")
                    .font(.subheadline)
                    .labelStyle(.titleOnly)
                    .padding(.leading, 10)
                    .textSelection(.enabled)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(.bottom, 4)
    }

    static func phraseTable(_ headers: [String], _ rows: [[String]]) -> some View {
        Grid(alignment: .leading, horizontalSpacing: 14, verticalSpacing: 8) {
            GridRow {
                ForEach(headers, id: \.self) { Text($0).font(.caption.bold()).foregroundStyle(.secondary) }
            }
            Divider()
            ForEach(rows, id: \.self) { row in
                GridRow {
                    ForEach(Array(row.enumerated()), id: \.offset) { index, cell in
                        Text(cell)
                            .font(index == 0 ? .subheadline.bold() : .subheadline)
                            .textSelection(.enabled)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
            }
        }
    }

    static func mistakes(_ rows: [(String, String, String)]) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            ForEach(rows, id: \.0) { row in
                VStack(alignment: .leading, spacing: 3) {
                    HStack(alignment: .firstTextBaseline, spacing: 8) {
                        Image(systemName: "xmark.circle.fill").foregroundStyle(.red)
                        Text(row.0).font(.subheadline).strikethrough(color: .red.opacity(0.6))
                    }
                    HStack(alignment: .firstTextBaseline, spacing: 8) {
                        Image(systemName: "checkmark.circle.fill").foregroundStyle(TOEFLTheme.teal)
                        Text(row.1).font(.subheadline.bold())
                    }
                    Text(row.2).font(.caption).foregroundStyle(.secondary).padding(.leading, 24)
                }
            }
        }
    }

    static func checklist(_ items: [String]) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            ForEach(items, id: \.self) { item in
                Label(item, systemImage: "checkmark.square").font(.subheadline)
            }
        }
    }

    static func note(_ text: String) -> some View {
        Label(text, systemImage: "info.circle").font(.caption).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
    }

    static func legend(_ text: String, _ color: Color) -> some View {
        HStack(spacing: 5) {
            Circle().fill(color).frame(width: 8, height: 8)
            Text(text).font(.caption).foregroundStyle(.secondary)
        }
    }
}
