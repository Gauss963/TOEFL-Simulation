import AVFoundation
import Foundation
import Combine

/// Pre-rendered neural word clips (Kokoro-82M, Apache-2.0) bundled as one resource pack.
nonisolated final class WordPronunciationLibrary: Sendable {
    static let shared = WordPronunciationLibrary()

    private let pack: Data?
    private let clips: [String: [Int]]

    init(bundle: Bundle = .main) {
        struct Index: Decodable { let clips: [String: [Int]] }
        let indexURL = bundle.url(forResource: "WordPronunciations", withExtension: "json")
        let packURL = bundle.url(forResource: "WordPronunciations", withExtension: "pack")
        let index = indexURL
            .flatMap { try? Data(contentsOf: $0) }
            .flatMap { try? JSONDecoder().decode(Index.self, from: $0) }
        clips = index?.clips ?? [:]
        pack = packURL.flatMap { try? Data(contentsOf: $0, options: .alwaysMapped) }
    }

    var clipCount: Int { pack == nil ? 0 : clips.count }

    func clip(for word: String) -> Data? {
        guard let pack, let range = clips[Self.key(for: word)], range.count == 2,
              range[0] >= 0, range[1] > 0, range[0] + range[1] <= pack.count else { return nil }
        return pack.subdata(in: range[0]..<(range[0] + range[1]))
    }

    static func key(for word: String) -> String {
        word.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
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

@MainActor
final class PromptSpeechPlayer: NSObject, ObservableObject, AVSpeechSynthesizerDelegate {
    @Published private(set) var isSpeaking = false
    private let synthesizer = AVSpeechSynthesizer()

    override init() {
        super.init()
        synthesizer.delegate = self
    }

    func speak(_ text: String) {
        stop()
        let utterance = AVSpeechUtterance(string: text)
        utterance.voice = EnglishVoice.best
        utterance.rate = 0.47
        utterance.pitchMultiplier = 1
        isSpeaking = true
        synthesizer.speak(utterance)
    }

    func stop() {
        if synthesizer.isSpeaking { synthesizer.stopSpeaking(at: .immediate) }
        isSpeaking = false
    }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        Task { @MainActor in self.isSpeaking = false }
    }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didCancel utterance: AVSpeechUtterance) {
        Task { @MainActor in self.isSpeaking = false }
    }
}

@MainActor
final class SpeakingResponseRecorder: NSObject, ObservableObject, AVAudioRecorderDelegate {
    @Published private(set) var isRecording = false
    @Published private(set) var permissionDenied = false

    private var recorder: AVAudioRecorder?

    func start(questionID: String) async -> Bool {
        let allowed = await requestPermission()
        guard allowed else {
            permissionDenied = true
            return false
        }

        #if os(iOS)
        do {
            let session = AVAudioSession.sharedInstance()
            try session.setCategory(.record, mode: .spokenAudio)
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
            recorder?.prepareToRecord()
            isRecording = recorder?.record() == true
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
        #if os(iOS)
        try? AVAudioSession.sharedInstance().setActive(false)
        #endif
        return duration
    }

    private func requestPermission() async -> Bool {
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
}
