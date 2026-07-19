import Testing
@testable import CPUAlert

struct ProtectedProcessPolicyTests {
    @Test func protectedProcessesAreDenied() {
        let rows: [(Int32, String)] = [
            (0, "kernel_task"),
            (1, "launchd"),
            (88, "WindowServer"),
            (99, "loginwindow"),
            (100, "CPUAlert"),
            (101, "com.cpualert.helper"),
        ]
        for (pid, name) in rows {
            #expect(ProtectedProcessPolicy.isProtected(pid: pid, name: name))
        }
    }

    @Test func ordinaryChildIsAllowed() {
        #expect(!ProtectedProcessPolicy.isProtected(pid: 4_242, name: "CPUStress"))
    }
}
