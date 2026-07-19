import Foundation
import Testing
@testable import CPUAlert

struct GPUCollectorTests {
    @Test func activeResidencyIsWeightedAcrossChannels() {
        let channels = [
            GPUResidency(active: 40, total: 100),
            GPUResidency(active: 30, total: 100),
        ]
        #expect(SystemGPUCollector.aggregate(channels) == 0.35)
    }

    @Test func emptyResidencyIsUnavailable() {
        #expect(SystemGPUCollector.aggregate([]) == nil)
        #expect(SystemGPUCollector.aggregate([.init(active: 0, total: 0)]) == nil)
    }

    @Test func coalitionDeltasBecomeShares() {
        let shares = CoalitionGPUCollector.shares(
            previous: [10: 100, 20: 200],
            current: [10: 130, 20: 270]
        )
        #expect(shares[10] == 0.3)
        #expect(shares[20] == 0.7)
    }

    @Test func coalitionRegressionIsDropped() {
        let shares = CoalitionGPUCollector.shares(
            previous: [10: 100, 20: 200],
            current: [10: 90, 20: 250]
        )
        #expect(shares == [20: 1.0])
    }

    @Test func activityShareScalesToEstimatedWholeMachineUsage() {
        let group = GPUGroupMetric(
            id: 10,
            name: "ChatGPT",
            leader: nil,
            members: [],
            activityShare: 0.891
        )

        #expect(abs(
            (group.estimatedWholeMachineUsage(systemUsage: 0.53) ?? 0) - 0.47223
        ) < 0.000_001)
        #expect(group.estimatedWholeMachineUsage(systemUsage: nil) == nil)
    }

    @Test func gpuGroupPreservesMemberDetailsForDisclosure() {
        let identity = ProcessIdentity(pid: 4_201, startTimeNanoseconds: 1)
        let member = GPUGroupMemberMetric(
            identity: identity,
            name: "render-worker",
            ownerUID: 501,
            isApplication: false
        )
        let group = GPUGroupMetric(
            id: 10,
            name: "ChatGPT",
            leader: identity,
            members: [member],
            activityShare: 0.5
        )

        #expect(group.members == [member])
        #expect(group.members.first?.identity == group.leader)
        #expect(group.members.first?.name == "render-worker")
    }

    @Test func singleDieFixtureAggregatesResidencyDeltas() throws {
        #expect(try fixtureResidencies(named: "io-report-single-die") == [
            GPUResidency(active: 40, total: 100),
        ])
        #expect(SystemGPUCollector.aggregate(
            try fixtureResidencies(named: "io-report-single-die")
        ) == 0.4)
    }

    @Test func multiDieFixtureUsesWeightedAggregation() throws {
        let rows = try fixtureResidencies(named: "io-report-multi-die")
        #expect(rows == [
            GPUResidency(active: 40, total: 100),
            GPUResidency(active: 30, total: 100),
        ])
        #expect(SystemGPUCollector.aggregate(rows) == 0.35)
    }

    @Test func liveGPUCollectorFailsClosed() async throws {
        let collector = SystemGPUCollector()
        _ = try await collector.sampleSystemGPU()
        try await Task.sleep(for: .milliseconds(100))
        let sample = try await collector.sampleSystemGPU()
        if let usage = sample.usage {
            #expect(usage.isFinite && (0...1).contains(usage))
            #expect(sample.source == .ioReport || sample.source == .ioAccelerator)
        } else {
            #expect(sample.source == .unavailable)
        }
        let groups = try await collector.sampleGroups()
        #expect(groups.allSatisfy {
            $0.activityShare.isFinite && (0...1).contains($0.activityShare)
        })
    }

    private func fixtureResidencies(named name: String) throws -> [GPUResidency] {
        let bundle = Bundle(for: GPUFixtureBundleMarker.self)
        let url = try #require(bundle.url(forResource: name, withExtension: "plist"))
        let fixture = try PropertyListDecoder().decode(
            IOReportFixture.self,
            from: Data(contentsOf: url)
        )
        return try fixture.channels.map { channel in
            var active: UInt64 = 0
            var total: UInt64 = 0
            for state in channel.states {
                guard state.current >= state.previous else {
                    throw FixtureError.counterRegression
                }
                let delta = state.current - state.previous
                total += delta
                if state.name.localizedCaseInsensitiveContains("active") {
                    active += delta
                }
            }
            return GPUResidency(active: active, total: total)
        }
    }
}

private final class GPUFixtureBundleMarker {}

private struct IOReportFixture: Decodable {
    struct Channel: Decodable {
        struct State: Decodable {
            let name: String
            let previous: UInt64
            let current: UInt64
        }

        let channelName: String
        let dieIdentifier: String
        let states: [State]
    }

    let channels: [Channel]
}

private enum FixtureError: Error {
    case counterRegression
}
