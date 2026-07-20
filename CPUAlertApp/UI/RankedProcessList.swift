import AppKit
import SwiftUI

struct RankedProcessList: View {
    @Bindable var model: MonitorModel
    @State private var gracefulTarget: ProcessMetric?
    @State private var forceTarget: ProcessMetric?
    @State private var showGracefulConfirmation = false
    @State private var showForceConfirmation = false
    @State private var terminationFeedback: String?

    var body: some View {
        LazyVStack(spacing: 4) {
            switch model.selectedResource {
            case .cpu:
                cpuRows
            case .gpu:
                gpuRows
            case .memory:
                memoryRows
            }
            if let terminationFeedback {
                Text(terminationFeedback)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .confirmationDialog(
            String(localized: "termination.graceful.title"),
            isPresented: $showGracefulConfirmation,
            titleVisibility: .visible
        ) {
            Button("action.terminate", role: .destructive) {
                performGracefulTermination()
            }
            Button("action.cancel", role: .cancel) {}
        } message: {
            Text(String(
                format: String(localized: "termination.graceful.message"),
                locale: .current,
                gracefulTarget?.name ?? ""
            ))
        }
        .confirmationDialog(
            String(localized: "termination.force.title"),
            isPresented: $showForceConfirmation,
            titleVisibility: .visible
        ) {
            Button("action.forceTerminate", role: .destructive) {
                performForceTermination()
            }
            Button("action.cancel", role: .cancel) {}
        } message: {
            Text(String(
                format: String(localized: "termination.force.message"),
                locale: .current,
                forceTarget?.name ?? ""
            ))
        }
    }

    @ViewBuilder
    private var memoryRows: some View {
        let rows = model.snapshot.memoryProcesses.prefix(model.showTenRows ? 10 : 5)
        if rows.isEmpty {
            emptyState("panel.memory.collecting")
        } else {
            ForEach(rows) { process in
                HStack(spacing: 8) {
                    ProcessIcon(identity: process.identity)
                    VStack(alignment: .leading, spacing: 1) {
                        Text(process.name)
                            .lineLimit(1)
                        Text(String(
                            format: String(localized: "panel.process.pid.format"),
                            locale: .current,
                            process.identity.pid
                        ))
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                    Spacer(minLength: 8)
                    Text(MemoryFormatting.bytes(process.physicalFootprintBytes))
                        .monospacedDigit()
                        .foregroundStyle(.primary)
                    terminationMenu(for: process)
                }
                .frame(minHeight: 38)
                .accessibilityElement(children: .contain)
                .accessibilityIdentifier("memory-process-\(process.identity.pid)")
            }
        }
    }

    @ViewBuilder
    private var cpuRows: some View {
        let rows = model.snapshot.processes.prefix(model.showTenRows ? 10 : 5)
        if rows.isEmpty {
            emptyState("panel.process.collecting")
        } else {
            ForEach(rows) { process in
                HStack(spacing: 6) {
                    Button {
                        model.expandedProcess = model.expandedProcess == process.identity
                            ? nil
                            : process.identity
                    } label: {
                        HStack(spacing: 8) {
                            ProcessIcon(identity: process.identity)
                            VStack(alignment: .leading, spacing: 1) {
                                Text(process.name)
                                    .lineLimit(1)
                                Text(String(
                                    format: String(localized: "panel.process.pid.format"),
                                    locale: .current,
                                    process.identity.pid
                                ))
                                    .font(.caption2)
                                    .foregroundStyle(.secondary)
                            }
                            Spacer(minLength: 8)
                            Text(process.cpuUsage, format: .percent.precision(.fractionLength(1)))
                                .monospacedDigit()
                        }
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    .accessibilityIdentifier(
                        "cpu-process-disclosure-\(process.identity.pid)"
                    )
                    .accessibilityHint(Text("panel.process.expandHint"))

                    terminationMenu(for: process)
                }

                if model.expandedProcess == process.identity {
                    ForEach(model.snapshot.expandedThreads) { thread in
                        HStack(spacing: 8) {
                            Image(systemName: "point.3.connected.trianglepath.dotted")
                                .frame(width: 24)
                                .foregroundStyle(.secondary)
                            Text(thread.name ?? String(
                                format: String(localized: "panel.thread.format"),
                                locale: .current,
                                thread.id
                            ))
                                .font(.caption)
                                .lineLimit(1)
                            Spacer()
                            Text(thread.cpuUsage, format: .percent.precision(.fractionLength(1)))
                                .font(.caption.monospacedDigit())
                        }
                        .padding(.leading, 12)
                        .accessibilityIdentifier("cpu-thread-\(thread.id)")
                    }
                }
            }
        }
    }

    @ViewBuilder
    private var gpuRows: some View {
        let rows = model.snapshot.gpuGroups.prefix(model.showTenRows ? 10 : 5)
        if rows.isEmpty {
            emptyState("panel.gpu.attributionUnavailable")
        } else {
            ForEach(rows) { group in
                let estimatedUsage = group.estimatedWholeMachineUsage(
                    systemUsage: model.snapshot.gpuUsage
                )
                Button {
                    withAnimation(.easeInOut(duration: 0.16)) {
                        model.expandedGPUGroupID = model.expandedGPUGroupID == group.id
                            ? nil
                            : group.id
                    }
                } label: {
                    HStack(spacing: 8) {
                        Image(systemName: "square.stack.3d.up.fill")
                            .frame(width: 24)
                            .foregroundStyle(.secondary)
                        VStack(alignment: .leading, spacing: 1) {
                            Text(group.name)
                                .lineLimit(1)
                            Text(String(
                                format: String(localized: "panel.gpu.members.format"),
                                locale: .current,
                                group.members.count
                            ))
                                .font(.caption2)
                                .foregroundStyle(.secondary)
                        }
                        Spacer(minLength: 8)
                        Text(estimatedUsage.map {
                            $0.formatted(.percent.precision(.fractionLength(1)))
                        } ?? "—")
                            .monospacedDigit()
                        Image(systemName: "chevron.right")
                            .font(.caption2.weight(.semibold))
                            .foregroundStyle(.tertiary)
                            .rotationEffect(
                                .degrees(model.expandedGPUGroupID == group.id ? 90 : 0)
                            )
                            .frame(width: 10)
                    }
                    .frame(maxWidth: .infinity, minHeight: 38, alignment: .leading)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 3)
                    .contentShape(Rectangle())
                    .background(
                        model.expandedGPUGroupID == group.id
                            ? Color.accentColor.opacity(0.10)
                            : Color.primary.opacity(0.035),
                        in: RoundedRectangle(cornerRadius: 7)
                    )
                }
                .buttonStyle(.plain)
                .accessibilityIdentifier("gpu-group-card-\(group.id)")
                .accessibilityLabel(gpuAccessibilityLabel(
                    for: group,
                    estimatedUsage: estimatedUsage
                ))
                .accessibilityHint(Text("panel.gpu.expandHint"))

                if model.expandedGPUGroupID == group.id {
                    ForEach(group.members) { member in
                        HStack(spacing: 6) {
                            HStack(spacing: 8) {
                                Image(systemName: member.isApplication ? "app.fill" : "gearshape.fill")
                                    .frame(width: 20)
                                    .foregroundStyle(.secondary)
                                VStack(alignment: .leading, spacing: 1) {
                                    Text(member.name)
                                        .font(.caption)
                                        .lineLimit(1)
                                        .accessibilityIdentifier(
                                            "gpu-group-member-name-\(member.identity.pid)"
                                        )
                                    HStack(spacing: 4) {
                                        Text(String(
                                            format: String(localized: "panel.process.pid.format"),
                                            locale: .current,
                                            member.identity.pid
                                        ))
                                        if member.identity == group.leader {
                                            Text("panel.gpu.member.leader")
                                        }
                                    }
                                    .font(.caption2)
                                    .foregroundStyle(.secondary)
                                }
                                Spacer(minLength: 8)
                            }
                            .accessibilityElement(children: .combine)
                            terminationMenu(for: member.processMetric)
                        }
                        .padding(.leading, 14)
                        .accessibilityIdentifier("gpu-group-member-\(member.identity.pid)")
                    }
                }
            }
        }
    }

    private func emptyState(_ key: LocalizedStringKey) -> some View {
        Text(key)
            .font(.caption)
            .foregroundStyle(.secondary)
            .frame(maxWidth: .infinity, minHeight: 72)
    }

    private func gpuAccessibilityLabel(
        for group: GPUGroupMetric,
        estimatedUsage: Double?
    ) -> Text {
        guard let estimatedUsage else {
            return Text("\(group.name), \(String(localized: "value.unavailable"))")
        }
        return Text(String(
            format: String(localized: "panel.gpu.accessibility.format"),
            locale: .current,
            group.name,
            Int((estimatedUsage * 100).rounded())
        ))
    }

    private func terminationMenu(for process: ProcessMetric) -> some View {
        Menu {
            Button("action.terminate", role: .destructive) {
                askToTerminate(process)
            }
        } label: {
            Image(systemName: "ellipsis.circle")
                .accessibilityLabel(Text("action.processActions"))
        }
        .menuStyle(.borderlessButton)
        .fixedSize()
    }

    private func askToTerminate(_ process: ProcessMetric) {
        gracefulTarget = process
        terminationFeedback = nil
        showGracefulConfirmation = true
    }

    private func performGracefulTermination() {
        guard let target = gracefulTarget else { return }
        Task {
            let result = await model.requestGracefulTermination(target)
            if result == .forceAvailable {
                forceTarget = target
                showForceConfirmation = true
            } else {
                terminationFeedback = feedback(for: result)
            }
        }
    }

    private func performForceTermination() {
        guard let target = forceTarget else { return }
        Task {
            terminationFeedback = feedback(
                for: await model.requestForceTermination(target)
            )
        }
    }

    private func feedback(for result: TerminationResult) -> String {
        switch result {
        case .terminated: String(localized: "termination.result.terminated")
        case .forceAvailable: String(localized: "termination.result.forceAvailable")
        case .identityChanged: String(localized: "termination.result.identityChanged")
        case .forceNotAvailable: String(localized: "termination.result.forceNotAvailable")
        case .protectedTarget: String(localized: "termination.result.protected")
        case .notFound: String(localized: "termination.result.notFound")
        case .failed(let errorCode): String(
            format: String(localized: "termination.result.failed.format"),
            locale: .current,
            errorCode
        )
        }
    }
}

struct ProcessIcon: View {
    let identity: ProcessIdentity
    @State private var icon: NSImage?

    var body: some View {
        Group {
            if let icon {
                Image(nsImage: icon)
                    .resizable()
            } else {
                Image(systemName: "app.dashed")
                    .resizable()
                    .foregroundStyle(.secondary)
            }
        }
        .scaledToFit()
        .frame(width: 24, height: 24)
        .task(id: identity) { @MainActor in
            icon = NSRunningApplication(processIdentifier: identity.pid)?.icon
        }
    }
}
