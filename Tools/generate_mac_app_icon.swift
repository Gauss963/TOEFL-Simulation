#!/usr/bin/env swift

import AppKit
import Foundation

guard CommandLine.arguments.count == 3 else {
    fputs("Usage: generate_mac_app_icon.swift SOURCE.png OUTPUT.png\n", stderr)
    exit(2)
}

let sourceURL = URL(fileURLWithPath: CommandLine.arguments[1])
let outputURL = URL(fileURLWithPath: CommandLine.arguments[2])
guard let source = NSImage(contentsOf: sourceURL) else {
    fputs("Unable to load source image: \(sourceURL.path)\n", stderr)
    exit(1)
}

let canvasSize = NSSize(width: 1024, height: 1024)
let iconRect = NSRect(x: 96, y: 106, width: 832, height: 832)
let iconPath = NSBezierPath(roundedRect: iconRect, xRadius: 188, yRadius: 188)

guard let bitmap = NSBitmapImageRep(
    bitmapDataPlanes: nil,
    pixelsWide: 1024,
    pixelsHigh: 1024,
    bitsPerSample: 8,
    samplesPerPixel: 4,
    hasAlpha: true,
    isPlanar: false,
    colorSpaceName: .deviceRGB,
    bitmapFormat: [],
    bytesPerRow: 0,
    bitsPerPixel: 0
), let context = NSGraphicsContext(bitmapImageRep: bitmap) else {
    fputs("Unable to create transparent bitmap context\n", stderr)
    exit(1)
}

bitmap.size = canvasSize
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = context
context.imageInterpolation = .high
NSColor.clear.setFill()
NSRect(origin: .zero, size: canvasSize).fill(using: .copy)

let shadow = NSShadow()
shadow.shadowColor = NSColor(calibratedWhite: 0, alpha: 0.30)
shadow.shadowOffset = NSSize(width: 0, height: -24)
shadow.shadowBlurRadius = 24
shadow.set()
NSColor(calibratedRed: 0.03, green: 0.25, blue: 0.46, alpha: 1).setFill()
iconPath.fill()

NSGraphicsContext.saveGraphicsState()
iconPath.addClip()
source.draw(
    in: iconRect,
    from: NSRect(origin: .zero, size: source.size),
    operation: .sourceOver,
    fraction: 1,
    respectFlipped: true,
    hints: [.interpolation: NSImageInterpolation.high]
)
NSGraphicsContext.restoreGraphicsState()
NSGraphicsContext.restoreGraphicsState()

guard let corner = bitmap.colorAt(x: 0, y: 0), corner.alphaComponent == 0 else {
    fputs("Transparency validation failed\n", stderr)
    exit(1)
}
guard let png = bitmap.representation(using: .png, properties: [:]) else {
    fputs("Unable to encode PNG\n", stderr)
    exit(1)
}
try png.write(to: outputURL, options: .atomic)
print("Generated macOS rounded icon: \(outputURL.path)")
