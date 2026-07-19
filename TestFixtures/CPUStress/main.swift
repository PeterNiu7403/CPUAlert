import Dispatch
import Darwin
import Foundation

private nonisolated(unsafe) var stopRequested: sig_atomic_t = 0

private func requestStop(_ signal: Int32) {
    stopRequested = 1
}

private struct Options {
    let workers: Int
    let dutyPercent: Int
    let seconds: Int

    init(arguments: [String]) {
        let activeProcessors = max(ProcessInfo.processInfo.activeProcessorCount, 1)
        workers = Self.integer(after: "--workers", in: arguments, default: 1)
            .clamped(to: 1...activeProcessors)
        dutyPercent = Self.integer(after: "--duty-percent", in: arguments, default: 50)
            .clamped(to: 1...100)
        seconds = Self.integer(after: "--seconds", in: arguments, default: 10)
            .clamped(to: 1...300)
    }

    private static func integer(after option: String, in arguments: [String], default value: Int) -> Int {
        guard let index = arguments.firstIndex(of: option),
              arguments.indices.contains(index + 1),
              let parsed = Int(arguments[index + 1]) else {
            return value
        }
        return parsed
    }
}

private extension Comparable {
    func clamped(to range: ClosedRange<Self>) -> Self {
        min(max(self, range.lowerBound), range.upperBound)
    }
}

private final class Digest: @unchecked Sendable {
    private let lock = NSLock()
    private var value: UInt64 = 0

    func combine(_ candidate: UInt64) {
        lock.lock()
        value ^= candidate
        lock.unlock()
    }

    var result: UInt64 {
        lock.lock()
        defer { lock.unlock() }
        return value
    }
}

signal(SIGTERM, requestStop)
signal(SIGINT, requestStop)

private let options = Options(arguments: CommandLine.arguments)
let intervalNanoseconds: UInt64 = 100_000_000
let busyNanoseconds = intervalNanoseconds * UInt64(options.dutyPercent) / 100
let deadline = DispatchTime.now().uptimeNanoseconds
    + UInt64(options.seconds) * 1_000_000_000
private let digest = Digest()
let group = DispatchGroup()

for worker in 0..<options.workers {
    group.enter()
    DispatchQueue.global(qos: .userInitiated).async {
        defer { group.leave() }
        var accumulator = UInt64(worker + 1)

        while stopRequested == 0 {
            let intervalStart = DispatchTime.now().uptimeNanoseconds
            guard intervalStart < deadline else { break }
            let busyDeadline = min(intervalStart + busyNanoseconds, deadline)
            while stopRequested == 0,
                  DispatchTime.now().uptimeNanoseconds < busyDeadline {
                accumulator = accumulator &* 2_862_933_555_777_941_757
                    &+ 3_037_000_493
                accumulator ^= accumulator >> 29
            }

            let intervalDeadline = min(intervalStart + intervalNanoseconds, deadline)
            let now = DispatchTime.now().uptimeNanoseconds
            if stopRequested == 0, now < intervalDeadline {
                let microseconds = min((intervalDeadline - now) / 1_000, UInt64(UInt32.max))
                usleep(useconds_t(microseconds))
            }
        }
        digest.combine(accumulator)
    }
}

group.wait()
print(
    "CPUStress completed: workers=\(options.workers) "
        + "duty=\(options.dutyPercent)% digest=\(digest.result)"
)
