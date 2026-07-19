import Darwin
import Foundation

struct ProcessIdentityRecord: Equatable, Sendable {
    let identity: ProcessIdentity
    let name: String
    let executablePath: String
    let uid: UInt32
}

protocol ProcessIdentityReading: Sendable {
    func currentIdentity(pid: Int32) -> ProcessIdentityRecord?
}

struct ProcessIdentityReader: ProcessIdentityReading {
    func currentIdentity(pid: Int32) -> ProcessIdentityRecord? {
        guard pid > 0 else { return nil }
        var info = proc_bsdinfo()
        let copied = proc_pidinfo(
            pid,
            PROC_PIDTBSDINFO,
            0,
            &info,
            Int32(MemoryLayout<proc_bsdinfo>.size)
        )
        guard copied == MemoryLayout<proc_bsdinfo>.size else { return nil }

        let seconds = UInt64(info.pbi_start_tvsec)
        let microseconds = UInt64(info.pbi_start_tvusec)
        let secondsResult = seconds.multipliedReportingOverflow(by: 1_000_000_000)
        let microsecondsResult = microseconds.multipliedReportingOverflow(by: 1_000)
        guard !secondsResult.overflow, !microsecondsResult.overflow else { return nil }
        let startResult = secondsResult.partialValue.addingReportingOverflow(
            microsecondsResult.partialValue
        )
        guard !startResult.overflow else { return nil }

        var pathBuffer = [CChar](repeating: 0, count: 4_096)
        let pathLength = pathBuffer.withUnsafeMutableBufferPointer { buffer in
            proc_pidpath(pid, buffer.baseAddress, UInt32(buffer.count))
        }
        let pathBytes = pathBuffer.prefix { $0 != 0 }.map { UInt8(bitPattern: $0) }
        let path = pathLength > 0 ? String(decoding: pathBytes, as: UTF8.self) : ""

        var nameStorage = info.pbi_name
        var commandStorage = info.pbi_comm
        let name = tupleString(&nameStorage)
        let command = tupleString(&commandStorage)
        return ProcessIdentityRecord(
            identity: ProcessIdentity(
                pid: pid,
                startTimeNanoseconds: startResult.partialValue
            ),
            name: name.isEmpty ? command : name,
            executablePath: path,
            uid: info.pbi_uid
        )
    }
}

private func tupleString<Value>(_ value: inout Value) -> String {
    withUnsafeBytes(of: &value) { bytes in
        guard let baseAddress = bytes.bindMemory(to: CChar.self).baseAddress else {
            return ""
        }
        return String(cString: baseAddress)
    }
}
