import Darwin
import Foundation
import Metal

private nonisolated(unsafe) var stopRequested: sig_atomic_t = 0

private func requestStop(_ signal: Int32) {
    stopRequested = 1
}

private func boundedDuration(arguments: [String]) -> Int {
    guard let index = arguments.firstIndex(of: "--seconds"),
          arguments.indices.contains(index + 1),
          let parsed = Int(arguments[index + 1]) else {
        return 10
    }
    return min(max(parsed, 1), 60)
}

private func loadLibrary(device: any MTLDevice) throws -> any MTLLibrary {
    let executable = URL(fileURLWithPath: CommandLine.arguments[0])
        .resolvingSymlinksInPath()
    let adjacentLibrary = executable.deletingLastPathComponent()
        .appendingPathComponent("default.metallib")
    if FileManager.default.fileExists(atPath: adjacentLibrary.path) {
        return try device.makeLibrary(URL: adjacentLibrary)
    }
    if let library = device.makeDefaultLibrary() {
        return library
    }
    throw NSError(
        domain: "GPUStress",
        code: 1,
        userInfo: [NSLocalizedDescriptionKey: "default.metallib was not found"]
    )
}

signal(SIGTERM, requestStop)
signal(SIGINT, requestStop)

let seconds = boundedDuration(arguments: CommandLine.arguments)
guard let device = MTLCreateSystemDefaultDevice() else {
    fputs("GPUStress: Metal is unavailable\n", stderr)
    exit(EXIT_FAILURE)
}

do {
    let library = try loadLibrary(device: device)
    guard let function = library.makeFunction(name: "stressKernel") else {
        throw NSError(
            domain: "GPUStress",
            code: 2,
            userInfo: [NSLocalizedDescriptionKey: "stressKernel was not found"]
        )
    }
    let pipeline = try device.makeComputePipelineState(function: function)
    guard let queue = device.makeCommandQueue() else {
        throw NSError(
            domain: "GPUStress",
            code: 3,
            userInfo: [NSLocalizedDescriptionKey: "Metal command queue could not be created"]
        )
    }

    let valueCount = 1_048_576
    let bufferLength = valueCount * MemoryLayout<Float>.stride
    guard let buffer = device.makeBuffer(length: bufferLength, options: .storageModePrivate) else {
        throw NSError(
            domain: "GPUStress",
            code: 4,
            userInfo: [NSLocalizedDescriptionKey: "private Metal buffer could not be created"]
        )
    }

    if let commandBuffer = queue.makeCommandBuffer(),
       let blit = commandBuffer.makeBlitCommandEncoder() {
        blit.fill(buffer: buffer, range: 0..<bufferLength, value: 1)
        blit.endEncoding()
        commandBuffer.commit()
        commandBuffer.waitUntilCompleted()
    }

    let grid = MTLSize(width: valueCount, height: 1, depth: 1)
    let groupWidth = min(
        pipeline.maxTotalThreadsPerThreadgroup,
        max(pipeline.threadExecutionWidth, 1) * 4
    )
    let threadsPerGroup = MTLSize(width: groupWidth, height: 1, depth: 1)
    let deadline = DispatchTime.now().uptimeNanoseconds
        + UInt64(seconds) * 1_000_000_000
    var commandCount = 0

    while stopRequested == 0, DispatchTime.now().uptimeNanoseconds < deadline {
        guard let commandBuffer = queue.makeCommandBuffer(),
              let encoder = commandBuffer.makeComputeCommandEncoder() else {
            throw NSError(
                domain: "GPUStress",
                code: 5,
                userInfo: [NSLocalizedDescriptionKey: "Metal command buffer could not be encoded"]
            )
        }
        encoder.setComputePipelineState(pipeline)
        encoder.setBuffer(buffer, offset: 0, index: 0)
        encoder.dispatchThreads(grid, threadsPerThreadgroup: threadsPerGroup)
        encoder.endEncoding()
        commandBuffer.commit()
        commandBuffer.waitUntilCompleted()
        if commandBuffer.status == .error {
            throw commandBuffer.error ?? NSError(
                domain: "GPUStress",
                code: 6,
                userInfo: [NSLocalizedDescriptionKey: "Metal command buffer failed"]
            )
        }
        commandCount += 1
    }

    print("GPUStress completed: seconds=\(seconds) commandBuffers=\(commandCount)")
} catch {
    fputs("GPUStress: \(error.localizedDescription)\n", stderr)
    exit(EXIT_FAILURE)
}
