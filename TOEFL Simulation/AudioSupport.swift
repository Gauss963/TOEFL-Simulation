import AVFoundation
import Foundation
import Combine

/// Pre-rendered audio clips (Kokoro-82M, Apache-2.0) bundled as resource packs with a byte-range index.
/// Large packs are split into volumes (`Name.pack`, `Name-2.pack`, …) so each file stays small enough
/// for the repository; an index entry is `[offset, length]` or `[offset, length, volume]`.
nonisolated final class AudioClipPack: Sendable {
    private let volumes: [Data]
    private let clips: [String: [Int]]
    private let lowercasedKeys: Bool

    init(name: String, lowercasedKeys: Bool, bundle: Bundle = .main) {
        struct Index: Decodable { let clips: [String: [Int]] }
        let index = bundle.url(forResource: name, withExtension: "json")
            .flatMap { try? Data(contentsOf: $0) }
            .flatMap { try? JSONDecoder().decode(Index.self, from: $0) }
        clips = index?.clips ?? [:]
        var volumes: [Data] = []
        while let url = bundle.url(forResource: volumes.isEmpty ? name : "\(name)-\(volumes.count + 1)", withExtension: "pack"),
              let data = try? Data(contentsOf: url, options: .alwaysMapped) {
            volumes.append(data)
        }
        self.volumes = volumes
        self.lowercasedKeys = lowercasedKeys
    }

    var clipCount: Int { volumes.isEmpty ? 0 : clips.count }

    func clip(for key: String) -> Data? {
        let lookup = lowercasedKeys ? key.lowercased().trimmingCharacters(in: .whitespacesAndNewlines) : key
        guard let range = clips[lookup], range.count == 2 || range.count == 3 else { return nil }
        let volume = range.count == 3 ? range[2] : 0
        guard volumes.indices.contains(volume), range[0] >= 0, range[1] > 0,
              range[0] + range[1] <= volumes[volume].count else { return nil }
        return volumes[volume].subdata(in: range[0]..<(range[0] + range[1]))
    }
}

/// Natural word pronunciations for flash cards and the vocabulary bank.
enum WordPronunciationLibrary {
    static let shared = AudioClipPack(name: "WordPronunciations", lowercasedKeys: true)
}

/// Multi-voice recordings for Listening stimuli and Speaking prompts.
enum TOEFLExamAudio {
    static let pack = AudioClipPack(name: "ExamAudio", lowercasedKeys: false)

    static func hasClip(_ key: String) -> Bool { pack.clip(for: key) != nil }

    static func introKey(for setID: String) -> String { "intro-\(setID)" }

    /// A short sine tone in WAV format, used for the "start speaking" and "stop" signals.
    static let beep: Data = tone(frequency: 880, seconds: 0.32)
    static let endTone: Data = tone(frequency: 660, seconds: 0.22)

    private static func tone(frequency: Double, seconds: Double) -> Data {
        let sampleRate = 24_000.0
        let count = Int(sampleRate * seconds)
        var samples = Data(capacity: count * 2)
        for index in 0..<count {
            let t = Double(index) / sampleRate
            let envelope = min(1, min(t, seconds - t) / 0.02)
            let value = Int16(sin(2 * .pi * frequency * t) * envelope * 0.45 * Double(Int16.max))
            withUnsafeBytes(of: value.littleEndian) { samples.append(contentsOf: $0) }
        }
        var header = Data()
        func append<T: FixedWidthInteger>(_ value: T) { withUnsafeBytes(of: value.littleEndian) { header.append(contentsOf: $0) } }
        header.append(contentsOf: Array("RIFF".utf8)); append(UInt32(36 + samples.count))
        header.append(contentsOf: Array("WAVEfmt ".utf8)); append(UInt32(16)); append(UInt16(1)); append(UInt16(1))
        append(UInt32(sampleRate)); append(UInt32(sampleRate * 2)); append(UInt16(2)); append(UInt16(16))
        header.append(contentsOf: Array("data".utf8)); append(UInt32(samples.count))
        return header + samples
    }
}

enum EnglishVoice {
    /// The highest-quality installed US English voice, skipping novelty voices.
    static let best: AVSpeechSynthesisVoice? = {
        let candidates = AVSpeechSynthesisVoice.speechVoices().filter {
            $0.language == "en-US" && !$0.voiceTraits.contains(.isNoveltyVoice)
        }
        return candidates.max { $0.quality.rawValue < $1.quality.rawValue }
            ?? AVSpeechSynthesisVoice(language: "en-US")
    }()
}

/// Plays a word's bundled neural clip, falling back to the best installed system voice.
@MainActor
final class WordPronunciationPlayer: NSObject, ObservableObject, AVAudioPlayerDelegate, AVSpeechSynthesizerDelegate {
    @Published private(set) var isSpeaking = false
    private var audioPlayer: AVAudioPlayer?
    private let synthesizer = AVSpeechSynthesizer()

    override init() {
        super.init()
        synthesizer.delegate = self
    }

    func speak(_ word: String) {
        stop()
        #if os(iOS)
        try? AVAudioSession.sharedInstance().setCategory(.playback, mode: .spokenAudio)
        try? AVAudioSession.sharedInstance().setActive(true)
        #endif
        if let data = WordPronunciationLibrary.shared.clip(for: word),
           let player = try? AVAudioPlayer(data: data, fileTypeHint: AVFileType.m4a.rawValue) {
            player.delegate = self
            audioPlayer = player
            if player.play() {
                isSpeaking = true
                return
            }
            audioPlayer = nil
        }
        let utterance = AVSpeechUtterance(string: word)
        utterance.voice = EnglishVoice.best
        utterance.rate = 0.45
        isSpeaking = true
        synthesizer.speak(utterance)
    }

    func stop() {
        audioPlayer?.stop()
        audioPlayer = nil
        if synthesizer.isSpeaking { synthesizer.stopSpeaking(at: .immediate) }
        isSpeaking = false
    }

    nonisolated func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        Task { @MainActor in self.isSpeaking = false }
    }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        Task { @MainActor in self.isSpeaking = false }
    }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didCancel utterance: AVSpeechUtterance) {
        Task { @MainActor in self.isSpeaking = false }
    }
}

/// Plays exam recordings once, in order, with a test-wide volume setting.
@MainActor
final class ExamAudioPlayer: NSObject, ObservableObject, AVAudioPlayerDelegate, AVSpeechSynthesizerDelegate {
    @Published private(set) var isPlaying = false
    @Published var volume: Float = 0.85 {
        didSet { audioPlayer?.volume = volume }
    }

    private var audioPlayer: AVAudioPlayer?
    private let synthesizer = AVSpeechSynthesizer()
    private var completion: (() -> Void)?
    private var generation = 0

    override init() {
        super.init()
        synthesizer.delegate = self
    }

    /// Plays the clip for `key`; if it is missing, reads `fallbackText` with the best system voice.
    func play(key: String, fallbackText: String?, completion: @escaping () -> Void) {
        if let data = TOEFLExamAudio.pack.clip(for: key) {
            play(data: data, hint: AVFileType.m4a.rawValue, completion: completion)
        } else if let fallbackText, !fallbackText.isEmpty {
            stop()
            prepareSession()
            self.completion = completion
            let utterance = AVSpeechUtterance(string: fallbackText)
            utterance.voice = EnglishVoice.best
            utterance.rate = 0.5
            utterance.volume = volume
            isPlaying = true
            synthesizer.speak(utterance)
        } else {
            completion()
        }
    }

    func playTone(_ data: Data, completion: @escaping () -> Void) {
        play(data: data, hint: AVFileType.wav.rawValue, completion: completion)
    }

    private func play(data: Data, hint: String, completion: @escaping () -> Void) {
        stop()
        prepareSession()
        guard let player = try? AVAudioPlayer(data: data, fileTypeHint: hint) else {
            completion()
            return
        }
        generation += 1
        player.delegate = self
        player.volume = volume
        audioPlayer = player
        self.completion = completion
        isPlaying = player.play()
        if !isPlaying { finish() }
    }

    func stop() {
        completion = nil
        audioPlayer?.stop()
        audioPlayer = nil
        if synthesizer.isSpeaking { synthesizer.stopSpeaking(at: .immediate) }
        isPlaying = false
    }

    private func prepareSession() {
        #if os(iOS)
        try? AVAudioSession.sharedInstance().setCategory(.playAndRecord, mode: .spokenAudio, options: [.defaultToSpeaker, .allowBluetoothA2DP])
        try? AVAudioSession.sharedInstance().setActive(true)
        #endif
    }

    private func finish() {
        isPlaying = false
        audioPlayer = nil
        let done = completion
        completion = nil
        done?()
    }

    nonisolated func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        Task { @MainActor in
            guard player === self.audioPlayer else { return }
            self.finish()
        }
    }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        Task { @MainActor in self.finish() }
    }
}

@MainActor
final class SpeakingResponseRecorder: NSObject, ObservableObject, AVAudioRecorderDelegate {
    @Published private(set) var isRecording = false
    @Published private(set) var permissionDenied = false
    @Published private(set) var level: Float = 0

    private var recorder: AVAudioRecorder?
    private(set) var lastRecordingURL: URL?
    private var meterTimer: Timer?

    func start(questionID: String) async -> Bool {
        let allowed = await requestPermission()
        guard allowed else {
            permissionDenied = true
            return false
        }

        #if os(iOS)
        do {
            let session = AVAudioSession.sharedInstance()
            try session.setCategory(.playAndRecord, mode: .spokenAudio, options: [.defaultToSpeaker])
            try session.setActive(true)
        } catch {
            return false
        }
        #endif

        let safeID = questionID.replacingOccurrences(of: "/", with: "-")
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("toefl-\(safeID)-\(UUID().uuidString).m4a")
        let settings: [String: Any] = [
            AVFormatIDKey: Int(kAudioFormatMPEG4AAC),
            AVSampleRateKey: 44_100,
            AVNumberOfChannelsKey: 1,
            AVEncoderAudioQualityKey: AVAudioQuality.high.rawValue
        ]
        do {
            recorder = try AVAudioRecorder(url: url, settings: settings)
            recorder?.delegate = self
            recorder?.isMeteringEnabled = true
            recorder?.prepareToRecord()
            isRecording = recorder?.record() == true
            lastRecordingURL = isRecording ? url : nil
            startMetering()
            return isRecording
        } catch {
            recorder = nil
            isRecording = false
            return false
        }
    }

    @discardableResult
    func stop() -> Double {
        let duration = recorder?.currentTime ?? 0
        recorder?.stop()
        recorder = nil
        isRecording = false
        meterTimer?.invalidate()
        meterTimer = nil
        level = 0
        return duration
    }

    /// Microphone permission without recording, used by the pre-Speaking microphone check.
    func requestPermission() async -> Bool {
        #if os(iOS)
        return await withCheckedContinuation { continuation in
            AVAudioApplication.requestRecordPermission { continuation.resume(returning: $0) }
        }
        #else
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: return true
        case .notDetermined:
            return await AVCaptureDevice.requestAccess(for: .audio)
        default: return false
        }
        #endif
    }

    private func startMetering() {
        meterTimer?.invalidate()
        meterTimer = Timer.scheduledTimer(withTimeInterval: 0.08, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self, let recorder = self.recorder else { return }
                recorder.updateMeters()
                let power = recorder.averagePower(forChannel: 0)
                self.level = max(0, min(1, (power + 55) / 50))
            }
        }
    }
}
