import Testing
@testable import CPUAlert

struct BenchmarkModeTests {
    @Test
    func parsesEverySupportedMode() {
        #expect(BenchmarkMode(arguments: ["CPUAlert", "--benchmark-green"]) == .green)
        #expect(BenchmarkMode(arguments: ["CPUAlert", "--benchmark-panel-open"]) == .panelOpen)
        #expect(BenchmarkMode(arguments: ["CPUAlert", "--benchmark-elevated-cpu"]) == .elevatedCPU)
        #expect(BenchmarkMode(arguments: ["CPUAlert", "--benchmark-elevated-gpu"]) == .elevatedGPU)
        #expect(BenchmarkMode(arguments: [
            "CPUAlert", "--benchmark-expanded-thread", "--target-pid", "4242",
        ]) == .expandedThread(pid: 4_242))
    }

    @Test
    func rejectsMissingOrInvalidExpandedPID() {
        #expect(BenchmarkMode(arguments: ["CPUAlert"]) == nil)
        #expect(BenchmarkMode(arguments: ["CPUAlert", "--benchmark-expanded-thread"]) == nil)
        #expect(BenchmarkMode(arguments: [
            "CPUAlert", "--benchmark-expanded-thread", "--target-pid", "not-a-pid",
        ]) == nil)
        #expect(BenchmarkMode(arguments: [
            "CPUAlert", "--benchmark-expanded-thread", "--target-pid", "0",
        ]) == nil)
    }

    @Test
    func onlyInteractiveModesOpenThePanel() {
        #expect(!BenchmarkMode.green.opensPanel)
        #expect(BenchmarkMode.panelOpen.opensPanel)
        #expect(!BenchmarkMode.elevatedCPU.opensPanel)
        #expect(!BenchmarkMode.elevatedGPU.opensPanel)
        #expect(BenchmarkMode.expandedThread(pid: 7).opensPanel)
    }
}
