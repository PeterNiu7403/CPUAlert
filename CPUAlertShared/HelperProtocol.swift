import Foundation

@objc enum HelperOperation: Int {
    case terminate = 1
    case uninstall = 2
}

@objc final class HelperRequest: NSObject, NSSecureCoding, @unchecked Sendable {
    static var supportsSecureCoding: Bool { true }

    let operation: HelperOperation
    let pid: Int32
    let startTimeNanoseconds: UInt64
    let signal: Int32

    init(
        operation: HelperOperation,
        pid: Int32,
        startTimeNanoseconds: UInt64,
        signal: Int32
    ) {
        self.operation = operation
        self.pid = pid
        self.startTimeNanoseconds = startTimeNanoseconds
        self.signal = signal
    }

    required init?(coder: NSCoder) {
        guard let operation = HelperOperation(
            rawValue: coder.decodeInteger(forKey: "operation")
        ) else {
            return nil
        }
        self.operation = operation
        pid = coder.decodeInt32(forKey: "pid")
        startTimeNanoseconds = UInt64(
            bitPattern: coder.decodeInt64(forKey: "startTime")
        )
        signal = coder.decodeInt32(forKey: "signal")
    }

    func encode(with coder: NSCoder) {
        coder.encode(operation.rawValue, forKey: "operation")
        coder.encode(pid, forKey: "pid")
        coder.encode(Int64(bitPattern: startTimeNanoseconds), forKey: "startTime")
        coder.encode(signal, forKey: "signal")
    }
}

@objc final class HelperResponse: NSObject, NSSecureCoding, @unchecked Sendable {
    static var supportsSecureCoding: Bool { true }

    let errorCode: Int32

    init(errorCode: Int32) {
        self.errorCode = errorCode
    }

    required init?(coder: NSCoder) {
        errorCode = coder.decodeInt32(forKey: "errorCode")
    }

    func encode(with coder: NSCoder) {
        coder.encode(errorCode, forKey: "errorCode")
    }
}

@objc protocol HelperXPCProtocol {
    func perform(
        _ request: HelperRequest,
        withReply reply: @escaping (HelperResponse) -> Void
    )
}

func helperInterface() -> NSXPCInterface {
    let interface = NSXPCInterface(with: HelperXPCProtocol.self)
    let selector = #selector(HelperXPCProtocol.perform(_:withReply:))
    let requestClasses = NSSet(array: [HelperRequest.self]) as! Set<AnyHashable>
    let responseClasses = NSSet(array: [HelperResponse.self]) as! Set<AnyHashable>
    interface.setClasses(
        requestClasses,
        for: selector,
        argumentIndex: 0,
        ofReply: false
    )
    interface.setClasses(
        responseClasses,
        for: selector,
        argumentIndex: 0,
        ofReply: true
    )
    return interface
}
